using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LogCheck.Models
{
    /// <summary>
    /// 작업 관리자 스타일의 프로세스 트리뷰 모델
    /// 각 프로세스의 확장 상태를 개별적으로 관리합니다
    /// </summary>
    public class ProcessTreeNode : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private string _processName = "";
        private string _processPath = "";

        // 전역 확장 상태 저장소 (작업 관리자 방식)
        private static readonly Dictionary<string, bool> ProcessExpandedStates = new();

        public int ProcessId { get; set; }

        public string ProcessName
        {
            get => _processName;
            set
            {
                if (_processName != value)
                {
                    _processName = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayText));
                    OnPropertyChanged(nameof(UniqueId));
                }
            }
        }

        public string ProcessPath
        {
            get => _processPath;
            set
            {
                if (_processPath != value)
                {
                    _processPath = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<ProcessNetworkInfo> Connections { get; set; } = new();

        /// <summary>
        /// 차단된 연결 목록 (별도 관리)
        /// </summary>
        public ObservableCollection<ProcessNetworkInfo> BlockedConnections { get; set; } = new();

        /// <summary>
        /// 전체 연결 목록 (활성 + 차단) - TreeView에서 사용
        /// </summary>
        public ObservableCollection<ProcessNetworkInfo> AllConnections { get; set; } = new();

        /// <summary>
        /// 작업 관리자처럼 확장 상태를 개별 관리
        /// 상태 변경 시 전역 딕셔너리에 자동 저장
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();

                    // 상태 변경 시 전역 딕셔너리에 저장
                    ProcessExpandedStates[UniqueId] = value;

                    System.Diagnostics.Debug.WriteLine(
                        $"[ProcessTreeNode] {ProcessName} ({ProcessId}) 확장 상태: {(value ? "펼침" : "접힘")}");
                }
            }
        }

        /// <summary>
        /// 현재 활성 연결 수
        /// </summary>
        public int ConnectionCount => Connections.Count;

        /// <summary>
        /// 차단된 연결 수
        /// </summary>
        public int BlockedConnectionCount => BlockedConnections.Count;

        /// <summary>
        /// 전체 연결 수 (활성 + 차단)
        /// </summary>
        public int TotalConnectionCount => ConnectionCount + BlockedConnectionCount;

        /// <summary>
        /// 트리뷰에 표시될 텍스트 (활성/차단 연결 수 구분)
        /// </summary>
        public string DisplayText => BlockedConnectionCount > 0
            ? $"{ProcessName} ({ConnectionCount}개 활성, {BlockedConnectionCount}개 차단)"
            : $"{ProcessName} ({ConnectionCount}개 연결)";

        /// <summary>
        /// 고유 식별자 생성 (PID는 재사용될 수 있으므로 프로세스명과 함께 사용)
        /// </summary>
        public string UniqueId => $"{ProcessId}_{ProcessName}";

        /// <summary>
        /// 위험도 기반 배경색 (가장 높은 위험도 기준)
        /// </summary>
        public string BackgroundColor
        {
            get
            {
                if (!Connections.Any()) return "Transparent";

                var maxRisk = Connections.Max(c => c.RiskLevel);
                return maxRisk switch
                {
                    SecurityRiskLevel.Critical => "#FFCDD2", // 진한 빨강
                    SecurityRiskLevel.High => "#FFEBEE",     // 연한 빨강
                    SecurityRiskLevel.Medium => "#FFF3E0",   // 연한 주황
                    SecurityRiskLevel.Low => "#E8F5E8",      // 연한 초록
                    SecurityRiskLevel.System => "#E3F2FD",   // 연한 파랑
                    _ => "Transparent"
                };
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 저장된 확장 상태 조회
        /// </summary>
        /// <param name="uniqueId">고유 식별자</param>
        /// <returns>저장된 확장 상태 (기본값: false)</returns>
        public static bool GetSavedExpandedState(string uniqueId)
        {
            return ProcessExpandedStates.TryGetValue(uniqueId, out bool state) && state;
        }

        /// <summary>
        /// 확장 상태 초기화 (애플리케이션 시작 시 호출)
        /// </summary>
        public static void ClearExpandedStates()
        {
            ProcessExpandedStates.Clear();
            System.Diagnostics.Debug.WriteLine("[ProcessTreeNode] 모든 확장 상태 초기화됨");
        }

        /// <summary>
        /// 현재 저장된 확장 상태 디버그 출력
        /// </summary>
        public static void DebugPrintExpandedStates()
        {
            System.Diagnostics.Debug.WriteLine($"[ProcessTreeNode] 현재 저장된 확장 상태: {ProcessExpandedStates.Count}개");
            foreach (var kvp in ProcessExpandedStates)
            {
                System.Diagnostics.Debug.WriteLine($"  {kvp.Key}: {(kvp.Value ? "펼침" : "접힘")}");
            }
        }

        /// <summary>
        /// 연결 목록 업데이트 및 관련 프로퍼티 갱신 (활성/차단 연결 분리)
        /// </summary>
        /// <param name="newConnections">새로운 연결 목록</param>
        public void UpdateConnections(List<ProcessNetworkInfo> newConnections)
        {
            // 활성 연결과 차단된 연결 분리
            var activeConnections = newConnections.Where(c => !c.IsBlocked).ToList();
            var blockedConnections = newConnections.Where(c => c.IsBlocked).ToList();

            // 활성 연결 업데이트
            Connections.Clear();
            foreach (var connection in activeConnections)
            {
                Connections.Add(connection);
            }

            // 차단된 연결 업데이트 (기존 차단 목록과 병합)
            foreach (var blockedConnection in blockedConnections)
            {
                // 중복 제거 (같은 원격 주소:포트 조합)
                var existing = BlockedConnections.FirstOrDefault(c =>
                    c.RemoteAddress == blockedConnection.RemoteAddress &&
                    c.RemotePort == blockedConnection.RemotePort &&
                    c.ProcessId == blockedConnection.ProcessId);

                if (existing == null)
                {
                    BlockedConnections.Add(blockedConnection);
                }
            }

            // 전체 연결 목록 업데이트 (TreeView 표시용)
            AllConnections.Clear();
            foreach (var connection in activeConnections)
            {
                AllConnections.Add(connection);
            }
            foreach (var connection in BlockedConnections)
            {
                AllConnections.Add(connection);
            }

            // 관련 프로퍼티들 갱신 알림
            OnPropertyChanged(nameof(ConnectionCount));
            OnPropertyChanged(nameof(BlockedConnectionCount));
            OnPropertyChanged(nameof(TotalConnectionCount));
            OnPropertyChanged(nameof(DisplayText));
            OnPropertyChanged(nameof(BackgroundColor));
        }

        /// <summary>
        /// 프로세스 기본 정보 업데이트
        /// </summary>
        /// <param name="processInfo">프로세스 정보</param>
        public void UpdateProcessInfo(ProcessNetworkInfo processInfo)
        {
            if (ProcessId != processInfo.ProcessId)
            {
                ProcessId = processInfo.ProcessId;
                OnPropertyChanged(nameof(ProcessId));
                OnPropertyChanged(nameof(UniqueId));
            }

            if (ProcessName != processInfo.ProcessName)
            {
                ProcessName = processInfo.ProcessName;
            }

            if (ProcessPath != processInfo.ProcessPath)
            {
                ProcessPath = processInfo.ProcessPath;
            }
        }

        public override string ToString()
        {
            return $"ProcessTreeNode: {ProcessName} ({ProcessId}) - {ConnectionCount} connections, Expanded: {IsExpanded}";
        }
    }
}