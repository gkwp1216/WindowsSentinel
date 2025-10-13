using System.Collections.Concurrent;
using LogCheck.Models;

namespace LogCheck.Services
{
    /// <summary>
    /// 실시간 IP 차단 시스템
    /// </summary>
    public class RealTimeIPBlocker
    {
        private readonly ConcurrentDictionary<string, BlockedIPAddress> _blockedIPs;
        private readonly ConcurrentDictionary<string, IPBlockRule> _blockRules;
        private readonly AbuseIPDBClient _abuseIPDBClient;
        private readonly NetworkConnectionManager _connectionManager;
        private readonly System.Threading.Timer _cleanupTimer;
        private readonly System.Threading.Timer _autoBlockTimer;
        private bool _isAutoBlockingEnabled = false;
        private int _autoBlockThreshold = 250; // 50 → 250 (5배 상향) 위협 점수 임계값
        private readonly object _lockObject = new object();

        public event EventHandler<BlockedIPAddress>? IPBlocked;
        public event EventHandler<string>? IPUnblocked;
        public event EventHandler<string>? ErrorOccurred;
        public event EventHandler<ThreatLookupResult>? ThreatDetected;

        public RealTimeIPBlocker(AbuseIPDBClient abuseIPDBClient, NetworkConnectionManager connectionManager)
        {
            _blockedIPs = new ConcurrentDictionary<string, BlockedIPAddress>();
            _blockRules = new ConcurrentDictionary<string, IPBlockRule>();
            _abuseIPDBClient = abuseIPDBClient;
            _connectionManager = connectionManager;

            // 이벤트 구독
            _abuseIPDBClient.ThreatDataReceived += OnThreatDataReceived;
            _abuseIPDBClient.ErrorOccurred += OnAbuseIPDBError;

            // 정리 타이머 (1시간마다 만료된 차단 해제)
            _cleanupTimer = new System.Threading.Timer(CleanupExpiredBlocks, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));

            // 자동 차단 타이머 (5분마다 실행)
            _autoBlockTimer = new System.Threading.Timer(AutoBlockThreats, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// 자동 차단 활성화/비활성화
        /// </summary>
        public bool IsAutoBlockingEnabled
        {
            get => _isAutoBlockingEnabled;
            set
            {
                _isAutoBlockingEnabled = value;
                if (value)
                {
                    AddLogMessage("자동 IP 차단이 활성화되었습니다.");
                }
                else
                {
                    AddLogMessage("자동 IP 차단이 비활성화되었습니다.");
                }
            }
        }

        /// <summary>
        /// 자동 차단 임계값 설정
        /// </summary>
        public int AutoBlockThreshold
        {
            get => _autoBlockThreshold;
            set
            {
                _autoBlockThreshold = Math.Max(0, Math.Min(100, value));
                AddLogMessage($"자동 차단 임계값이 {_autoBlockThreshold}로 설정되었습니다.");
            }
        }

        /// <summary>
        /// IP 주소 수동 차단
        /// </summary>
        public async Task<bool> BlockIPAddressAsync(string ipAddress, string reason = "수동 차단", TimeSpan? duration = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ipAddress))
                {
                    OnErrorOccurred("IP 주소가 유효하지 않습니다.");
                    return false;
                }

                if (!IsValidIPAddress(ipAddress))
                {
                    OnErrorOccurred($"유효하지 않은 IP 주소 형식: {ipAddress}");
                    return false;
                }

                // 이미 차단된 IP인지 확인
                if (_blockedIPs.ContainsKey(ipAddress))
                {
                    OnErrorOccurred($"IP {ipAddress}는 이미 차단되어 있습니다.");
                    return false;
                }

                // Windows 방화벽 규칙 생성
                var ruleName = $"WindowsSentinel_Block_{ipAddress}_{DateTime.Now:yyyyMMddHHmmss}";
                var success = await _connectionManager.BlockIPAddressAsync(ipAddress, reason);

                if (!success)
                {
                    OnErrorOccurred($"Windows 방화벽 규칙 생성 실패: {ipAddress}");
                    return false;
                }

                // 차단 정보 저장
                var blockedIP = new BlockedIPAddress
                {
                    IPAddress = ipAddress,
                    Reason = reason,
                    BlockedAt = DateTime.Now,
                    ExpiresAt = duration.HasValue ? DateTime.Now.Add(duration.Value) : null,
                    IsActive = true,
                    Source = "Manual",
                    ThreatScore = 0,
                    Categories = new List<string>(),
                    FirewallRuleName = ruleName
                };

                _blockedIPs[ipAddress] = blockedIP;

                // 차단 규칙 생성
                var blockRule = new IPBlockRule
                {
                    IPAddress = ipAddress,
                    RuleName = ruleName,
                    Description = reason,
                    Reason = BlockReason.Manual,
                    BlockedAt = DateTime.Now,
                    ExpiresAt = duration.HasValue ? DateTime.Now.Add(duration.Value) : null,
                    IsActive = true,
                    CreatedBy = "User",
                    FirewallRuleName = ruleName,
                    Priority = 100
                };

                _blockRules[ruleName] = blockRule;

                AddLogMessage($"IP {ipAddress}가 차단되었습니다. 사유: {reason}");
                IPBlocked?.Invoke(this, blockedIP);

                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"IP 차단 중 오류 발생: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// IP 주소 차단 해제
        /// </summary>
        public async Task<bool> UnblockIPAddressAsync(string ipAddress)
        {
            try
            {
                if (!_blockedIPs.TryGetValue(ipAddress, out var blockedIP))
                {
                    OnErrorOccurred($"IP {ipAddress}는 차단되지 않았습니다.");
                    return false;
                }

                // Windows 방화벽 규칙 삭제
                var success = await _connectionManager.UnblockIPAsync(ipAddress);

                if (!success)
                {
                    OnErrorOccurred($"Windows 방화벽 규칙 삭제 실패: {ipAddress}");
                    return false;
                }

                // 차단 정보 제거
                _blockedIPs.TryRemove(ipAddress, out _);

                // 차단 규칙 제거
                if (_blockRules.TryGetValue(blockedIP.FirewallRuleName, out var rule))
                {
                    _blockRules.TryRemove(blockedIP.FirewallRuleName, out _);
                }

                AddLogMessage($"IP {ipAddress}의 차단이 해제되었습니다.");
                IPUnblocked?.Invoke(this, ipAddress);

                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"IP 차단 해제 중 오류 발생: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// IP 주소 위협 정보 조회 및 자동 차단
        /// </summary>
        public async Task<ThreatLookupResult> CheckAndBlockIPAsync(string ipAddress)
        {
            try
            {
                // 이미 차단된 IP인지 확인
                if (_blockedIPs.ContainsKey(ipAddress))
                {
                    var blockedIP = _blockedIPs[ipAddress];
                    return new ThreatLookupResult
                    {
                        IPAddress = ipAddress,
                        IsThreat = true,
                        ThreatScore = blockedIP.ThreatScore,
                        ThreatDescription = $"이미 차단됨: {blockedIP.Reason}",
                        Source = blockedIP.Source,
                        LookupTime = DateTime.Now,
                        IsBlocked = true,
                        BlockReason = blockedIP.Reason
                    };
                }

                // AbuseIPDB에서 위협 정보 조회
                var threatResult = await _abuseIPDBClient.LookupIPAsync(ipAddress);

                if (threatResult.IsThreat && threatResult.ThreatScore >= _autoBlockThreshold)
                {
                    if (_isAutoBlockingEnabled)
                    {
                        var reason = $"자동 차단: {threatResult.ThreatDescription}";
                        await BlockIPAddressAsync(ipAddress, reason);

                        // 위협 정보 업데이트
                        threatResult.IsBlocked = true;
                        threatResult.BlockReason = reason;
                    }

                    ThreatDetected?.Invoke(this, threatResult);
                }

                return threatResult;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"IP 위협 정보 조회 중 오류 발생: {ex.Message}");
                return new ThreatLookupResult
                {
                    IPAddress = ipAddress,
                    IsThreat = false,
                    ThreatScore = 0,
                    ThreatDescription = $"오류: {ex.Message}",
                    Source = "Error",
                    LookupTime = DateTime.Now,
                    IsBlocked = false,
                    BlockReason = string.Empty
                };
            }
        }

        /// <summary>
        /// 여러 IP 주소 일괄 검사
        /// </summary>
        public async Task<List<ThreatLookupResult>> CheckMultipleIPsAsync(List<string> ipAddresses)
        {
            var results = new List<ThreatLookupResult>();

            foreach (var ip in ipAddresses)
            {
                var result = await CheckAndBlockIPAsync(ip);
                results.Add(result);

                // Rate limiting을 위한 지연
                await Task.Delay(100);
            }

            return results;
        }

        /// <summary>
        /// 차단된 IP 목록 조회
        /// </summary>
        public List<BlockedIPAddress> GetBlockedIPs()
        {
            return _blockedIPs.Values.ToList();
        }

        /// <summary>
        /// 차단 규칙 목록 조회
        /// </summary>
        public List<IPBlockRule> GetBlockRules()
        {
            return _blockRules.Values.ToList();
        }

        /// <summary>
        /// 특정 IP가 차단되었는지 확인
        /// </summary>
        public bool IsIPBlocked(string ipAddress)
        {
            return _blockedIPs.ContainsKey(ipAddress);
        }

        /// <summary>
        /// 차단된 IP 통계 정보
        /// </summary>
        public (int total, int active, int expired) GetBlockStatistics()
        {
            var total = _blockedIPs.Count;
            var active = _blockedIPs.Values.Count(ip => ip.IsActive);
            var expired = _blockedIPs.Values.Count(ip => ip.ExpiresAt.HasValue && ip.ExpiresAt.Value < DateTime.Now);

            return (total, active, expired);
        }

        /// <summary>
        /// 만료된 차단 자동 해제
        /// </summary>
        private async void CleanupExpiredBlocks(object? state)
        {
            try
            {
                var expiredIPs = _blockedIPs.Values
                    .Where(ip => ip.ExpiresAt.HasValue && ip.ExpiresAt.Value < DateTime.Now)
                    .ToList();

                foreach (var expiredIP in expiredIPs)
                {
                    await UnblockIPAddressAsync(expiredIP.IPAddress);
                }

                if (expiredIPs.Count > 0)
                {
                    AddLogMessage($"{expiredIPs.Count}개의 만료된 IP 차단이 자동으로 해제되었습니다.");
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"만료된 차단 정리 중 오류 발생: {ex.Message}");
            }
        }

        /// <summary>
        /// 자동 위협 차단
        /// </summary>
        private async void AutoBlockThreats(object? state)
        {
            if (!_isAutoBlockingEnabled) return;

            try
            {
                // 현재 활성 네트워크 연결에서 외부 IP 추출
                var activeConnections = await _connectionManager.GetProcessConnectionsAsync();
                var externalIPs = activeConnections
                    .Where(c => !IsPrivateIP(c.RemoteAddress.ip))
                    .Select(c => c.RemoteAddress.ip)
                    .Distinct()
                    .ToList();

                foreach (var ip in externalIPs)
                {
                    if (!_blockedIPs.ContainsKey(ip))
                    {
                        await CheckAndBlockIPAsync(ip);
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"자동 위협 차단 중 오류 발생: {ex.Message}");
            }
        }

        /// <summary>
        /// 위협 데이터 수신 시 처리
        /// </summary>
        private void OnThreatDataReceived(object? sender, ThreatIntelligenceData threatData)
        {
            if (_isAutoBlockingEnabled && threatData.ThreatLevel >= ThreatLevel.Medium)
            {
                var reason = $"자동 차단: {threatData.Description}";
                _ = Task.Run(async () => await BlockIPAddressAsync(threatData.IPAddress, reason));
            }
        }

        /// <summary>
        /// AbuseIPDB 오류 처리
        /// </summary>
        private void OnAbuseIPDBError(object? sender, string error)
        {
            OnErrorOccurred($"AbuseIPDB 오류: {error}");
        }

        /// <summary>
        /// IP 주소 유효성 검사
        /// </summary>
        private bool IsValidIPAddress(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return false;

            var parts = ipAddress.Split('.');
            if (parts.Length != 4)
                return false;

            foreach (var part in parts)
            {
                if (!int.TryParse(part, out int num) || num < 0 || num > 255)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 사설 IP 주소 확인
        /// </summary>
        private bool IsPrivateIP(string ipAddress)
        {
            if (!IsValidIPAddress(ipAddress))
                return false;

            var parts = ipAddress.Split('.');
            var first = int.Parse(parts[0]);
            var second = int.Parse(parts[1]);

            return (first == 10) ||
                   (first == 172 && second >= 16 && second <= 31) ||
                   (first == 192 && second == 168);
        }

        /// <summary>
        /// 로그 메시지 추가
        /// </summary>
        private void AddLogMessage(string message)
        {
            // 로그 메시지 추가 로직 (필요시 구현)
        }

        /// <summary>
        /// 오류 발생 시 이벤트 발생
        /// </summary>
        private void OnErrorOccurred(string message)
        {
            ErrorOccurred?.Invoke(this, message);
        }

        /// <summary>
        /// 리소스 정리
        /// </summary>
        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            _autoBlockTimer?.Dispose();
        }
    }
}
