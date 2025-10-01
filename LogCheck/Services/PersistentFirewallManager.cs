using System;
using System.Collections.Generic;
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
            GC.SuppressFinalize(this);
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