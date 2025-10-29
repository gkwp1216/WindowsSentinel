using System;
using System.Collections.Concurrent;
using System.Threading;
using LogCheck.Models;

namespace LogCheck.Services
{
    /// <summary>
    /// Single, clean IntegratedDDoSDefenseSystem implementation.
    /// Replaces prior corrupted content with a minimal, stable baseline.
    /// </summary>
    public class IntegratedDDoSDefenseSystem
    {
        // Optional collaborators expected by UI and other components
        private readonly DDoSDetectionEngine? _detectionEngine;
        private readonly AdvancedPacketAnalyzer? _packetAnalyzer;
        private readonly DDoSSignatureDatabase? _signatureDatabase;
        private readonly ICaptureService _captureService;
        private readonly RateLimitingService _rateLimiter;

        private readonly ConcurrentQueue<PacketDto> _packetQueue = new();
        private readonly ConcurrentDictionary<int, ProcessTrafficStats> _processTrafficStats = new();
        private readonly ConcurrentDictionary<string, ProcessTrafficStats> _sourceIpTrafficStats = new(StringComparer.OrdinalIgnoreCase);
        private readonly System.Threading.Timer _analysisTimer;
        private volatile bool _isRunning;

        public event EventHandler<DDoSDetectionResult>? AttackDetected;
        public event EventHandler<DefenseActionResult>? DefenseActionExecuted;
        public event Action? MetricsUpdated;

        public IntegratedDDoSDefenseSystem(ICaptureService captureService, RateLimitingService rateLimiter)
        {
            _captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
            _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));

            _captureService.OnPacket += (_, p) => { if (_isRunning) _packetQueue.Enqueue(p); };
            _analysisTimer = new System.Threading.Timer(_ => AnalyzeCycle(), null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Constructor used by MainWindows/NetWorks_New which pass several components.
        /// Keeps backward-compatible surface for existing callers.
        /// </summary>
        public IntegratedDDoSDefenseSystem(
            DDoSDetectionEngine detectionEngine,
            AdvancedPacketAnalyzer packetAnalyzer,
            RateLimitingService rateLimiter,
            DDoSSignatureDatabase signatureDatabase,
            ICaptureService captureService)
            : this(captureService, rateLimiter)
        {
            _detectionEngine = detectionEngine ?? throw new ArgumentNullException(nameof(detectionEngine));
            _packetAnalyzer = packetAnalyzer ?? throw new ArgumentNullException(nameof(packetAnalyzer));
            _signatureDatabase = signatureDatabase ?? throw new ArgumentNullException(nameof(signatureDatabase));

            // Optionally hook detection engine events to forward to UI
            _detectionEngine.DDoSDetected += (_, alert) =>
            {
                if (alert == null) return;
                var res = new DDoSDetectionResult
                {
                    IsAttackDetected = true,
                    AttackType = alert.AttackType,
                    Severity = alert.Severity,
                    AttackDescription = alert.Description ?? string.Empty,
                    SourceIP = alert.SourceIP ?? string.Empty,
                    TargetIP = string.Empty,
                    PacketCount = alert.PacketCount,
                    DetectedAt = alert.DetectedAt
                };

                AttackDetected?.Invoke(this, res);
            };
        }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _analysisTimer.Change(0, 1000);
            _ = _captureService.StartAsync();
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;
            _analysisTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _ = _captureService.StopAsync();
        }

        public void ProcessPacket(PacketDto packet)
        {
            if (!_isRunning) return;
            _packetQueue.Enqueue(packet);
        }

        public void AddPacket(PacketDto packet)
        {
            ProcessPacket(packet);
        }

        /// <summary>
        /// Return aggregated DDoS detection statistics for UI.
        /// </summary>
        public DDoSDetectionStats GetStatistics()
        {
            var stats = new DDoSDetectionStats();
            stats.TotalAttacksDetected = 0;
            stats.AttacksBlocked = 0;
            stats.UniqueAttackers = _processTrafficStats.Count;
            stats.AttacksBySeverity = new Dictionary<DDoSSeverity, int>();
            stats.AttacksByType = new Dictionary<DDoSAttackType, int>();
            stats.TopAttackerIPs = new Dictionary<string, int>();
            stats.TopTargetPorts = new Dictionary<int, int>();
            stats.LastUpdated = DateTime.Now;

            // Minimal population from process stats
            foreach (var kv in _processTrafficStats)
            {
                var s = kv.Value;
                var pps = (int)s.GetPacketsPerSecond();
                if (pps > 0)
                {
                    stats.TotalAttacksDetected += 0; // not incrementing here; kept for compatibility
                }
            }

            return stats;
        }

        /// <summary>
        /// Return a simple hourly trend placeholder for UI charts.
        /// </summary>
        public Dictionary<int, int> GetHourlyThreatTrend()
        {
            var dict = new Dictionary<int, int>(24);
            var baseVal = Math.Max(0, _processTrafficStats.Count);
            for (int i = 0; i < 24; i++) dict[i] = baseVal;
            return dict;
        }

        private async void AnalyzeCycle()
        {
            if (!_isRunning) return;

            while (_packetQueue.TryDequeue(out var p))
            {
                if (p.ProcessId.HasValue && p.ProcessId.Value > 0)
                {
                    var stats = _processTrafficStats.GetOrAdd(p.ProcessId.Value, id => new ProcessTrafficStats(id, p.ProcessName ?? "Unknown"));
                    stats.AddPacket(p);
                }
                else if (!string.IsNullOrEmpty(p.SrcIp))
                {
                    // Aggregate by source IP when process information is not available
                    var key = p.SrcIp;
                    var stats = _sourceIpTrafficStats.GetOrAdd(key, ip => new ProcessTrafficStats(0, ip));
                    stats.AddPacket(p);
                }
            }

            foreach (var kv in _processTrafficStats)
            {
                var s = kv.Value;
                if (s.GetPacketsPerSecond() > 50 && (DateTime.UtcNow - s.LastAlertTime).TotalSeconds > 10) // 임계값 낮춤
                {
                    s.LastAlertTime = DateTime.UtcNow;
                    var res = new DDoSDetectionResult
                    {
                        IsAttackDetected = true,
                        AttackType = DDoSAttackType.HighTrafficProcess,
                        Severity = DDoSSeverity.High,
                        AttackDescription = $"Process {s.ProcessName} (PID {s.ProcessId}) high PPS: {s.GetPacketsPerSecond():F0}",
                        SourceIP = s.ProcessName,
                        DetectedAt = DateTime.UtcNow,
                        PacketCount = (int)s.GetPacketsPerSecond()
                    };

                    AttackDetected?.Invoke(this, res);
                    Console.WriteLine($"Detection: {res.AttackDescription}");

                    // 안전한 기본 조치: 네트워크 레벨 rate-limit 적용(임시)
                    try
                    {
                        // Best-effort: if rate limiter available, apply rate limiting by process IP if known
                        if (_rateLimiter != null)
                        {
                            // Use ProcessName as source identifier fallback when IP unknown
                            var sourceIp = res.SourceIP; // may contain processName when process-based
                            var networkInfo = new ProcessNetworkInfo
                            {
                                ProcessId = s.ProcessId,
                                ProcessName = s.ProcessName ?? string.Empty,
                                RemoteAddress = sourceIp,
                                RemotePort = 0,
                                LocalPort = 0
                            };

                            // 기본 제한값: 200 pps, 30분
                            await _rateLimiter.ApplyRateLimit(sourceIp, networkInfo, ppsLimit: 200, duration: TimeSpan.FromMinutes(30));
                            Console.WriteLine($"Applied rate limit for {sourceIp}");
                        }

                        // 기록: AutoBlockStatisticsService에 차단 이벤트 저장
                        try
                        {
                            var statsService = new AutoBlockStatisticsService("Data Source=autoblock.db");
                            var blocked = new AutoBlockedConnection
                            {
                                ProcessName = s.ProcessName ?? string.Empty,
                                ProcessPath = string.Empty,
                                ProcessId = s.ProcessId,
                                RemoteAddress = res.SourceIP,
                                RemotePort = 0,
                                LocalPort = 0,
                                Protocol = "Unknown",
                                BlockLevel = BlockLevel.Immediate,
                                Reason = res.AttackDescription,
                                ConfidenceScore = 0.9,
                                TriggeredRules = string.Join(',', new[] { "HighPPS" }),
                                BlockedAt = DateTime.UtcNow,
                                IsBlocked = true
                            };

                            Console.WriteLine($"About to record block event for {blocked.RemoteAddress}");
                            await statsService.InitializeDatabaseAsync();
                            await statsService.RecordBlockEventAsync(blocked);
                            Console.WriteLine($"Recorded block event for {blocked.RemoteAddress} at {blocked.BlockedAt}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to record block event: {ex}");
                            System.Diagnostics.Debug.WriteLine($"차단 기록 실패: {ex.Message}");
                        }

                        var action = new DefenseActionResult
                        {
                            ActionType = DefenseActionType.RateLimit,
                            Success = true,
                            Description = "Temporary rate-limit applied",
                            ExecutedAt = DateTime.UtcNow
                        };

                        DefenseActionExecuted?.Invoke(this, action);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"방어 액션 적용 중 오류: {ex.Message}");
                    }
                }
            }

            // Also check aggregated source-IP stats
            foreach (var kv in _sourceIpTrafficStats)
            {
                var s = kv.Value;
                if (s.GetPacketsPerSecond() > 100 && (DateTime.UtcNow - s.LastAlertTime).TotalSeconds > 30)
                {
                    s.LastAlertTime = DateTime.UtcNow;
                    var res = new DDoSDetectionResult
                    {
                        IsAttackDetected = true,
                        AttackType = DDoSAttackType.HighTrafficProcess,
                        Severity = DDoSSeverity.High,
                        AttackDescription = $"Source IP {s.ProcessName} high PPS: {s.GetPacketsPerSecond():F0}",
                        SourceIP = s.ProcessName,
                        DetectedAt = DateTime.UtcNow,
                        PacketCount = (int)s.GetPacketsPerSecond()
                    };

                    AttackDetected?.Invoke(this, res);

                    try
                    {
                        if (_rateLimiter != null)
                        {
                            var networkInfo = new ProcessNetworkInfo
                            {
                                ProcessId = 0,
                                ProcessName = s.ProcessName ?? string.Empty,
                                RemoteAddress = s.ProcessName ?? string.Empty,
                                RemotePort = 0,
                                LocalPort = 0
                            };

                            await _rateLimiter.ApplyRateLimit(s.ProcessName ?? string.Empty, networkInfo, ppsLimit: 200, duration: TimeSpan.FromMinutes(30));
                        }

                        try
                        {
                            var statsService = new AutoBlockStatisticsService("Data Source=autoblock.db");
                            var blocked = new AutoBlockedConnection
                            {
                                ProcessName = s.ProcessName ?? string.Empty,
                                ProcessPath = string.Empty,
                                ProcessId = 0,
                                RemoteAddress = s.ProcessName ?? string.Empty,
                                RemotePort = 0,
                                LocalPort = 0,
                                Protocol = "Unknown",
                                BlockLevel = BlockLevel.Immediate,
                                Reason = res.AttackDescription,
                                ConfidenceScore = 0.9,
                                TriggeredRules = string.Join(',', new[] { "HighPPS" }),
                                BlockedAt = DateTime.UtcNow,
                                IsBlocked = true
                            };

                            Console.WriteLine($"About to record block event for {blocked.RemoteAddress}");
                            await statsService.InitializeDatabaseAsync();
                            await statsService.RecordBlockEventAsync(blocked);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"차단 기록 실패: {ex.Message}");
                        }

                        var action = new DefenseActionResult
                        {
                            ActionType = DefenseActionType.RateLimit,
                            Success = true,
                            Description = "Temporary rate-limit applied",
                            ExecutedAt = DateTime.UtcNow
                        };

                        DefenseActionExecuted?.Invoke(this, action);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"방어 액션 적용 중 오류: {ex.Message}");
                    }
                }
            }
        }
    }
}
