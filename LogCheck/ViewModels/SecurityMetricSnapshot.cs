using System;
using LogCheck.Models;

namespace LogCheck.ViewModels
{
    /// <summary>
    /// 보안 메트릭 스냅샷 - 실시간 보안 상태를 기록하는 모델
    /// </summary>
    public class SecurityMetricSnapshot
    {
        public DateTime Timestamp { get; set; }
        public ThreatLevel ThreatLevel { get; set; }
        public int ActiveThreats { get; set; }
        public int BlockedConnections { get; set; }
        public double NetworkTrafficMBps { get; set; }
        public int DDoSAttacksBlocked { get; set; }
        public bool DDoSDefenseActive { get; set; }
        public int SecurityScore { get; set; }
        public int PermanentRulesCount { get; set; }

        public SecurityMetricSnapshot()
        {
            Timestamp = DateTime.Now;
        }

        public SecurityMetricSnapshot(
            ThreatLevel threatLevel,
            int activeThreats,
            int blockedConnections,
            double networkTraffic,
            int ddosAttacksBlocked,
            bool ddosDefenseActive,
            int securityScore,
            int permanentRulesCount)
        {
            Timestamp = DateTime.Now;
            ThreatLevel = threatLevel;
            ActiveThreats = activeThreats;
            BlockedConnections = blockedConnections;
            NetworkTrafficMBps = networkTraffic;
            DDoSAttacksBlocked = ddosAttacksBlocked;
            DDoSDefenseActive = ddosDefenseActive;
            SecurityScore = securityScore;
            PermanentRulesCount = permanentRulesCount;
        }
    }
}