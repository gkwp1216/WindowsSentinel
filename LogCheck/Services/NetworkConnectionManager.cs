using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Management;
using LogCheck.Models;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Net;

namespace LogCheck.Services
{
    /// <summary>
    /// 네트워크 연결을 관리하고 차단하는 서비스
    /// </summary>
    public class NetworkConnectionManager
    {
        private readonly ConcurrentDictionary<string, BlockedConnection> _blockedConnections;
        private readonly ConcurrentDictionary<string, BlockedIP> _blockedIPs;
        private readonly object _lockObject = new object();

        public event EventHandler<string>? ConnectionBlocked;
        public event EventHandler<string>? ProcessTerminated;
        public event EventHandler<string>? ErrorOccurred;

        public NetworkConnectionManager()
        {
            _blockedConnections = new ConcurrentDictionary<string, BlockedConnection>();
            _blockedIPs = new ConcurrentDictionary<string, BlockedIP>();
        }

        /// <summary>
        /// 프로세스의 모든 네트워크 연결 강제 차단
        /// </summary>
        public async Task<bool> DisconnectProcessAsync(int processId, string reason = "사용자 요청")
        {
            try
            {
                var result = await Task.Run(async () =>
                {
                    try
                    {
                        // 1. 프로세스의 모든 네트워크 연결 식별
                        var connections = GetProcessConnections(processId);
                        
                        if (!connections.Any())
                        {
                            OnErrorOccurred($"프로세스 {processId}의 네트워크 연결을 찾을 수 없습니다.");
                            return false;
                        }

                        // 2. 각 연결에 대해 방화벽 규칙 생성
                        foreach (var connection in connections)
                        {
                            var ruleName = $"Block_Process_{processId}_{connection.RemoteAddress.ip}_{connection.RemoteAddress.port}";
                            var success = CreateFirewallRule(ruleName, connection.RemoteAddress.ip, reason);
                            
                            if (success)
                            {
                                var blockedConn = new BlockedConnection
                                {
                                    ProcessId = processId,
                                    RemoteAddress = connection.RemoteAddress.ip,
                                    RemotePort = connection.RemoteAddress.port,
                                    Protocol = connection.Protocol,
                                    BlockReason = reason,
                                    BlockedTime = DateTime.Now,
                                    RuleName = ruleName
                                };

                                _blockedConnections.TryAdd($"{processId}_{connection.RemoteAddress.ip}_{connection.RemoteAddress.port}", blockedConn);
                                OnConnectionBlocked($"프로세스 {processId}의 연결 {connection.RemoteAddress.ip}:{connection.RemoteAddress.port} 차단됨");
                            }
                        }

                        // 3. 프로세스 종료 (선택적)
                        var shouldTerminate = await ShowTerminateProcessDialogAsync(processId);
                        if (shouldTerminate)
                        {
                            var terminated = TerminateProcess(processId);
                            if (terminated)
                            {
                                OnProcessTerminated($"프로세스 {processId} 종료됨");
                            }
                        }

                        return true;
                    }
                    catch (Exception ex)
                    {
                        OnErrorOccurred($"프로세스 연결 차단 중 오류: {ex.Message}");
                        return false;
                    }
                });

                return result;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"프로세스 연결 차단 중 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 특정 IP 주소 차단
        /// </summary>
        public async Task<bool> BlockIPAddressAsync(string ipAddress, string reason = "보안 위협")
        {
            try
            {
                var result = await Task.Run(() =>
                {
                    try
                    {
                        // IP 주소 유효성 검사
                        if (!IPAddress.TryParse(ipAddress, out _))
                        {
                            OnErrorOccurred($"잘못된 IP 주소 형식: {ipAddress}");
                            return false;
                        }

                        // 이미 차단된 IP인지 확인
                        if (_blockedIPs.ContainsKey(ipAddress))
                        {
                            OnErrorOccurred($"IP 주소 {ipAddress}는 이미 차단되어 있습니다.");
                            return false;
                        }

                        // 방화벽 규칙 생성
                        var ruleName = $"Block_IP_{ipAddress.Replace('.', '_')}";
                        var success = CreateFirewallRule(ruleName, ipAddress, reason);

                        if (success)
                        {
                            var blockedIP = new BlockedIP
                            {
                                IPAddress = ipAddress,
                                BlockReason = reason,
                                BlockedTime = DateTime.Now,
                                RuleName = ruleName
                            };

                            _blockedIPs.TryAdd(ipAddress, blockedIP);
                            OnConnectionBlocked($"IP 주소 {ipAddress} 차단됨");
                            return true;
                        }

                        return false;
                    }
                    catch (Exception ex)
                    {
                        OnErrorOccurred($"IP 주소 차단 중 오류: {ex.Message}");
                        return false;
                    }
                });

                return result;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"IP 주소 차단 중 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 차단된 연결 해제
        /// </summary>
        public async Task<bool> UnblockConnectionAsync(string connectionKey)
        {
            try
            {
                var result = await Task.Run(() =>
                {
                    try
                    {
                        if (_blockedConnections.TryRemove(connectionKey, out var blockedConn))
                        {
                            // 방화벽 규칙 삭제
                            var success = DeleteFirewallRule(blockedConn.RuleName);
                            if (success)
                            {
                                OnConnectionBlocked($"연결 {blockedConn.RemoteAddress}:{blockedConn.RemotePort} 차단 해제됨");
                                return true;
                            }
                        }

                        return false;
                    }
                    catch (Exception ex)
                    {
                        OnErrorOccurred($"연결 차단 해제 중 오류: {ex.Message}");
                        return false;
                    }
                });

                return result;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"연결 차단 해제 중 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 차단된 IP 해제
        /// </summary>
        public async Task<bool> UnblockIPAsync(string ipAddress)
        {
            try
            {
                var result = await Task.Run(() =>
                {
                    try
                    {
                        if (_blockedIPs.TryRemove(ipAddress, out var blockedIP))
                        {
                            // 방화벽 규칙 삭제
                            var success = DeleteFirewallRule(blockedIP.RuleName);
                            if (success)
                            {
                                OnConnectionBlocked($"IP 주소 {ipAddress} 차단 해제됨");
                                return true;
                            }
                        }

                        return false;
                    }
                    catch (Exception ex)
                    {
                        OnErrorOccurred($"IP 차단 해제 중 오류: {ex.Message}");
                        return false;
                    }
                });

                return result;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"IP 차단 해제 중 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 차단된 연결 목록 조회
        /// </summary>
        public List<BlockedConnection> GetBlockedConnections()
        {
            return _blockedConnections.Values.ToList();
        }

        /// <summary>
        /// 차단된 IP 목록 조회
        /// </summary>
        public List<BlockedIP> GetBlockedIPs()
        {
            return _blockedIPs.Values.ToList();
        }

        /// <summary>
        /// 프로세스의 네트워크 연결 조회 (비동기)
        /// </summary>
        public async Task<List<ProcessConnection>> GetProcessConnectionsAsync()
        {
            return await Task.Run(() =>
            {
                var allConnections = new List<ProcessConnection>();

                try
                {
                    // netstat -ano 명령어로 모든 연결 조회
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "netstat",
                        Arguments = "-ano",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(startInfo);
                    if (process != null)
                    {
                        var output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();

                        // 출력 파싱하여 모든 연결 정보 추출
                        allConnections = ParseNetstatOutputForAllConnections(output);
                    }
                }
                catch (Exception ex)
                {
                    OnErrorOccurred($"모든 프로세스 연결 조회 중 오류: {ex.Message}");
                }

                return allConnections;
            });
        }

        /// <summary>
        /// 프로세스의 네트워크 연결 조회
        /// </summary>
        private List<ProcessConnection> GetProcessConnections(int processId)
        {
            var connections = new List<ProcessConnection>();

            try
            {
                // netstat -ano 명령어로 프로세스별 연결 조회
                var startInfo = new ProcessStartInfo
                {
                    FileName = "netstat",
                    Arguments = "-ano",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    // 출력 파싱하여 해당 프로세스의 연결만 필터링
                    connections = ParseNetstatOutputForProcess(output, processId);
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"프로세스 연결 조회 중 오류: {ex.Message}");
            }

            return connections;
        }

        /// <summary>
        /// netstat 출력을 모든 연결에 대해 파싱
        /// </summary>
        private List<ProcessConnection> ParseNetstatOutputForAllConnections(string output)
        {
            var connections = new List<ProcessConnection>();
            var lines = output.Split('\n');

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // TCP    0.0.0.0:135    0.0.0.0:0    LISTENING    1234
                var match = Regex.Match(line.Trim(), @"^(TCP|UDP)\s+([^\s]+)\s+([^\s]+)\s+(\w+)\s+(\d+)$");
                if (match.Success)
                {
                    try
                    {
                        var processId = int.Parse(match.Groups[5].Value);
                        var (localIP, localPort) = ParseAddress(match.Groups[2].Value);
                        var (remoteIP, remotePort) = ParseAddress(match.Groups[3].Value);

                        var connection = new ProcessConnection
                        {
                            Protocol = match.Groups[1].Value,
                            LocalAddress = (localIP, localPort),
                            RemoteAddress = (remoteIP, remotePort),
                            State = match.Groups[4].Value,
                            ProcessId = processId
                        };

                        connections.Add(connection);
                    }
                    catch (Exception ex)
                    {
                        OnErrorOccurred($"연결 정보 파싱 오류: {ex.Message}");
                    }
                }
            }

            return connections;
        }

        /// <summary>
        /// netstat 출력을 특정 프로세스에 대해 파싱
        /// </summary>
        private List<ProcessConnection> ParseNetstatOutputForProcess(string output, int targetProcessId)
        {
            var connections = new List<ProcessConnection>();
            var lines = output.Split('\n');

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // TCP    0.0.0.0:135    0.0.0.0:0    LISTENING    1234
                var match = Regex.Match(line.Trim(), @"^(TCP|UDP)\s+([^\s]+)\s+([^\s]+)\s+(\w+)\s+(\d+)$");
                if (match.Success)
                {
                    try
                    {
                        var processId = int.Parse(match.Groups[5].Value);
                        if (processId == targetProcessId)
                        {
                            var connection = new ProcessConnection
                            {
                                Protocol = match.Groups[1].Value,
                                LocalAddress = ParseAddress(match.Groups[2].Value),
                                RemoteAddress = ParseAddress(match.Groups[3].Value),
                                State = match.Groups[4].Value,
                                ProcessId = processId
                            };

                            connections.Add(connection);
                        }
                    }
                    catch (Exception ex)
                    {
                        OnErrorOccurred($"연결 정보 파싱 오류: {ex.Message}");
                    }
                }
            }

            return connections;
        }

        /// <summary>
        /// 주소 파싱 (IP:Port)
        /// </summary>
        private (string ip, int port) ParseAddress(string address)
        {
            try
            {
                var parts = address.Split(':');
                if (parts.Length == 2)
                {
                    var ip = parts[0];
                    var port = int.Parse(parts[1]);
                    return (ip, port);
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"주소 파싱 오류: {ex.Message}");
            }

            return (address, 0);
        }

        /// <summary>
        /// Windows 방화벽 규칙 생성
        /// </summary>
        private bool CreateFirewallRule(string ruleName, string ipAddress, string description)
        {
            try
            {
                // netsh advfirewall firewall add rule 명령어 실행
                var startInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"advfirewall firewall add rule name=\"{ruleName}\" dir=out action=block remoteip={ipAddress} description=\"{description}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    Verb = "runas" // 관리자 권한으로 실행
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        return true;
                    }
                    else
                    {
                        OnErrorOccurred($"방화벽 규칙 생성 실패: {error}");
                        return false;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"방화벽 규칙 생성 중 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Windows 방화벽 규칙 삭제
        /// </summary>
        private bool DeleteFirewallRule(string ruleName)
        {
            try
            {
                // netsh advfirewall firewall delete rule 명령어 실행
                var startInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"advfirewall firewall delete rule name=\"{ruleName}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    Verb = "runas" // 관리자 권한으로 실행
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        return true;
                    }
                    else
                    {
                        OnErrorOccurred($"방화벽 규칙 삭제 실패: {error}");
                        return false;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"방화벽 규칙 삭제 중 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 프로세스 종료 확인 다이얼로그 (실제로는 UI에서 구현)
        /// </summary>
        private async Task<bool> ShowTerminateProcessDialogAsync(int processId)
        {
            // 실제 구현에서는 사용자에게 확인 다이얼로그를 보여줘야 함
            // 여기서는 기본적으로 false를 반환
            await Task.Delay(100); // 비동기 시뮬레이션
            return false;
        }

        /// <summary>
        /// 프로세스 강제 종료
        /// </summary>
        private bool TerminateProcess(int processId)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                process.Kill();
                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"프로세스 종료 중 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 이벤트 발생 메서드들
        /// </summary>
        private void OnConnectionBlocked(string message)
        {
            ConnectionBlocked?.Invoke(this, message);
        }

        private void OnProcessTerminated(string message)
        {
            ProcessTerminated?.Invoke(this, message);
        }

        private void OnErrorOccurred(string message)
        {
            ErrorOccurred?.Invoke(this, message);
        }
    }

    /// <summary>
    /// 프로세스 연결 정보
    /// </summary>
    public class ProcessConnection
    {
        public string Protocol { get; set; } = string.Empty;
        public (string ip, int port) LocalAddress { get; set; }
        public (string ip, int port) RemoteAddress { get; set; }
        public string State { get; set; } = string.Empty;
        public int ProcessId { get; set; }
    }

    /// <summary>
    /// 차단된 연결 정보
    /// </summary>
    public class BlockedConnection
    {
        public int ProcessId { get; set; }
        public string RemoteAddress { get; set; } = string.Empty;
        public int RemotePort { get; set; }
        public string Protocol { get; set; } = string.Empty;
        public string BlockReason { get; set; } = string.Empty;
        public DateTime BlockedTime { get; set; }
        public string RuleName { get; set; } = string.Empty;
    }

    /// <summary>
    /// 차단된 IP 정보
    /// </summary>
    public class BlockedIP
    {
        public string IPAddress { get; set; } = string.Empty;
        public string BlockReason { get; set; } = string.Empty;
        public DateTime BlockedTime { get; set; }
        public string RuleName { get; set; } = string.Empty;
    }
}
