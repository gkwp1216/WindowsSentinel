namespace LogCheck.Models
{
    /// <summary>
    /// 외부 위협 정보 소스에서 받아온 IP 위협 데이터
    /// </summary>
    public class ThreatIntelligenceData
    {
        public string IPAddress { get; set; } = string.Empty;
        public int AbuseConfidenceScore { get; set; } // 0-100
        public string CountryCode { get; set; } = string.Empty;
        public string CountryName { get; set; } = string.Empty;
        public string ISP { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public List<string> Categories { get; set; } = new List<string>();
        public DateTime LastReportedAt { get; set; }
        public int TotalReports { get; set; }
        public string Description { get; set; } = string.Empty;
        public ThreatLevel ThreatLevel { get; set; }
        public DateTime RetrievedAt { get; set; } = DateTime.Now;
        public string Source { get; set; } = string.Empty; // "AbuseIPDB", "Custom", etc.
    }

    /// <summary>
    /// IP 차단 규칙
    /// </summary>
    public class IPBlockRule
    {
        public string IPAddress { get; set; } = string.Empty;
        public string RuleName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public BlockReason Reason { get; set; }
        public DateTime BlockedAt { get; set; } = DateTime.Now;
        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; } = true;
        public string CreatedBy { get; set; } = string.Empty;
        public string FirewallRuleName { get; set; } = string.Empty;
        public int Priority { get; set; } = 100;
    }

    /// <summary>
    /// 차단된 IP 주소
    /// </summary>
    public class BlockedIPAddress
    {
        public string IPAddress { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime BlockedAt { get; set; } = DateTime.Now;
        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; } = true;
        public string Source { get; set; } = string.Empty; // "Manual", "ThreatIntel", "Auto"
        public int ThreatScore { get; set; }
        public List<string> Categories { get; set; } = new List<string>();
        public string FirewallRuleName { get; set; } = string.Empty;
    }

    /// <summary>
    /// 위협 정보 소스 설정
    /// </summary>
    public class ThreatIntelligenceSource
    {
        public string Name { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = false;
        public int UpdateIntervalMinutes { get; set; } = 60;
        public DateTime LastUpdated { get; set; }
        public int MaxRequestsPerMinute { get; set; } = 100;
        public bool RequiresApiKey { get; set; } = true;
    }

    /// <summary>
    /// 위협 정보 검색 결과
    /// </summary>
    public class ThreatLookupResult
    {
        public string IPAddress { get; set; } = string.Empty;
        public bool IsThreat { get; set; } = false;
        public int ThreatScore { get; set; } = 0;
        public string ThreatDescription { get; set; } = string.Empty;
        public List<string> Categories { get; set; } = new List<string>();
        public DateTime LookupTime { get; set; } = DateTime.Now;
        public string Source { get; set; } = string.Empty;
        public bool IsBlocked { get; set; } = false;
        public string BlockReason { get; set; } = string.Empty;
    }

    /// <summary>
    /// 위험도 수준
    /// </summary>
    public enum ThreatLevel
    {
        Safe = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }

    /// <summary>
    /// 차단 사유
    /// </summary>
    public enum BlockReason
    {
        Manual = 0,
        ThreatIntelligence = 1,
        SuspiciousActivity = 2,
        PortScan = 3,
        DDoS = 4,
        Malware = 5,
        Phishing = 6,
        Botnet = 7,
        Other = 99
    }

    /// <summary>
    /// AbuseIPDB API 응답 모델
    /// </summary>
    public class AbuseIPDBResponse
    {
        public AbuseIPDBData Data { get; set; } = new();
    }

    public class AbuseIPDBData
    {
        public string IPAddress { get; set; } = string.Empty;
        public string CountryCode { get; set; } = string.Empty;
        public string CountryName { get; set; } = string.Empty;
        public string ISP { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public List<AbuseIPDBCategory> Categories { get; set; } = new();
        public int AbuseConfidenceScore { get; set; }
        public DateTime LastReportedAt { get; set; }
        public int TotalReports { get; set; }
    }

    public class AbuseIPDBCategory
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
