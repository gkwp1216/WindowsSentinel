using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LogCheck.ViewModels;

namespace LogCheck.Services
{
    public class SecurityEventLogger
    {
        private static readonly Lazy<SecurityEventLogger> _instance = new(() => new SecurityEventLogger());
        public static SecurityEventLogger Instance => _instance.Value;

        private readonly ConcurrentQueue<SecurityEventInfo> _eventQueue = new();
        private readonly object _lock = new();
        private const int MaxEvents = 50; // 최대 보관할 이벤트 수

        public event EventHandler<SecurityEventInfo>? NewEventLogged;

        private SecurityEventLogger() { }

        /// <summary>
        /// 새로운 보안 이벤트를 로깅합니다.
        /// </summary>
        public void LogEvent(string eventType, string description, SecurityEventRiskLevel riskLevel, string source = "시스템")
        {
            // 🔥 NEW: 최종 방어선 - 로깅 시점에서 필터링
            if (ShouldFilterEvent(source, description, eventType))
            {
                System.Diagnostics.Debug.WriteLine($"[SecurityEventLogger] Filtered: {eventType} - {source} - {description}");
                return; // 로깅하지 않음
            }

            var eventInfo = new SecurityEventInfo
            {
                Timestamp = DateTime.Now,
                EventType = eventType,
                Description = description,
                RiskLevel = GetRiskLevelText(riskLevel),
                Source = source,
                TypeColor = GetEventTypeColor(eventType),
                RiskColor = GetRiskLevelColor(riskLevel)
            };

            _eventQueue.Enqueue(eventInfo);

            // 최대 이벤트 수 유지
            while (_eventQueue.Count > MaxEvents)
            {
                _eventQueue.TryDequeue(out _);
            }

            // 새 이벤트 알림
            NewEventLogged?.Invoke(this, eventInfo);
        }

        /// <summary>
        /// DDoS 공격 감지 이벤트 로깅
        /// </summary>
        public void LogDDoSEvent(string attackType, string sourceIP, int attackIntensity)
        {
            // 🔥 NEW: DDoS 이벤트도 사설 IP 필터링 적용
            if (IsValidIPAddress(sourceIP) && IsPrivateIP(sourceIP))
            {
                System.Diagnostics.Debug.WriteLine($"[DDoSEventFilter] Private IP DDoS event filtered: {sourceIP}");
                return;
            }

            var riskLevel = attackIntensity switch
            {
                >= 8 => SecurityEventRiskLevel.Critical,
                >= 5 => SecurityEventRiskLevel.High,
                >= 3 => SecurityEventRiskLevel.Medium,
                _ => SecurityEventRiskLevel.Low
            };

            LogEvent("DDoS", $"{attackType} 공격 탐지 및 차단 (강도: {attackIntensity})", riskLevel, sourceIP);
        }

        /// <summary>
        /// 네트워크 연결 차단 이벤트 로깅
        /// </summary>
        public void LogBlockEvent(string processName, string remoteIP, string reason)
        {
            // 🔥 NEW: 차단 이벤트도 사설 IP와 시스템 프로세스 필터링 적용
            if (IsValidIPAddress(remoteIP) && IsPrivateIP(remoteIP))
            {
                System.Diagnostics.Debug.WriteLine($"[BlockEventFilter] Private IP block event filtered: {remoteIP}");
                return;
            }

            if (IsSystemProcess(processName))
            {
                System.Diagnostics.Debug.WriteLine($"[BlockEventFilter] System process block event filtered: {processName}");
                return;
            }

            LogEvent("차단", $"{processName} → {remoteIP} 연결 차단: {reason}", SecurityEventRiskLevel.Medium, processName);
        }

        /// <summary>
        /// 의심스러운 활동 탐지 이벤트 로깅
        /// </summary>
        public void LogThreatDetection(string threatType, string details, SecurityEventRiskLevel riskLevel, string source)
        {
            LogEvent("탐지", $"{threatType} 감지: {details}", riskLevel, source);
        }

        /// <summary>
        /// 시스템 복구 이벤트 로깅
        /// </summary>
        public void LogRecoveryEvent(string recoveryType, string details)
        {
            LogEvent("복구", $"{recoveryType} 복구: {details}", SecurityEventRiskLevel.Info, "시스템");
        }

        /// <summary>
        /// 방화벽 규칙 관련 이벤트 로깅
        /// </summary>
        public void LogFirewallEvent(string action, string target, string ruleDescription)
        {
            // 🔥 NEW: 방화벽 이벤트도 사설 IP 필터링 적용
            if (IsValidIPAddress(target) && IsPrivateIP(target))
            {
                System.Diagnostics.Debug.WriteLine($"[FirewallEventFilter] Private IP firewall event filtered: {target}");
                return;
            }

            var riskLevel = action.Contains("차단") ? SecurityEventRiskLevel.Medium : SecurityEventRiskLevel.Info;
            LogEvent("방화벽", $"{action}: {target} - {ruleDescription}", riskLevel, "Windows Defender");
        }

        /// <summary>
        /// 최근 보안 이벤트 목록 조회
        /// </summary>
        public List<SecurityEventInfo> GetRecentEvents(int count = 20)
        {
            lock (_lock)
            {
                return _eventQueue.TakeLast(count).Reverse().ToList();
            }
        }

        /// <summary>
        /// 특정 위험도 이상의 이벤트만 조회
        /// </summary>
        public List<SecurityEventInfo> GetEventsByRiskLevel(SecurityEventRiskLevel minRiskLevel, int count = 20)
        {
            var events = GetRecentEvents(50); // 더 많은 이벤트에서 필터링
            return events.Where(e => GetRiskLevelFromText(e.RiskLevel) >= minRiskLevel)
                        .Take(count)
                        .ToList();
        }

        /// <summary>
        /// 이벤트 타입별 조회
        /// </summary>
        public List<SecurityEventInfo> GetEventsByType(string eventType, int count = 20)
        {
            var events = GetRecentEvents(50);
            return events.Where(e => e.EventType.Equals(eventType, StringComparison.OrdinalIgnoreCase))
                        .Take(count)
                        .ToList();
        }

        private static string GetRiskLevelText(SecurityEventRiskLevel level)
        {
            return level switch
            {
                SecurityEventRiskLevel.Critical => "위험",
                SecurityEventRiskLevel.High => "높음",
                SecurityEventRiskLevel.Medium => "보통",
                SecurityEventRiskLevel.Low => "낮음",
                SecurityEventRiskLevel.Info => "정보",
                _ => "알림"
            };
        }

        private static SecurityEventRiskLevel GetRiskLevelFromText(string riskText)
        {
            return riskText switch
            {
                "위험" => SecurityEventRiskLevel.Critical,
                "높음" => SecurityEventRiskLevel.High,
                "보통" => SecurityEventRiskLevel.Medium,
                "낮음" => SecurityEventRiskLevel.Low,
                "정보" => SecurityEventRiskLevel.Info,
                _ => SecurityEventRiskLevel.Info
            };
        }

        private static System.Windows.Media.Brush GetEventTypeColor(string eventType)
        {
            try
            {
                var app = System.Windows.Application.Current;
                if (app?.Resources == null) return System.Windows.Media.Brushes.Gray;

                return eventType.ToLower() switch
                {
                    "ddos" => (System.Windows.Media.Brush)(app.Resources["RiskHighColor"] ?? System.Windows.Media.Brushes.Red),
                    "차단" => (System.Windows.Media.Brush)(app.Resources["RiskMediumColor"] ?? System.Windows.Media.Brushes.Orange),
                    "탐지" => (System.Windows.Media.Brush)(app.Resources["AccentBrush"] ?? System.Windows.Media.Brushes.Blue),
                    "복구" => (System.Windows.Media.Brush)(app.Resources["RiskLowColor"] ?? System.Windows.Media.Brushes.Green),
                    "방화벽" => (System.Windows.Media.Brush)(app.Resources["AccentBrush"] ?? System.Windows.Media.Brushes.Purple),
                    _ => System.Windows.Media.Brushes.Gray
                };
            }
            catch
            {
                return System.Windows.Media.Brushes.Gray;
            }
        }

        private static System.Windows.Media.Brush GetRiskLevelColor(SecurityEventRiskLevel level)
        {
            try
            {
                var app = System.Windows.Application.Current;
                if (app?.Resources == null) return System.Windows.Media.Brushes.Gray;

                return level switch
                {
                    SecurityEventRiskLevel.Critical => (System.Windows.Media.Brush)(app.Resources["RiskCriticalColor"] ?? System.Windows.Media.Brushes.DarkRed),
                    SecurityEventRiskLevel.High => (System.Windows.Media.Brush)(app.Resources["RiskHighColor"] ?? System.Windows.Media.Brushes.Red),
                    SecurityEventRiskLevel.Medium => (System.Windows.Media.Brush)(app.Resources["RiskMediumColor"] ?? System.Windows.Media.Brushes.Orange),
                    SecurityEventRiskLevel.Low => (System.Windows.Media.Brush)(app.Resources["RiskLowColor"] ?? System.Windows.Media.Brushes.Green),
                    SecurityEventRiskLevel.Info => (System.Windows.Media.Brush)(app.Resources["AccentBrush"] ?? System.Windows.Media.Brushes.Blue),
                    _ => System.Windows.Media.Brushes.Gray
                };
            }
            catch
            {
                return System.Windows.Media.Brushes.Gray;
            }
        }

        /// <summary>
        /// 이벤트 필터링 여부 판단
        /// </summary>
        private bool ShouldFilterEvent(string source, string description, string eventType)
        {
            try
            {
                // 1. IP 주소 소스 필터링
                if (IsValidIPAddress(source) && IsPrivateIP(source))
                {
                    System.Diagnostics.Debug.WriteLine($"[EventFilter] Private IP source detected: {source}");
                    return true;
                }

                // 2. 프로세스 이름 소스 필터링  
                if (source.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && IsSystemProcess(source))
                {
                    System.Diagnostics.Debug.WriteLine($"[EventFilter] System process detected: {source}");
                    return true;
                }

                // 3. 설명 내용에서 사설 IP 탐지
                if (ContainsPrivateIP(description))
                {
                    System.Diagnostics.Debug.WriteLine($"[EventFilter] Private IP in description: {description}");
                    return true;
                }

                // 4. 설명 내용에서 시스템 프로세스 탐지
                if (ContainsSystemProcess(description))
                {
                    System.Diagnostics.Debug.WriteLine($"[EventFilter] System process in description: {description}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EventFilter] Error in filtering: {ex.Message}");
                return false; // 에러 시 로깅 허용 (안전한 기본값)
            }
        }

        /// <summary>
        /// 유효한 IP 주소인지 확인
        /// </summary>
        private bool IsValidIPAddress(string input)
        {
            return IPAddress.TryParse(input, out _);
        }

        /// <summary>
        /// 사설 IP 주소인지 확인 (RFC 1918)
        /// </summary>
        private bool IsPrivateIP(string ipAddress)
        {
            try
            {
                if (!IPAddress.TryParse(ipAddress, out IPAddress? ip))
                    return false;

                byte[] bytes = ip.GetAddressBytes();

                // IPv4 체크
                if (bytes.Length == 4)
                {
                    // 10.0.0.0/8 (10.0.0.0 ~ 10.255.255.255)
                    if (bytes[0] == 10)
                        return true;

                    // 172.16.0.0/12 (172.16.0.0 ~ 172.31.255.255)
                    if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                        return true;

                    // 192.168.0.0/16 (192.168.0.0 ~ 192.168.255.255)
                    if (bytes[0] == 192 && bytes[1] == 168)
                        return true;

                    // 127.0.0.0/8 (Loopback)
                    if (bytes[0] == 127)
                        return true;

                    // 169.254.0.0/16 (Link-local)
                    if (bytes[0] == 169 && bytes[1] == 254)
                        return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 시스템 프로세스인지 확인
        /// </summary>
        private bool IsSystemProcess(string processName)
        {
            try
            {
                var lowerName = processName.ToLowerInvariant();

                // Windows 시스템 디렉토리 체크
                if (lowerName.Contains(@"c:\windows\system32\") ||
                    lowerName.Contains(@"c:\windows\syswow64\") ||
                    lowerName.Contains(@"c:\program files\windows defender\") ||
                    lowerName.Contains(@"c:\windows\microsoft.net\"))
                {
                    return true;
                }

                // 일반적인 시스템/정상 프로세스들
                var systemProcesses = new[]
                {
                    "notepad.exe", "calc.exe", "mspaint.exe", "winword.exe", "excel.exe",
                    "chrome.exe", "firefox.exe", "msedge.exe", "explorer.exe",
                    "svchost.exe", "services.exe", "lsass.exe", "csrss.exe"
                };

                var processFileName = System.IO.Path.GetFileName(lowerName);
                return systemProcesses.Any(sysProc => processFileName.Contains(sysProc));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 설명 텍스트에 사설 IP가 포함되어 있는지 확인
        /// </summary>
        private bool ContainsPrivateIP(string description)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(description))
                    return false;

                // 정규식으로 설명 텍스트에서 IP 주소 추출
                var ipRegex = new Regex(@"\b(?:[0-9]{1,3}\.){3}[0-9]{1,3}\b");
                var matches = ipRegex.Matches(description);

                foreach (Match match in matches)
                {
                    if (IsPrivateIP(match.Value))
                        return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 설명 텍스트에 시스템 프로세스가 포함되어 있는지 확인
        /// </summary>
        private bool ContainsSystemProcess(string description)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(description))
                    return false;

                var lowerDescription = description.ToLowerInvariant();

                // 시스템 프로세스 이름들 체크
                var systemProcesses = new[]
                {
                    "notepad.exe", "calc.exe", "mspaint.exe", "winword.exe", "excel.exe",
                    "chrome.exe", "firefox.exe", "msedge.exe", "explorer.exe"
                };

                return systemProcesses.Any(proc => lowerDescription.Contains(proc));
            }
            catch
            {
                return false;
            }
        }
    }

    public enum SecurityEventRiskLevel
    {
        Info = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }
}