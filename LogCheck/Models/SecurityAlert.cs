using System;

namespace LogCheck.Models
{
    public class SecurityAlert
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string AlertType { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty; // Low, Medium, High, Critical
        public string SourceIP { get; set; } = string.Empty;
        public string DestinationIP { get; set; } = string.Empty;
        public int SourcePort { get; set; }
        public int DestinationPort { get; set; }
        public string Protocol { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public bool IsResolved { get; set; } = false;
        public string Action { get; set; } = string.Empty; // Blocked, Allowed, Monitored

        // 포트 번호 표시를 위한 속성 (ICMP, WMI 등 특별한 경우 처리)
        public string SourcePortDisplay
        {
            get
            {
                if (Protocol?.ToUpper() == "ICMP")
                    return "ICMP 프로토콜";
                if (Protocol?.ToUpper() == "WMI" || Description?.Contains("WMI") == true)
                    return "WMI 네트워크";
                return SourcePort == 0 ? "-" : SourcePort.ToString();
            }
        }

        public string DestinationPortDisplay
        {
            get
            {
                if (Protocol?.ToUpper() == "ICMP")
                    return "ICMP 프로토콜";
                if (Protocol?.ToUpper() == "WMI" || Description?.Contains("WMI") == true)
                    return "WMI 네트워크";
                return DestinationPort == 0 ? "-" : DestinationPort.ToString();
            }
        }
    }

    public class MaliciousIP
    {
        public int Id { get; set; }
        public string IPAddress { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty; // Malware, Botnet, Phishing, etc.
        public string Source { get; set; } = string.Empty; // Database source
        public DateTime LastUpdated { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public class SuspiciousActivity
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string ActivityType { get; set; } = string.Empty;
        public string SourceIP { get; set; } = string.Empty;
        public int ConnectionCount { get; set; }
        public long DataVolume { get; set; }
        public string Pattern { get; set; } = string.Empty;
        public double RiskScore { get; set; }
    }
} 