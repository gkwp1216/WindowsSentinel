using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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