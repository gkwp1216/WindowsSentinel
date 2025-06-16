using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using LogCheck.Models;
using WindowsSentinel.Models;

namespace LogCheck.Services
{
    public class SecurityAnalyzer
    {
        private readonly List<MaliciousIP> _maliciousIPs;
        private readonly List<SecurityAlert> _securityAlerts;
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
            _securityAlerts = new List<SecurityAlert>();
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
        public async Task<List<SecurityAlert>> AnalyzePacketAsync(NetworkUsageRecord packet)
        {
            var alerts = new List<SecurityAlert>();

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
        private SecurityAlert? CheckMaliciousIP(NetworkUsageRecord packet)
        {
            var maliciousIP = _maliciousIPs.FirstOrDefault(ip => 
                ip.IPAddress == packet.SourceIP || ip.IPAddress == packet.DestinationIP);

            if (maliciousIP != null)
            {
                return new SecurityAlert
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
        private SecurityAlert? CheckSuspiciousPort(NetworkUsageRecord packet)
        {
            if (_suspiciousPorts.Contains(packet.DestinationPort) || _suspiciousPorts.Contains(packet.SourcePort))
            {
                var suspiciousPort = _suspiciousPorts.Contains(packet.DestinationPort) ? packet.DestinationPort : packet.SourcePort;
                
                return new SecurityAlert
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
        private async Task<SecurityAlert?> CheckTrafficPatternAsync(NetworkUsageRecord packet)
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
                    return new SecurityAlert
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
        private SecurityAlert? CheckDataVolume(NetworkUsageRecord packet)
        {
            // 단일 패킷이 10MB 이상인 경우
            if (packet.PacketSize > 10 * 1024 * 1024)
            {
                return new SecurityAlert
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
        public List<SecurityAlert> GetSecurityAlerts(DateTime? startDate = null, DateTime? endDate = null)
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