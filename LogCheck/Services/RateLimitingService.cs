using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using LogCheck.Models;

namespace LogCheck.Services
{
    /// <summary>
    /// 실시간 트래픽 제한 및 DDoS 방어 시스템
    /// IP별, 포트별, 프로세스별 연결 수와 대역폭을 실시간으로 제한
    /// </summary>
    public class RateLimitingService : IDisposable
    {
        #region Private Fields

        private readonly ConcurrentDictionary<string, IPRateLimiter> _ipLimiters;
        private readonly ConcurrentDictionary<int, PortRateLimiter> _portLimiters;
        private readonly ConcurrentDictionary<int, ProcessRateLimiter> _processLimiters;
        private readonly RateLimitConfig _config;
        private readonly System.Timers.Timer _cleanupTimer;
        private readonly object _lockObject = new object();
        private bool _disposed = false;

        #endregion

        #region Events

        public event EventHandler<RateLimitViolation>? RateLimitExceeded;
        public event EventHandler<string>? ErrorOccurred;

        #endregion

        #region Constructor

        public RateLimitingService(RateLimitConfig? config = null)
        {
            _ipLimiters = new ConcurrentDictionary<string, IPRateLimiter>();
            _portLimiters = new ConcurrentDictionary<int, PortRateLimiter>();
            _processLimiters = new ConcurrentDictionary<int, ProcessRateLimiter>();

            // 기본 설정 또는 사용자 설정
            _config = config ?? new RateLimitConfig();

            // 주기적으로 만료된 제한기들을 정리
            _cleanupTimer = new System.Timers.Timer(TimeSpan.FromMinutes(5).TotalMilliseconds);
            _cleanupTimer.Elapsed += CleanupExpiredLimiters;
            _cleanupTimer.AutoReset = true;
            _cleanupTimer.Start();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 연결 요청에 대한 Rate Limit 검사
        /// </summary>
        public async Task<RateLimitResult> CheckRateLimitAsync(ProcessNetworkInfo connection)
        {
            try
            {
                var results = new List<RateLimitResult>();

                // 1. IP별 Rate Limit 검사
                var ipResult = await CheckIPRateLimitAsync(connection);
                results.Add(ipResult);

                // 2. 포트별 Rate Limit 검사
                var portResult = await CheckPortRateLimitAsync(connection);
                results.Add(portResult);

                // 3. 프로세스별 Rate Limit 검사
                var processResult = await CheckProcessRateLimitAsync(connection);
                results.Add(processResult);

                // 하나라도 제한에 걸리면 차단
                var blocked = results.Any(r => !r.IsAllowed);
                var violationType = results.Where(r => !r.IsAllowed).FirstOrDefault()?.ViolationType ?? RateLimitViolationType.None;

                var result = new RateLimitResult
                {
                    IsAllowed = !blocked,
                    ViolationType = violationType,
                    ConnectionInfo = connection,
                    CheckedAt = DateTime.Now,
                    Details = string.Join("; ", results.Where(r => !r.IsAllowed).Select(r => r.Details))
                };

                if (blocked)
                {
                    // Rate Limit 위반 이벤트 발생
                    OnRateLimitExceeded(new RateLimitViolation
                    {
                        ViolationType = violationType,
                        SourceIP = connection.RemoteAddress,
                        ProcessId = connection.ProcessId,
                        ProcessName = connection.ProcessName,
                        Port = connection.RemotePort,
                        DetectedAt = DateTime.Now,
                        Details = result.Details
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Rate Limit 검사 중 오류: {ex.Message}");
                // 오류 발생 시 기본적으로 허용
                return new RateLimitResult
                {
                    IsAllowed = true,
                    ViolationType = RateLimitViolationType.None,
                    ConnectionInfo = connection,
                    CheckedAt = DateTime.Now,
                    Details = $"검사 오류: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 특정 IP에 대한 Rate Limit 설정
        /// </summary>
        public async Task SetCustomIPRateLimitAsync(string ipAddress, IPRateLimitSettings settings)
        {
            try
            {
                var limiter = _ipLimiters.GetOrAdd(ipAddress, _ => new IPRateLimiter(ipAddress, _config.DefaultIPSettings));
                limiter.UpdateSettings(settings);
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"IP Rate Limit 설정 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 현재 Rate Limit 통계 조회
        /// </summary>
        public async Task<RateLimitStatistics> GetStatisticsAsync()
        {
            try
            {
                var stats = new RateLimitStatistics
                {
                    GeneratedAt = DateTime.Now,
                    ActiveIPLimiters = _ipLimiters.Count,
                    ActivePortLimiters = _portLimiters.Count,
                    ActiveProcessLimiters = _processLimiters.Count
                };

                // IP별 통계
                stats.IPStatistics = _ipLimiters.Values.Select(limiter => new IPRateLimitStatistics
                {
                    IPAddress = limiter.IPAddress,
                    ConnectionsPerSecond = limiter.GetCurrentConnectionsPerSecond(),
                    ConnectionsPerMinute = limiter.GetCurrentConnectionsPerMinute(),
                    BytesPerSecond = limiter.GetCurrentBytesPerSecond(),
                    IsBlocked = limiter.IsBlocked,
                    LastActivity = limiter.LastActivity
                }).ToList();

                return stats;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Rate Limit 통계 조회 중 오류: {ex.Message}");
                return new RateLimitStatistics { GeneratedAt = DateTime.Now };
            }
        }

        /// <summary>
        /// 특정 IP의 Rate Limit 해제
        /// </summary>
        public async Task UnblockIPAsync(string ipAddress)
        {
            try
            {
                if (_ipLimiters.TryGetValue(ipAddress, out var limiter))
                {
                    limiter.Unblock();
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"IP 차단 해제 중 오류: {ex.Message}");
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// IP별 Rate Limit 검사
        /// </summary>
        private async Task<RateLimitResult> CheckIPRateLimitAsync(ProcessNetworkInfo connection)
        {
            var ipAddress = connection.RemoteAddress;
            var limiter = _ipLimiters.GetOrAdd(ipAddress, _ => new IPRateLimiter(ipAddress, _config.DefaultIPSettings));

            return await limiter.CheckLimitAsync(connection);
        }

        /// <summary>
        /// 포트별 Rate Limit 검사
        /// </summary>
        private async Task<RateLimitResult> CheckPortRateLimitAsync(ProcessNetworkInfo connection)
        {
            var port = connection.LocalPort; // 로컬 포트 기준
            var limiter = _portLimiters.GetOrAdd(port, _ => new PortRateLimiter(port, _config.DefaultPortSettings));

            return await limiter.CheckLimitAsync(connection);
        }

        /// <summary>
        /// 프로세스별 Rate Limit 검사
        /// </summary>
        private async Task<RateLimitResult> CheckProcessRateLimitAsync(ProcessNetworkInfo connection)
        {
            var processId = connection.ProcessId;
            var limiter = _processLimiters.GetOrAdd(processId, _ => new ProcessRateLimiter(processId, _config.DefaultProcessSettings));

            return await limiter.CheckLimitAsync(connection);
        }

        /// <summary>
        /// 만료된 Rate Limiter들을 정리
        /// </summary>
        private void CleanupExpiredLimiters(object? sender, ElapsedEventArgs e)
        {
            try
            {
                var now = DateTime.Now;
                var expireThreshold = TimeSpan.FromMinutes(10); // 10분 비활성 시 제거

                // IP Limiters 정리
                var expiredIPs = _ipLimiters
                    .Where(kvp => now - kvp.Value.LastActivity > expireThreshold)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var ip in expiredIPs)
                {
                    _ipLimiters.TryRemove(ip, out _);
                }

                // Port Limiters 정리
                var expiredPorts = _portLimiters
                    .Where(kvp => now - kvp.Value.LastActivity > expireThreshold)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var port in expiredPorts)
                {
                    _portLimiters.TryRemove(port, out _);
                }

                // Process Limiters 정리
                var expiredProcesses = _processLimiters
                    .Where(kvp => now - kvp.Value.LastActivity > expireThreshold)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var processId in expiredProcesses)
                {
                    _processLimiters.TryRemove(processId, out _);
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Rate Limiter 정리 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// Rate Limit 위반 이벤트 발생
        /// </summary>
        private void OnRateLimitExceeded(RateLimitViolation violation)
        {
            RateLimitExceeded?.Invoke(this, violation);
        }

        /// <summary>
        /// 오류 이벤트 발생
        /// </summary>
        private void OnErrorOccurred(string message)
        {
            ErrorOccurred?.Invoke(this, message);
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _cleanupTimer?.Stop();
                _cleanupTimer?.Dispose();

                // Rate Limiters 정리
                foreach (var limiter in _ipLimiters.Values)
                {
                    limiter.Dispose();
                }

                foreach (var limiter in _portLimiters.Values)
                {
                    limiter.Dispose();
                }

                foreach (var limiter in _processLimiters.Values)
                {
                    limiter.Dispose();
                }

                _disposed = true;
            }
        }

        #endregion
    }

    #region Rate Limiter Classes

    /// <summary>
    /// IP별 Rate Limiter
    /// </summary>
    public class IPRateLimiter : IDisposable
    {
        private readonly ConcurrentQueue<DateTime> _connections;
        private readonly ConcurrentQueue<(DateTime Time, long Bytes)> _dataTransfers;
        private IPRateLimitSettings _settings;
        private bool _isBlocked;
        private DateTime _blockStartTime;

        public string IPAddress { get; }
        public DateTime LastActivity { get; private set; }
        public bool IsBlocked => _isBlocked && (DateTime.Now - _blockStartTime) < _settings.BlockDuration;

        public IPRateLimiter(string ipAddress, IPRateLimitSettings settings)
        {
            IPAddress = ipAddress;
            _settings = settings;
            _connections = new ConcurrentQueue<DateTime>();
            _dataTransfers = new ConcurrentQueue<(DateTime, long)>();
            LastActivity = DateTime.Now;
        }

        public async Task<RateLimitResult> CheckLimitAsync(ProcessNetworkInfo connection)
        {
            LastActivity = DateTime.Now;

            // 현재 차단된 상태인지 확인
            if (IsBlocked)
            {
                return new RateLimitResult
                {
                    IsAllowed = false,
                    ViolationType = RateLimitViolationType.IPBlocked,
                    ConnectionInfo = connection,
                    CheckedAt = DateTime.Now,
                    Details = $"IP {IPAddress}가 차단됨 (해제까지 {(_settings.BlockDuration - (DateTime.Now - _blockStartTime)).TotalMinutes:F1}분)"
                };
            }

            var now = DateTime.Now;

            // 연결 기록 추가
            _connections.Enqueue(now);
            _dataTransfers.Enqueue((now, connection.DataTransferred));

            // 오래된 기록 제거
            CleanupOldRecords(now);

            // 초당 연결 수 확인
            var connectionsLastSecond = _connections.Count(t => (now - t).TotalSeconds <= 1);
            if (connectionsLastSecond > _settings.MaxConnectionsPerSecond)
            {
                Block();
                return new RateLimitResult
                {
                    IsAllowed = false,
                    ViolationType = RateLimitViolationType.ConnectionRate,
                    ConnectionInfo = connection,
                    CheckedAt = now,
                    Details = $"IP {IPAddress}: 초당 연결 수 초과 ({connectionsLastSecond}/{_settings.MaxConnectionsPerSecond})"
                };
            }

            // 분당 연결 수 확인
            var connectionsLastMinute = _connections.Count(t => (now - t).TotalMinutes <= 1);
            if (connectionsLastMinute > _settings.MaxConnectionsPerMinute)
            {
                Block();
                return new RateLimitResult
                {
                    IsAllowed = false,
                    ViolationType = RateLimitViolationType.ConnectionRate,
                    ConnectionInfo = connection,
                    CheckedAt = now,
                    Details = $"IP {IPAddress}: 분당 연결 수 초과 ({connectionsLastMinute}/{_settings.MaxConnectionsPerMinute})"
                };
            }

            // 초당 데이터 전송량 확인
            var bytesLastSecond = _dataTransfers
                .Where(t => (now - t.Time).TotalSeconds <= 1)
                .Sum(t => t.Bytes);

            if (bytesLastSecond > _settings.MaxBytesPerSecond)
            {
                Block();
                return new RateLimitResult
                {
                    IsAllowed = false,
                    ViolationType = RateLimitViolationType.BandwidthLimit,
                    ConnectionInfo = connection,
                    CheckedAt = now,
                    Details = $"IP {IPAddress}: 초당 대역폭 초과 ({bytesLastSecond / 1024 / 1024:F1}MB/{_settings.MaxBytesPerSecond / 1024 / 1024}MB)"
                };
            }

            return new RateLimitResult
            {
                IsAllowed = true,
                ViolationType = RateLimitViolationType.None,
                ConnectionInfo = connection,
                CheckedAt = now,
                Details = "정상"
            };
        }

        public void UpdateSettings(IPRateLimitSettings settings)
        {
            _settings = settings;
        }

        public void Block()
        {
            _isBlocked = true;
            _blockStartTime = DateTime.Now;
        }

        public void Unblock()
        {
            _isBlocked = false;
        }

        public int GetCurrentConnectionsPerSecond()
        {
            var now = DateTime.Now;
            return _connections.Count(t => (now - t).TotalSeconds <= 1);
        }

        public int GetCurrentConnectionsPerMinute()
        {
            var now = DateTime.Now;
            return _connections.Count(t => (now - t).TotalMinutes <= 1);
        }

        public long GetCurrentBytesPerSecond()
        {
            var now = DateTime.Now;
            return _dataTransfers
                .Where(t => (now - t.Time).TotalSeconds <= 1)
                .Sum(t => t.Bytes);
        }

        private void CleanupOldRecords(DateTime now)
        {
            // 1분 이상 된 연결 기록 제거
            while (_connections.TryPeek(out var connectionTime) && (now - connectionTime).TotalMinutes > 1)
            {
                _connections.TryDequeue(out _);
            }

            // 1분 이상 된 데이터 전송 기록 제거
            while (_dataTransfers.TryPeek(out var transfer) && (now - transfer.Time).TotalMinutes > 1)
            {
                _dataTransfers.TryDequeue(out _);
            }
        }

        public void Dispose()
        {
            // IP Limiter는 메모리 정리만 수행
        }
    }

    /// <summary>
    /// 포트별 Rate Limiter
    /// </summary>
    public class PortRateLimiter : IDisposable
    {
        private readonly ConcurrentQueue<DateTime> _connections;
        private PortRateLimitSettings _settings;

        public int Port { get; }
        public DateTime LastActivity { get; private set; }

        public PortRateLimiter(int port, PortRateLimitSettings settings)
        {
            Port = port;
            _settings = settings;
            _connections = new ConcurrentQueue<DateTime>();
            LastActivity = DateTime.Now;
        }

        public async Task<RateLimitResult> CheckLimitAsync(ProcessNetworkInfo connection)
        {
            LastActivity = DateTime.Now;
            var now = DateTime.Now;

            _connections.Enqueue(now);
            CleanupOldRecords(now);

            var connectionsLastSecond = _connections.Count(t => (now - t).TotalSeconds <= 1);
            if (connectionsLastSecond > _settings.MaxConnectionsPerSecond)
            {
                return new RateLimitResult
                {
                    IsAllowed = false,
                    ViolationType = RateLimitViolationType.PortOverload,
                    ConnectionInfo = connection,
                    CheckedAt = now,
                    Details = $"포트 {Port}: 초당 연결 수 초과 ({connectionsLastSecond}/{_settings.MaxConnectionsPerSecond})"
                };
            }

            return new RateLimitResult
            {
                IsAllowed = true,
                ViolationType = RateLimitViolationType.None,
                ConnectionInfo = connection,
                CheckedAt = now,
                Details = "정상"
            };
        }

        private void CleanupOldRecords(DateTime now)
        {
            while (_connections.TryPeek(out var connectionTime) && (now - connectionTime).TotalMinutes > 1)
            {
                _connections.TryDequeue(out _);
            }
        }

        public void Dispose()
        {
            // Port Limiter는 메모리 정리만 수행
        }
    }

    /// <summary>
    /// 프로세스별 Rate Limiter
    /// </summary>
    public class ProcessRateLimiter : IDisposable
    {
        private readonly ConcurrentQueue<DateTime> _connections;
        private ProcessRateLimitSettings _settings;

        public int ProcessId { get; }
        public DateTime LastActivity { get; private set; }

        public ProcessRateLimiter(int processId, ProcessRateLimitSettings settings)
        {
            ProcessId = processId;
            _settings = settings;
            _connections = new ConcurrentQueue<DateTime>();
            LastActivity = DateTime.Now;
        }

        public async Task<RateLimitResult> CheckLimitAsync(ProcessNetworkInfo connection)
        {
            LastActivity = DateTime.Now;
            var now = DateTime.Now;

            _connections.Enqueue(now);
            CleanupOldRecords(now);

            var connectionsLastSecond = _connections.Count(t => (now - t).TotalSeconds <= 1);
            if (connectionsLastSecond > _settings.MaxConnectionsPerSecond)
            {
                return new RateLimitResult
                {
                    IsAllowed = false,
                    ViolationType = RateLimitViolationType.ProcessOverload,
                    ConnectionInfo = connection,
                    CheckedAt = now,
                    Details = $"프로세스 {ProcessId}: 초당 연결 수 초과 ({connectionsLastSecond}/{_settings.MaxConnectionsPerSecond})"
                };
            }

            return new RateLimitResult
            {
                IsAllowed = true,
                ViolationType = RateLimitViolationType.None,
                ConnectionInfo = connection,
                CheckedAt = now,
                Details = "정상"
            };
        }

        private void CleanupOldRecords(DateTime now)
        {
            while (_connections.TryPeek(out var connectionTime) && (now - connectionTime).TotalMinutes > 1)
            {
                _connections.TryDequeue(out _);
            }
        }

        public void Dispose()
        {
            // Process Limiter는 메모리 정리만 수행
        }
    }

    #endregion

    #region Configuration Classes

    /// <summary>
    /// Rate Limit 전체 설정
    /// </summary>
    public class RateLimitConfig
    {
        public IPRateLimitSettings DefaultIPSettings { get; set; } = new IPRateLimitSettings();
        public PortRateLimitSettings DefaultPortSettings { get; set; } = new PortRateLimitSettings();
        public ProcessRateLimitSettings DefaultProcessSettings { get; set; } = new ProcessRateLimitSettings();
    }

    /// <summary>
    /// IP별 Rate Limit 설정
    /// </summary>
    public class IPRateLimitSettings
    {
        public int MaxConnectionsPerSecond { get; set; } = 10;
        public int MaxConnectionsPerMinute { get; set; } = 100;
        public long MaxBytesPerSecond { get; set; } = 5 * 1024 * 1024; // 5MB/s
        public TimeSpan BlockDuration { get; set; } = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// 포트별 Rate Limit 설정
    /// </summary>
    public class PortRateLimitSettings
    {
        public int MaxConnectionsPerSecond { get; set; } = 100;
    }

    /// <summary>
    /// 프로세스별 Rate Limit 설정
    /// </summary>
    public class ProcessRateLimitSettings
    {
        public int MaxConnectionsPerSecond { get; set; } = 50;
    }

    #endregion

    #region Result Classes

    /// <summary>
    /// Rate Limit 검사 결과
    /// </summary>
    public class RateLimitResult
    {
        public bool IsAllowed { get; set; }
        public RateLimitViolationType ViolationType { get; set; }
        public ProcessNetworkInfo ConnectionInfo { get; set; } = new();
        public DateTime CheckedAt { get; set; }
        public string Details { get; set; } = string.Empty;
    }

    /// <summary>
    /// Rate Limit 위반 정보
    /// </summary>
    public class RateLimitViolation
    {
        public RateLimitViolationType ViolationType { get; set; }
        public string SourceIP { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public int Port { get; set; }
        public DateTime DetectedAt { get; set; }
        public string Details { get; set; } = string.Empty;
    }

    /// <summary>
    /// Rate Limit 통계
    /// </summary>
    public class RateLimitStatistics
    {
        public DateTime GeneratedAt { get; set; }
        public int ActiveIPLimiters { get; set; }
        public int ActivePortLimiters { get; set; }
        public int ActiveProcessLimiters { get; set; }
        public List<IPRateLimitStatistics> IPStatistics { get; set; } = new();
    }

    /// <summary>
    /// IP별 Rate Limit 통계
    /// </summary>
    public class IPRateLimitStatistics
    {
        public string IPAddress { get; set; } = string.Empty;
        public int ConnectionsPerSecond { get; set; }
        public int ConnectionsPerMinute { get; set; }
        public long BytesPerSecond { get; set; }
        public bool IsBlocked { get; set; }
        public DateTime LastActivity { get; set; }
    }

    /// <summary>
    /// Rate Limit 위반 유형
    /// </summary>
    public enum RateLimitViolationType
    {
        None,
        ConnectionRate,
        BandwidthLimit,
        IPBlocked,
        PortOverload,
        ProcessOverload
    }

    #endregion
}