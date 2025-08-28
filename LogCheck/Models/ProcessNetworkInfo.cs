using System;
using System.Net;
using System.Collections.Generic; // Added for List

namespace LogCheck.Models
{
    /// <summary>
    /// 프로세스와 네트워크 연결 정보를 통합한 데이터 모델
    /// </summary>
    public class ProcessNetworkInfo
    {
        // 프로세스 정보
        public string ProcessName { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public string ProcessPath { get; set; } = string.Empty;
        public DateTime ProcessStartTime { get; set; }
        public ProcessRiskLevel ProcessRiskLevel { get; set; }

        // 네트워크 연결 정보
        public string LocalAddress { get; set; } = string.Empty;
        public int LocalPort { get; set; }
        public string RemoteAddress { get; set; } = string.Empty;
        public int RemotePort { get; set; }
        public string Protocol { get; set; } = string.Empty;
        public string ConnectionState { get; set; } = string.Empty;
        public DateTime ConnectionStartTime { get; set; }
        public TimeSpan ConnectionDuration => DateTime.Now - ConnectionStartTime;

        // 데이터 전송 정보
        public long DataTransferred { get; set; }
        public double DataRate { get; set; } // KB/s
        public long PacketsSent { get; set; }
        public long PacketsReceived { get; set; }

        // 보안 정보
        public SecurityRiskLevel RiskLevel { get; set; }
        public string RiskDescription { get; set; } = string.Empty;
        public bool IsBlocked { get; set; }
        public DateTime? BlockedTime { get; set; }
        public string BlockReason { get; set; } = string.Empty;

        // 추가 메타데이터
        public string UserName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string FileVersion { get; set; } = string.Empty;
        public bool IsSystemProcess { get; set; }
        public bool IsSigned { get; set; }

        // 계산된 속성
        public string DisplayName => string.IsNullOrEmpty(ProcessName) ? $"PID {ProcessId}" : ProcessName;
        public string FullAddress => $"{RemoteAddress}:{RemotePort}";
        public string LocalFullAddress => $"{LocalAddress}:{LocalPort}";
        
        // 생성자
        public ProcessNetworkInfo()
        {
            ProcessStartTime = DateTime.Now;
            ConnectionStartTime = DateTime.Now;
            ProcessRiskLevel = ProcessRiskLevel.Normal;
            RiskLevel = SecurityRiskLevel.Low;
        }
        
        // 위험도 계산
        public void CalculateRiskLevel()
        {
            var riskScore = 0;

            // 프로세스 위험도
            if (IsSystemProcess) riskScore += 10;
            if (!IsSigned) riskScore += 20;
            if (string.IsNullOrEmpty(CompanyName)) riskScore += 15;

            // 네트워크 연결 위험도
            if (IsPrivateIP(RemoteAddress)) riskScore += 5;
            if (IsWellKnownPort(RemotePort)) riskScore += 10;
            if (IsSuspiciousPort(RemotePort)) riskScore += 30;

            // 데이터 전송 위험도
            if (DataRate > 1000) riskScore += 25; // 1MB/s 이상
            if (DataTransferred > 100 * 1024 * 1024) riskScore += 20; // 100MB 이상

            // 연결 시간 위험도
            if (ConnectionDuration.TotalHours > 24) riskScore += 15; // 24시간 이상

            // 위험도 레벨 결정
            RiskLevel = riskScore switch
            {
                < 30 => SecurityRiskLevel.Low,
                < 60 => SecurityRiskLevel.Medium,
                < 90 => SecurityRiskLevel.High,
                _ => SecurityRiskLevel.Critical
            };

            // ProcessRiskLevel도 함께 업데이트
            ProcessRiskLevel = RiskLevel switch
            {
                SecurityRiskLevel.Low => ProcessRiskLevel.Normal,
                SecurityRiskLevel.Medium => ProcessRiskLevel.Warning,
                SecurityRiskLevel.High => ProcessRiskLevel.Danger,
                SecurityRiskLevel.Critical => ProcessRiskLevel.Danger,
                _ => ProcessRiskLevel.Unknown
            };

            // 위험도 설명 생성
            RiskDescription = GenerateRiskDescription(riskScore);
        }

        private string GenerateRiskDescription(int riskScore)
        {
            var reasons = new List<string>();

            if (!IsSigned) reasons.Add("서명되지 않은 프로세스");
            if (IsSuspiciousPort(RemotePort)) reasons.Add("의심스러운 포트 사용");
            if (DataRate > 1000) reasons.Add("높은 데이터 전송률");
            if (ConnectionDuration.TotalHours > 24) reasons.Add("장시간 연결");

            return reasons.Count > 0 ? string.Join(", ", reasons) : "정상 범위";
        }

        private bool IsPrivateIP(string ipAddress)
        {
            if (IPAddress.TryParse(ipAddress, out var ip))
            {
                var bytes = ip.GetAddressBytes();
                return (bytes[0] == 10) || 
                       (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) || 
                       (bytes[0] == 192 && bytes[1] == 168);
            }
            return false;
        }

        private bool IsWellKnownPort(int port)
        {
            return port <= 1024;
        }

        private bool IsSuspiciousPort(int port)
        {
            var suspiciousPorts = new[] { 22, 23, 3389, 5900, 1433, 1521, 3306, 5432, 27017 };
            return suspiciousPorts.Contains(port);
        }

        public override string ToString()
        {
            return $"{ProcessName} (PID: {ProcessId}) - {RemoteAddress}:{RemotePort} ({Protocol})";
        }
    }

    /// <summary>
    /// 프로세스 위험도 레벨
    /// </summary>
    public enum ProcessRiskLevel
    {
        Normal = 0,
        Warning = 1,
        Danger = 2,
        Unknown = 3
    }

    /// <summary>
    /// 보안 위험도 레벨
    /// </summary>
    public enum SecurityRiskLevel
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3
    }
}
