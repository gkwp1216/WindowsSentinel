using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace LogCheck.Models
{
    /// <summary>
    /// PID별로 그룹화된 프로세스 정보를 나타내는 클래스
    /// </summary>
    public class ProcessGroup : INotifyPropertyChanged
    {
        private bool _isExpanded = false;
        private ObservableCollection<ProcessNetworkInfo> _processes;

        /// <summary>
        /// 프로세스 ID
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// 프로세스명
        /// </summary>
        public string ProcessName { get; set; } = string.Empty;

        /// <summary>
        /// 프로세스 경로
        /// </summary>
        public string ProcessPath { get; set; } = string.Empty;

        /// <summary>
        /// 그룹화된 프로세스들
        /// </summary>
        public ObservableCollection<ProcessNetworkInfo> Processes
        {
            get => _processes;
            set
            {
                _processes = value;
                OnPropertyChanged();
                UpdateSummaryData();
            }
        }

        /// <summary>
        /// 그룹이 확장되었는지 여부
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 연결 수
        /// </summary>
        public int ConnectionCount => Processes?.Count ?? 0;

        /// <summary>
        /// 프로세스 개수
        /// </summary>
        public int ProcessCount => Processes?.Count ?? 0;

        /// <summary>
        /// 최고 위험도
        /// </summary>
        public SecurityRiskLevel MaxRiskLevel => Processes?.Any() == true
            ? Processes.Max(p => p.RiskLevel)
            : SecurityRiskLevel.Low;

        /// <summary>
        /// 총 데이터 전송량
        /// </summary>
        public long TotalDataTransferred => Processes?.Sum(p => p.DataTransferred) ?? 0;

        /// <summary>
        /// 활성 연결 수 (ESTABLISHED 상태)
        /// </summary>
        public int ActiveConnections => Processes?.Count(p => p.ConnectionState == "ESTABLISHED") ?? 0;

        /// <summary>
        /// 프로토콜 요약 (TCP/UDP 개수)
        /// </summary>
        public string ProtocolSummary
        {
            get
            {
                if (Processes?.Any() != true) return "";

                var tcpCount = Processes.Count(p => p.Protocol == "TCP");
                var udpCount = Processes.Count(p => p.Protocol == "UDP");
                var icmpCount = Processes.Count(p => p.Protocol == "ICMP");

                var parts = new List<string>();
                if (tcpCount > 0) parts.Add($"TCP({tcpCount})");
                if (udpCount > 0) parts.Add($"UDP({udpCount})");
                if (icmpCount > 0) parts.Add($"ICMP({icmpCount})");

                return string.Join(", ", parts);
            }
        }

        /// <summary>
        /// 시스템 프로세스인지 여부
        /// </summary>
        public bool IsSystemProcess => Processes?.FirstOrDefault()?.IsSystemProcess ?? false;

        /// <summary>
        /// 화이트리스트 프로세스인지 여부
        /// </summary>
        public bool IsWhitelisted => Processes?.FirstOrDefault()?.IsWhitelisted ?? false;

        /// <summary>
        /// 생성자
        /// </summary>
        public ProcessGroup()
        {
            _processes = new ObservableCollection<ProcessNetworkInfo>();
            _processes.CollectionChanged += (s, e) => UpdateSummaryData();
        }

        /// <summary>
        /// 요약 데이터 업데이트
        /// </summary>
        private void UpdateSummaryData()
        {
            OnPropertyChanged(nameof(ConnectionCount));
            OnPropertyChanged(nameof(MaxRiskLevel));
            OnPropertyChanged(nameof(TotalDataTransferred));
            OnPropertyChanged(nameof(ActiveConnections));
            OnPropertyChanged(nameof(ProtocolSummary));
            OnPropertyChanged(nameof(IsSystemProcess));
            OnPropertyChanged(nameof(IsWhitelisted));
        }

        /// <summary>
        /// PropertyChanged 이벤트
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}