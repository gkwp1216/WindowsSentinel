using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using LogCheck.Models;
using LogCheck.Services;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace LogCheck
{
    [SupportedOSPlatform("windows")]
    public partial class AutoBlock : Page, INotifyPropertyChanged
    {
        #region Properties and Fields

        private readonly AutoBlockService? autoBlockService;
        private readonly AutoBlockStatisticsService? statisticsService;
        private readonly PersistentFirewallManager? firewallManager;
        private readonly ToastNotificationService? toastService;
        private ObservableCollection<IBlockedConnection> blockedConnections = new();
        private ICollectionView? filteredView;
        private string currentFilter = "All";
        private bool isLoading = false;

        public ObservableCollection<IBlockedConnection> BlockedConnections
        {
            get => blockedConnections;
            set
            {
                blockedConnections = value;
                OnPropertyChanged(nameof(BlockedConnections));
            }
        }

        public bool IsLoading
        {
            get => isLoading;
            set
            {
                isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
                UpdateLoadingVisibility();
            }
        }

        #endregion

        #region Constructor

        public AutoBlock()
        {
            try
            {
                InitializeComponent();
                DataContext = this;

                // 서비스 초기화
                autoBlockService = new AutoBlockService();
                statisticsService = new AutoBlockStatisticsService("Data Source=autoblock.db");
                firewallManager = new PersistentFirewallManager();

                // Toast 서비스 초기화 (Windows 플랫폼에서만)
                try
                {
                    toastService = ToastNotificationService.Instance;
                }
                catch
                {
                    toastService = null; // Windows가 아닌 환경에서는 null
                }

                // BlockedConnections는 이미 초기화됨

                // 필터링을 위한 CollectionView 설정
                filteredView = CollectionViewSource.GetDefaultView(BlockedConnections);
                filteredView.Filter = FilterConnections;

                BlockedConnectionsDataGrid.ItemsSource = filteredView;

                // 기본 필터 설정
                SetActiveFilter(AllFilterButton);
            }
            catch (Exception ex)
            {
                // 오류 발생 시 디버그 정보 출력 및 기본 처리
                System.Diagnostics.Debug.WriteLine($"AutoBlock 초기화 오류: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"스택 트레이스: {ex.StackTrace}");

                // 사용자에게 오류 알림
                System.Windows.MessageBox.Show($"자동 차단 시스템 로딩 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "초기화 오류", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);

                InitializeComponent(); // 최소한 UI만 초기화
                DataContext = this;
            }
        }

        #endregion

        #region Event Handlers

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();

            // 5초마다 데이터 갱신
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            timer.Tick += async (s, args) => await RefreshStatisticsAsync();
            timer.Start();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // 정리 작업
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // AutoBlock 설정 창 열기
            MessageBox.Show("AutoBlock 설정 기능은 개발 중입니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                SetActiveFilter(button);
                currentFilter = button.Tag?.ToString() ?? "All";
                filteredView?.Refresh();
                UpdateEmptyVisibility();
            }
        }

        private async void UnblockSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedConnections = BlockedConnections.Where(c => c.IsSelected).ToList();

            if (!selectedConnections.Any())
            {
                MessageBox.Show("차단 해제할 연결을 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"선택된 {selectedConnections.Count}개의 연결을 차단 해제하시겠습니까?",
                "차단 해제 확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                await UnblockConnectionsAsync(selectedConnections);
            }
        }

        private async void AddToWhitelist_Click(object sender, RoutedEventArgs e)
        {
            var selectedConnections = BlockedConnections.Where(c => c.IsSelected).ToList();

            if (!selectedConnections.Any())
            {
                MessageBox.Show("화이트리스트에 추가할 연결을 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"선택된 {selectedConnections.Count}개의 연결을 화이트리스트에 추가하시겠습니까?\n" +
                "화이트리스트에 추가된 연결은 앞으로 차단되지 않습니다.",
                "화이트리스트 추가 확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                await AddToWhitelistAsync(selectedConnections);
            }
        }

        private void ExportList_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
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

        private async void UnblockSingle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is IBlockedConnection connection)
            {
                var result = MessageBox.Show(
                    $"'{connection.ProcessName}'의 차단을 해제하시겠습니까?",
                    "차단 해제 확인",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await UnblockConnectionsAsync(new[] { connection });
                }
            }
        }

        private void ViewDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is IBlockedConnection connection)
            {
                ShowConnectionDetails(connection);
            }
        }

        private void BlockedConnectionsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedCount = BlockedConnections.Count(c => c.IsSelected);
            UnblockSelectedButton.IsEnabled = selectedCount > 0;
            AddToWhitelistButton.IsEnabled = selectedCount > 0;
        }

        #endregion

        #region Data Loading Methods

        private async Task LoadDataAsync()
        {
            try
            {
                IsLoading = true;
                LoadingPanel.Visibility = Visibility.Visible;
                BlockedConnectionsDataGrid.Visibility = Visibility.Collapsed;
                EmptyPanel.Visibility = Visibility.Collapsed;

                // 서비스가 초기화되지 않은 경우 처리
                if (autoBlockService == null || statisticsService == null)
                {
                    toastService?.ShowErrorAsync("오류", "자동 차단 서비스가 초기화되지 않았습니다.");
                    return;
                }

                // 데이터베이스 초기화 먼저 수행
                try
                {
                    await statisticsService.InitializeDatabaseAsync();
                }
                catch (Exception dbEx)
                {
                    System.Diagnostics.Debug.WriteLine($"데이터베이스 초기화 오류: {dbEx.Message}");
                }

                // 임시 차단 연결 로드
                var temporaryConnections = await autoBlockService.GetBlockedConnectionsAsync();

                // 영구 차단 연결 로드
                List<PermanentBlockedConnection> permanentConnections = new();
                try
                {
                    permanentConnections = await statisticsService.GetPermanentlyBlockedConnectionsAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"영구 차단 연결 로드 오류: {ex.Message}");
                    // 영구 차단 연결 로드에 실패해도 임시 차단은 표시
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
                Application.Current.Dispatcher.Invoke(() =>
                {
                    BlockedConnections.Clear();
                    foreach (var connection in allConnections)
                    {
                        BlockedConnections.Add(connection);
                    }
                });

                await RefreshStatisticsAsync();
                UpdateEmptyVisibility();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"데이터 로드 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                LoadingPanel.Visibility = Visibility.Collapsed;
                BlockedConnectionsDataGrid.Visibility = Visibility.Visible;
            }
        }

        private async Task RefreshStatisticsAsync()
        {
            try
            {
                // 방화벽 규칙 상태 실시간 확인
                await UpdateFirewallRuleStatusAsync();

                // 영구/임시 차단 분리 통계 가져오기
                var separatedStats = await statisticsService.GetSeparatedBlockStatisticsAsync();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 전체 차단 수 (실제 통계 기반)
                    TotalBlocksText.Text = separatedStats.TotalBlocked.ToString();
                    TotalBlocksSubText.Text = $"임시 {separatedStats.TemporaryBlocked} | 영구 {separatedStats.PermanentBlocked}";

                    // 24시간 차단 (실제 통계 기반)
                    Last24HBlocksText.Text = separatedStats.TotalBlocked.ToString();

                    // 성공률 계산 (실제 통계 기반)
                    SuccessRateText.Text = $"{separatedStats.BlockSuccessRate:F1}%";
                    SuccessRateSubText.Text = $"성공 {separatedStats.TotalBlocked - separatedStats.RecentBlocked} / 실패 {separatedStats.RecentBlocked}";

                    // 활성 방화벽 규칙 수
                    var firewallRuleCount = BlockedConnections.Count(c => c.FirewallRuleExists);
                    FirewallRulesText.Text = firewallRuleCount.ToString();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"통계 갱신 오류: {ex.Message}");
            }
        }

        private async Task UpdateFirewallRuleStatusAsync()
        {
            try
            {
                // 방화벽 매니저가 초기화되지 않은 경우 처리
                if (firewallManager == null)
                {
                    return;
                }

                // LogCheck 관련 방화벽 규칙 목록 가져오기
                var firewallRules = await firewallManager.GetLogCheckRulesAsync();
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

        #endregion

        #region Filtering Methods

        private bool FilterConnections(object item)
        {
            if (item is not IBlockedConnection connection)
                return false;

            return currentFilter switch
            {
                "All" => true,
                "Temporary" => !connection.IsPermanentlyBlocked,
                "Permanent" => connection.IsPermanentlyBlocked,
                "Failed" => !connection.FirewallRuleExists,
                _ => true
            };
        }

        private void SetActiveFilter(Button activeButton)
        {
            // 모든 필터 버튼을 기본 상태로
            foreach (Button button in new[] { AllFilterButton, TemporaryFilterButton, PermanentFilterButton, FailedFilterButton })
            {
                button.Background = Brushes.Transparent;
                button.Foreground = FindResource("AccentBrush") as Brush;
            }

            // 활성 버튼 스타일 설정
            activeButton.Background = FindResource("AccentBrush") as Brush;
            activeButton.Foreground = Brushes.White;
        }

        #endregion

        #region Action Methods

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
                            success = await firewallManager.RemoveBlockRuleAsync(ruleName);

                            if (success)
                            {
                                // 데이터베이스에서도 영구 차단 기록 제거 (선택사항)
                                // await statisticsService.RemovePermanentBlockAsync(connection);
                                toastService?.ShowSuccessAsync("영구 차단 해제", $"{connection.ProcessName} → {connection.RemoteAddress}");
                            }
                            else
                            {
                                toastService?.ShowWarningAsync("방화벽 규칙 제거 실패", connection.ProcessName);
                            }
                        }
                        catch (Exception ex)
                        {
                            toastService?.ShowErrorAsync("영구 차단 해제 실패", ex.Message);
                            success = false;
                        }
                    }
                    else
                    {
                        // 임시 차단 해제
                        try
                        {
                            success = await autoBlockService.UnblockConnectionAsync(connection.RemoteAddress);
                            if (success)
                            {
                                toastService?.ShowSuccessAsync("임시 차단 해제", connection.RemoteAddress);
                            }
                        }
                        catch (Exception ex)
                        {
                            toastService?.ShowErrorAsync("임시 차단 해제 실패", ex.Message);
                            success = false;
                        }
                    }

                    if (success)
                    {
                        successCount++;
                        // UI에서 제거
                        Application.Current.Dispatcher.Invoke(() =>
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
                    toastService?.ShowSuccessAsync("차단 해제 완료", $"✅ {successCount}개 연결 해제됨");
                }
                else if (successCount > 0 && failCount > 0)
                {
                    toastService?.ShowWarningAsync("부분 성공", $"⚠️ {successCount}개 성공, {failCount}개 실패");
                }
                else
                {
                    toastService?.ShowErrorAsync("차단 해제 실패", $"❌ 모든 해제 실패 ({failCount}개)");
                }
            }
            catch (Exception ex)
            {
                toastService?.ShowErrorAsync("차단 해제 오류", ex.Message);
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
                        await autoBlockService.AddAddressToWhitelistAsync(connection.RemoteAddress, $"사용자 추가 - {DateTime.Now:yyyy-MM-dd HH:mm}");

                        // 차단도 함께 해제
                        if (connection.IsPermanentlyBlocked)
                        {
                            // 영구 차단 해제 - 방화벽 규칙 제거
                            var ruleName = $"LogCheck_Block_{connection.ProcessName}_{connection.RemoteAddress}";
                            await firewallManager.RemoveBlockRuleAsync(ruleName);
                        }
                        else
                        {
                            // 임시 차단 해제
                            await autoBlockService.UnblockConnectionAsync(connection.RemoteAddress);
                        }

                        successCount++;

                        toastService?.ShowSuccessAsync("화이트리스트 추가", $"{connection.RemoteAddress} 추가 및 차단 해제");

                        // UI에서 제거
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            BlockedConnections.Remove(connection);
                        });
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        toastService?.ShowErrorAsync("화이트리스트 추가 실패", $"{connection.RemoteAddress}: {ex.Message}");
                    }
                }

                await RefreshStatisticsAsync();

                // 결과 요약 Toast 알림
                if (successCount > 0 && failCount == 0)
                {
                    toastService?.ShowSuccessAsync("화이트리스트 추가 완료", $"✅ {successCount}개 주소가 화이트리스트에 추가되었습니다");
                }
                else if (successCount > 0 && failCount > 0)
                {
                    toastService?.ShowWarningAsync("부분적 성공", $"⚠️ {successCount}개 성공, {failCount}개 실패");
                }
                else
                {
                    toastService?.ShowErrorAsync("화이트리스트 추가 실패", $"❌ 모든 추가 실패 ({failCount}개)");
                }
            }
            catch (Exception ex)
            {
                toastService?.ShowErrorAsync("화이트리스트 추가 오류", ex.Message);
            }
        }

        private void ExportToFile(string filePath)
        {
            try
            {
                var lines = new List<string>
                {
                    "유형,프로세스명,원격주소,포트,프로토콜,차단사유,차단시간,방화벽상태"
                };

                foreach (var connection in BlockedConnections)
                {
                    var type = connection.IsPermanentlyBlocked ? "영구" : "임시";
                    var firewallStatus = connection.FirewallRuleExists ? "활성" : "없음";
                    var line = $"{type},{connection.ProcessName},{connection.RemoteAddress},{connection.RemotePort},{connection.Protocol},{connection.Reason},{connection.BlockedAt:yyyy-MM-dd HH:mm:ss},{firewallStatus}";
                    lines.Add(line);
                }

                File.WriteAllLines(filePath, lines, System.Text.Encoding.UTF8);
                MessageBox.Show($"차단 목록을 성공적으로 내보냈습니다.\n파일: {filePath}", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"파일 내보내기 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowConnectionDetails(IBlockedConnection connection)
        {
            var details = $"=== 차단 연결 상세 정보 ===\n\n" +
                         $"유형: {(connection.IsPermanentlyBlocked ? "영구 차단" : "임시 차단")}\n" +
                         $"프로세스명: {connection.ProcessName}\n" +
                         $"원격 주소: {connection.RemoteAddress}\n" +
                         $"원격 포트: {connection.RemotePort}\n" +
                         $"프로토콜: {connection.Protocol}\n" +
                         $"차단 사유: {connection.Reason}\n" +
                         $"차단 시간: {connection.BlockedAt:yyyy-MM-dd HH:mm:ss}\n" +
                         $"방화벽 규칙: {(connection.FirewallRuleExists ? "활성" : "없음")}\n";

            MessageBox.Show(details, "연결 상세 정보", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region UI Helper Methods

        private void UpdateLoadingVisibility()
        {
            if (IsLoading)
            {
                LoadingPanel.Visibility = Visibility.Visible;
                BlockedConnectionsDataGrid.Visibility = Visibility.Collapsed;
                EmptyPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                UpdateEmptyVisibility();
            }
        }

        private void UpdateEmptyVisibility()
        {
            var hasVisibleItems = filteredView?.Cast<object>().Any() == true;

            if (hasVisibleItems)
            {
                BlockedConnectionsDataGrid.Visibility = Visibility.Visible;
                EmptyPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                BlockedConnectionsDataGrid.Visibility = Visibility.Collapsed;
                EmptyPanel.Visibility = Visibility.Visible;
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

    #region Helper Classes

    // 임시 차단과 영구 차단을 통합하기 위한 인터페이스
    public interface IBlockedConnection : INotifyPropertyChanged
    {
        bool IsSelected { get; set; }
        bool IsPermanentlyBlocked { get; }
        string ProcessName { get; }
        string RemoteAddress { get; }
        int RemotePort { get; }
        string Protocol { get; }
        string Reason { get; }
        DateTime BlockedAt { get; }
        bool FirewallRuleExists { get; }
    }

    // AutoBlockedConnection과 PermanentBlockedConnection을 래핑하는 클래스
    public class BlockedConnectionWrapper : IBlockedConnection
    {
        private bool isSelected;
        private readonly object wrappedConnection;
        private readonly bool isPermanent;

        public BlockedConnectionWrapper(AutoBlockedConnection autoBlocked, bool isPermanent = false)
        {
            wrappedConnection = autoBlocked;
            this.isPermanent = isPermanent;
        }

        public BlockedConnectionWrapper(PermanentBlockedConnection permanentBlocked, bool isPermanent = true)
        {
            wrappedConnection = permanentBlocked;
            this.isPermanent = isPermanent;
        }

        public bool IsSelected
        {
            get => isSelected;
            set
            {
                isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public bool IsPermanentlyBlocked => isPermanent;

        public string ProcessName =>
            wrappedConnection is AutoBlockedConnection ab ? ab.ProcessName :
            wrappedConnection is PermanentBlockedConnection pb ? pb.ProcessName : "";

        public string RemoteAddress =>
            wrappedConnection is AutoBlockedConnection ab ? ab.RemoteAddress :
            wrappedConnection is PermanentBlockedConnection pb ? pb.RemoteAddress : "";

        public int RemotePort =>
            wrappedConnection is AutoBlockedConnection ab ? ab.RemotePort :
            wrappedConnection is PermanentBlockedConnection pb ? pb.RemotePort : 0;

        public string Protocol =>
            wrappedConnection is AutoBlockedConnection ab ? ab.Protocol :
            wrappedConnection is PermanentBlockedConnection pb ? pb.Protocol : "";

        public string Reason =>
            wrappedConnection is AutoBlockedConnection ab ? ab.Reason :
            wrappedConnection is PermanentBlockedConnection pb ? pb.Reason : "";

        public DateTime BlockedAt =>
            wrappedConnection is AutoBlockedConnection ab ? ab.BlockedAt :
            wrappedConnection is PermanentBlockedConnection pb ? pb.LastBlockedAt : DateTime.MinValue;

        private bool? _firewallRuleExistsOverride;

        public bool FirewallRuleExists =>
            _firewallRuleExistsOverride ??
            (wrappedConnection is AutoBlockedConnection ab ? ab.FirewallRuleExists :
            wrappedConnection is PermanentBlockedConnection pb ? pb.FirewallRuleExists : false);

        /// <summary>
        /// 방화벽 규칙 존재 상태를 업데이트합니다
        /// </summary>
        public void UpdateFirewallRuleStatus(bool exists)
        {
            if (_firewallRuleExistsOverride != exists)
            {
                _firewallRuleExistsOverride = exists;
                OnPropertyChanged(nameof(FirewallRuleExists));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    #endregion
}