using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LogCheck.Models;

namespace LogCheck.Services
{
    /// <summary>
    /// 고도화된 DDoS 공격 탐지 엔진
    /// 다양한 DDoS 패턴을 실시간으로 분석하여 탐지
    /// </summary>
    public class DDoSDetectionEngine
    {
        #region Private Fields

        private readonly ConcurrentDictionary<string, ConnectionTracker> _connectionTrackers;
        private readonly ConcurrentDictionary<string, TrafficAnalyzer> _trafficAnalyzers;
        private readonly ConcurrentDictionary<string, SynFloodTracker> _synFloodTrackers;
        private readonly object _lockObject = new object();
        private readonly ToastNotificationService _toastService;

        // 임계값 설정
        private readonly DDoSThresholds _thresholds;

        #endregion

        #region Events

        public event EventHandler<DDoSAlert>? DDoSDetected;
        public event EventHandler<string>? ErrorOccurred;

        #endregion

        #region Constructor

        public DDoSDetectionEngine()
        {
            _connectionTrackers = new ConcurrentDictionary<string, ConnectionTracker>();
            _trafficAnalyzers = new ConcurrentDictionary<string, TrafficAnalyzer>();
            _synFloodTrackers = new ConcurrentDictionary<string, SynFloodTracker>();

            // Windows 플랫폼에서만 Toast 서비스 초기화
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                _toastService = ToastNotificationService.Instance;
            }
            else
            {
                _toastService = null!; // 다른 플랫폼에서는 null로 설정
            }

            // 기본 임계값 설정
            _thresholds = new DDoSThresholds
            {
                MaxConnectionsPerSecond = 50,
                MaxConnectionsPerMinute = 300,
                MaxConcurrentConnections = 1000,
                MaxBytesPerSecond = 10 * 1024 * 1024, // 10MB/s
                SynFloodThreshold = 100, // 1초 내 100개 SYN 패킷
                UdpFloodThreshold = 200, // 1초 내 200개 UDP 패킷
                SlowLorisTimeout = 10, // 10초 이상 미완성 연결
                HttpFloodThreshold = 100 // 1초 내 100개 HTTP 요청
            };
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 네트워크 연결 데이터 분석 (동기 래퍼)
        /// </summary>
        public List<DDoSAlert> AnalyzeConnections(List<ProcessNetworkInfo> connections)
        {
            return AnalyzeConnectionsAsync(connections).Result;
        }

        /// <summary>
        /// 네트워크 연결 데이터를 분석하여 DDoS 공격 탐지
        /// </summary>
        public async Task<List<DDoSAlert>> AnalyzeConnectionsAsync(List<ProcessNetworkInfo> connections)
        {
            var alerts = new List<DDoSAlert>();

            try
            {
                // 1. SYN Flood 탐지
                var synFloodAlerts = await DetectSynFloodAsync(connections);
                alerts.AddRange(synFloodAlerts);

                // 2. UDP Flood 탐지
                var udpFloodAlerts = await DetectUdpFloodAsync(connections);
                alerts.AddRange(udpFloodAlerts);

                // 3. 연결 기반 DDoS 탐지
                var connectionFloodAlerts = await DetectConnectionFloodAsync(connections);
                alerts.AddRange(connectionFloodAlerts);

                // 4. 슬로로리스 공격 탐지
                var slowLorisAlerts = await DetectSlowLorisAsync(connections);
                alerts.AddRange(slowLorisAlerts);

                // 5. 대역폭 기반 DDoS 탐지
                var bandwidthFloodAlerts = await DetectBandwidthFloodAsync(connections);
                alerts.AddRange(bandwidthFloodAlerts);

                // 6. HTTP Flood 탐지 (포트 80, 443 대상)
                var httpFloodAlerts = await DetectHttpFloodAsync(connections);
                alerts.AddRange(httpFloodAlerts);

                return alerts;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"DDoS 탐지 중 오류: {ex.Message}");
                return alerts;
            }
        }

        /// <summary>
        /// 패킷 레벨 분석 (SharpPcap 데이터용)
        /// </summary>
        public Task<List<DDoSAlert>> AnalyzePacketsAsync(List<PacketDto> packets)
        {
            var alerts = new List<DDoSAlert>();

            try
            {
                // TCP 플래그 기반 SYN Flood 탐지
                var synPackets = packets.Where(p => p.Protocol == ProtocolKind.TCP &&
                                                   (p.Flags & 0x02) != 0 && // SYN 플래그
                                                   (p.Flags & 0x10) == 0);  // ACK 플래그 없음

                var synFloodsByIP = synPackets
                    .GroupBy(p => p.SrcIp)
                    .Where(g => g.Count() > _thresholds.SynFloodThreshold)
                    .Select(g => new DDoSAlert
                    {
                        AttackType = DDoSAttackType.SynFlood,
                        SourceIP = g.Key,
                        Severity = DDoSSeverity.High,
                        Description = $"SYN Flood 공격 탐지: {g.Key}에서 {g.Count()}개의 SYN 패킷",
                        DetectedAt = DateTime.Now,
                        PacketCount = g.Count(),
                        RecommendedAction = "즉시 IP 차단 및 SYN 쿠키 활성화"
                    });

                alerts.AddRange(synFloodsByIP);

                // UDP Flood 탐지
                var udpPackets = packets.Where(p => p.Protocol == ProtocolKind.UDP);
                var udpFloodsByIP = udpPackets
                    .GroupBy(p => p.SrcIp)
                    .Where(g => g.Count() > _thresholds.UdpFloodThreshold)
                    .Select(g => new DDoSAlert
                    {
                        AttackType = DDoSAttackType.UdpFlood,
                        SourceIP = g.Key,
                        Severity = DDoSSeverity.High,
                        Description = $"UDP Flood 공격 탐지: {g.Key}에서 {g.Count()}개의 UDP 패킷",
                        DetectedAt = DateTime.Now,
                        PacketCount = g.Count(),
                        RecommendedAction = "UDP 트래픽 필터링 및 IP 차단"
                    });

                alerts.AddRange(udpFloodsByIP);

                return Task.FromResult(alerts);
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"패킷 레벨 DDoS 탐지 중 오류: {ex.Message}");
                return Task.FromResult(alerts);
            }
        }

        #endregion

        #region Private Detection Methods

        /// <summary>
        /// SYN Flood 공격 탐지
        /// </summary>
        private async Task<List<DDoSAlert>> DetectSynFloodAsync(List<ProcessNetworkInfo> connections)
        {
            var alerts = new List<DDoSAlert>();

            try
            {
                var now = DateTime.Now;

                // TCP 연결에서 연결 상태가 SYN_SENT 또는 SYN_RECEIVED인 것들 분석
                var suspiciousSynConnections = connections
                    .Where(c => c.Protocol.Equals("TCP", StringComparison.OrdinalIgnoreCase))
                    .Where(c => c.ConnectionState == "SYN_SENT" || c.ConnectionState == "SYN_RECEIVED")
                    .GroupBy(c => c.RemoteAddress);

                foreach (var group in suspiciousSynConnections)
                {
                    var ipAddress = group.Key;
                    var synCount = group.Count();

                    // 임계값 초과 시 SYN Flood로 판단
                    if (synCount > _thresholds.SynFloodThreshold)
                    {
                        var alert = new DDoSAlert
                        {
                            AttackType = DDoSAttackType.SynFlood,
                            SourceIP = ipAddress,
                            Severity = DetermineSeverity(synCount, _thresholds.SynFloodThreshold),
                            Description = $"SYN Flood 공격 탐지: {ipAddress}에서 {synCount}개의 미완성 TCP 연결",
                            DetectedAt = now,
                            ConnectionCount = synCount,
                            RecommendedAction = "즉시 IP 차단 및 SYN 쿠키 활성화"
                        };

                        alerts.Add(alert);

                        // Toast 알림 표시
                        await ShowDDoSAlertAsync(alert);

                        // 이벤트 발생
                        DDoSDetected?.Invoke(this, alert);
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"SYN Flood 탐지 중 오류: {ex.Message}");
            }

            return alerts;
        }

        /// <summary>
        /// UDP Flood 공격 탐지
        /// </summary>
        private async Task<List<DDoSAlert>> DetectUdpFloodAsync(List<ProcessNetworkInfo> connections)
        {
            var alerts = new List<DDoSAlert>();

            try
            {
                var now = DateTime.Now;

                // UDP 연결에서 단시간 내 대량 연결 분석
                var udpConnections = connections
                    .Where(c => c.Protocol.Equals("UDP", StringComparison.OrdinalIgnoreCase))
                    .Where(c => (now - c.ConnectionStartTime).TotalSeconds <= 1) // 1초 내
                    .GroupBy(c => c.RemoteAddress);

                foreach (var group in udpConnections)
                {
                    var ipAddress = group.Key;
                    var udpCount = group.Count();

                    if (udpCount > _thresholds.UdpFloodThreshold)
                    {
                        var alert = new DDoSAlert
                        {
                            AttackType = DDoSAttackType.UdpFlood,
                            SourceIP = ipAddress,
                            Severity = DetermineSeverity(udpCount, _thresholds.UdpFloodThreshold),
                            Description = $"UDP Flood 공격 탐지: {ipAddress}에서 {udpCount}개의 UDP 연결",
                            DetectedAt = now,
                            ConnectionCount = udpCount,
                            RecommendedAction = "UDP 트래픽 필터링 및 IP 차단"
                        };

                        alerts.Add(alert);

                        // Toast 알림 표시
                        await ShowDDoSAlertAsync(alert);

                        // 이벤트 발생
                        DDoSDetected?.Invoke(this, alert);
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"UDP Flood 탐지 중 오류: {ex.Message}");
            }

            return alerts;
        }

        /// <summary>
        /// 연결 기반 DDoS (Connection Flood) 탐지
        /// </summary>
        private async Task<List<DDoSAlert>> DetectConnectionFloodAsync(List<ProcessNetworkInfo> connections)
        {
            var alerts = new List<DDoSAlert>();

            try
            {
                var now = DateTime.Now;

                // IP별 연결 수 분석
                var connectionsByIP = connections
                    .Where(c => (now - c.ConnectionStartTime).TotalSeconds <= 1) // 1초 내
                    .GroupBy(c => c.RemoteAddress);

                foreach (var group in connectionsByIP)
                {
                    var ipAddress = group.Key;
                    var connectionCount = group.Count();

                    if (connectionCount > _thresholds.MaxConnectionsPerSecond)
                    {
                        var alert = new DDoSAlert
                        {
                            AttackType = DDoSAttackType.ConnectionFlood,
                            SourceIP = ipAddress,
                            Severity = DetermineSeverity(connectionCount, _thresholds.MaxConnectionsPerSecond),
                            Description = $"연결 기반 DDoS 공격 탐지: {ipAddress}에서 초당 {connectionCount}개 연결",
                            DetectedAt = now,
                            ConnectionCount = connectionCount,
                            RecommendedAction = "연결 수 제한 및 IP 차단"
                        };

                        alerts.Add(alert);

                        // Toast 알림 표시
                        await ShowDDoSAlertAsync(alert);

                        // 이벤트 발생
                        DDoSDetected?.Invoke(this, alert);
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"연결 기반 DDoS 탐지 중 오류: {ex.Message}");
            }

            return alerts;
        }

        /// <summary>
        /// 슬로로리스 (Slowloris) 공격 탐지
        /// </summary>
        private async Task<List<DDoSAlert>> DetectSlowLorisAsync(List<ProcessNetworkInfo> connections)
        {
            var alerts = new List<DDoSAlert>();

            try
            {
                var now = DateTime.Now;

                // 장시간 유지되는 미완성 연결들 분석
                var slowConnections = connections
                    .Where(c => c.Protocol.Equals("TCP", StringComparison.OrdinalIgnoreCase))
                    .Where(c => c.ConnectionState == "ESTABLISHED")
                    .Where(c => c.ConnectionDuration.TotalSeconds > _thresholds.SlowLorisTimeout)
                    .Where(c => c.DataTransferred < 1024) // 적은 데이터 전송
                    .GroupBy(c => c.RemoteAddress);

                foreach (var group in slowConnections)
                {
                    var ipAddress = group.Key;
                    var slowConnectionCount = group.Count();

                    // 동일 IP에서 다수의 슬로우 연결
                    if (slowConnectionCount > 10)
                    {
                        var alert = new DDoSAlert
                        {
                            AttackType = DDoSAttackType.SlowLoris,
                            SourceIP = ipAddress,
                            Severity = DDoSSeverity.Medium,
                            Description = $"슬로로리스 공격 탐지: {ipAddress}에서 {slowConnectionCount}개의 슬로우 연결",
                            DetectedAt = now,
                            ConnectionCount = slowConnectionCount,
                            RecommendedAction = "연결 타임아웃 단축 및 IP 모니터링"
                        };

                        alerts.Add(alert);

                        // Toast 알림 표시
                        await ShowDDoSAlertAsync(alert);

                        // 이벤트 발생
                        DDoSDetected?.Invoke(this, alert);
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"슬로로리스 공격 탐지 중 오류: {ex.Message}");
            }

            return alerts;
        }

        /// <summary>
        /// 대역폭 기반 DDoS 탐지
        /// </summary>
        private async Task<List<DDoSAlert>> DetectBandwidthFloodAsync(List<ProcessNetworkInfo> connections)
        {
            var alerts = new List<DDoSAlert>();

            try
            {
                var now = DateTime.Now;

                // IP별 초당 데이터 전송량 분석
                var bandwidthByIP = connections
                    .Where(c => (now - c.ConnectionStartTime).TotalSeconds <= 1)
                    .GroupBy(c => c.RemoteAddress)
                    .Select(g => new
                    {
                        IP = g.Key,
                        TotalBytes = g.Sum(c => c.DataTransferred),
                        ConnectionCount = g.Count()
                    });

                foreach (var item in bandwidthByIP)
                {
                    if (item.TotalBytes > _thresholds.MaxBytesPerSecond)
                    {
                        var alert = new DDoSAlert
                        {
                            AttackType = DDoSAttackType.BandwidthFlood,
                            SourceIP = item.IP,
                            Severity = DetermineSeverity((int)(item.TotalBytes / 1024 / 1024),
                                                       (int)(_thresholds.MaxBytesPerSecond / 1024 / 1024)),
                            Description = $"대역폭 기반 DDoS 공격 탐지: {item.IP}에서 초당 {item.TotalBytes / 1024 / 1024:F1}MB 전송",
                            DetectedAt = now,
                            DataTransferred = item.TotalBytes,
                            ConnectionCount = item.ConnectionCount,
                            RecommendedAction = "대역폭 제한 및 IP 차단"
                        };

                        alerts.Add(alert);

                        // Toast 알림 표시
                        await ShowDDoSAlertAsync(alert);

                        // 이벤트 발생
                        DDoSDetected?.Invoke(this, alert);
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"대역폭 기반 DDoS 탐지 중 오류: {ex.Message}");
            }

            return alerts;
        }

        /// <summary>
        /// HTTP Flood 공격 탐지
        /// </summary>
        private async Task<List<DDoSAlert>> DetectHttpFloodAsync(List<ProcessNetworkInfo> connections)
        {
            var alerts = new List<DDoSAlert>();

            try
            {
                var now = DateTime.Now;

                // HTTP/HTTPS 포트로의 대량 연결 분석
                var httpConnections = connections
                    .Where(c => c.LocalPort == 80 || c.LocalPort == 443 ||
                               c.RemotePort == 80 || c.RemotePort == 443)
                    .Where(c => (now - c.ConnectionStartTime).TotalSeconds <= 1)
                    .GroupBy(c => c.RemoteAddress);

                foreach (var group in httpConnections)
                {
                    var ipAddress = group.Key;
                    var httpRequestCount = group.Count();

                    if (httpRequestCount > _thresholds.HttpFloodThreshold)
                    {
                        var alert = new DDoSAlert
                        {
                            AttackType = DDoSAttackType.HttpFlood,
                            SourceIP = ipAddress,
                            Severity = DetermineSeverity(httpRequestCount, _thresholds.HttpFloodThreshold),
                            Description = $"HTTP Flood 공격 탐지: {ipAddress}에서 초당 {httpRequestCount}개 HTTP 요청",
                            DetectedAt = now,
                            ConnectionCount = httpRequestCount,
                            RecommendedAction = "HTTP 요청 제한 및 IP 차단"
                        };

                        alerts.Add(alert);

                        // Toast 알림 표시
                        await ShowDDoSAlertAsync(alert);

                        // 이벤트 발생
                        DDoSDetected?.Invoke(this, alert);
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"HTTP Flood 탐지 중 오류: {ex.Message}");
            }

            return alerts;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// DDoS 공격 심각도 결정
        /// </summary>
        private DDoSSeverity DetermineSeverity(int currentValue, int threshold)
        {
            var ratio = (double)currentValue / threshold;

            return ratio switch
            {
                >= 5.0 => DDoSSeverity.Critical,
                >= 3.0 => DDoSSeverity.High,
                >= 2.0 => DDoSSeverity.Medium,
                _ => DDoSSeverity.Low
            };
        }

        /// <summary>
        /// 오류 이벤트 발생
        /// </summary>
        private void OnErrorOccurred(string message)
        {
            ErrorOccurred?.Invoke(this, message);
        }

        /// <summary>
        /// DDoS 탐지 시 Toast 알림 표시
        /// </summary>
        private async Task ShowDDoSAlertAsync(DDoSAlert alert)
        {
            if (_toastService == null) return;

            try
            {
                var title = GetAlertTitle(alert.AttackType, alert.Severity);
                var message = $"{alert.SourceIP}\n{alert.Description}\n권장조치: {alert.RecommendedAction}";

                // Windows 플랫폼에서만 Toast 알림 표시
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    // 심각도에 따른 알림 표시
                    switch (alert.Severity)
                    {
                        case DDoSSeverity.Critical:
                            await _toastService.ShowSecurityAsync($"🚨 긴급 - {title}", message);
                            break;
                        case DDoSSeverity.High:
                            await _toastService.ShowErrorAsync($"⚠️ 위험 - {title}", message);
                            break;
                        case DDoSSeverity.Medium:
                            await _toastService.ShowWarningAsync($"⚠️ 경고 - {title}", message);
                            break;
                        case DDoSSeverity.Low:
                            await _toastService.ShowInfoAsync($"ℹ️ 정보 - {title}", message);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"DDoS 알림 표시 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 공격 유형과 심각도에 따른 알림 제목 생성
        /// </summary>
        private static string GetAlertTitle(DDoSAttackType attackType, DDoSSeverity severity)
        {
            var attackName = attackType switch
            {
                DDoSAttackType.SynFlood => "SYN Flood 공격",
                DDoSAttackType.UdpFlood => "UDP Flood 공격",
                DDoSAttackType.ConnectionFlood => "연결 폭주 공격",
                DDoSAttackType.SlowLoris => "Slowloris 공격",
                DDoSAttackType.BandwidthFlood => "대역폭 공격",
                DDoSAttackType.HttpFlood => "HTTP Flood 공격",
                _ => "DDoS 공격"
            };

            return $"{attackName} 탐지";
        }

        #endregion
    }

    #region Data Models

    /// <summary>
    /// DDoS 임계값 설정
    /// </summary>
    public class DDoSThresholds
    {
        public int MaxConnectionsPerSecond { get; set; } = 50;
        public int MaxConnectionsPerMinute { get; set; } = 300;
        public int MaxConcurrentConnections { get; set; } = 1000;
        public long MaxBytesPerSecond { get; set; } = 10 * 1024 * 1024;
        public int SynFloodThreshold { get; set; } = 100;
        public int UdpFloodThreshold { get; set; } = 200;
        public int SlowLorisTimeout { get; set; } = 10;
        public int HttpFloodThreshold { get; set; } = 100;
    }

    /// <summary>
    /// DDoS 경고 정보
    /// </summary>
    public class DDoSAlert
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DDoSAttackType AttackType { get; set; }
        public string SourceIP { get; set; } = string.Empty;
        public DDoSSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; }
        public int ConnectionCount { get; set; }
        public int PacketCount { get; set; }
        public long DataTransferred { get; set; }
        public string RecommendedAction { get; set; } = string.Empty;
        public bool IsResolved { get; set; } = false;
        public DateTime? ResolvedAt { get; set; }
    }

    // DDoSAttackType은 Models/DDoSAttackTypes.cs에 정의됨

    // DDoSSeverity는 Models/DDoSAttackTypes.cs에 정의됨

    /// <summary>
    /// 연결 추적기
    /// </summary>
    public class ConnectionTracker
    {
        public string IPAddress { get; set; } = string.Empty;
        public int ConnectionCount { get; set; }
        public DateTime LastConnectionTime { get; set; }
        public List<DateTime> ConnectionTimes { get; set; } = new();
    }

    /// <summary>
    /// 트래픽 분석기
    /// </summary>
    public class TrafficAnalyzer
    {
        public string IPAddress { get; set; } = string.Empty;
        public long BytesTransferred { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public double BytesPerSecond => CalculateBytesPerSecond();

        private double CalculateBytesPerSecond()
        {
            var elapsed = (DateTime.Now - LastUpdateTime).TotalSeconds;
            return elapsed > 0 ? BytesTransferred / elapsed : 0;
        }
    }

    /// <summary>
    /// SYN Flood 추적기
    /// </summary>
    public class SynFloodTracker
    {
        public string IPAddress { get; set; } = string.Empty;
        public int SynPacketCount { get; set; }
        public DateTime LastSynTime { get; set; }
        public List<DateTime> SynTimes { get; set; } = new();
    }

    #endregion
}