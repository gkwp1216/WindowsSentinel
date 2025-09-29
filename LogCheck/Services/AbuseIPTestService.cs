using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LogCheck.Services
{
    public class AbuseIPTestService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public AbuseIPTestService(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient();
            if (!string.IsNullOrEmpty(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Key", _apiKey);
            }
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        /// <summary>
        /// AbuseIPDB에서 의심스러운 IP 목록 가져오기
        /// </summary>
        public async Task<List<string>> GetSuspiciousIPsAsync(int limit = 10)
        {
            try
            {
                if (!string.IsNullOrEmpty(_apiKey))
                {
                    // AbuseIPDB Blacklist API 호출
                    var response = await _httpClient.GetAsync(
                        $"https://api.abuseipdb.com/api/v2/blacklist?confidenceMinimum=75&limit={limit}");

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var result = JsonSerializer.Deserialize<AbuseIPResponse>(content);

                        return result?.Data?.Select(ip => ip.IpAddress).ToList() ?? new List<string>();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AbuseIPDB API 호출 실패: {ex.Message}");
            }

            // API 실패 시 테스트용 알려진 의심스러운 IP들 반환
            return GetKnownMaliciousIPs();
        }

        /// <summary>
        /// 테스트용 알려진 악성 IP 목록 (API 실패 시 사용)
        /// </summary>
        private List<string> GetKnownMaliciousIPs()
        {
            return new List<string>
            {
                "185.220.100.240", // Tor 네트워크
                "185.220.100.241",
                "185.220.101.32",
                "198.96.155.3",    // 알려진 악성 IP
                "89.248.165.146",
                "45.95.169.157",
                "194.26.229.178",
                "80.82.77.139",
                "89.248.167.131",
                "185.220.102.8"
            };
        }

        /// <summary>
        /// 특정 IP의 위험도 확인
        /// </summary>
        public async Task<AbuseIPCheckResult> CheckIPAsync(string ipAddress)
        {
            try
            {
                if (!string.IsNullOrEmpty(_apiKey))
                {
                    var response = await _httpClient.GetAsync(
                        $"https://api.abuseipdb.com/api/v2/check?ipAddress={ipAddress}&maxAgeInDays=90&verbose");

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var result = JsonSerializer.Deserialize<AbuseIPCheckResponse>(content);

                        return result?.Data ?? new AbuseIPCheckResult { IpAddress = ipAddress };
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"IP 확인 실패 ({ipAddress}): {ex.Message}");
            }

            // API 실패 시 기본 위험도 반환
            return new AbuseIPCheckResult
            {
                IpAddress = ipAddress,
                AbuseConfidencePercentage = 80, // 기본 높은 위험도
                CountryCode = "Unknown",
                TotalReports = 100,
                IsPublic = true
            };
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    // JSON 응답 모델들
    public class AbuseIPResponse
    {
        public List<AbuseIPData> Data { get; set; } = new();
    }

    public class AbuseIPData
    {
        public string IpAddress { get; set; } = string.Empty;
        public int AbuseConfidencePercentage { get; set; }
    }

    public class AbuseIPCheckResponse
    {
        public AbuseIPCheckResult Data { get; set; } = new();
    }

    public class AbuseIPCheckResult
    {
        public string IpAddress { get; set; } = string.Empty;
        public bool IsPublic { get; set; }
        public int AbuseConfidencePercentage { get; set; }
        public string CountryCode { get; set; } = string.Empty;
        public int TotalReports { get; set; }
        public int NumDistinctUsers { get; set; }
        public DateTime LastReportedAt { get; set; }
    }
}