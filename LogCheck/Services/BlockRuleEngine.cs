using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using LogCheck.Models;

namespace LogCheck.Services
{
    /// <summary>
    /// Rules.md 기반 3단계 차단 규칙 엔진
    /// 네트워크 연결을 분석하여 위험도에 따른 차단 결정을 내림
    /// </summary>
    public class BlockRuleEngine
    {
        #region 정적 규칙 데이터

        /// <summary>
        /// 알려진 악성 IP 주소 목록 (동적 업데이트 가능)
        /// </summary>
        private static readonly HashSet<string> MaliciousIPs = new()
        {
            // 테스트용 로컬 IP
            "192.168.1.100", "10.0.0.50", "127.0.0.2",
            
            // AbuseIPDB에서 확인된 실제 악성 IP들
            "185.220.100.240", "185.220.100.241", "185.220.101.32",
            "185.220.70.8", "185.220.70.39", "185.220.70.75",
            "198.96.155.3", "89.248.165.146", "45.95.169.157",
            "194.26.229.178", "80.82.77.139", "89.248.167.131",
            "185.220.102.8", "198.98.60.19", "104.248.144.120",
            "89.248.165.2", "45.95.169.0", "185.220.103.7",
            "185.220.103.119", "185.220.102.240", "185.220.102.241"
        };

        /// <summary>
        /// 의심스러운 포트 목록
        /// </summary>
        private static readonly int[] SuspiciousPorts =
        {
            // 일반적인 해킹 도구 포트
            1337, 31337, 12345, 54321, 9999, 4444, 5555,
            // IRC 봇넷 포트
            6666, 6667, 6668, 6669,
            // 기타 의심스러운 포트
            1234, 2222, 3333, 7777,
            // 비표준 웹 서버 포트 (의심스러운 경우)
            9090, 8080, 8888, 8443
        };

        /// <summary>
        /// 정당한 포트 화이트리스트 (의심스러운 포트라도 예외 처리)
        /// </summary>
        private static readonly int[] LegitimatePortsWhitelist =
        {
            // 표준 웹 서비스
            80, 443, 8080, 8443,
            // 이메일 서비스
            25, 110, 143, 993, 995, 587,
            // 파일 전송
            21, 22, 23,
            // 원격 접속
            3389, 5900,
            // Windows 서비스
            445, 139, 135, 137,
            // DNS
            53
        };

        /// <summary>
        /// 제한 국가 코드 목록
        /// </summary>
        private static readonly string[] RestrictedCountries =
        {
            "CN", "RU", "KP", "IR" // 중국, 러시아, 북한, 이란 (예시)
        };

        #endregion

        #region 생성자

        public BlockRuleEngine()
        {
        }

        /// <summary>
        /// 런타임에 악성 IP 목록에 IP 추가
        /// </summary>
        /// <param name="ipAddresses">추가할 IP 주소 목록</param>
        public static void AddMaliciousIPs(IEnumerable<string> ipAddresses)
        {
            if (ipAddresses != null)
            {
                foreach (var ip in ipAddresses)
                {
                    if (!string.IsNullOrWhiteSpace(ip))
                    {
                        MaliciousIPs.Add(ip.Trim());
                        System.Diagnostics.Debug.WriteLine($"악성 IP 추가됨: {ip}");
                    }
                }
            }
        }

        /// <summary>
        /// 현재 악성 IP 목록 조회
        /// </summary>
        public static IReadOnlySet<string> GetMaliciousIPs()
        {
            return MaliciousIPs;
        }

        #endregion

        #region 공개 메서드

        /// <summary>
        /// 프로세스 네트워크 연결을 평가하여 차단 결정을 내림
        /// </summary>
        /// <param name="processInfo">평가할 프로세스 네트워크 정보</param>
        /// <returns>차단 결정 정보</returns>
        public async Task<BlockDecision> EvaluateConnectionAsync(ProcessNetworkInfo processInfo)
        {
            try
            {
                var decision = new BlockDecision
                {
                    Level = BlockLevel.None,
                    Reason = "No threats detected",
                    ConfidenceScore = 0.0,
                    TriggeredRules = new List<string>(),
                    AnalyzedAt = DateTime.Now
                };

                // Level 1: 즉시 차단 규칙들 (최고 우선순위)
                if (await CheckLevel1RulesAsync(processInfo, decision))
                {
                    decision.Level = BlockLevel.Immediate;
                    decision.RecommendedAction = "즉시 연결을 차단하고 프로세스를 종료하세요.";
                    return decision;
                }

                // Level 2: 경고 후 차단 규칙들
                if (await CheckLevel2RulesAsync(processInfo, decision))
                {
                    decision.Level = BlockLevel.Warning;
                    decision.RecommendedAction = "사용자 확인 후 차단 여부를 결정하세요.";
                    return decision;
                }

                // Level 3: 모니터링 강화 규칙들
                if (await CheckLevel3RulesAsync(processInfo, decision))
                {
                    decision.Level = BlockLevel.Monitor;
                    decision.RecommendedAction = "지속적인 모니터링을 수행하세요.";
                    return decision;
                }

                return decision;
            }
            catch (Exception ex)
            {
                // 로그는 추후 LogHelper 사용
                System.Diagnostics.Debug.WriteLine($"Error evaluating connection for {processInfo.ProcessName}: {ex.Message}");

                // 오류 발생 시 안전한 기본값 반환
                return new BlockDecision
                {
                    Level = BlockLevel.Warning,
                    Reason = "분석 중 오류 발생",
                    ConfidenceScore = 0.5,
                    TriggeredRules = new List<string> { "Analysis Error" },
                    RecommendedAction = "수동 검토가 필요합니다."
                };
            }
        }

        #endregion

        #region Level 1: 즉시 차단 규칙

        /// <summary>
        /// Level 1 즉시 차단 규칙 검사
        /// </summary>
        private async Task<bool> CheckLevel1RulesAsync(ProcessNetworkInfo processInfo, BlockDecision decision)
        {
            bool shouldBlock = false;

            // 1.1 알려진 악성 IP/도메인 체크
            if (IsMaliciousIP(processInfo.RemoteAddress))
            {
                decision.TriggeredRules.Add("Known malicious IP");
                decision.ConfidenceScore += 0.9;
                decision.ThreatCategory = "Malware Communication";
                shouldBlock = true;
            }

            // 1.2 의심스러운 포트 사용 (화이트리스트 예외 처리)
            if (IsSuspiciousPort(processInfo.RemotePort) &&
                !IsWhitelistedPort(processInfo.RemotePort))
            {
                decision.TriggeredRules.Add($"Suspicious port: {processInfo.RemotePort}");
                decision.ConfidenceScore += 0.8;
                decision.ThreatCategory = "Suspicious Network Activity";
                shouldBlock = true;
            }

            // 1.3 System Idle Process 위장 탐지
            if (IsSystemIdleProcessForgery(processInfo))
            {
                decision.TriggeredRules.Add("System Idle Process forgery detected");
                decision.ConfidenceScore += 1.0;
                decision.ThreatCategory = "Process Impersonation";
                shouldBlock = true;
            }

            // 1.4 대용량 데이터 전송 탐지 (임계값 기반)
            if (IsAbnormalDataTransfer(processInfo))
            {
                decision.TriggeredRules.Add("Abnormal data transfer detected");
                decision.ConfidenceScore += 0.7;
                decision.ThreatCategory = "Data Exfiltration";
                shouldBlock = true;
            }

            // 1.5 프로세스 경로 위조 탐지
            if (await IsProcessPathSuspiciousAsync(processInfo))
            {
                decision.TriggeredRules.Add("Suspicious process path");
                decision.ConfidenceScore += 0.85;
                decision.ThreatCategory = "File System Anomaly";
                shouldBlock = true;
            }

            if (shouldBlock)
            {
                decision.Reason = $"Critical threat detected: {string.Join(", ", decision.TriggeredRules)}";
                decision.Details["ThreatLevel"] = "Critical";
                decision.Details["AutomaticAction"] = true;
            }

            return shouldBlock;
        }

        #endregion

        #region Level 2: 경고 차단 규칙

        /// <summary>
        /// Level 2 경고 차단 규칙 검사
        /// </summary>
        private async Task<bool> CheckLevel2RulesAsync(ProcessNetworkInfo processInfo, BlockDecision decision)
        {
            bool needsWarning = false;

            // 2.1 의심스러운 네트워크 패턴 (다수 연결, 다중 IP 등)
            if (await HasSuspiciousNetworkPatternAsync(processInfo))
            {
                decision.TriggeredRules.Add("Suspicious network pattern");
                decision.ConfidenceScore += 0.6;
                decision.ThreatCategory = "Network Anomaly";
                needsWarning = true;
            }

            // 2.2 알려지지 않은 프로세스 (디지털 서명 없음)
            if (await IsUnknownProcessAsync(processInfo))
            {
                decision.TriggeredRules.Add("Unknown/unsigned process");
                decision.ConfidenceScore += 0.5;
                decision.ThreatCategory = "Unknown Software";
                needsWarning = true;
            }

            // 2.3 외부 국가 IP 연결 (GeoIP 기반)
            if (await IsRestrictedCountryConnectionAsync(processInfo))
            {
                decision.TriggeredRules.Add("Connection to restricted country");
                decision.ConfidenceScore += 0.4;
                decision.ThreatCategory = "Geographic Risk";
                needsWarning = true;
            }

            // 2.4 비정상적 시간대 활동
            if (IsUnusualTimeActivity())
            {
                decision.TriggeredRules.Add("Activity during unusual hours");
                decision.ConfidenceScore += 0.3;
                decision.ThreatCategory = "Temporal Anomaly";
                needsWarning = true;
            }

            if (needsWarning)
            {
                decision.Reason = $"Suspicious activity detected: {string.Join(", ", decision.TriggeredRules)}";
                decision.Details["ThreatLevel"] = "Medium";
                decision.Details["UserConfirmationRequired"] = true;
            }

            return needsWarning;
        }

        #endregion

        #region Level 3: 모니터링 규칙

        /// <summary>
        /// Level 3 모니터링 강화 규칙 검사
        /// </summary>
        private async Task<bool> CheckLevel3RulesAsync(ProcessNetworkInfo processInfo, BlockDecision decision)
        {
            bool needsMonitoring = false;

            // 3.1 새로운 프로그램의 네트워크 활동
            if (await IsNewProgramNetworkActivityAsync(processInfo))
            {
                decision.TriggeredRules.Add("New program network activity");
                decision.ConfidenceScore += 0.3;
                decision.ThreatCategory = "New Software Activity";
                needsMonitoring = true;
            }

            // 3.2 비표준 포트 사용
            if (IsNonStandardPort(processInfo.RemotePort))
            {
                decision.TriggeredRules.Add($"Non-standard port usage: {processInfo.RemotePort}");
                decision.ConfidenceScore += 0.2;
                decision.ThreatCategory = "Port Anomaly";
                needsMonitoring = true;
            }

            // 3.3 주기적 통신 패턴 (봇넷 의심)
            if (await HasPeriodicCommunicationPatternAsync(processInfo))
            {
                decision.TriggeredRules.Add("Periodic communication pattern detected");
                decision.ConfidenceScore += 0.4;
                decision.ThreatCategory = "Botnet Suspicion";
                needsMonitoring = true;
            }

            // 3.4 프로세스 권한 이상
            if (await HasElevatedPrivilegesAsync(processInfo))
            {
                decision.TriggeredRules.Add("Process with elevated privileges");
                decision.ConfidenceScore += 0.25;
                decision.ThreatCategory = "Privilege Escalation";
                needsMonitoring = true;
            }

            if (needsMonitoring)
            {
                decision.Reason = $"Monitoring required: {string.Join(", ", decision.TriggeredRules)}";
                decision.Details["ThreatLevel"] = "Low";
                decision.Details["EnhancedMonitoring"] = true;
            }

            return needsMonitoring;
        }

        #endregion

        #region 헬퍼 메서드들

        /// <summary>
        /// IP 주소가 알려진 악성 IP인지 확인
        /// </summary>
        private bool IsMaliciousIP(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return false;

            bool isMalicious = MaliciousIPs.Contains(ipAddress);

            // 디버그 로그 추가
            if (isMalicious)
            {
                System.Diagnostics.Debug.WriteLine($"🚨 악성 IP 탐지: {ipAddress}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"✅ 정상 IP: {ipAddress} (악성 목록에 없음, 총 {MaliciousIPs.Count}개 악성 IP 등록됨)");
            }

            return isMalicious;
        }

        /// <summary>
        /// 포트가 의심스러운지 확인
        /// </summary>
        private bool IsSuspiciousPort(int port)
        {
            return SuspiciousPorts.Contains(port);
        }

        /// <summary>
        /// 포트가 화이트리스트에 있는지 확인
        /// </summary>
        private bool IsWhitelistedPort(int port)
        {
            return LegitimatePortsWhitelist.Contains(port);
        }

        /// <summary>
        /// System Idle Process 위조 탐지
        /// </summary>
        private bool IsSystemIdleProcessForgery(ProcessNetworkInfo process)
        {
            var processName = process.ProcessName?.Trim();

            if (string.IsNullOrWhiteSpace(processName))
                return false;

            if (processName == "System Idle Process")
            {
                // 정상적인 System Idle Process 확인
                if (IsLegitimateSystemIdleProcess(process))
                {
                    return false; // 정상적인 System Idle Process는 위조가 아님
                }

                // 위조된 System Idle Process 탐지
                // 실제 System Idle Process는 PID 0이어야 함
                if (process.ProcessId != 0) return true;

                // 실제 System Idle Process는 .exe 확장자가 없어야 함
                if (process.ProcessPath?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true) return true;

                // 실제 System Idle Process는 네트워크 연결이 없어야 함 (경고: 실제로는 연결이 있을 수 있음)
                // 이 조건은 너무 엄격할 수 있으므로 주석 처리
                // if (process.LocalPort > 0 || process.RemotePort > 0) return true;
            }

            // 유사한 이름 패턴 탐지
            var suspiciousNames = new[]
            {
                "System ldle Process",    // I 대신 소문자 l
                "System Idle Process.exe",
                "System  Idle Process",   // 공백 2개
                "System Idle  Process",   // 공백 2개
                "Systern Idle Process",   // m 대신 rn
                "Sys tem Idle Process",   // 공백 삽입
                "SystemIdleProcess",      // 공백 제거
                "System idle Process"     // 소문자 i
            };

            return suspiciousNames.Any(name =>
                string.Equals(processName, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 정상적인 System Idle Process인지 확인
        /// </summary>
        private bool IsLegitimateSystemIdleProcess(ProcessNetworkInfo process)
        {
            if (process?.ProcessName != "System Idle Process")
                return false;

            // 정상적인 System Idle Process 조건:
            // 1. PID가 0이어야 함
            // 2. ProcessPath가 비어있거나 .exe로 끝나지 않아야 함
            return process.ProcessId == 0 &&
                   (string.IsNullOrEmpty(process.ProcessPath) ||
                    !process.ProcessPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 대용량 데이터 전송 탐지
        /// </summary>
        private bool IsAbnormalDataTransfer(ProcessNetworkInfo process)
        {
            // 데이터 전송량이 임계값을 초과하는지 확인
            // 임계값: 1시간 내 100MB 이상
            const long threshold = 100 * 1024 * 1024; // 100MB

            // process.DataTransferred가 구현되어 있다면 사용
            // 현재는 기본적인 체크만 수행
            return process.DataTransferred > threshold;
        }

        /// <summary>
        /// 프로세스 경로가 의심스러운지 비동기 확인
        /// </summary>
        private async Task<bool> IsProcessPathSuspiciousAsync(ProcessNetworkInfo process)
        {
            // 임시 디렉토리, 시스템 디렉토리 외부 등에서 실행되는 시스템 프로세스명 확인
            if (string.IsNullOrWhiteSpace(process.ProcessPath))
                return false;

            var suspiciousPath = process.ProcessPath.ToLowerInvariant();

            // 의심스러운 경로 패턴들
            var suspiciousPatterns = new[]
            {
                @"c:\temp",
                @"c:\windows\temp",
                @"c:\users\.*\appdata\local\temp",
                @"c:\users\.*\downloads",
                @"\$recycle.bin",
                @"c:\programdata\[^\\]*\.exe$" // ProgramData 루트의 실행파일
            };

            return await Task.FromResult(
                suspiciousPatterns.Any(pattern =>
                    Regex.IsMatch(suspiciousPath, pattern, RegexOptions.IgnoreCase))
            );
        }

        #endregion

        #region 추후 구현할 메서드들 (스텁)

        private async Task<bool> HasSuspiciousNetworkPatternAsync(ProcessNetworkInfo processInfo)
        {
            // TODO: 네트워크 패턴 분석 구현
            await Task.Delay(1); // 비동기 시뮬레이션
            return false;
        }

        private async Task<bool> IsUnknownProcessAsync(ProcessNetworkInfo processInfo)
        {
            // TODO: 디지털 서명 검증 구현
            await Task.Delay(1);
            return false;
        }

        private async Task<bool> IsRestrictedCountryConnectionAsync(ProcessNetworkInfo processInfo)
        {
            // TODO: GeoIP 조회 구현
            await Task.Delay(1);
            return false;
        }

        private bool IsUnusualTimeActivity()
        {
            // TODO: 시간대 기반 분석 구현
            var currentHour = DateTime.Now.Hour;
            // 새벽 2시~6시를 의심스러운 시간대로 가정
            return currentHour >= 2 && currentHour <= 6;
        }

        private async Task<bool> IsNewProgramNetworkActivityAsync(ProcessNetworkInfo processInfo)
        {
            // TODO: 새 프로그램 탐지 구현
            await Task.Delay(1);
            return false;
        }

        private bool IsNonStandardPort(int port)
        {
            // 표준 포트 범위 (0-1023) 외부의 포트 사용
            return port > 1023 && !LegitimatePortsWhitelist.Contains(port);
        }

        private async Task<bool> HasPeriodicCommunicationPatternAsync(ProcessNetworkInfo processInfo)
        {
            // TODO: 주기적 통신 패턴 분석 구현
            await Task.Delay(1);
            return false;
        }

        private async Task<bool> HasElevatedPrivilegesAsync(ProcessNetworkInfo processInfo)
        {
            // TODO: 프로세스 권한 확인 구현
            await Task.Delay(1);
            return false;
        }

        #endregion
    }
}