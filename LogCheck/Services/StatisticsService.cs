using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using LogCheck.Models;

namespace LogCheck.Services
{
    /// <summary>
    /// 통계 데이터 관리를 위한 인터페이스
    /// </summary>
    public interface IStatisticsProvider
    {
        void UpdateStatistics(List<ProcessNetworkInfo> data);
        void UpdateStatistics(); // 매개변수 없는 버전 (ThreatIntelligence용)
        event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>
    /// 네트워크 통계 데이터 관리 서비스
    /// UpdateStatistics 중복 제거 및 통일된 통계 관리 제공
    /// </summary>
    public class NetworkStatisticsService : IStatisticsProvider, INotifyPropertyChanged
    {
        // 통계 데이터 필드
        private int _totalConnections = 0;
        private int _lowRiskCount = 0;
        private int _mediumRiskCount = 0;
        private int _highRiskCount = 0;
        private int _criticalRiskCount = 0;
        private int _tcpCount = 0;
        private int _udpCount = 0;
        private int _icmpCount = 0;
        private long _totalDataTransferred = 0;

        // 바인딩용 공개 프로퍼티들
        public int TotalConnections
        {
            get => _totalConnections;
            set { _totalConnections = value; OnPropertyChanged(); }
        }

        public int LowRiskCount
        {
            get => _lowRiskCount;
            set { _lowRiskCount = value; OnPropertyChanged(); }
        }

        public int MediumRiskCount
        {
            get => _mediumRiskCount;
            set { _mediumRiskCount = value; OnPropertyChanged(); }
        }

        public int HighRiskCount
        {
            get => _highRiskCount;
            set { _highRiskCount = value; OnPropertyChanged(); }
        }

        public int CriticalRiskCount
        {
            get => _criticalRiskCount;
            set { _criticalRiskCount = value; OnPropertyChanged(); }
        }

        public int TcpCount
        {
            get => _tcpCount;
            set { _tcpCount = value; OnPropertyChanged(); }
        }

        public int UdpCount
        {
            get => _udpCount;
            set { _udpCount = value; OnPropertyChanged(); }
        }

        public int IcmpCount
        {
            get => _icmpCount;
            set { _icmpCount = value; OnPropertyChanged(); }
        }

        public string TotalDataTransferred
        {
            get => $"{_totalDataTransferred / (1024.0 * 1024.0):F1} MB";
        }

        /// <summary>
        /// 위험 연결 수 (Medium + High + Critical)
        /// </summary>
        public int DangerousConnections => _mediumRiskCount + _highRiskCount + _criticalRiskCount;

        /// <summary>
        /// 통계 요약 텍스트
        /// </summary>
        public string StatisticsSummary =>
            $"총 연결: {_totalConnections} | " +
            $"위험 연결: {DangerousConnections} | " +
            $"TCP: {_tcpCount} | " +
            $"UDP: {_udpCount} | " +
            $"총 데이터: {_totalDataTransferred / (1024 * 1024):F1} MB";

        /// <summary>
        /// 프로세스 네트워크 데이터를 기반으로 통계 업데이트
        /// </summary>
        /// <param name="data">프로세스 네트워크 데이터 목록</param>
        public void UpdateStatistics(List<ProcessNetworkInfo> data)
        {
            data ??= new List<ProcessNetworkInfo>();

            // 프로퍼티를 통해 업데이트하여 자동으로 UI가 갱신되도록 함
            TotalConnections = data.Count;
            LowRiskCount = data.Count(x => x.RiskLevel == SecurityRiskLevel.Low);
            MediumRiskCount = data.Count(x => x.RiskLevel == SecurityRiskLevel.Medium);
            HighRiskCount = data.Count(x => x.RiskLevel == SecurityRiskLevel.High);
            CriticalRiskCount = data.Count(x => x.RiskLevel == SecurityRiskLevel.Critical);
            TcpCount = data.Count(x => x.Protocol == "TCP");
            UdpCount = data.Count(x => x.Protocol == "UDP");
            IcmpCount = data.Count(x => x.Protocol == "ICMP");
            _totalDataTransferred = data.Sum(x => x.DataTransferred);

            // 계산된 프로퍼티들 수동 알림
            OnPropertyChanged(nameof(TotalDataTransferred));
            OnPropertyChanged(nameof(DangerousConnections));
            OnPropertyChanged(nameof(StatisticsSummary));
        }

        /// <summary>
        /// 매개변수 없는 통계 업데이트 (ThreatIntelligence용)
        /// </summary>
        public void UpdateStatistics()
        {
            // 계산된 프로퍼티들 수동 알림
            OnPropertyChanged(nameof(TotalDataTransferred));
            OnPropertyChanged(nameof(DangerousConnections));
            OnPropertyChanged(nameof(StatisticsSummary));
        }

        /// <summary>
        /// 통계 데이터 재설정
        /// </summary>
        public void ResetStatistics()
        {
            TotalConnections = 0;
            LowRiskCount = 0;
            MediumRiskCount = 0;
            HighRiskCount = 0;
            CriticalRiskCount = 0;
            TcpCount = 0;
            UdpCount = 0;
            IcmpCount = 0;
            _totalDataTransferred = 0;

            OnPropertyChanged(nameof(TotalDataTransferred));
            OnPropertyChanged(nameof(DangerousConnections));
            OnPropertyChanged(nameof(StatisticsSummary));
        }

        /// <summary>
        /// PropertyChanged 이벤트
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// PropertyChanged 이벤트 발생
        /// </summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 위협 정보 통계 서비스 (ThreatIntelligence 페이지용)
    /// </summary>
    public class ThreatIntelligenceStatisticsService : IStatisticsProvider, INotifyPropertyChanged
    {
        private int _totalThreats = 0;
        private int _blockedIPs = 0;
        private int _allowedIPs = 0;
        private int _pendingAnalysis = 0;

        public int TotalThreats
        {
            get => _totalThreats;
            set { _totalThreats = value; OnPropertyChanged(); }
        }

        public int BlockedIPs
        {
            get => _blockedIPs;
            set { _blockedIPs = value; OnPropertyChanged(); }
        }

        public int AllowedIPs
        {
            get => _allowedIPs;
            set { _allowedIPs = value; OnPropertyChanged(); }
        }

        public int PendingAnalysis
        {
            get => _pendingAnalysis;
            set { _pendingAnalysis = value; OnPropertyChanged(); }
        }

        public string ThreatSummary =>
            $"총 위협: {_totalThreats} | 차단된 IP: {_blockedIPs} | 허용된 IP: {_allowedIPs}";

        public void UpdateStatistics(List<ProcessNetworkInfo> data)
        {
            // ThreatIntelligence는 ProcessNetworkInfo를 직접 사용하지 않음
            UpdateStatistics();
        }

        public void UpdateStatistics()
        {
            // 계산된 프로퍼티들 수동 알림
            OnPropertyChanged(nameof(ThreatSummary));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}