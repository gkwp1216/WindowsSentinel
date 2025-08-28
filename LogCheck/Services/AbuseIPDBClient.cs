using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using LogCheck.Models;
using System.Net;
using System.Text;

namespace LogCheck.Services
{
    /// <summary>
    /// AbuseIPDB API 클라이언트
    /// </summary>
    public class AbuseIPDBClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl = "https://api.abuseipdb.com/api/v2";
        private string _apiKey = string.Empty;
        private bool _isConfigured = false;
        private readonly Dictionary<string, ThreatIntelligenceData> _cache;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(30);
        private DateTime _lastRequestTime = DateTime.MinValue;
        private readonly TimeSpan _rateLimitDelay = TimeSpan.FromMilliseconds(100); // 10 requests per second

        public event EventHandler<string>? ErrorOccurred;
        public event EventHandler<ThreatIntelligenceData>? ThreatDataReceived;

        public AbuseIPDBClient()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "WindowsSentinel/1.0");
            _cache = new Dictionary<string, ThreatIntelligenceData>();
        }

        /// <summary>
        /// API 키 설정
        /// </summary>
        public void Configure(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _isConfigured = false;
                _apiKey = string.Empty;
                return;
            }

            _apiKey = apiKey;
            _httpClient.DefaultRequestHeaders.Remove("Key");
            _httpClient.DefaultRequestHeaders.Add("Key", apiKey);
            _isConfigured = true;
        }

        /// <summary>
        /// IP 주소의 위협 정보 조회
        /// </summary>
        public async Task<ThreatLookupResult> LookupIPAsync(string ipAddress)
        {
            try
            {
                // 캐시 확인
                if (_cache.TryGetValue(ipAddress, out var cachedData))
                {
                    if (DateTime.Now - cachedData.RetrievedAt < _cacheExpiration)
                    {
                        return CreateLookupResult(cachedData);
                    }
                    _cache.Remove(ipAddress);
                }

                // API 키가 설정되지 않은 경우 기본 정보만 반환
                if (!_isConfigured)
                {
                    return new ThreatLookupResult
                    {
                        IPAddress = ipAddress,
                        IsThreat = false,
                        ThreatScore = 0,
                        ThreatDescription = "API 키가 설정되지 않음",
                        Source = "Local",
                        LookupTime = DateTime.Now
                    };
                }

                // Rate limiting
                await EnforceRateLimit();

                var response = await _httpClient.GetAsync($"{_baseUrl}/check?ipAddress={ipAddress}&maxAgeInDays=90");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var abuseResponse = JsonSerializer.Deserialize<AbuseIPDBResponse>(json);
                    
                    if (abuseResponse?.Data != null)
                    {
                        var threatData = ConvertToThreatIntelligenceData(abuseResponse.Data);
                        _cache[ipAddress] = threatData;
                        
                        ThreatDataReceived?.Invoke(this, threatData);
                        return CreateLookupResult(threatData);
                    }
                }
                else if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    OnErrorOccurred($"Rate limit exceeded for IP: {ipAddress}");
                    return new ThreatLookupResult
                    {
                        IPAddress = ipAddress,
                        IsThreat = false,
                        ThreatScore = 0,
                        ThreatDescription = "Rate limit exceeded",
                        Source = "AbuseIPDB",
                        LookupTime = DateTime.Now
                    };
                }
                else
                {
                    OnErrorOccurred($"API request failed for IP {ipAddress}: {response.StatusCode}");
                }

                return new ThreatLookupResult
                {
                    IPAddress = ipAddress,
                    IsThreat = false,
                    ThreatScore = 0,
                    ThreatDescription = "조회 실패",
                    Source = "AbuseIPDB",
                    LookupTime = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"IP 조회 중 오류 발생: {ex.Message}");
                return new ThreatLookupResult
                {
                    IPAddress = ipAddress,
                    IsThreat = false,
                    ThreatScore = 0,
                    ThreatDescription = $"오류: {ex.Message}",
                    Source = "AbuseIPDB",
                    LookupTime = DateTime.Now
                };
            }
        }

        /// <summary>
        /// 여러 IP 주소의 위협 정보 일괄 조회
        /// </summary>
        public async Task<List<ThreatLookupResult>> LookupMultipleIPsAsync(List<string> ipAddresses)
        {
            var results = new List<ThreatLookupResult>();
            
            foreach (var ip in ipAddresses)
            {
                var result = await LookupIPAsync(ip);
                results.Add(result);
                
                // Rate limiting을 위한 지연
                if (_isConfigured)
                {
                    await Task.Delay(100); // 100ms 지연
                }
            }
            
            return results;
        }

        /// <summary>
        /// 위협 정보 보고 (IP 신고)
        /// </summary>
        public async Task<bool> ReportIPAsync(string ipAddress, int categoryId, string comment = "")
        {
            try
            {
                if (!_isConfigured)
                {
                    OnErrorOccurred("API 키가 설정되지 않아 신고할 수 없습니다.");
                    return false;
                }

                await EnforceRateLimit();

                var reportData = new
                {
                    ip = ipAddress,
                    categories = categoryId.ToString(),
                    comment = comment
                };

                var json = JsonSerializer.Serialize(reportData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/report", content);
                
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    OnErrorOccurred($"IP 신고 실패: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"IP 신고 중 오류 발생: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 캐시된 위협 정보 조회
        /// </summary>
        public ThreatIntelligenceData? GetCachedData(string ipAddress)
        {
            if (_cache.TryGetValue(ipAddress, out var data))
            {
                if (DateTime.Now - data.RetrievedAt < _cacheExpiration)
                {
                    return data;
                }
                _cache.Remove(ipAddress);
            }
            return null;
        }

        /// <summary>
        /// 캐시 정리
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
        }

        /// <summary>
        /// 만료된 캐시 항목 정리
        /// </summary>
        public void CleanupExpiredCache()
        {
            var expiredKeys = _cache
                .Where(kvp => DateTime.Now - kvp.Value.RetrievedAt >= _cacheExpiration)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _cache.Remove(key);
            }
        }

        /// <summary>
        /// 설정 상태 확인
        /// </summary>
        public bool IsConfigured => _isConfigured;

        /// <summary>
        /// API 키 상태 확인 (마스킹된 형태)
        /// </summary>
        public string GetApiKeyStatus()
        {
            if (string.IsNullOrEmpty(_apiKey))
                return "설정되지 않음";
            
            if (_apiKey.Length <= 8)
                return "잘못된 형식";
            
            return $"{_apiKey.Substring(0, 4)}...{_apiKey.Substring(_apiKey.Length - 4)}";
        }

        private ThreatIntelligenceData ConvertToThreatIntelligenceData(AbuseIPDBData abuseData)
        {
            var threatLevel = DetermineThreatLevel(abuseData.AbuseConfidenceScore);
            var categories = abuseData.Categories.Select(c => c.Name).ToList();

            return new ThreatIntelligenceData
            {
                IPAddress = abuseData.IPAddress,
                AbuseConfidenceScore = abuseData.AbuseConfidenceScore,
                CountryCode = abuseData.CountryCode,
                CountryName = abuseData.CountryName,
                ISP = abuseData.ISP,
                Domain = abuseData.Domain,
                Categories = categories,
                LastReportedAt = abuseData.LastReportedAt,
                TotalReports = abuseData.TotalReports,
                ThreatLevel = threatLevel,
                RetrievedAt = DateTime.Now,
                Source = "AbuseIPDB",
                Description = GenerateThreatDescription(abuseData.AbuseConfidenceScore, categories, abuseData.TotalReports)
            };
        }

        private ThreatLevel DetermineThreatLevel(int abuseConfidenceScore)
        {
            return abuseConfidenceScore switch
            {
                0 => ThreatLevel.Safe,
                <= 25 => ThreatLevel.Low,
                <= 50 => ThreatLevel.Medium,
                <= 75 => ThreatLevel.High,
                _ => ThreatLevel.Critical
            };
        }

        private string GenerateThreatDescription(int score, List<string> categories, int totalReports)
        {
            var level = DetermineThreatLevel(score);
            var levelText = level switch
            {
                ThreatLevel.Safe => "안전",
                ThreatLevel.Low => "낮음",
                ThreatLevel.Medium => "보통",
                ThreatLevel.High => "높음",
                ThreatLevel.Critical => "매우 높음",
                _ => "알 수 없음"
            };

            var categoryText = categories.Count > 0 ? string.Join(", ", categories) : "알 수 없음";
            
            return $"위험도: {levelText} (점수: {score}/100), 카테고리: {categoryText}, 총 신고: {totalReports}회";
        }

        private ThreatLookupResult CreateLookupResult(ThreatIntelligenceData threatData)
        {
            return new ThreatLookupResult
            {
                IPAddress = threatData.IPAddress,
                IsThreat = threatData.ThreatLevel > ThreatLevel.Low,
                ThreatScore = threatData.AbuseConfidenceScore,
                ThreatDescription = threatData.Description,
                Categories = threatData.Categories,
                LookupTime = threatData.RetrievedAt,
                Source = threatData.Source,
                IsBlocked = false,
                BlockReason = string.Empty
            };
        }

        private async Task EnforceRateLimit()
        {
            var timeSinceLastRequest = DateTime.Now - _lastRequestTime;
            if (timeSinceLastRequest < _rateLimitDelay)
            {
                var delay = _rateLimitDelay - timeSinceLastRequest;
                await Task.Delay(delay);
            }
            _lastRequestTime = DateTime.Now;
        }

        private void OnErrorOccurred(string message)
        {
            ErrorOccurred?.Invoke(this, message);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
