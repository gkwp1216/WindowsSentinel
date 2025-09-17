using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using LogCheck.Models;

namespace LogCheck.Services
{
    public class SecurityAnalyzer
    {
        private readonly List<MaliciousIP> _maliciousIPs;
        private readonly List<LogCheck.Models.SecurityAlert> _securityAlerts;
        private readonly Dictionary<string, SuspiciousActivity> _suspiciousActivities;

        // 알려진 악성 포트 목록
        private readonly HashSet<int> _suspiciousPorts = new HashSet<int>
        {
            1433, 1521, 3306, 5432, // 데이터베이스 포트
            23, 135, 139, 445, 593, // 취약한 서비스 포트
            4444, 5555, 6666, 7777, // 일반적인 백도어 포트
            31337, 12345, 54321 // 알려진 트로이목마 포트
        };

        // 정상적인 포트 목록
        private readonly HashSet<int> _legitimatePorts = new HashSet<int>
        {
            80, 443, 53, 21, 22, 25, 110, 143, 993, 995
        };

        public SecurityAnalyzer()
        {
            _maliciousIPs = new List<MaliciousIP>();
            _securityAlerts = new List<LogCheck.Models.SecurityAlert>();
            _suspiciousActivities = new Dictionary<string, SuspiciousActivity>();

            InitializeMaliciousIPDatabase();
        }

        // 악성 IP 데이터베이스 초기화 (샘플 데이터)
        private void InitializeMaliciousIPDatabase()
        {
            var sampleMaliciousIPs = new[]
            {
                new MaliciousIP { IPAddress = "192.168.1.100", Category = "Botnet", Source = "Internal Detection", LastUpdated = DateTime.Now },
                new MaliciousIP { IPAddress = "10.0.0.50", Category = "Malware", Source = "Threat Intelligence", LastUpdated = DateTime.Now },
                new MaliciousIP { IPAddress = "172.16.0.25", Category = "Phishing", Source = "Security Feed", LastUpdated = DateTime.Now }
            };

            _maliciousIPs.AddRange(sampleMaliciousIPs);
        }

        // 네트워크 패킷 보안 분석
        public async Task<List<LogCheck.Models.SecurityAlert>> AnalyzePacketAsync(NetworkUsageRecord packet)
        {
            var alerts = new List<LogCheck.Models.SecurityAlert>();

            try
            {
                // 1. 악성 IP 검사
                var maliciousIPAlert = CheckMaliciousIP(packet);
                if (maliciousIPAlert != null)
                    alerts.Add(maliciousIPAlert);

                // 2. 의심스러운 포트 검사
                var suspiciousPortAlert = CheckSuspiciousPort(packet);
                if (suspiciousPortAlert != null)
                    alerts.Add(suspiciousPortAlert);

                // 3. 비정상적인 트래픽 패턴 검사
                var trafficPatternAlert = await CheckTrafficPatternAsync(packet);
                if (trafficPatternAlert != null)
                    alerts.Add(trafficPatternAlert);

                // 4. 대용량 데이터 전송 검사
                var dataVolumeAlert = CheckDataVolume(packet);
                if (dataVolumeAlert != null)
                    alerts.Add(dataVolumeAlert);

                // 경고 저장
                foreach (var alert in alerts)
                {
                    _securityAlerts.Add(alert);
                    LogHelper.LogWarning($"보안 경고 생성: {alert.AlertType} - {alert.Description}");
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"보안 분석 중 오류: {ex.Message}");
            }

            return alerts;
        }

        // 악성 IP 검사
        private LogCheck.Models.SecurityAlert? CheckMaliciousIP(NetworkUsageRecord packet)
        {
            var maliciousIP = _maliciousIPs.FirstOrDefault(ip =>
                ip.IPAddress == packet.SourceIP || ip.IPAddress == packet.DestinationIP);

            if (maliciousIP != null)
            {
                return new LogCheck.Models.SecurityAlert
                {
                    Timestamp = DateTime.Now,
                    AlertType = "악성 IP 감지",
                    Severity = "High",
                    SourceIP = packet.SourceIP,
                    DestinationIP = packet.DestinationIP,
                    SourcePort = packet.SourcePort,
                    DestinationPort = packet.DestinationPort,
                    Protocol = packet.Protocol,
                    Description = $"알려진 악성 IP와의 통신 감지: {maliciousIP.IPAddress}",
                    Details = $"카테고리: {maliciousIP.Category}, 출처: {maliciousIP.Source}",
                    Action = "Monitored"
                };
            }

            return null;
        }

        // 의심스러운 포트 검사
        private LogCheck.Models.SecurityAlert? CheckSuspiciousPort(NetworkUsageRecord packet)
        {
            if (_suspiciousPorts.Contains(packet.DestinationPort) || _suspiciousPorts.Contains(packet.SourcePort))
            {
                var suspiciousPort = _suspiciousPorts.Contains(packet.DestinationPort) ? packet.DestinationPort : packet.SourcePort;

                return new LogCheck.Models.SecurityAlert
                {
                    Timestamp = DateTime.Now,
                    AlertType = "의심스러운 포트 사용",
                    Severity = "Medium",
                    SourceIP = packet.SourceIP,
                    DestinationIP = packet.DestinationIP,
                    SourcePort = packet.SourcePort,
                    DestinationPort = packet.DestinationPort,
                    Protocol = packet.Protocol,
                    Description = $"의심스러운 포트 사용 감지: {suspiciousPort}",
                    Details = GetPortDescription(suspiciousPort),
                    Action = "Monitored"
                };
            }

            return null;
        }

        // 트래픽 패턴 분석
        private async Task<LogCheck.Models.SecurityAlert?> CheckTrafficPatternAsync(NetworkUsageRecord packet)
        {
            var key = $"{packet.SourceIP}_{packet.DestinationIP}";

            if (!_suspiciousActivities.ContainsKey(key))
            {
                _suspiciousActivities[key] = new SuspiciousActivity
                {
                    Timestamp = DateTime.Now,
                    ActivityType = "연결 패턴 분석",
                    SourceIP = packet.SourceIP,
                    ConnectionCount = 0,
                    DataVolume = 0
                };
            }

            var activity = _suspiciousActivities[key];
            activity.ConnectionCount++;
            activity.DataVolume += packet.PacketSize;

            // 5분 내 동일 IP 간 50회 이상 연결 시 의심스러운 활동으로 판단
            if (activity.ConnectionCount > 50 &&
                (DateTime.Now - activity.Timestamp).TotalMinutes <= 5)
            {
                activity.RiskScore = CalculateRiskScore(activity);

                if (activity.RiskScore > 7.0) // 위험도 7점 이상
                {
                    return new LogCheck.Models.SecurityAlert
                    {
                        Timestamp = DateTime.Now,
                        AlertType = "비정상적인 트래픽 패턴",
                        Severity = "High",
                        SourceIP = packet.SourceIP,
                        DestinationIP = packet.DestinationIP,
                        Protocol = packet.Protocol,
                        Description = $"비정상적인 연결 패턴 감지: {activity.ConnectionCount}회 연결",
                        Details = $"위험도: {activity.RiskScore:F1}/10, 데이터량: {FormatBytes(activity.DataVolume)}",
                        Action = "Monitored"
                    };
                }
            }

            return null;
        }

        // 대용량 데이터 전송 검사
        private LogCheck.Models.SecurityAlert? CheckDataVolume(NetworkUsageRecord packet)
        {
            // 단일 패킷이 10MB 이상인 경우
            if (packet.PacketSize > 10 * 1024 * 1024)
            {
                return new LogCheck.Models.SecurityAlert
                {
                    Timestamp = DateTime.Now,
                    AlertType = "대용량 데이터 전송",
                    Severity = "Medium",
                    SourceIP = packet.SourceIP,
                    DestinationIP = packet.DestinationIP,
                    SourcePort = packet.SourcePort,
                    DestinationPort = packet.DestinationPort,
                    Protocol = packet.Protocol,
                    Description = $"대용량 데이터 전송 감지: {FormatBytes(packet.PacketSize)}",
                    Details = $"정상 범위를 초과하는 데이터 전송량",
                    Action = "Monitored"
                };
            }

            return null;
        }

        // 위험도 계산
        private double CalculateRiskScore(SuspiciousActivity activity)
        {
            double score = 0;

            // 연결 횟수 기반 점수 (최대 4점)
            score += Math.Min(activity.ConnectionCount / 25.0, 4.0);

            // 데이터량 기반 점수 (최대 3점)
            score += Math.Min(activity.DataVolume / (100 * 1024 * 1024.0), 3.0);

            // 시간 패턴 기반 점수 (최대 3점)
            var timeSpan = DateTime.Now - activity.Timestamp;
            if (timeSpan.TotalMinutes < 1) score += 3.0;
            else if (timeSpan.TotalMinutes < 5) score += 2.0;
            else if (timeSpan.TotalMinutes < 10) score += 1.0;

            return Math.Min(score, 10.0);
        }

        // 포트 설명 반환
        private string GetPortDescription(int port)
        {
            return port switch
            {
                1433 => "SQL Server 데이터베이스 포트",
                1521 => "Oracle 데이터베이스 포트",
                3306 => "MySQL 데이터베이스 포트",
                5432 => "PostgreSQL 데이터베이스 포트",
                23 => "Telnet (암호화되지 않은 원격 접속)",
                135 => "RPC Endpoint Mapper",
                139 => "NetBIOS Session Service",
                445 => "SMB over IP",
                4444 => "일반적인 백도어 포트",
                31337 => "Back Orifice 트로이목마",
                _ => "알려진 의심스러운 포트"
            };
        }

        // 바이트 포맷팅
        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        // 보안 경고 목록 반환
        public List<LogCheck.Models.SecurityAlert> GetSecurityAlerts(DateTime? startDate = null, DateTime? endDate = null)
        {
            var alerts = _securityAlerts.AsQueryable();

            if (startDate.HasValue)
                alerts = alerts.Where(a => a.Timestamp >= startDate.Value);

            if (endDate.HasValue)
                alerts = alerts.Where(a => a.Timestamp <= endDate.Value);

            return alerts.OrderByDescending(a => a.Timestamp).ToList();
        }

        // 보안 통계 생성
        public SecurityStatistics GetSecurityStatistics()
        {
            var now = DateTime.Now;
            var last24Hours = now.AddHours(-24);
            var last7Days = now.AddDays(-7);

            var recentAlerts = _securityAlerts.Where(a => a.Timestamp >= last24Hours).ToList();
            var weeklyAlerts = _securityAlerts.Where(a => a.Timestamp >= last7Days).ToList();

            return new SecurityStatistics
            {
                TotalAlerts = _securityAlerts.Count,
                AlertsLast24Hours = recentAlerts.Count,
                AlertsLast7Days = weeklyAlerts.Count,
                HighSeverityAlerts = _securityAlerts.Count(a => a.Severity == "High"),
                UnresolvedAlerts = _securityAlerts.Count(a => !a.IsResolved),
                MostCommonAlertType = _securityAlerts.GroupBy(a => a.AlertType)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault()?.Key ?? "없음",
                AverageAlertsPerDay = weeklyAlerts.Count / 7.0
            };
        }

        /// <summary>
        /// ProcessNetworkInfo 리스트를 분석하여 화이트리스트 처리 및 위험도 평가
        /// </summary>
        public async Task<List<ProcessNetworkInfo>> AnalyzeProcessNetworkInfoAsync(List<ProcessNetworkInfo> processInfoList)
        {
            var analyzedList = new List<ProcessNetworkInfo>();

            foreach (var processInfo in processInfoList)
            {
                // System Idle Process 화이트리스트 처리
                if (IsSystemIdleProcess(processInfo))
                {
                    processInfo.IsWhitelisted = true;
                    processInfo.RiskLevel = SecurityRiskLevel.System;
                    processInfo.RiskDescription = "System Idle Process - 시스템 유휴 프로세스";
                    processInfo.IsSystemProcess = true;
                }
                // 기타 시스템 프로세스 확인
                else if (IsKnownSystemProcess(processInfo))
                {
                    processInfo.IsWhitelisted = true;
                    processInfo.RiskLevel = SecurityRiskLevel.System;
                    processInfo.RiskDescription = "알려진 시스템 프로세스";
                    processInfo.IsSystemProcess = true;
                }
                // System Idle Process 위장 공격 탐지
                else if (IsSuspiciousSystemIdleProcessImpersonation(processInfo))
                {
                    processInfo.RiskLevel = SecurityRiskLevel.Critical;
                    processInfo.RiskDescription = "System Idle Process 위장 의심 - 악성코드 가능성 높음";

                    // 경고 생성
                    var alert = new LogCheck.Models.SecurityAlert
                    {
                        Timestamp = DateTime.Now,
                        Severity = "Critical",
                        AlertType = "Process Impersonation",
                        Description = $"PID {processInfo.ProcessId}의 '{processInfo.ProcessName}' 프로세스가 System Idle Process를 위장하고 있을 가능성이 있습니다.",
                        Details = $"프로세스 경로: {processInfo.ProcessPath}, 원격 주소: {processInfo.RemoteAddress}:{processInfo.RemotePort}",
                        SourceIP = processInfo.RemoteAddress,
                        DestinationIP = processInfo.LocalAddress,
                        SourcePort = processInfo.RemotePort,
                        DestinationPort = processInfo.LocalPort,
                        Protocol = processInfo.Protocol,
                        IsResolved = false,
                        Action = "Monitored"
                    };
                    _securityAlerts.Add(alert);
                }
                else
                {
                    // 일반 프로세스 위험도 계산
                    processInfo.CalculateRiskLevel();
                }

                analyzedList.Add(processInfo);
            }

            return await Task.FromResult(analyzedList);
        }

        /// <summary>
        /// System Idle Process 여부 확인
        /// </summary>
        private bool IsSystemIdleProcess(ProcessNetworkInfo processInfo)
        {
            // PID 0은 System Idle Process
            if (processInfo.ProcessId == 0)
                return true;

            // 프로세스 이름이 "System Idle Process"인 경우
            if (string.Equals(processInfo.ProcessName, "System Idle Process", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        /// <summary>
        /// 알려진 시스템 프로세스 여부 확인
        /// </summary>
        private bool IsKnownSystemProcess(ProcessNetworkInfo processInfo)
        {
            var systemProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "System", "csrss.exe", "winlogon.exe", "services.exe", "lsass.exe",
                "svchost.exe", "explorer.exe", "dwm.exe", "winlogon.exe"
            };

            return systemProcessNames.Contains(processInfo.ProcessName);
        }

        /// <summary>
        /// System Idle Process 위장 공격 탐지
        /// </summary>
        private bool IsSuspiciousSystemIdleProcessImpersonation(ProcessNetworkInfo processInfo)
        {
            // System Idle Process와 유사한 이름이지만 PID가 0이 아닌 경우
            var suspiciousNames = new[]
            {
                "system idle process",
                "systemidle",
                "system_idle",
                "idle_process",
                "systemidleprocess"
            };

            var processNameLower = processInfo.ProcessName?.ToLowerInvariant() ?? "";

            // 유사한 이름을 가졌지만 PID가 0이 아닌 경우 의심
            foreach (var suspiciousName in suspiciousNames)
            {
                if (processNameLower.Contains(suspiciousName) && processInfo.ProcessId != 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public class SecurityStatistics
    {
        public int TotalAlerts { get; set; }
        public int AlertsLast24Hours { get; set; }
        public int AlertsLast7Days { get; set; }
        public int HighSeverityAlerts { get; set; }
        public int UnresolvedAlerts { get; set; }
        public string MostCommonAlertType { get; set; } = string.Empty;
        public double AverageAlertsPerDay { get; set; }
    }
}