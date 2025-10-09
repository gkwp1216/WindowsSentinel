using System.Collections.Concurrent;
using System.Net;
using LogCheck.Models;

namespace LogCheck.Services
{
    /// <summary>
    /// 실시간 네트워크 보안 분석 서비스
    /// </summary>
    public class RealTimeSecurityAnalyzer
    {
        private readonly ConcurrentDictionary<string, SecurityAlert> _securityAlerts;
        private readonly ConcurrentDictionary<string, ThreatPattern> _threatPatterns;
        private readonly object _lockObject = new object();

        // 알려진 악성 IP 데이터베이스 (실제로는 외부 API나 DB에서 가져와야 함)
        private HashSet<string> _knownMaliciousIPs = new();

        // 의심스러운 포트 목록
        private HashSet<int> _suspiciousPorts = new();

        // 정상적인 포트 목록
        private HashSet<int> _legitimatePorts = new();

        public event EventHandler<SecurityAlert>? SecurityAlertGenerated;
        public event EventHandler<string>? ErrorOccurred;

        public RealTimeSecurityAnalyzer()
        {
            _securityAlerts = new ConcurrentDictionary<string, SecurityAlert>();
            _threatPatterns = new ConcurrentDictionary<string, ThreatPattern>();

            // 초기 데이터 초기화
            InitializeThreatDatabase();
        }

        /// <summary>
        /// 위협 데이터베이스 초기화
        /// </summary>
        private void InitializeThreatDatabase()
        {
            // 알려진 악성 IP (샘플 데이터)
            _knownMaliciousIPs = new HashSet<string>
            {
                "192.168.1.100", // 내부 네트워크 악성 IP
                "10.0.0.50",     // 테스트용 악성 IP
                "172.16.0.25"    // 테스트용 악성 IP
            };

            // 의심스러운 포트
            _suspiciousPorts = new HashSet<int>
            {
                22,     // SSH (자주 공격 대상)
                23,     // Telnet (암호화되지 않은 원격 접속)
                3389,   // RDP (원격 데스크톱)
                5900,   // VNC (가상 네트워크 컴퓨팅)
                1433,   // SQL Server
                1521,   // Oracle Database
                3306,   // MySQL
                5432,   // PostgreSQL
                27017,  // MongoDB
                6379,   // Redis
                11211,  // Memcached
                8080,   // HTTP 대체 포트
                8443,   // HTTPS 대체 포트
                4444,   // Metasploit 기본 포트
                5555,   // Android ADB
                6666,   // IRC
                7777,   // 게임 서버
                8888,   // HTTP 대체 포트
                9999    // HTTP 대체 포트
            };

            // 정상적인 포트
            _legitimatePorts = new HashSet<int>
            {
                80,     // HTTP
                443,    // HTTPS
                53,     // DNS
                21,     // FTP
                22,     // SSH (정상적인 경우)
                25,     // SMTP
                110,    // POP3
                143,    // IMAP
                993,    // IMAPS
                995,    // POP3S
                587,    // SMTP (Submission)
                465,    // SMTPS
                123,    // NTP
                161,    // SNMP
                162,    // SNMP Trap
                389,    // LDAP
                636,    // LDAPS
                1433,   // SQL Server (정상적인 경우)
                1521,   // Oracle (정상적인 경우)
                3306,   // MySQL (정상적인 경우)
                5432    // PostgreSQL (정상적인 경우)
            };
        }

        /// <summary>
        /// 프로세스-네트워크 연결 분석 및 위험도 평가
        /// </summary>
        public async Task<List<SecurityAlert>> AnalyzeConnectionsAsync(List<ProcessNetworkInfo> connections)
        {
            try
            {
                var alerts = new List<SecurityAlert>();

                foreach (var connection in connections)
                {
                    var alert = await AnalyzeSingleConnectionAsync(connection);
                    if (alert != null)
                    {
                        alerts.Add(alert);

                        // 보안 경고 이벤트 발생
                        SecurityAlertGenerated?.Invoke(this, alert);

                        // 보안 경고 저장
                        var alertKey = $"{connection.ProcessId}_{connection.RemoteAddress}_{connection.RemotePort}";
                        _securityAlerts.TryAdd(alertKey, alert);
                    }
                }

                // 위협 패턴 분석
                var patternAlerts = await AnalyzeThreatPatternsAsync(connections);
                alerts.AddRange(patternAlerts);

                return alerts;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"연결 분석 중 오류: {ex.Message}");
                return new List<SecurityAlert>();
            }
        }

        /// <summary>
        /// 단일 연결 분석
        /// </summary>
        private async Task<SecurityAlert?> AnalyzeSingleConnectionAsync(ProcessNetworkInfo connection)
        {
            try
            {
                var riskFactors = new List<string>();
                var riskScore = 0;

                // 1. 프로세스 위험도 분석
                var processRisk = AnalyzeProcessRisk(connection);
                riskScore += processRisk.score;
                riskFactors.AddRange(processRisk.factors);

                // 2. 네트워크 연결 위험도 분석
                var networkRisk = AnalyzeNetworkRisk(connection);
                riskScore += networkRisk.score;
                riskFactors.AddRange(networkRisk.factors);

                // 3. 데이터 전송 위험도 분석
                var dataRisk = AnalyzeDataTransferRisk(connection);
                riskScore += dataRisk.score;
                riskFactors.AddRange(dataRisk.factors);

                // 4. 연결 시간 위험도 분석
                var timeRisk = AnalyzeConnectionTimeRisk(connection);
                riskScore += timeRisk.score;
                riskFactors.AddRange(timeRisk.factors);

                // 위험도가 임계값을 넘는 경우에만 경고 생성
                if (riskScore >= 30)
                {
                    var alertLevel = DetermineAlertLevel(riskScore);
                    var recommendedAction = GenerateRecommendedAction(connection, riskFactors);

                    var alert = new SecurityAlert
                    {
                        Title = GenerateAlertTitle(connection, alertLevel),
                        Description = GenerateAlertDescription(connection, riskFactors),
                        AlertLevel = alertLevel,
                        ProcessId = connection.ProcessId,
                        ProcessName = connection.ProcessName,
                        RemoteAddress = connection.RemoteAddress,
                        RemotePort = connection.RemotePort,
                        Protocol = connection.Protocol,
                        RiskScore = riskScore,
                        RiskFactors = riskFactors,
                        RecommendedAction = recommendedAction,
                        Timestamp = DateTime.Now,
                        IsResolved = false
                    };

                    return alert;
                }

                return null;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"단일 연결 분석 중 오류: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 프로세스 위험도 분석
        /// </summary>
        private (int score, List<string> factors) AnalyzeProcessRisk(ProcessNetworkInfo connection)
        {
            var score = 0;
            var factors = new List<string>();

            try
            {
                // 서명되지 않은 프로세스
                if (!connection.IsSigned)
                {
                    score += 25;
                    factors.Add("서명되지 않은 프로세스");
                }

                // 회사명이 없는 프로세스
                if (string.IsNullOrEmpty(connection.CompanyName))
                {
                    score += 15;
                    factors.Add("제작사 정보 없음");
                }

                // 시스템 프로세스
                if (connection.IsSystemProcess)
                {
                    score += 10;
                    factors.Add("시스템 프로세스");
                }

                // 프로세스 경로가 의심스러운 경우
                if (IsSuspiciousProcessPath(connection.ProcessPath))
                {
                    score += 20;
                    factors.Add("의심스러운 프로세스 경로");
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"프로세스 위험도 분석 중 오류: {ex.Message}");
            }

            return (score, factors);
        }

        /// <summary>
        /// 네트워크 연결 위험도 분석
        /// </summary>
        private (int score, List<string> factors) AnalyzeNetworkRisk(ProcessNetworkInfo connection)
        {
            var score = 0;
            var factors = new List<string>();

            try
            {
                // 알려진 악성 IP
                if (_knownMaliciousIPs.Contains(connection.RemoteAddress))
                {
                    score += 50;
                    factors.Add("알려진 악성 IP");
                }

                // 의심스러운 포트
                if (_suspiciousPorts.Contains(connection.RemotePort))
                {
                    score += 30;
                    factors.Add($"의심스러운 포트 ({connection.RemotePort})");
                }

                // 비표준 포트 (1024-65535 범위에서 잘 사용되지 않는 포트)
                if (connection.RemotePort > 1024 && !_legitimatePorts.Contains(connection.RemotePort))
                {
                    score += 15;
                    factors.Add($"비표준 포트 ({connection.RemotePort})");
                }

                // 외부 IP로의 연결 (공용 IP)
                if (IsPublicIP(connection.RemoteAddress))
                {
                    score += 10;
                    factors.Add("외부 공용 IP로의 연결");
                }

                // 연결 상태가 의심스러운 경우
                if (IsSuspiciousConnectionState(connection.ConnectionState))
                {
                    score += 20;
                    factors.Add($"의심스러운 연결 상태 ({connection.ConnectionState})");
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"네트워크 위험도 분석 중 오류: {ex.Message}");
            }

            return (score, factors);
        }

        /// <summary>
        /// 데이터 전송 위험도 분석
        /// </summary>
        private (int score, List<string> factors) AnalyzeDataTransferRisk(ProcessNetworkInfo connection)
        {
            var score = 0;
            var factors = new List<string>();

            try
            {
                // 높은 데이터 전송률 (1MB/s 이상)
                if (connection.DataRate > 1000)
                {
                    score += 25;
                    factors.Add($"높은 데이터 전송률 ({connection.DataRate:F1} KB/s)");
                }

                // 대용량 데이터 전송 (100MB 이상)
                if (connection.DataTransferred > 100 * 1024 * 1024)
                {
                    score += 20;
                    factors.Add($"대용량 데이터 전송 ({connection.DataTransferred / (1024 * 1024):F1} MB)");
                }

                // 비정상적인 패킷 비율
                if (connection.PacketsSent > 0 && connection.PacketsReceived > 0)
                {
                    var packetRatio = (double)connection.PacketsSent / connection.PacketsReceived;
                    if (packetRatio > 10 || packetRatio < 0.1)
                    {
                        score += 15;
                        factors.Add($"비정상적인 패킷 비율 (송신:수신 = {packetRatio:F2})");
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"데이터 전송 위험도 분석 중 오류: {ex.Message}");
            }

            return (score, factors);
        }

        /// <summary>
        /// 연결 시간 위험도 분석
        /// </summary>
        private (int score, List<string> factors) AnalyzeConnectionTimeRisk(ProcessNetworkInfo connection)
        {
            var score = 0;
            var factors = new List<string>();

            try
            {
                var connectionDuration = connection.ConnectionDuration;

                // 장시간 연결 (24시간 이상)
                if (connectionDuration.TotalHours > 24)
                {
                    score += 15;
                    factors.Add($"장시간 연결 ({connectionDuration.TotalHours:F1}시간)");
                }

                // 비정상적인 시간대 연결 (새벽 2-6시)
                var currentHour = DateTime.Now.Hour;
                if (currentHour >= 2 && currentHour <= 6)
                {
                    score += 10;
                    factors.Add("비정상적인 시간대 연결 (새벽)");
                }

                // 연결 시간이 프로세스 시작 시간보다 오래된 경우
                if (connection.ConnectionStartTime < connection.ProcessStartTime)
                {
                    score += 20;
                    factors.Add("프로세스 시작 전부터 연결됨");
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"연결 시간 위험도 분석 중 오류: {ex.Message}");
            }

            return (score, factors);
        }

        /// <summary>
        /// 위협 패턴 분석
        /// </summary>
        private async Task<List<SecurityAlert>> AnalyzeThreatPatternsAsync(List<ProcessNetworkInfo> connections)
        {
            var alerts = new List<SecurityAlert>();

            try
            {
                // 1. 포트 스캔 패턴 탐지
                var portScanAlerts = DetectPortScanPattern(connections);
                alerts.AddRange(portScanAlerts);

                // 2. 데이터 유출 패턴 탐지
                var dataExfiltrationAlerts = DetectDataExfiltrationPattern(connections);
                alerts.AddRange(dataExfiltrationAlerts);

                // 3. 비정상적인 연결 빈도 탐지
                var frequencyAlerts = DetectAbnormalFrequencyPattern(connections);
                alerts.AddRange(frequencyAlerts);

                // 4. 의심스러운 프로세스 그룹 탐지
                var processGroupAlerts = DetectSuspiciousProcessGroup(connections);
                alerts.AddRange(processGroupAlerts);
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"위협 패턴 분석 중 오류: {ex.Message}");
            }

            return alerts;
        }

        /// <summary>
        /// 포트 스캔 패턴 탐지
        /// </summary>
        private List<SecurityAlert> DetectPortScanPattern(List<ProcessNetworkInfo> connections)
        {
            var alerts = new List<SecurityAlert>();

            try
            {
                // 같은 IP에서 여러 포트로 연결 시도하는 패턴 탐지
                var portScanGroups = connections
                    .GroupBy(c => c.RemoteAddress)
                    .Where(g => g.Count() > 5) // 5개 이상의 포트로 연결
                    .Select(g => new { IP = g.Key, Ports = g.Select(c => c.RemotePort).ToList() });

                foreach (var group in portScanGroups)
                {
                    var alert = new SecurityAlert
                    {
                        Title = "포트 스캔 탐지",
                        Description = $"IP 주소 {group.IP}에서 {group.Ports.Count}개의 포트로 연결 시도",
                        AlertLevel = SecurityAlertLevel.High,
                        RemoteAddress = group.IP,
                        RiskScore = 80,
                        RiskFactors = new List<string> { "포트 스캔 패턴", "다중 포트 연결 시도" },
                        RecommendedAction = "해당 IP 주소 차단 및 연결 모니터링 강화",
                        Timestamp = DateTime.Now,
                        IsResolved = false
                    };

                    alerts.Add(alert);
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"포트 스캔 패턴 탐지 중 오류: {ex.Message}");
            }

            return alerts;
        }

        /// <summary>
        /// 데이터 유출 패턴 탐지
        /// </summary>
        private List<SecurityAlert> DetectDataExfiltrationPattern(List<ProcessNetworkInfo> connections)
        {
            var alerts = new List<SecurityAlert>();

            try
            {
                // 대용량 데이터를 외부로 전송하는 패턴 탐지
                var dataExfiltration = connections
                    .Where(c => c.DataTransferred > 50 * 1024 * 1024) // 50MB 이상
                    .Where(c => IsPublicIP(c.RemoteAddress));

                foreach (var connection in dataExfiltration)
                {
                    var alert = new SecurityAlert
                    {
                        Title = "데이터 유출 의심",
                        Description = $"프로세스 {connection.ProcessName}이(가) 외부 IP {connection.RemoteAddress}로 {connection.DataTransferred / (1024 * 1024):F1}MB 데이터 전송",
                        AlertLevel = SecurityAlertLevel.High,
                        ProcessId = connection.ProcessId,
                        ProcessName = connection.ProcessName,
                        RemoteAddress = connection.RemoteAddress,
                        RiskScore = 75,
                        RiskFactors = new List<string> { "대용량 데이터 전송", "외부 IP로 전송", "데이터 유출 의심" },
                        RecommendedAction = "프로세스 활동 모니터링 및 필요시 연결 차단",
                        Timestamp = DateTime.Now,
                        IsResolved = false
                    };

                    alerts.Add(alert);
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"데이터 유출 패턴 탐지 중 오류: {ex.Message}");
            }

            return alerts;
        }

        /// <summary>
        /// 비정상적인 연결 빈도 탐지
        /// </summary>
        private List<SecurityAlert> DetectAbnormalFrequencyPattern(List<ProcessNetworkInfo> connections)
        {
            var alerts = new List<SecurityAlert>();

            try
            {
                // 같은 프로세스에서 비정상적으로 많은 연결을 생성하는 패턴 탐지
                var frequencyGroups = connections
                    .GroupBy(c => c.ProcessId)
                    .Where(g => g.Count() > 20) // 20개 이상의 연결
                    .Select(g => new { ProcessId = g.Key, ProcessName = g.First().ProcessName, Count = g.Count() });

                foreach (var group in frequencyGroups)
                {
                    var alert = new SecurityAlert
                    {
                        Title = "비정상적인 연결 빈도",
                        Description = $"프로세스 {group.ProcessName}이(가) {group.Count}개의 네트워크 연결 생성",
                        AlertLevel = SecurityAlertLevel.Medium,
                        ProcessId = group.ProcessId,
                        ProcessName = group.ProcessName,
                        RiskScore = 60,
                        RiskFactors = new List<string> { "높은 연결 빈도", "비정상적인 네트워크 활동" },
                        RecommendedAction = "프로세스 활동 모니터링 및 연결 패턴 분석",
                        Timestamp = DateTime.Now,
                        IsResolved = false
                    };

                    alerts.Add(alert);
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"연결 빈도 패턴 탐지 중 오류: {ex.Message}");
            }

            return alerts;
        }

        /// <summary>
        /// 의심스러운 프로세스 그룹 탐지
        /// </summary>
        private List<SecurityAlert> DetectSuspiciousProcessGroup(List<ProcessNetworkInfo> connections)
        {
            var alerts = new List<SecurityAlert>();

            try
            {
                // 서명되지 않은 프로세스들이 동시에 네트워크 활동을 하는 패턴 탐지
                var suspiciousProcesses = connections
                    .Where(c => !c.IsSigned)
                    .GroupBy(c => c.ProcessName)
                    .Where(g => g.Count() > 3); // 3개 이상의 연결

                foreach (var group in suspiciousProcesses)
                {
                    var alert = new SecurityAlert
                    {
                        Title = "의심스러운 프로세스 그룹",
                        Description = $"서명되지 않은 프로세스 {group.Key}이(가) {group.Count()}개의 네트워크 연결 생성",
                        AlertLevel = SecurityAlertLevel.Medium,
                        ProcessName = group.Key,
                        RiskScore = 65,
                        RiskFactors = new List<string> { "서명되지 않은 프로세스", "다중 네트워크 연결", "의심스러운 활동" },
                        RecommendedAction = "프로세스 신뢰성 검증 및 필요시 실행 차단",
                        Timestamp = DateTime.Now,
                        IsResolved = false
                    };

                    alerts.Add(alert);
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"의심스러운 프로세스 그룹 탐지 중 오류: {ex.Message}");
            }

            return alerts;
        }

        /// <summary>
        /// 보안 경고 수준 결정
        /// </summary>
        private SecurityAlertLevel DetermineAlertLevel(int riskScore)
        {
            return riskScore switch
            {
                < 40 => SecurityAlertLevel.Low,
                < 60 => SecurityAlertLevel.Medium,
                < 80 => SecurityAlertLevel.High,
                _ => SecurityAlertLevel.Critical
            };
        }

        /// <summary>
        /// 권장 조치 생성
        /// </summary>
        private string GenerateRecommendedAction(ProcessNetworkInfo connection, List<string> riskFactors)
        {
            var actions = new List<string>();

            if (riskFactors.Contains("알려진 악성 IP"))
            {
                actions.Add("즉시 해당 IP 주소 차단");
            }

            if (riskFactors.Contains("서명되지 않은 프로세스"))
            {
                actions.Add("프로세스 신뢰성 검증");
            }

            if (riskFactors.Contains("의심스러운 포트"))
            {
                actions.Add("포트 사용 목적 확인");
            }

            if (riskFactors.Contains("대용량 데이터 전송"))
            {
                actions.Add("데이터 전송 내용 검토");
            }

            if (riskFactors.Contains("장시간 연결"))
            {
                actions.Add("연결 필요성 검토");
            }

            if (actions.Count == 0)
            {
                actions.Add("연결 모니터링 지속");
            }

            return string.Join(", ", actions);
        }

        /// <summary>
        /// 경고 제목 생성
        /// </summary>
        private string GenerateAlertTitle(ProcessNetworkInfo connection, SecurityAlertLevel level)
        {
            var levelText = level switch
            {
                SecurityAlertLevel.Low => "낮음",
                SecurityAlertLevel.Medium => "보통",
                SecurityAlertLevel.High => "높음",
                SecurityAlertLevel.Critical => "심각",
                _ => "알 수 없음"
            };

            return $"[{levelText}] {connection.ProcessName} 네트워크 위협 탐지";
        }

        /// <summary>
        /// 경고 설명 생성
        /// </summary>
        private string GenerateAlertDescription(ProcessNetworkInfo connection, List<string> riskFactors)
        {
            return $"프로세스 {connection.ProcessName} (PID: {connection.ProcessId})이(가) {connection.RemoteAddress}:{connection.RemotePort}로 연결 중 위험 요소 발견: {string.Join(", ", riskFactors)}";
        }

        /// <summary>
        /// 유틸리티 메서드들
        /// </summary>
        private bool IsSuspiciousProcessPath(string processPath)
        {
            if (string.IsNullOrEmpty(processPath)) return false;

            var suspiciousPaths = new[] { "temp", "downloads", "desktop", "appdata" };
            return suspiciousPaths.Any(path => processPath.ToLower().Contains(path));
        }

        private bool IsPublicIP(string ipAddress)
        {
            if (IPAddress.TryParse(ipAddress, out var ip))
            {
                var bytes = ip.GetAddressBytes();
                return !((bytes[0] == 10) ||
                        (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                        (bytes[0] == 192 && bytes[1] == 168) ||
                        (bytes[0] == 127));
            }
            return false;
        }

        private bool IsSuspiciousConnectionState(string state)
        {
            var suspiciousStates = new[] { "TIME_WAIT", "CLOSE_WAIT", "FIN_WAIT" };
            return suspiciousStates.Contains(state.ToUpper());
        }

        /// <summary>
        /// 보안 경고 해결 표시
        /// </summary>
        public void ResolveAlert(string alertKey)
        {
            if (_securityAlerts.TryGetValue(alertKey, out var alert))
            {
                alert.IsResolved = true;
                alert.ResolvedTime = DateTime.Now;
            }
        }

        /// <summary>
        /// 활성 보안 경고 조회
        /// </summary>
        public List<SecurityAlert> GetActiveAlerts()
        {
            return _securityAlerts.Values.Where(a => !a.IsResolved).ToList();
        }

        /// <summary>
        /// 모든 보안 경고 조회
        /// </summary>
        public List<SecurityAlert> GetAllAlerts()
        {
            return _securityAlerts.Values.ToList();
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
    /// 보안 경고 수준
    /// </summary>
    public enum SecurityAlertLevel
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3
    }

    /// <summary>
    /// 보안 경고 정보
    /// </summary>
    public class SecurityAlert
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public SecurityAlertLevel AlertLevel { get; set; }
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string RemoteAddress { get; set; } = string.Empty;
        public int RemotePort { get; set; }
        public string Protocol { get; set; } = string.Empty;
        public int RiskScore { get; set; }
        public List<string> RiskFactors { get; set; } = new List<string>();
        public string RecommendedAction { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool IsResolved { get; set; }
        public DateTime? ResolvedTime { get; set; }
    }

    /// <summary>
    /// 위협 패턴 정보
    /// </summary>
    public class ThreatPattern
    {
        public string PatternType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Severity { get; set; }
        public DateTime FirstDetected { get; set; }
        public DateTime LastDetected { get; set; }
        public int DetectionCount { get; set; }
    }
}
