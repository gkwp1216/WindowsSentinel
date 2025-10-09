using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LogCheck.Models
{
    /// <summary>
    /// 지역별 위협 정보를 나타내는 모델
    /// </summary>
    public class GeographicThreatInfo : INotifyPropertyChanged
    {
        private string _countryCode = "";
        public string CountryCode
        {
            get => _countryCode;
            set
            {
                _countryCode = value;
                OnPropertyChanged();
            }
        }

        private string _countryName = "";
        public string CountryName
        {
            get => _countryName;
            set
            {
                _countryName = value;
                OnPropertyChanged();
            }
        }

        private int _threatCount = 0;
        public int ThreatCount
        {
            get => _threatCount;
            set
            {
                _threatCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ThreatLevel));
            }
        }

        private double _latitude = 0;
        public double Latitude
        {
            get => _latitude;
            set
            {
                _latitude = value;
                OnPropertyChanged();
            }
        }

        private double _longitude = 0;
        public double Longitude
        {
            get => _longitude;
            set
            {
                _longitude = value;
                OnPropertyChanged();
            }
        }

        public ThreatLevel ThreatLevel => ThreatCount switch
        {
            >= 100 => Models.ThreatLevel.Critical,
            >= 50 => Models.ThreatLevel.High,
            >= 20 => Models.ThreatLevel.Medium,
            >= 1 => Models.ThreatLevel.Low,
            _ => Models.ThreatLevel.Low
        };

        public string ThreatLevelText => ThreatLevel switch
        {
            Models.ThreatLevel.Critical => "🔴 매우 높음",
            Models.ThreatLevel.High => "🟠 높음",
            Models.ThreatLevel.Medium => "🟡 보통",
            Models.ThreatLevel.Low => "🟢 낮음",
            _ => "알 수 없음"
        };

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 공격 패턴 분석 데이터
    /// </summary>
    public class AttackPatternData
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public DDoSAttackType AttackType { get; set; }
        public int Count { get; set; }
        public double Intensity { get; set; } // 0-100 스케일

        public string AttackTypeDisplayName => AttackType switch
        {
            DDoSAttackType.SynFlood => "SYN Flood",
            DDoSAttackType.UdpFlood => "UDP Flood",
            DDoSAttackType.HttpFlood => "HTTP Flood",
            DDoSAttackType.ConnectionFlood => "Connection Flood",
            DDoSAttackType.SlowLoris => "Slowloris",
            DDoSAttackType.BandwidthExhaustion => "대역폭 소진",
            DDoSAttackType.VolumetricAttack => "볼류메트릭 공격",
            _ => "알 수 없는 공격"
        };
    }

    /// <summary>
    /// 보안 점수 계산 요소
    /// </summary>
    public class SecurityScoreFactors
    {
        public double DefenseEfficiency { get; set; } = 1.0; // 0-1 스케일
        public double NetworkHealthScore { get; set; } = 1.0; // 0-1 스케일
        public double ThreatActivityScore { get; set; } = 1.0; // 0-1 스케일
        public int DDoSDefenseScore { get; set; } = 25; // 최대 25점
        public int FirewallIntegrityScore { get; set; } = 20; // 최대 20점
        public int ThreatResponseScore { get; set; } = 20; // 최대 20점
        public int SystemStabilityScore { get; set; } = 15; // 최대 15점
        public int NetworkSecurityScore { get; set; } = 10; // 최대 10점
        public int ComplianceScore { get; set; } = 10; // 최대 10점

        public int TotalScore => DDoSDefenseScore + FirewallIntegrityScore +
                                ThreatResponseScore + SystemStabilityScore +
                                NetworkSecurityScore + ComplianceScore;

        public List<string> GetWeakAreas()
        {
            var weakAreas = new List<string>();

            if (DefenseEfficiency < 0.8) weakAreas.Add("방어 효율성");
            if (NetworkHealthScore < 0.8) weakAreas.Add("네트워크 상태");
            if (ThreatActivityScore < 0.8) weakAreas.Add("위협 활동");
            if (DDoSDefenseScore < 20) weakAreas.Add("DDoS 방어 시스템");
            if (FirewallIntegrityScore < 15) weakAreas.Add("방화벽 무결성");
            if (ThreatResponseScore < 15) weakAreas.Add("위협 대응");
            if (SystemStabilityScore < 10) weakAreas.Add("시스템 안정성");
            if (NetworkSecurityScore < 7) weakAreas.Add("네트워크 보안");
            if (ComplianceScore < 7) weakAreas.Add("컴플라이언스");

            return weakAreas;
        }
    }

    /// <summary>
    /// 위협 예측 분석 결과
    /// </summary>
    public class ThreatPredictionResult
    {
        public DateTime PredictionTime { get; set; } = DateTime.Now;
        public TimeSpan PredictionPeriod { get; set; } = TimeSpan.FromHours(4);
        public double RiskIncreasePercentage { get; set; }
        public ThreatLevel PredictedThreatLevel { get; set; }
        public string PredictedRiskLevel { get; set; } = "보통"; // 문자열 형태의 위험도
        public List<string> RiskFactors { get; set; } = new();
        public string Recommendation { get; set; } = "";
        public double Confidence { get; set; } // 0-1 스케일

        public string ConfidenceText => $"신뢰도: {Confidence * 100:F0}%";

        public string PredictionSummary =>
            $"{PredictionPeriod.TotalHours}시간 내 {RiskIncreasePercentage:+0.0;-0.0;0}% 위험도 변화 예상";
    }
}