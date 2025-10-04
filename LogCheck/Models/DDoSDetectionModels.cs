using System;
using System.Collections.Generic;

namespace LogCheck.Models
{
    /// <summary>
    /// DDoS 공격 감지 결과
    /// </summary>
    public class DDoSDetectionResult
    {
        public bool IsAttackDetected { get; set; }
        public DDoSAttackType AttackType { get; set; }
        public DDoSSeverity Severity { get; set; }
        public string AttackDescription { get; set; } = string.Empty;
        public string SourceIP { get; set; } = string.Empty;
        public string TargetIP { get; set; } = string.Empty;
        public int TargetPort { get; set; }
        public int PacketCount { get; set; }
        public double AttackScore { get; set; }
        public DateTime DetectedAt { get; set; } = DateTime.Now;
        public TimeSpan Duration { get; set; }
        public List<string> MatchedSignatures { get; set; } = new();
        public List<DefenseActionType> RecommendedActions { get; set; } = new();
        public Dictionary<string, object> AdditionalData { get; set; } = new();

        /// <summary>
        /// 공격 정보를 문자열로 변환
        /// </summary>
        public override string ToString()
        {
            return $"[{Severity}] {AttackType} 공격 감지 - {SourceIP} → {TargetIP}:{TargetPort} " +
                   $"(점수: {AttackScore:F1}, 패킷: {PacketCount})";
        }

        /// <summary>
        /// 상세 정보 생성
        /// </summary>
        public string GetDetailedInfo()
        {
            var info = $"=== DDoS 공격 감지 상세정보 ===\n";
            info += $"공격 유형: {AttackType}\n";
            info += $"심각도: {Severity}\n";
            info += $"소스 IP: {SourceIP}\n";
            info += $"대상: {TargetIP}:{TargetPort}\n";
            info += $"감지 시간: {DetectedAt:yyyy-MM-dd HH:mm:ss}\n";
            info += $"지속 시간: {Duration.TotalSeconds:F1}초\n";
            info += $"패킷 수: {PacketCount:N0}\n";
            info += $"공격 점수: {AttackScore:F1}\n";
            info += $"설명: {AttackDescription}\n";

            if (MatchedSignatures.Count > 0)
            {
                info += $"매칭된 시그니처: {string.Join(", ", MatchedSignatures)}\n";
            }

            if (RecommendedActions.Count > 0)
            {
                info += $"권장 조치: {string.Join(", ", RecommendedActions)}\n";
            }

            return info;
        }
    }

    /// <summary>
    /// DDoS 감지 통계 정보
    /// </summary>
    public class DDoSDetectionStats
    {
        public int TotalAttacksDetected { get; set; }
        public int AttacksBlocked { get; set; }
        public int UniqueAttackers { get; set; }
        public Dictionary<DDoSAttackType, int> AttacksByType { get; set; } = new();
        public Dictionary<DDoSSeverity, int> AttacksBySeverity { get; set; } = new();
        public Dictionary<string, int> TopAttackerIPs { get; set; } = new();
        public Dictionary<int, int> TopTargetPorts { get; set; } = new();
        public double TotalTrafficBlocked { get; set; } // MB
        public TimeSpan AnalysisDuration { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        /// <summary>
        /// 공격 감지율 계산
        /// </summary>
        public double DetectionRate => TotalAttacksDetected > 0 ?
            (double)AttacksBlocked / TotalAttacksDetected * 100 : 0;

        /// <summary>
        /// 시간당 공격 수 계산
        /// </summary>
        public double AttacksPerHour => AnalysisDuration.TotalHours > 0 ?
            TotalAttacksDetected / AnalysisDuration.TotalHours : 0;
    }

    /// <summary>
    /// 실시간 DDoS 모니터링 메트릭
    /// </summary>
    public class DDoSMonitoringMetrics
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public long TotalPacketsAnalyzed { get; set; }
        public long PacketsPerSecond { get; set; }
        public double TrafficVolumeMbps { get; set; }
        public int ActiveConnections { get; set; }
        public int SuspiciousConnections { get; set; }
        public int BlockedIPs { get; set; }
        public DDoSDetectionState CurrentState { get; set; } = DDoSDetectionState.Normal;
        public Dictionary<string, double> ProtocolDistribution { get; set; } = new();
        public Dictionary<int, int> PortActivity { get; set; } = new();
        public List<string> RecentAlerts { get; set; } = new();

        /// <summary>
        /// 위험 점수 계산 (0-100)
        /// </summary>
        public double RiskScore
        {
            get
            {
                var score = 0.0;

                // 트래픽 볼륨 기반 점수 (최대 30점)
                score += Math.Min(30, TrafficVolumeMbps / 100 * 30);

                // 의심스러운 연결 비율 기반 점수 (최대 25점)
                if (ActiveConnections > 0)
                {
                    var suspiciousRatio = (double)SuspiciousConnections / ActiveConnections;
                    score += suspiciousRatio * 25;
                }

                // 차단된 IP 수 기반 점수 (최대 20점)
                score += Math.Min(20, BlockedIPs / 10.0 * 20);

                // 최근 알림 수 기반 점수 (최대 25점)
                score += Math.Min(25, RecentAlerts.Count / 5.0 * 25);

                return Math.Min(100, score);
            }
        }

        /// <summary>
        /// 현재 상태를 위험 점수 기반으로 업데이트
        /// </summary>
        public void UpdateStateFromRiskScore()
        {
            var risk = RiskScore;

            CurrentState = risk switch
            {
                < 20 => DDoSDetectionState.Normal,
                < 40 => DDoSDetectionState.Suspicious,
                < 70 => DDoSDetectionState.AttackDetected,
                < 90 => DDoSDetectionState.AttackBlocked,
                _ => DDoSDetectionState.SystemOverload
            };
        }
    }

    /// <summary>
    /// DDoS 방어 조치 결과
    /// </summary>
    public class DefenseActionResult
    {
        public DefenseActionType ActionType { get; set; }
        public bool Success { get; set; }
        public string Description { get; set; } = string.Empty;
        public string TargetIP { get; set; } = string.Empty;
        public DateTime ExecutedAt { get; set; } = DateTime.Now;
        public TimeSpan ExecutionDuration { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public Dictionary<string, object> ActionData { get; set; } = new();

        /// <summary>
        /// 조치 결과 요약
        /// </summary>
        public string GetSummary()
        {
            var status = Success ? "성공" : "실패";
            var result = $"{ActionType} 조치 {status}";

            if (!string.IsNullOrEmpty(TargetIP))
            {
                result += $" (대상: {TargetIP})";
            }

            if (!Success && !string.IsNullOrEmpty(ErrorMessage))
            {
                result += $" - 오류: {ErrorMessage}";
            }

            return result;
        }
    }

    /// <summary>
    /// 패킷 분석 결과
    /// </summary>
    public class PacketAnalysisResult
    {
        public DateTime AnalysisTime { get; set; } = DateTime.Now;
        public int TotalPackets { get; set; }
        public Dictionary<ProtocolKind, int> ProtocolCounts { get; set; } = new();
        public Dictionary<string, int> SourceIPCounts { get; set; } = new();
        public Dictionary<int, int> PortCounts { get; set; } = new();
        public List<TcpFlagAnalysis> TcpFlagAnalyses { get; set; } = new();
        public List<PacketSizeDistribution> SizeDistributions { get; set; } = new();
        public List<AnomalyDetection> AnomaliesDetected { get; set; } = new();
        public double AveragePacketSize { get; set; }
        public double PacketsPerSecond { get; set; }
        public TimeSpan AnalysisDuration { get; set; }

        /// <summary>
        /// 분석 결과 요약
        /// </summary>
        public string GetAnalysisSummary()
        {
            var summary = $"패킷 분석 완료: {TotalPackets:N0}개 패킷 ";
            summary += $"({PacketsPerSecond:F1} pps, 평균 크기: {AveragePacketSize:F1} bytes)\n";
            summary += $"프로토콜 분포: {string.Join(", ", ProtocolCounts.Select(p => $"{p.Key}({p.Value})"))}";

            if (AnomaliesDetected.Count > 0)
            {
                summary += $"\n이상 징후 {AnomaliesDetected.Count}건 감지";
            }

            return summary;
        }
    }

    /// <summary>
    /// TCP 플래그 분석 정보
    /// </summary>
    public class TcpFlagAnalysis
    {
        public string SourceIP { get; set; } = string.Empty;
        public TcpFlags Flags { get; set; }
        public int PacketCount { get; set; }
        public double Percentage { get; set; }
        public bool IsAnomalous { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// 패킷 크기 분포 정보
    /// </summary>
    public class PacketSizeDistribution
    {
        public string SizeRange { get; set; } = string.Empty;
        public int PacketCount { get; set; }
        public double Percentage { get; set; }
        public bool IsUnusual { get; set; }
    }

    /// <summary>
    /// 이상 징후 감지 정보
    /// </summary>
    public class AnomalyDetection
    {
        public string AnomalyType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Severity { get; set; }
        public string AffectedIP { get; set; } = string.Empty;
        public Dictionary<string, object> Details { get; set; } = new();
        public DateTime DetectedAt { get; set; } = DateTime.Now;
    }
}