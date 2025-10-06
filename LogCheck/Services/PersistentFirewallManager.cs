using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Threading.Tasks;

namespace LogCheck.Services
{
    /// <summary>
    /// Windows 방화벽을 통한 영구 연결 차단 관리 서비스
    /// 동적 COM Interop을 사용하여 방화벽 규칙을 관리합니다.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class PersistentFirewallManager : IDisposable
    {
        private readonly string _ruleNamePrefix;
        private dynamic? _firewallPolicy;
        private readonly EventLog _eventLog;

        // COM 상수들을 직접 정의
        private const int NET_FW_ACTION_BLOCK = 0;
        private const int NET_FW_ACTION_ALLOW = 1;
        private const int NET_FW_RULE_DIR_IN = 1;
        private const int NET_FW_RULE_DIR_OUT = 2;
        private const int NET_FW_IP_PROTOCOL_TCP = 6;
        private const int NET_FW_IP_PROTOCOL_UDP = 17;
        private const int NET_FW_PROFILE2_ALL = 2147483647;

        public PersistentFirewallManager(string ruleNamePrefix = "LogCheck_Block")
        {
            _ruleNamePrefix = ruleNamePrefix;

            // Windows Event Log 초기화 (Windows Sentinel 소스)
            _eventLog = new EventLog();
            try
            {
                if (!EventLog.SourceExists("WindowsSentinel"))
                {
                    EventLog.CreateEventSource("WindowsSentinel", "Application");
                }
                _eventLog.Source = "WindowsSentinel";
            }
            catch (Exception)
            {
                // Event Log 초기화 실패 시 기본 Application 로그 사용
                _eventLog.Log = "Application";
            }
        }

        /// <summary>
        /// 방화벽 정책 초기화
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                if (!IsAdministrator())
                {
                    throw new UnauthorizedAccessException("방화벽 규칙 관리에는 관리자 권한이 필요합니다.");
                }

                await Task.Run(() =>
                {
                    var policyType = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
                    if (policyType == null)
                    {
                        throw new COMException("Windows Firewall COM 객체를 찾을 수 없습니다.");
                    }

                    _firewallPolicy = Activator.CreateInstance(policyType);
                    if (_firewallPolicy == null)
                    {
                        throw new COMException("Windows Firewall 정책 객체 생성에 실패했습니다.");
                    }
                });

                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"방화벽 초기화 실패: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 프로세스별 영구 차단 규칙 추가
        /// </summary>
        public async Task<bool> AddPermanentProcessBlockRuleAsync(string processPath, string processName)
        {
            try
            {
                if (_firewallPolicy == null)
                {
                    throw new InvalidOperationException("방화벽 정책이 초기화되지 않았습니다.");
                }

                return await Task.Run(() =>
                {
                    string ruleName = $"{_ruleNamePrefix}_{processName}_{DateTime.Now:yyyyMMdd_HHmmss}";

                    // Inbound 규칙 생성
                    CreateFirewallRule(ruleName + "_IN", processPath, NET_FW_RULE_DIR_IN);

                    // Outbound 규칙 생성  
                    CreateFirewallRule(ruleName + "_OUT", processPath, NET_FW_RULE_DIR_OUT);

                    // 성공 로깅
                    LogFirewallAction("프로세스 차단 규칙 추가", ruleName, true, $"프로세스: {processPath}");
                    LogSecurityEvent("프로세스 자동 차단", $"Path: {processPath}, Name: {processName}", "네트워크 활동 차단", EventLogEntryType.Information);

                    return true;
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"프로세스 차단 규칙 생성 실패: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// IP 주소별 영구 차단 규칙 추가
        /// </summary>
        public async Task<bool> AddPermanentIPBlockRuleAsync(string ipAddress, string description = "")
        {
            try
            {
                if (_firewallPolicy == null)
                {
                    throw new InvalidOperationException("방화벽 정책이 초기화되지 않았습니다.");
                }

                return await Task.Run(() =>
                {
                    string ruleName = $"{_ruleNamePrefix}_IP_{ipAddress.Replace(".", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}";

                    // Inbound IP 차단
                    CreateIPBlockRule(ruleName + "_IN", ipAddress, NET_FW_RULE_DIR_IN, description);

                    // Outbound IP 차단
                    CreateIPBlockRule(ruleName + "_OUT", ipAddress, NET_FW_RULE_DIR_OUT, description);

                    // 성공 로깅
                    LogFirewallAction("IP 차단 규칙 추가", ruleName, true, $"IP: {ipAddress}, 설명: {description}");
                    LogSecurityEvent("IP 주소 자동 차단", $"IP: {ipAddress}", "의심스러운 네트워크 활동", EventLogEntryType.Warning);

                    return true;
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"IP 차단 규칙 생성 실패: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 포트별 영구 차단 규칙 추가
        /// </summary>
        public async Task<bool> AddPermanentPortBlockRuleAsync(int port, int protocol = NET_FW_IP_PROTOCOL_TCP, string description = "")
        {
            try
            {
                if (_firewallPolicy == null)
                {
                    throw new InvalidOperationException("방화벽 정책이 초기화되지 않았습니다.");
                }

                return await Task.Run(() =>
                {
                    string protocolName = protocol == NET_FW_IP_PROTOCOL_TCP ? "TCP" : "UDP";
                    string ruleName = $"{_ruleNamePrefix}_Port_{port}_{protocolName}_{DateTime.Now:yyyyMMdd_HHmmss}";

                    // Inbound 포트 차단
                    CreatePortBlockRule(ruleName + "_IN", port, protocol, NET_FW_RULE_DIR_IN, description);

                    // Outbound 포트 차단
                    CreatePortBlockRule(ruleName + "_OUT", port, protocol, NET_FW_RULE_DIR_OUT, description);

                    return true;
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"포트 차단 규칙 생성 실패: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 방화벽 규칙 생성 (프로세스용)
        /// </summary>
        private void CreateFirewallRule(string ruleName, string applicationPath, int direction)
        {
            try
            {
                var ruleType = Type.GetTypeFromProgID("HNetCfg.FWRule");
                if (ruleType == null)
                {
                    throw new COMException("방화벽 규칙 COM 객체를 찾을 수 없습니다.");
                }

                dynamic rule = Activator.CreateInstance(ruleType);

                rule.Name = ruleName;
                rule.Description = $"LogCheck에 의해 생성된 프로세스 차단 규칙: {applicationPath}";
                rule.ApplicationName = applicationPath;
                rule.Action = NET_FW_ACTION_BLOCK;
                rule.Direction = direction;
                rule.Enabled = true;
                rule.Profiles = NET_FW_PROFILE2_ALL;

                _firewallPolicy.Rules.Add(rule);
            }
            finally
            {
                // COM 객체 해제는 GC가 처리하도록 함
            }
        }

        /// <summary>
        /// IP 차단 규칙 생성
        /// </summary>
        private void CreateIPBlockRule(string ruleName, string ipAddress, int direction, string description)
        {
            try
            {
                var ruleType = Type.GetTypeFromProgID("HNetCfg.FWRule");
                if (ruleType == null)
                {
                    throw new COMException("방화벽 규칙 COM 객체를 찾을 수 없습니다.");
                }

                dynamic rule = Activator.CreateInstance(ruleType);

                rule.Name = ruleName;
                rule.Description = string.IsNullOrEmpty(description)
                    ? $"LogCheck에 의해 생성된 IP 차단 규칙: {ipAddress}"
                    : description;
                rule.RemoteAddresses = ipAddress;
                rule.Action = NET_FW_ACTION_BLOCK;
                rule.Direction = direction;
                rule.Enabled = true;
                rule.Profiles = NET_FW_PROFILE2_ALL;

                _firewallPolicy.Rules.Add(rule);
            }
            finally
            {
                // COM 객체 해제는 GC가 처리하도록 함
            }
        }

        /// <summary>
        /// 포트 차단 규칙 생성
        /// </summary>
        private void CreatePortBlockRule(string ruleName, int port, int protocol, int direction, string description)
        {
            try
            {
                var ruleType = Type.GetTypeFromProgID("HNetCfg.FWRule");
                if (ruleType == null)
                {
                    throw new COMException("방화벽 규칙 COM 객체를 찾을 수 없습니다.");
                }

                dynamic rule = Activator.CreateInstance(ruleType);

                string protocolName = protocol == NET_FW_IP_PROTOCOL_TCP ? "TCP" : "UDP";

                rule.Name = ruleName;
                rule.Description = string.IsNullOrEmpty(description)
                    ? $"LogCheck에 의해 생성된 포트 차단 규칙: {port}/{protocolName}"
                    : description;
                rule.Protocol = protocol;
                rule.LocalPorts = port.ToString();
                rule.Action = NET_FW_ACTION_BLOCK;
                rule.Direction = direction;
                rule.Enabled = true;
                rule.Profiles = NET_FW_PROFILE2_ALL;

                _firewallPolicy.Rules.Add(rule);
            }
            finally
            {
                // COM 객체 해제는 GC가 처리하도록 함
            }
        }

        /// <summary>
        /// 특정 규칙이 존재하는지 확인
        /// </summary>
        public async Task<bool> RuleExistsAsync(string ruleName)
        {
            try
            {
                if (_firewallPolicy == null)
                {
                    return false;
                }

                return await Task.Run(() =>
                {
                    foreach (dynamic rule in _firewallPolicy.Rules)
                    {
                        if (rule.Name == ruleName)
                        {
                            return true;
                        }
                    }
                    return false;
                });
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 차단 규칙 제거
        /// </summary>
        public async Task<bool> RemoveBlockRuleAsync(string ruleName)
        {
            try
            {
                if (_firewallPolicy == null)
                {
                    throw new InvalidOperationException("방화벽 정책이 초기화되지 않았습니다.");
                }

                return await Task.Run(() =>
                {
                    try
                    {
                        _firewallPolicy.Rules.Remove(ruleName);
                        return true;
                    }
                    catch (COMException)
                    {
                        // 규칙이 존재하지 않는 경우
                        return false;
                    }
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"방화벽 규칙 제거 실패: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// LogCheck 관련 모든 규칙 조회
        /// </summary>
        public async Task<List<FirewallRuleInfo>> GetLogCheckRulesAsync()
        {
            try
            {
                if (_firewallPolicy == null)
                {
                    return new List<FirewallRuleInfo>();
                }

                return await Task.Run(() =>
                {
                    var rules = new List<FirewallRuleInfo>();

                    foreach (dynamic rule in _firewallPolicy.Rules)
                    {
                        string ruleName = rule.Name;
                        if (ruleName.StartsWith(_ruleNamePrefix))
                        {
                            rules.Add(new FirewallRuleInfo
                            {
                                Name = ruleName,
                                Description = rule.Description,
                                ApplicationName = rule.ApplicationName ?? string.Empty,
                                RemoteAddresses = rule.RemoteAddresses ?? string.Empty,
                                RemotePorts = rule.RemotePorts ?? string.Empty,
                                Protocol = rule.Protocol,
                                Direction = rule.Direction,
                                Enabled = rule.Enabled,
                                CreatedDate = DateTime.Now // COM에서 생성일 추출이 어려우므로 현재시간 사용
                            });
                        }
                    }

                    return rules;
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"방화벽 규칙 조회 실패: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// LogCheck 관련 모든 규칙 제거
        /// </summary>
        public async Task<int> RemoveAllLogCheckRulesAsync()
        {
            try
            {
                if (_firewallPolicy == null)
                {
                    throw new InvalidOperationException("방화벽 정책이 초기화되지 않았습니다.");
                }

                return await Task.Run(() =>
                {
                    var rulesToRemove = new List<string>();

                    // 제거할 규칙 목록 수집
                    foreach (dynamic rule in _firewallPolicy.Rules)
                    {
                        string ruleName = rule.Name;
                        if (ruleName.StartsWith(_ruleNamePrefix))
                        {
                            rulesToRemove.Add(ruleName);
                        }
                    }

                    // 규칙 제거
                    foreach (string ruleName in rulesToRemove)
                    {
                        try
                        {
                            _firewallPolicy.Rules.Remove(ruleName);
                        }
                        catch (COMException)
                        {
                            // 개별 규칙 제거 실패는 무시
                        }
                    }

                    return rulesToRemove.Count;
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"LogCheck 규칙 일괄 제거 실패: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 데이터베이스에서 차단 규칙을 복구
        /// </summary>
        public async Task<int> RestoreBlockRulesFromDatabase()
        {
            try
            {
                if (_firewallPolicy == null)
                {
                    await InitializeAsync();
                }

                var restoredCount = 0;
                var connectionString = "Data Source=autoblock.db";

                using var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
                await connection.OpenAsync();

                // 차단된 연결들로부터 방화벽 규칙 복구
                var sql = @"
                    SELECT DISTINCT ProcessName, ProcessPath, RemoteAddress, RemotePort, Protocol
                    FROM BlockedConnections 
                    WHERE BlockedAt > datetime('now', '-30 days')
                    ORDER BY BlockedAt DESC";

                using var command = connection.CreateCommand();
                command.CommandText = sql;

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    try
                    {
                        var processName = reader.IsDBNull(0) ? "Unknown" : reader.GetString(0);
                        var processPath = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        var remoteAddress = reader.IsDBNull(2) ? "" : reader.GetString(2);
                        var remotePort = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                        var protocol = reader.IsDBNull(4) ? "TCP" : reader.GetString(4);

                        // 규칙 이름 생성
                        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        var ruleName = $"{_ruleNamePrefix}_{processName}_{timestamp}_{restoredCount}";

                        // 기존 규칙 존재 여부 확인
                        if (!await RuleExistsAsync(ruleName))
                        {
                            // 프로세스 경로 기반 규칙 생성 시도
                            if (!string.IsNullOrEmpty(processPath) && System.IO.File.Exists(processPath))
                            {
                                var success = await AddPermanentProcessBlockRuleAsync(processPath, processName);
                                if (success)
                                {
                                    restoredCount++;
                                    System.Diagnostics.Debug.WriteLine($"Restored firewall rule for process: {processName}");
                                }
                            }
                            // IP 기반 규칙 생성 시도
                            else if (!string.IsNullOrEmpty(remoteAddress) &&
                                     System.Net.IPAddress.TryParse(remoteAddress, out _))
                            {
                                var success = await AddPermanentIPBlockRuleAsync(remoteAddress,
                                    $"Auto-restored block for {processName}");
                                if (success)
                                {
                                    restoredCount++;
                                    System.Diagnostics.Debug.WriteLine($"Restored firewall rule for IP: {remoteAddress}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to restore individual rule: {ex.Message}");
                        // 개별 규칙 복구 실패는 전체 작업을 중단하지 않음
                        continue;
                    }
                }

                // 복구 완료 로깅
                LogFirewallAction("방화벽 규칙 복구 완료", "복구 작업", true, $"총 {restoredCount}개 규칙 복구");
                LogSecurityEvent("시스템 시작 시 방화벽 복구", $"복구된 규칙: {restoredCount}개", "보안 정책 자동 복원", EventLogEntryType.Information);

                System.Diagnostics.Debug.WriteLine($"Restored {restoredCount} firewall rules from database");
                return restoredCount;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RestoreBlockRulesFromDatabase error: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Event Log에 방화벽 작업 기록
        /// </summary>
        private void LogFirewallAction(string action, string ruleName, bool success, string? details = null)
        {
            try
            {
                var eventType = success ? EventLogEntryType.Information : EventLogEntryType.Warning;
                var message = $"방화벽 {action}: {ruleName}";
                if (!string.IsNullOrEmpty(details))
                {
                    message += $" - {details}";
                }

                _eventLog.WriteEntry(message, eventType, success ? 1001 : 2001);
                System.Diagnostics.Debug.WriteLine($"[EventLog] {message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Event Log 기록 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 중요한 방화벽 보안 이벤트 기록
        /// </summary>
        private void LogSecurityEvent(string action, string processInfo, string threat, EventLogEntryType type = EventLogEntryType.Warning)
        {
            try
            {
                var message = $"보안 이벤트 - {action}: {processInfo} (위험: {threat})";
                var eventId = type switch
                {
                    EventLogEntryType.Information => 3001,
                    EventLogEntryType.Warning => 3002,
                    EventLogEntryType.Error => 3003,
                    _ => 3000
                };

                _eventLog.WriteEntry(message, type, eventId);
                System.Diagnostics.Debug.WriteLine($"[SecurityLog] {message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Security Event Log 기록 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 관리자 권한 확인
        /// </summary>
        private static bool IsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 리소스 해제
        /// </summary>
        public void Dispose()
        {
            if (_firewallPolicy != null)
            {
                // COM 객체는 .NET의 GC가 자동으로 해제하므로 명시적 해제 불필요
                _firewallPolicy = null;
            }

            // EventLog 리소스 해제
            try
            {
                _eventLog?.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EventLog Dispose 실패: {ex.Message}");
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 모든 LogCheck 방화벽 규칙 조회
        /// </summary>
        private async Task<List<FirewallRuleInfo>> GetAllLogCheckRulesAsync()
        {
            var rules = new List<FirewallRuleInfo>();

            return await Task.Run(() =>
            {
                try
                {
                    if (_firewallPolicy?.Rules == null) return rules;

                    foreach (dynamic rule in _firewallPolicy.Rules)
                    {
                        if (rule?.Name?.ToString()?.StartsWith(_ruleNamePrefix) == true)
                        {
                            var ruleInfo = new FirewallRuleInfo
                            {
                                Name = rule.Name?.ToString() ?? "",
                                Description = rule.Description?.ToString() ?? "",
                                ApplicationName = rule.ApplicationName?.ToString() ?? "",
                                RemoteAddresses = rule.RemoteAddresses?.ToString() ?? "",
                                RemotePorts = rule.RemotePorts?.ToString() ?? "",
                                Protocol = rule.Protocol ?? 0,
                                Direction = rule.Direction ?? 0,
                                Enabled = rule.Enabled ?? false,
                                CreatedDate = DateTime.Now // COM에서는 생성일 직접 조회 불가
                            };
                            rules.Add(ruleInfo);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"방화벽 규칙 조회 오류: {ex.Message}");
                }

                return rules;
            });
        }

        /// <summary>
        /// 특정 방화벽 규칙 제거
        /// </summary>
        private async Task<bool> RemoveFirewallRuleAsync(string ruleName)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (_firewallPolicy?.Rules == null) return false;

                    _firewallPolicy.Rules.Remove(ruleName);
                    LogFirewallAction("방화벽 규칙 제거", ruleName, true, "수동 제거");
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"방화벽 규칙 제거 오류 ({ruleName}): {ex.Message}");
                    LogFirewallAction("방화벽 규칙 제거 실패", ruleName, false, ex.Message);
                    return false;
                }
            });
        }

        /// <summary>
        /// Windows Firewall 규칙과 데이터베이스 동기화
        /// </summary>
        public async Task<SyncResult> SynchronizeFirewallWithDatabaseAsync()
        {
            var syncResult = new SyncResult();

            try
            {
                if (_firewallPolicy == null)
                {
                    await InitializeAsync();
                }

                var connectionString = "Data Source=autoblock.db";
                var existingRules = await GetAllLogCheckRulesAsync();
                var dbConnections = new HashSet<string>();

                using var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
                await connection.OpenAsync();

                // 데이터베이스에서 차단된 연결 확인
                var sql = @"
                    SELECT DISTINCT ProcessName, ProcessPath, RemoteAddress 
                    FROM BlockedConnections 
                    WHERE BlockedAt > datetime('now', '-30 days')";

                using var command = connection.CreateCommand();
                command.CommandText = sql;

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var processName = reader.GetString(0);
                    var processPath = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    var remoteAddress = reader.IsDBNull(2) ? "" : reader.GetString(2);

                    // 연결 식별자 생성
                    var connectionId = !string.IsNullOrEmpty(processPath)
                        ? $"process_{processPath}"
                        : $"ip_{remoteAddress}";

                    dbConnections.Add(connectionId);

                    // 해당하는 방화벽 규칙이 없으면 생성
                    var hasMatchingRule = existingRules.Any(r =>
                        r.ApplicationName.Equals(processPath, StringComparison.OrdinalIgnoreCase) ||
                        r.RemoteAddresses.Contains(remoteAddress));

                    if (!hasMatchingRule)
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(processPath) && System.IO.File.Exists(processPath))
                            {
                                await AddPermanentProcessBlockRuleAsync(processPath, processName);
                                syncResult.RulesAdded++;
                                LogFirewallAction("동기화 중 규칙 추가", processName, true, $"프로세스: {processPath}");
                            }
                            else if (!string.IsNullOrEmpty(remoteAddress))
                            {
                                await AddPermanentIPBlockRuleAsync(remoteAddress, $"동기화로 추가된 {processName} 차단");
                                syncResult.RulesAdded++;
                                LogFirewallAction("동기화 중 IP 규칙 추가", remoteAddress, true, $"IP: {remoteAddress}");
                            }
                        }
                        catch (Exception ex)
                        {
                            syncResult.Errors.Add($"규칙 추가 실패 - {processName}: {ex.Message}");
                            LogFirewallAction("동기화 중 규칙 추가 실패", processName, false, ex.Message);
                        }
                    }
                }

                // 데이터베이스에 없는 오래된 LogCheck 규칙 제거
                foreach (var rule in existingRules)
                {
                    var shouldKeep = false;

                    // 프로세스 경로 기반 규칙 확인
                    if (!string.IsNullOrEmpty(rule.ApplicationName))
                    {
                        var connectionId = $"process_{rule.ApplicationName}";
                        shouldKeep = dbConnections.Contains(connectionId);
                    }

                    // IP 기반 규칙 확인
                    if (!shouldKeep && !string.IsNullOrEmpty(rule.RemoteAddresses))
                    {
                        var ips = rule.RemoteAddresses.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        shouldKeep = ips.Any(ip => dbConnections.Contains($"ip_{ip.Trim()}"));
                    }

                    // 30일 이상 오래된 규칙은 제거
                    if (!shouldKeep && rule.CreatedDate < DateTime.Now.AddDays(-30))
                    {
                        try
                        {
                            await RemoveFirewallRuleAsync(rule.Name);
                            syncResult.RulesRemoved++;
                            LogFirewallAction("동기화 중 오래된 규칙 제거", rule.Name, true, "30일 경과");
                        }
                        catch (Exception ex)
                        {
                            syncResult.Errors.Add($"규칙 제거 실패 - {rule.Name}: {ex.Message}");
                            LogFirewallAction("동기화 중 규칙 제거 실패", rule.Name, false, ex.Message);
                        }
                    }
                }

                syncResult.Success = true;
                LogSecurityEvent("방화벽 동기화 완료",
                    $"추가: {syncResult.RulesAdded}, 제거: {syncResult.RulesRemoved}, 오류: {syncResult.Errors.Count}",
                    "자동 동기화", EventLogEntryType.Information);

                return syncResult;
            }
            catch (Exception ex)
            {
                syncResult.Success = false;
                syncResult.Errors.Add($"동기화 실패: {ex.Message}");
                LogSecurityEvent("방화벽 동기화 실패", ex.Message, "시스템 오류", EventLogEntryType.Error);

                System.Diagnostics.Debug.WriteLine($"Firewall synchronization failed: {ex.Message}");
                return syncResult;
            }
        }

        /// <summary>
        /// 정기적 동기화 작업 시작 (24시간 간격)
        /// </summary>
        public void StartPeriodicSynchronization()
        {
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromHours(24)); // 24시간 대기
                        var result = await SynchronizeFirewallWithDatabaseAsync();

                        System.Diagnostics.Debug.WriteLine($"정기 동기화 완료 - 추가: {result.RulesAdded}, 제거: {result.RulesRemoved}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"정기 동기화 오류: {ex.Message}");

                        // 오류 발생 시 1시간 후 재시도
                        await Task.Delay(TimeSpan.FromHours(1));
                    }
                }
            });
        }
    }

    /// <summary>
    /// 동기화 결과 정보
    /// </summary>
    public class SyncResult
    {
        public bool Success { get; set; }
        public int RulesAdded { get; set; }
        public int RulesRemoved { get; set; }
        public List<string> Errors { get; set; } = new List<string>();

        public override string ToString()
        {
            return $"동기화 {(Success ? "성공" : "실패")} - 추가: {RulesAdded}, 제거: {RulesRemoved}, 오류: {Errors.Count}개";
        }
    }

    /// <summary>
    /// 방화벽 규칙 방향 열거형
    /// </summary>
    public enum FirewallDirection
    {
        Inbound = 1,
        Outbound = 2
    }

    /// <summary>
    /// 방화벽 규칙 정보 클래스
    /// </summary>
    public class FirewallRuleInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ApplicationName { get; set; } = string.Empty;
        public string RemoteAddresses { get; set; } = string.Empty;
        public string RemotePorts { get; set; } = string.Empty;
        public int Protocol { get; set; }
        public int Direction { get; set; }
        public bool Enabled { get; set; }
        public DateTime CreatedDate { get; set; }

        public string ProtocolName => Protocol == 6 ? "TCP" : (Protocol == 17 ? "UDP" : "Other");
        public string DirectionName => Direction == 1 ? "Inbound" : "Outbound";
    }
}