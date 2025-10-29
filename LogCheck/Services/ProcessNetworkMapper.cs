using System.Collections.Concurrent;
using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using LogCheck.Models;

namespace LogCheck.Services
{
    /// <summary>
    /// 프로세스와 네트워크 연결을 실시간으로 매핑하는 서비스
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class ProcessNetworkMapper
    {
        private readonly ConcurrentDictionary<int, ProcessInfo> _processCache;
        private readonly ConcurrentDictionary<string, NetworkConnection> _connectionCache;
        private readonly object _lockObject = new object();
        private bool _isMonitoring = false;
        private Task? _monitoringTask;

        public event EventHandler<List<ProcessNetworkInfo>>? ProcessNetworkDataUpdated;
        public event EventHandler<string>? ErrorOccurred;

        public ProcessNetworkMapper()
        {
            _processCache = new ConcurrentDictionary<int, ProcessInfo>();
            _connectionCache = new ConcurrentDictionary<string, NetworkConnection>();
        }

        /// <summary>
        /// 모니터링 시작
        /// </summary>
        public async Task StartMonitoringAsync()
        {
            if (_isMonitoring) return;

            try
            {
                _isMonitoring = true;
                _monitoringTask = Task.Run(MonitoringLoop);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"모니터링 시작 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 모니터링 중지
        /// </summary>
        public async Task StopMonitoringAsync()
        {
            if (!_isMonitoring) return;

            try
            {
                _isMonitoring = false;
                if (_monitoringTask != null)
                {
                    await _monitoringTask;
                    _monitoringTask = null;
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"모니터링 중지 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 모니터링 루프
        /// </summary>
        private async Task MonitoringLoop()
        {
            while (_isMonitoring)
            {
                try
                {
                    var processNetworkData = await GetProcessNetworkDataAsync();
                    ProcessNetworkDataUpdated?.Invoke(this, processNetworkData);

                    await Task.Delay(2000); // 2초마다 업데이트
                }
                catch (Exception ex)
                {
                    OnErrorOccurred($"모니터링 루프 오류: {ex.Message}");
                    await Task.Delay(5000); // 오류 시 5초 대기
                }
            }
        }

        /// <summary>
        /// 프로세스-네트워크 데이터 수집
        /// </summary>
        public async Task<List<ProcessNetworkInfo>> GetProcessNetworkDataAsync()
        {
            try
            {
                // 1. 활성 네트워크 연결 수집
                var connections = await GetActiveNetworkConnectionsAsync();

                // 2. 프로세스 정보 수집
                var processes = await GetProcessInformationAsync();

                // 3. 데이터 매핑 및 통합
                var result = await MapProcessToNetworkAsync(connections, processes);

                // 4. 위험도 계산
                foreach (var item in result)
                {
                    item.CalculateRiskLevel();
                }

                return result;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"프로세스-네트워크 데이터 수집 오류: {ex.Message}");
                return new List<ProcessNetworkInfo>();
            }
        }

        /// <summary>
        /// 활성 네트워크 연결 수집
        /// </summary>
        private async Task<List<NetworkConnection>> GetActiveNetworkConnectionsAsync()
        {
            return await Task.Run(async () =>
            {
                var connections = new List<NetworkConnection>();

                try
                {
                    // netstat -ano 명령어 실행
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
                        await process.WaitForExitAsync();

                        // 출력 파싱
                        connections = ParseNetstatOutput(output);
                    }
                }
                catch (Exception ex)
                {
                    OnErrorOccurred($"netstat 실행 오류: {ex.Message}");
                }

                return connections;
            });
        }

        /// <summary>
        /// netstat 출력 파싱
        /// </summary>
        private List<NetworkConnection> ParseNetstatOutput(string output)
        {
            var connections = new List<NetworkConnection>();
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
                        var connection = new NetworkConnection
                        {
                            Protocol = match.Groups[1].Value,
                            LocalAddress = ParseAddress(match.Groups[2].Value),
                            RemoteAddress = ParseAddress(match.Groups[3].Value),
                            State = match.Groups[4].Value,
                            ProcessId = int.Parse(match.Groups[5].Value)
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
        /// 프로세스 정보 수집
        /// </summary>
        private async Task<Dictionary<int, ProcessInfo>> GetProcessInformationAsync()
        {
            return await Task.Run(() =>
            {
                var processes = new Dictionary<int, ProcessInfo>();

                try
                {
                    // WMI를 통한 프로세스 정보 수집
                    using var searcher = new ManagementObjectSearcher(
                        "SELECT ProcessId, Name, ExecutablePath, CreationDate, UserModeTime, WorkingSetSize FROM Win32_Process");

                    foreach (ManagementObject obj in searcher.Get())
                    {
                        try
                        {
                            var processId = Convert.ToInt32(obj["ProcessId"]);
                            var processInfo = new ProcessInfo
                            {
                                ProcessId = processId,
                                ProcessName = obj["Name"]?.ToString() ?? string.Empty,
                                ProcessPath = obj["ExecutablePath"]?.ToString() ?? string.Empty,
                                ProcessStartTime = ParseWmiDateTime(obj["CreationDate"]?.ToString()),
                                UserModeTime = Convert.ToInt64(obj["UserModeTime"] ?? 0),
                                WorkingSetSize = Convert.ToInt64(obj["WorkingSetSize"] ?? 0)
                            };

                            // 추가 프로세스 정보 수집
                            EnrichProcessInfo(processInfo);

                            processes[processId] = processInfo;
                        }
                        catch (Exception ex)
                        {
                            OnErrorOccurred($"프로세스 정보 수집 오류 (PID: {obj["ProcessId"]}): {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    OnErrorOccurred($"WMI 프로세스 정보 수집 오류: {ex.Message}");
                }

                return processes;
            });
        }

        /// <summary>
        /// 프로세스 정보 보강
        /// </summary>
        private void EnrichProcessInfo(ProcessInfo processInfo)
        {
            try
            {
                // 파일 정보 수집
                if (!string.IsNullOrEmpty(processInfo.ProcessPath) && System.IO.File.Exists(processInfo.ProcessPath))
                {
                    var fileInfo = new System.IO.FileInfo(processInfo.ProcessPath);
                    processInfo.FileVersion = GetFileVersion(processInfo.ProcessPath);
                    processInfo.CompanyName = GetCompanyName(processInfo.ProcessPath);
                    processInfo.IsSigned = IsFileSigned(processInfo.ProcessPath);
                }

                // 시스템 프로세스 여부 확인
                processInfo.IsSystemProcess = IsSystemProcess(processInfo.ProcessName);
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"프로세스 정보 보강 오류 (PID: {processInfo.ProcessId}): {ex.Message}");
            }
        }

        /// <summary>
        /// 파일 버전 정보 수집
        /// </summary>
        private string GetFileVersion(string filePath)
        {
            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(filePath);
                return versionInfo.FileVersion ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 회사명 정보 수집
        /// </summary>
        private string GetCompanyName(string filePath)
        {
            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(filePath);
                return versionInfo.CompanyName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 파일 서명 여부 확인
        /// </summary>
        private bool IsFileSigned(string filePath)
        {
            try
            {
                // 간단한 서명 확인 (실제로는 더 정교한 검증 필요)
                var versionInfo = FileVersionInfo.GetVersionInfo(filePath);
                return !string.IsNullOrEmpty(versionInfo.CompanyName);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 시스템 프로세스 여부 확인
        /// </summary>
        private bool IsSystemProcess(string processName)
        {
            var systemProcesses = new[] { "svchost", "lsass", "winlogon", "csrss", "wininit", "services" };
            return systemProcesses.Contains(processName.ToLower());
        }

        /// <summary>
        /// WMI 날짜시간 파싱
        /// </summary>
        private DateTime ParseWmiDateTime(string? wmiDateTime)
        {
            if (string.IsNullOrEmpty(wmiDateTime))
                return DateTime.MinValue;

            try
            {
                // WMI 날짜시간 형식: 20231201120000.000000+000
                var match = Regex.Match(wmiDateTime, @"(\d{4})(\d{2})(\d{2})(\d{2})(\d{2})(\d{2})");
                if (match.Success)
                {
                    var year = int.Parse(match.Groups[1].Value);
                    var month = int.Parse(match.Groups[2].Value);
                    var day = int.Parse(match.Groups[3].Value);
                    var hour = int.Parse(match.Groups[4].Value);
                    var minute = int.Parse(match.Groups[5].Value);
                    var second = int.Parse(match.Groups[6].Value);

                    return new DateTime(year, month, day, hour, minute, second);
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"WMI 날짜시간 파싱 오류: {ex.Message}");
            }

            return DateTime.MinValue;
        }

        /// <summary>
        /// 프로세스와 네트워크 연결 매핑
        /// </summary>
        private async Task<List<ProcessNetworkInfo>> MapProcessToNetworkAsync(
            List<NetworkConnection> connections,
            Dictionary<int, ProcessInfo> processes)
        {
            return await Task.Run(() =>
            {
                var result = new List<ProcessNetworkInfo>();

                foreach (var connection in connections)
                {
                    if (processes.TryGetValue(connection.ProcessId, out var processInfo))
                    {
                        var processNetworkInfo = new ProcessNetworkInfo
                        {
                            // 프로세스 정보
                            ProcessName = processInfo.ProcessName,
                            ProcessId = processInfo.ProcessId,
                            ProcessPath = processInfo.ProcessPath,
                            ProcessStartTime = processInfo.ProcessStartTime,
                            FileVersion = processInfo.FileVersion,
                            CompanyName = processInfo.CompanyName,
                            IsSigned = processInfo.IsSigned,
                            IsSystemProcess = processInfo.IsSystemProcess,

                            // 네트워크 연결 정보
                            LocalAddress = connection.LocalAddress.ip,
                            LocalPort = connection.LocalAddress.port,
                            RemoteAddress = connection.RemoteAddress.ip,
                            RemotePort = connection.RemoteAddress.port,
                            Protocol = connection.Protocol,
                            ConnectionState = connection.State,
                            ConnectionStartTime = DateTime.Now.AddMinutes(-5), // 임시로 5분 전으로 설정

                            // 데이터 전송 정보 (실제로는 더 정교한 수집 필요)
                            DataTransferred = 0,
                            DataRate = 0,
                            PacketsSent = 0,
                            PacketsReceived = 0
                        };

                        result.Add(processNetworkInfo);
                    }
                }

                return result;
            });
        }

        /// <summary>
        /// 연결 정보로 프로세스 정보 조회
        /// </summary>
        public (int ProcessId, string ProcessName) GetProcessForConnection(ProtocolKind protocol, System.Net.IPAddress srcIp, int srcPort, System.Net.IPAddress dstIp, int dstPort)
        {
            var key = $"{protocol}:{srcIp}:{srcPort}-{dstIp}:{dstPort}";
            if (_connectionCache.TryGetValue(key, out var connection))
            {
                if (_processCache.TryGetValue(connection.ProcessId, out var process))
                {
                    return (process.ProcessId, process.ProcessName);
                }
            }
            return (0, "Unknown");
        }

        /// <summary>
        /// 오류 발생 이벤트 발생
        /// </summary>
        private void OnErrorOccurred(string message)
        {
            ErrorOccurred?.Invoke(this, message);
        }
    }

    /// <summary>
    /// 프로세스 정보
    /// </summary>
    public class ProcessInfo
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string ProcessPath { get; set; } = string.Empty;
        public DateTime ProcessStartTime { get; set; }
        public long UserModeTime { get; set; }
        public long WorkingSetSize { get; set; }
        public string FileVersion { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public bool IsSigned { get; set; }
        public bool IsSystemProcess { get; set; }
    }

    /// <summary>
    /// 네트워크 연결 정보
    /// </summary>
    public class NetworkConnection
    {
        public string Protocol { get; set; } = string.Empty;
        public (string ip, int port) LocalAddress { get; set; }
        public (string ip, int port) RemoteAddress { get; set; }
        public string State { get; set; } = string.Empty;
        public int ProcessId { get; set; }
    }
}
