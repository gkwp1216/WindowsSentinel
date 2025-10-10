using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using LogCheck.Models;
using LogCheck.Services;

namespace LogCheck.ViewModels
{
    [SupportedOSPlatform("windows")]
    public class AutoBlockViewModel : INotifyPropertyChanged
    {
        #region Private Fields

        private readonly AutoBlockService? _autoBlockService;
        private readonly AutoBlockStatisticsService? _statisticsService;
        private readonly PersistentFirewallManager? _firewallManager;
        private readonly ToastNotificationService? _toastService;

        private ObservableCollection<IBlockedConnection> _blockedConnections = new();
        private ICollectionView? _filteredView;
        private string _currentFilter = "All";
        private bool _isLoading = false;

        // 통계 프로퍼티들
        private int _totalBlocks = 0;
        private int _temporaryBlocks = 0;
        private int _permanentBlocks = 0;
        private int _last24HBlocks = 0;
        private double _successRate = 0.0;
        private int _firewallRules = 0;

        // 필터링 프로퍼티들
        private int _selectedConnectionsCount = 0;

        #endregion

        #region Public Properties

        public ObservableCollection<IBlockedConnection> BlockedConnections
        {
            get => _blockedConnections;
            set
            {
                _blockedConnections = value;
                OnPropertyChanged(nameof(BlockedConnections));
            }
        }

        public ICollectionView? FilteredView
        {
            get => _filteredView;
            set
            {
                _filteredView = value;
                OnPropertyChanged(nameof(FilteredView));
            }
        }

        public string CurrentFilter
        {
            get => _currentFilter;
            set
            {
                _currentFilter = value;
                OnPropertyChanged(nameof(CurrentFilter));
                RefreshFilter();
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
            }
        }

        // 통계 프로퍼티들
        public int TotalBlocks
        {
            get => _totalBlocks;
            set
            {
                _totalBlocks = value;
                OnPropertyChanged(nameof(TotalBlocks));
            }
        }

        public int TemporaryBlocks
        {
            get => _temporaryBlocks;
            set
            {
                _temporaryBlocks = value;
                OnPropertyChanged(nameof(TemporaryBlocks));
            }
        }

        public int PermanentBlocks
        {
            get => _permanentBlocks;
            set
            {
                _permanentBlocks = value;
                OnPropertyChanged(nameof(PermanentBlocks));
            }
        }

        public int Last24HBlocks
        {
            get => _last24HBlocks;
            set
            {
                _last24HBlocks = value;
                OnPropertyChanged(nameof(Last24HBlocks));
            }
        }

        public double SuccessRate
        {
            get => _successRate;
            set
            {
                _successRate = value;
                OnPropertyChanged(nameof(SuccessRate));
            }
        }

        public int FirewallRules
        {
            get => _firewallRules;
            set
            {
                _firewallRules = value;
                OnPropertyChanged(nameof(FirewallRules));
            }
        }

        public int SelectedConnectionsCount
        {
            get => _selectedConnectionsCount;
            set
            {
                _selectedConnectionsCount = value;
                OnPropertyChanged(nameof(SelectedConnectionsCount));
                OnPropertyChanged(nameof(HasSelectedConnections));
            }
        }

        public bool HasSelectedConnections => SelectedConnectionsCount > 0;

        // 서브 텍스트 프로퍼티들 (UI 바인딩용)
        public string TotalBlocksSubText => $"임시 {TemporaryBlocks} | 영구 {PermanentBlocks}";
        public string SuccessRateSubText => $"성공 {TotalBlocks - (int)(TotalBlocks * (1 - SuccessRate / 100))} / 실패 {(int)(TotalBlocks * (1 - SuccessRate / 100))}";

        #endregion

        #region Commands

        public ICommand RefreshCommand { get; private set; }
        public ICommand UnblockSelectedCommand { get; private set; }
        public ICommand AddToWhitelistCommand { get; private set; }
        public ICommand ExportListCommand { get; private set; }
        public ICommand SetFilterCommand { get; private set; }

        #endregion

        #region Constructor

        public AutoBlockViewModel()
        {
            try
            {
                // 서비스 초기화
                _autoBlockService = new AutoBlockService();
                _statisticsService = new AutoBlockStatisticsService("Data Source=autoblock.db");
                _firewallManager = new PersistentFirewallManager();

                // Toast 서비스 초기화 (Windows 플랫폼에서만)
                try
                {
                    _toastService = ToastNotificationService.Instance;
                }
                catch
                {
                    _toastService = null; // Windows가 아닌 환경에서는 null
                }

                // 필터링을 위한 CollectionView 설정
                FilteredView = CollectionViewSource.GetDefaultView(BlockedConnections);
                if (FilteredView != null)
                {
                    FilteredView.Filter = FilterConnections;
                }

                // Commands 초기화
                RefreshCommand = new RelayCommand(async () => await LoadDataAsync());
                UnblockSelectedCommand = new RelayCommand(async () => await UnblockSelectedConnectionsAsync(), () => HasSelectedConnections);
                AddToWhitelistCommand = new RelayCommand(async () => await AddSelectedToWhitelistAsync(), () => HasSelectedConnections);
                ExportListCommand = new RelayCommand(() => ExportConnectionList());
                SetFilterCommand = new RelayCommand(() => { });

                // 기본 필터 설정
                CurrentFilter = "All";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AutoBlockViewModel 초기화 오류: {ex.Message}");
                _toastService?.ShowErrorAsync("초기화 오류", ex.Message);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 데이터를 비동기적으로 로드합니다.
        /// </summary>
        public async Task LoadDataAsync()
        {
            try
            {
                IsLoading = true;
                _toastService?.ShowInfoAsync("데이터 로딩", "차단 목록을 불러오는 중...");

                // 서비스가 초기화되지 않은 경우 처리
                if (_autoBlockService == null || _statisticsService == null)
                {
                    _toastService?.ShowErrorAsync("오류", "자동 차단 서비스가 초기화되지 않았습니다.");
                    return;
                }

                // 데이터베이스 초기화 먼저 수행
                try
                {
                    await _statisticsService.InitializeDatabaseAsync();
                }
                catch (Exception dbEx)
                {
                    Debug.WriteLine($"데이터베이스 초기화 오류: {dbEx.Message}");
                }

                // 임시 차단 연결 로드
                var temporaryConnections = await _autoBlockService.GetBlockedConnectionsAsync();

                // 영구 차단 연결 로드 (데이터베이스 + 방화벽 규칙 통합)
                List<PermanentBlockedConnection> permanentConnections = new();
                try
                {
                    // 1. 데이터베이스에서 영구 차단 연결 로드
                    permanentConnections = await _statisticsService.GetPermanentlyBlockedConnectionsAsync();

                    // 2. 방화벽 규칙에서 실제 활성 규칙도 확인하여 보완
                    if (_firewallManager != null)
                    {
                        var firewallRules = await _firewallManager.GetLogCheckRulesAsync();

                        foreach (var rule in firewallRules)
                        {
                            // 방화벽 규칙은 있지만 DB에 없는 경우, 복구를 위해 추가
                            var existsInDb = permanentConnections.Any(pc =>
                                rule.Name.Contains(pc.ProcessName) && rule.Name.Contains(pc.RemoteAddress));

                            if (!existsInDb && rule.Name.StartsWith("LogCheck_Block_"))
                            {
                                // 규칙 이름에서 정보 파싱하여 임시 객체 생성
                                var parts = rule.Name.Replace("LogCheck_Block_", "").Split('_');
                                if (parts.Length >= 2)
                                {
                                    var processName = parts[0];
                                    var address = parts[1];

                                    var recoveredConnection = new PermanentBlockedConnection
                                    {
                                        ProcessName = processName,
                                        RemoteAddress = address,
                                        RemotePort = 0, // 방화벽 규칙에서는 포트 정보 부족
                                        Protocol = "Unknown",
                                        Reason = "방화벽 규칙에서 복구",
                                        LastBlockedAt = DateTime.Now,
                                        FirewallRuleExists = true
                                    };

                                    permanentConnections.Add(recoveredConnection);

                                    // 복구된 연결 정보를 로그에 기록
                                    Debug.WriteLine($"방화벽 규칙에서 복구된 연결: {processName} → {address}");
                                    _toastService?.ShowInfoAsync("연결 복구",
                                        $"방화벽에서 {processName} → {address} 복구");
                                }
                            }
                        }

                        _toastService?.ShowSuccessAsync("영구 차단 동기화",
                            $"DB: {permanentConnections.Count}개, 방화벽: {firewallRules.Count}개 규칙");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"영구 차단 연결 로드 오류: {ex.Message}");
                    _toastService?.ShowErrorAsync("영구 차단 로드 실패", ex.Message);
                }

                // 통합 목록 생성
                var allConnections = new List<IBlockedConnection>();

                // 임시 차단을 IBlockedConnection으로 변환
                allConnections.AddRange(temporaryConnections.Select(tc => new BlockedConnectionWrapper(tc, false)));

                // 영구 차단을 IBlockedConnection으로 변환
                allConnections.AddRange(permanentConnections.Select(pc => new BlockedConnectionWrapper(pc, true)));

                // 차단 시간 순으로 정렬 (최신순)
                allConnections = allConnections.OrderByDescending(c => c.BlockedAt).ToList();

                // UI 업데이트
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    BlockedConnections.Clear();
                    foreach (var connection in allConnections)
                    {
                        BlockedConnections.Add(connection);
                    }
                });

                await RefreshStatisticsAsync();
                RefreshFilter();

                _toastService?.ShowSuccessAsync("로딩 완료", $"{allConnections.Count}개 차단 연결 로드됨");
            }
            catch (Exception ex)
            {
                _toastService?.ShowErrorAsync("데이터 로드 실패", ex.Message);
                Debug.WriteLine($"LoadDataAsync 오류: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 통계를 새로고침합니다.
        /// </summary>
        public async Task RefreshStatisticsAsync()
        {
            try
            {
                // 방화벽 규칙 상태 실시간 확인
                await UpdateFirewallRuleStatusAsync();

                // 영구/임시 차단 분리 통계 가져오기
                var separatedStats = _statisticsService != null
                    ? await _statisticsService.GetSeparatedBlockStatisticsAsync()
                    : null;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (separatedStats != null)
                    {
                        // 통계 업데이트
                        TotalBlocks = separatedStats.TotalBlocked;
                        TemporaryBlocks = separatedStats.TemporaryBlocked;
                        PermanentBlocks = separatedStats.PermanentBlocked;
                        Last24HBlocks = separatedStats.TotalBlocked; // 필요시 별도 계산
                        SuccessRate = separatedStats.BlockSuccessRate;

                        // 활성 방화벽 규칙 수
                        FirewallRules = BlockedConnections.Count(c => c.FirewallRuleExists);
                    }

                    // UI 바인딩된 서브 텍스트 업데이트 알림
                    OnPropertyChanged(nameof(TotalBlocksSubText));
                    OnPropertyChanged(nameof(SuccessRateSubText));
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"통계 갱신 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 선택된 연결 수를 업데이트합니다.
        /// </summary>
        public void UpdateSelectedConnectionsCount()
        {
            SelectedConnectionsCount = BlockedConnections.Count(c => c.IsSelected);
        }

        #endregion

        #region Private Methods

        private bool FilterConnections(object item)
        {
            if (item is not IBlockedConnection connection)
                return false;

            return CurrentFilter switch
            {
                "All" => true,
                "Temporary" => !connection.IsPermanentlyBlocked,
                "Permanent" => connection.IsPermanentlyBlocked,
                "Failed" => !connection.FirewallRuleExists,
                _ => true
            };
        }

        private void RefreshFilter()
        {
            FilteredView?.Refresh();

            // 필터 적용 결과 Toast 알림
            var filteredCount = FilteredView?.Cast<object>().Count() ?? 0;
            var totalCount = BlockedConnections.Count;

            _toastService?.ShowInfoAsync($"필터 적용",
                $"{CurrentFilter} 필터: {filteredCount}/{totalCount}개 표시");
        }

        private async Task UpdateFirewallRuleStatusAsync()
        {
            try
            {
                // 방화벽 매니저가 초기화되지 않은 경우 처리
                if (_firewallManager == null)
                {
                    return;
                }

                // LogCheck 관련 방화벽 규칙 목록 가져오기
                var firewallRules = await _firewallManager.GetLogCheckRulesAsync();
                var activeRuleNames = firewallRules.Select(r => r.Name).ToHashSet();

                // 각 연결의 방화벽 규칙 존재 여부 확인
                await Task.Run(() =>
                {
                    foreach (var connection in BlockedConnections.ToList())
                    {
                        if (connection.IsPermanentlyBlocked)
                        {
                            var ruleName = $"LogCheck_Block_{connection.ProcessName}_{connection.RemoteAddress}";

                            // BlockedConnectionWrapper를 통해 내부 객체에 접근하여 상태 업데이트
                            if (connection is BlockedConnectionWrapper wrapper)
                            {
                                wrapper.UpdateFirewallRuleStatus(activeRuleNames.Contains(ruleName));
                            }
                        }
                        else
                        {
                            // 임시 차단의 경우, AutoBlockService에서 관리하므로 별도 처리
                            if (connection is BlockedConnectionWrapper wrapper)
                            {
                                wrapper.UpdateFirewallRuleStatus(true); // 임시로 true 설정
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"방화벽 규칙 상태 업데이트 오류: {ex.Message}");
            }
        }

        private async Task UnblockSelectedConnectionsAsync()
        {
            var selectedConnections = BlockedConnections.Where(c => c.IsSelected).ToList();

            if (!selectedConnections.Any())
            {
                _toastService?.ShowWarningAsync("선택 필요", "차단 해제할 연결을 선택해주세요.");
                return;
            }

            // 영구/임시 차단 분류
            var permanentCount = selectedConnections.Count(c => c.IsPermanentlyBlocked);
            var temporaryCount = selectedConnections.Count - permanentCount;

            // Toast로 확인 메시지 및 상세 정보 제공
            var detailMessage = permanentCount > 0 && temporaryCount > 0
                ? $"영구 {permanentCount}개 + 임시 {temporaryCount}개"
                : permanentCount > 0
                ? $"영구 차단 {permanentCount}개 (방화벽 규칙 제거 포함)"
                : $"임시 차단 {temporaryCount}개";

            _toastService?.ShowInfoAsync("일괄 차단 해제 시작", detailMessage);

            // 차단 해제 실행
            await UnblockConnectionsAsync(selectedConnections);

            // 데이터 새로고침
            await LoadDataAsync();
        }

        private async Task AddSelectedToWhitelistAsync()
        {
            var selectedConnections = BlockedConnections.Where(c => c.IsSelected).ToList();

            if (!selectedConnections.Any())
            {
                _toastService?.ShowWarningAsync("선택 필요", "화이트리스트에 추가할 연결을 선택해주세요.");
                return;
            }

            // 화이트리스트 추가 영향 분석
            var permanentCount = selectedConnections.Count(c => c.IsPermanentlyBlocked);
            var temporaryCount = selectedConnections.Count - permanentCount;

            var impactMessage = permanentCount > 0 && temporaryCount > 0
                ? $"{selectedConnections.Count}개 연결 (영구 {permanentCount}개, 임시 {temporaryCount}개)"
                : permanentCount > 0
                ? $"{permanentCount}개 영구 차단 (방화벽 규칙 제거 포함)"
                : $"{temporaryCount}개 임시 차단";

            _toastService?.ShowInfoAsync("화이트리스트 추가 시작",
                $"{impactMessage} → ✅ 향후 자동 차단 예외 처리");

            await AddToWhitelistAsync(selectedConnections);

            // 데이터 새로고침
            await LoadDataAsync();
        }

        private async Task UnblockConnectionsAsync(IEnumerable<IBlockedConnection> connections)
        {
            int successCount = 0;
            int failCount = 0;

            try
            {
                foreach (var connection in connections)
                {
                    bool success = false;

                    if (connection.IsPermanentlyBlocked)
                    {
                        // 영구 차단 해제 - 방화벽 규칙 제거
                        try
                        {
                            var ruleName = $"LogCheck_Block_{connection.ProcessName}_{connection.RemoteAddress}";
                            success = _firewallManager != null && await _firewallManager.RemoveBlockRuleAsync(ruleName);

                            if (success)
                            {
                                _toastService?.ShowSuccessAsync("영구 차단 해제", $"{connection.ProcessName} → {connection.RemoteAddress}");
                            }
                            else
                            {
                                _toastService?.ShowWarningAsync("방화벽 규칙 제거 실패", connection.ProcessName);
                            }
                        }
                        catch (Exception ex)
                        {
                            _toastService?.ShowErrorAsync("영구 차단 해제 실패", ex.Message);
                            success = false;
                        }
                    }
                    else
                    {
                        // 임시 차단 해제
                        try
                        {
                            success = _autoBlockService != null && await _autoBlockService.UnblockConnectionAsync(connection.RemoteAddress);
                            if (success)
                            {
                                _toastService?.ShowSuccessAsync("임시 차단 해제", connection.RemoteAddress);
                            }
                        }
                        catch (Exception ex)
                        {
                            _toastService?.ShowErrorAsync("임시 차단 해제 실패", ex.Message);
                            success = false;
                        }
                    }

                    if (success)
                    {
                        successCount++;
                        // UI에서 제거
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            BlockedConnections.Remove(connection);
                        });
                    }
                    else
                    {
                        failCount++;
                    }
                }

                await RefreshStatisticsAsync();

                // 결과 요약 Toast 알림
                if (successCount > 0 && failCount == 0)
                {
                    _toastService?.ShowSuccessAsync("차단 해제 완료", $"✅ {successCount}개 연결 해제됨");
                }
                else if (successCount > 0 && failCount > 0)
                {
                    _toastService?.ShowWarningAsync("부분 성공", $"⚠️ {successCount}개 성공, {failCount}개 실패");
                }
                else
                {
                    _toastService?.ShowErrorAsync("차단 해제 실패", $"❌ 모든 해제 실패 ({failCount}개)");
                }
            }
            catch (Exception ex)
            {
                _toastService?.ShowErrorAsync("차단 해제 오류", ex.Message);
            }
        }

        private async Task AddToWhitelistAsync(IEnumerable<IBlockedConnection> connections)
        {
            int successCount = 0;
            int failCount = 0;

            try
            {
                foreach (var connection in connections)
                {
                    try
                    {
                        // 화이트리스트에 추가
                        if (_autoBlockService != null)
                            await _autoBlockService.AddAddressToWhitelistAsync(connection.RemoteAddress, $"사용자 추가 - {DateTime.Now:yyyy-MM-dd HH:mm}");

                        // 차단도 함께 해제
                        if (connection.IsPermanentlyBlocked)
                        {
                            // 영구 차단 해제 - 방화벽 규칙 제거
                            var ruleName = $"LogCheck_Block_{connection.ProcessName}_{connection.RemoteAddress}";
                            if (_firewallManager != null)
                                await _firewallManager.RemoveBlockRuleAsync(ruleName);
                        }
                        else
                        {
                            // 임시 차단 해제
                            if (_autoBlockService != null)
                                await _autoBlockService.UnblockConnectionAsync(connection.RemoteAddress);
                        }

                        successCount++;

                        _toastService?.ShowSuccessAsync("화이트리스트 추가", $"{connection.RemoteAddress} 추가 및 차단 해제");

                        // UI에서 제거
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            BlockedConnections.Remove(connection);
                        });
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        _toastService?.ShowErrorAsync("화이트리스트 추가 실패", $"{connection.RemoteAddress}: {ex.Message}");
                    }
                }

                // 결과 요약 Toast 알림
                if (successCount > 0 && failCount == 0)
                {
                    _toastService?.ShowSuccessAsync("화이트리스트 추가 완료", $"✅ {successCount}개 주소 추가됨");
                }
                else if (successCount > 0 && failCount > 0)
                {
                    _toastService?.ShowWarningAsync("부분 성공", $"⚠️ {successCount}개 성공, {failCount}개 실패");
                }
                else
                {
                    _toastService?.ShowErrorAsync("화이트리스트 추가 실패", $"❌ 모든 추가 실패 ({failCount}개)");
                }
            }
            catch (Exception ex)
            {
                _toastService?.ShowErrorAsync("화이트리스트 처리 오류", ex.Message);
            }
        }

        private void ExportConnectionList()
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV 파일 (*.csv)|*.csv|텍스트 파일 (*.txt)|*.txt",
                    DefaultExt = "csv",
                    FileName = $"차단목록_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    ExportToFile(saveFileDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                _toastService?.ShowErrorAsync("내보내기 실패", ex.Message);
            }
        }

        private void ExportToFile(string fileName)
        {
            try
            {
                var lines = new List<string>
                {
                    "유형,프로세스명,원격주소,포트,프로토콜,차단사유,차단시간,방화벽규칙"
                };

                foreach (var connection in BlockedConnections)
                {
                    var line = $"{(connection.IsPermanentlyBlocked ? "영구" : "임시")}," +
                              $"{connection.ProcessName}," +
                              $"{connection.RemoteAddress}," +
                              $"{connection.RemotePort}," +
                              $"{connection.Protocol}," +
                              $"\"{connection.Reason}\"," +
                              $"{connection.BlockedAt:yyyy-MM-dd HH:mm:ss}," +
                              $"{(connection.FirewallRuleExists ? "활성" : "없음")}";
                    lines.Add(line);
                }

                System.IO.File.WriteAllLines(fileName, lines, System.Text.Encoding.UTF8);

                _toastService?.ShowSuccessAsync("내보내기 완료",
                    $"{BlockedConnections.Count}개 연결 정보가 {System.IO.Path.GetFileName(fileName)}로 저장됨");
            }
            catch (Exception ex)
            {
                _toastService?.ShowErrorAsync("파일 저장 실패", ex.Message);
            }
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }


}