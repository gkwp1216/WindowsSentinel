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
using System.Windows.Shapes;
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
                toastService?.ShowWarningAsync("선택 필요", "차단 해제할 연결을 선택해주세요.");
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

            toastService?.ShowInfoAsync("일괄 차단 해제 시작", detailMessage);

            // 즉시 실행 (Toast 기반 피드백으로 더 빠른 UX)
            await UnblockConnectionsAsync(selectedConnections);

            // 데이터 새로고침
            await LoadDataAsync();
        }

        private async void AddToWhitelist_Click(object sender, RoutedEventArgs e)
        {
            var selectedConnections = BlockedConnections.Where(c => c.IsSelected).ToList();

            if (!selectedConnections.Any())
            {
                toastService?.ShowWarningAsync("선택 필요", "화이트리스트에 추가할 연결을 선택해주세요.");
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

            toastService?.ShowInfoAsync("화이트리스트 추가 시작",
                $"{impactMessage} → ✅ 향후 자동 차단 예외 처리");

            await AddToWhitelistAsync(selectedConnections);

            // 데이터 새로고침
            await LoadDataAsync();
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
                // Toast 기반 확인 메시지 (더 나은 UX)
                var connectionType = connection.IsPermanentlyBlocked ? "영구 차단" : "임시 차단";
                var confirmMessage = $"{connectionType}: {connection.ProcessName} → {connection.RemoteAddress}";

                // 즉시 실행하되, 사용자에게 피드백 제공
                toastService?.ShowInfoAsync("차단 해제 시작", confirmMessage);

                await UnblockConnectionsAsync(new[] { connection });

                // 데이터 새로고침 (UI에서 즉시 제거)
                await LoadDataAsync();
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

                // 영구 차단 연결 로드 (데이터베이스 + 방화벽 규칙 통합)
                List<PermanentBlockedConnection> permanentConnections = new();
                try
                {
                    // 1. 데이터베이스에서 영구 차단 연결 로드
                    permanentConnections = await statisticsService.GetPermanentlyBlockedConnectionsAsync();

                    // 2. 방화벽 규칙에서 실제 활성 규칙도 확인하여 보완
                    if (firewallManager != null)
                    {
                        var firewallRules = await firewallManager.GetLogCheckRulesAsync();

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

                                    // 복구된 연결 정보를 로그에 기록 (나중에 저장 기능 추가 예정)
                                    Debug.WriteLine($"방화벽 규칙에서 복구된 연결: {processName} → {address}");
                                    toastService?.ShowInfoAsync("연결 복구",
                                        $"방화벽에서 {processName} → {address} 복구");
                                }
                            }
                        }

                        toastService?.ShowSuccessAsync("영구 차단 동기화",
                            $"DB: {permanentConnections.Count}개, 방화벽: {firewallRules.Count}개 규칙");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"영구 차단 연결 로드 오류: {ex.Message}");
                    toastService?.ShowErrorAsync("영구 차단 로드 실패", ex.Message);
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

                    // 차트 업데이트
                    UpdateCharts();
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
                var separatedStats = statisticsService != null
                    ? await statisticsService.GetSeparatedBlockStatisticsAsync()
                    : null;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (separatedStats != null)
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
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"통계 갱신 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 차트 데이터를 업데이트합니다. (개선된 통계 및 시각화)
        /// </summary>
        private void UpdateCharts()
        {
            try
            {
                // 필터링된 데이터를 기준으로 차단 유형별 분포 계산
                var visibleConnections = filteredView?.Cast<IBlockedConnection>().ToList() ?? BlockedConnections.ToList();
                var temporaryCount = visibleConnections.Count(c => !c.IsPermanentlyBlocked);
                var permanentCount = visibleConnections.Count(c => c.IsPermanentlyBlocked);
                var totalCount = temporaryCount + permanentCount;

                // 전체 데이터 통계 (필터링과 무관하게)
                var allTemporaryCount = BlockedConnections.Count(c => !c.IsPermanentlyBlocked);
                var allPermanentCount = BlockedConnections.Count(c => c.IsPermanentlyBlocked);
                var allTotalCount = allTemporaryCount + allPermanentCount;

                // XAML에서 정의된 컨트롤들을 FindName으로 찾기
                var temporaryPercentText = FindName("TemporaryPercentText") as TextBlock;
                var temporaryCountText = FindName("TemporaryCountText") as TextBlock;
                var permanentPercentText = FindName("PermanentPercentText") as TextBlock;
                var permanentCountText = FindName("PermanentCountText") as TextBlock;

                if (allTotalCount > 0)
                {
                    // 전체 데이터 기준 비율 계산 (더 의미있는 통계)
                    var temporaryPercent = (double)allTemporaryCount / allTotalCount * 100;
                    var permanentPercent = (double)allPermanentCount / allTotalCount * 100;

                    // 임시 차단 비율 업데이트
                    if (temporaryPercentText != null) temporaryPercentText.Text = $"{temporaryPercent:F1}%";
                    if (temporaryCountText != null) temporaryCountText.Text = $"{allTemporaryCount}개 (표시: {temporaryCount})";

                    // 영구 차단 비율 업데이트
                    if (permanentPercentText != null) permanentPercentText.Text = $"{permanentPercent:F1}%";
                    if (permanentCountText != null) permanentCountText.Text = $"{allPermanentCount}개 (표시: {permanentCount})";

                    // 차트에 필요한 추가 분석
                    var activeFirewallRules = BlockedConnections.Count(c => c.FirewallRuleExists);
                    var recentBlocks = BlockedConnections.Count(c => c.BlockedAt >= DateTime.Now.AddHours(-24));

                    Debug.WriteLine($"차트 업데이트: 임시 {allTemporaryCount}, 영구 {allPermanentCount}, " +
                                   $"활성 방화벽 {activeFirewallRules}, 24시간 내 {recentBlocks}");
                }
                else
                {
                    // 데이터가 없는 경우
                    if (temporaryPercentText != null) temporaryPercentText.Text = "0%";
                    if (temporaryCountText != null) temporaryCountText.Text = "0개";
                    if (permanentPercentText != null) permanentPercentText.Text = "0%";
                    if (permanentCountText != null) permanentCountText.Text = "0개";
                }

                // 시간별 차단 추이 업데이트 (최근 6시간)
                UpdateHourlyChart();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"차트 업데이트 오류: {ex.Message}");
                toastService?.ShowErrorAsync("차트 업데이트 실패", ex.Message);
            }
        }

        /// <summary>
        /// 시간별 차단 추이 차트를 업데이트합니다. (개선된 시각화 및 색상)
        /// </summary>
        private void UpdateHourlyChart()
        {
            try
            {
                var now = DateTime.Now;
                var hourlyData = new int[6];
                var hourLabels = new string[6];

                // 최근 6시간 동안의 차단 수를 계산
                for (int i = 0; i < 6; i++)
                {
                    var hourStart = now.AddHours(-(5 - i)).Date.AddHours(now.AddHours(-(5 - i)).Hour);
                    var hourEnd = hourStart.AddHours(1);

                    hourlyData[i] = BlockedConnections.Count(c =>
                        c.BlockedAt >= hourStart && c.BlockedAt < hourEnd);

                    // 시간 라벨 생성 (차트 툴팁용)
                    hourLabels[i] = hourStart.ToString("HH:mm");
                }

                // 최대값을 기준으로 높이 정규화 (최대 60px로 확장)
                var maxCount = hourlyData.Max();
                if (maxCount == 0) maxCount = 1; // 0으로 나누기 방지

                // 활동 수준에 따른 색상 결정
                var accentBrush = FindResource("AccentBrush") as Brush ?? Brushes.Blue;
                var riskMediumBrush = FindResource("RiskMediumColor") as Brush ?? Brushes.Orange;
                var riskHighBrush = FindResource("RiskHighColor") as Brush ?? Brushes.Red;

                // 각 막대의 높이 및 색상 업데이트
                var bars = new[]
                {
                    FindName("Hour1Bar") as System.Windows.Shapes.Rectangle,
                    FindName("Hour2Bar") as System.Windows.Shapes.Rectangle,
                    FindName("Hour3Bar") as System.Windows.Shapes.Rectangle,
                    FindName("Hour4Bar") as System.Windows.Shapes.Rectangle,
                    FindName("Hour5Bar") as System.Windows.Shapes.Rectangle,
                    FindName("Hour6Bar") as System.Windows.Shapes.Rectangle
                };

                for (int i = 0; i < bars.Length && i < hourlyData.Length; i++)
                {
                    var bar = bars[i];
                    if (bar != null)
                    {
                        // 높이 설정 (최소 3px, 최대 60px)
                        bar.Height = Math.Max(3.0, (double)hourlyData[i] / maxCount * 60.0);

                        // 활동 수준에 따른 색상 설정
                        if (hourlyData[i] == 0)
                        {
                            bar.Fill = Brushes.LightGray;
                        }
                        else if (hourlyData[i] > maxCount * 0.7) // 높은 활동
                        {
                            bar.Fill = riskHighBrush;
                        }
                        else if (hourlyData[i] > maxCount * 0.3) // 중간 활동
                        {
                            bar.Fill = riskMediumBrush;
                        }
                        else // 낮은 활동
                        {
                            bar.Fill = accentBrush;
                        }

                        // 툴팁 설정 (시간 및 차단 수 표시)
                        bar.ToolTip = $"{hourLabels[i]}: {hourlyData[i]}개 차단";
                    }
                }

                // 총 차단 수 및 평균 계산
                var totalHourlyBlocks = hourlyData.Sum();
                var averagePerHour = totalHourlyBlocks / 6.0;

                Debug.WriteLine($"시간별 차트: 총 {totalHourlyBlocks}개, 평균 {averagePerHour:F1}개/시간, 최대 {maxCount}개");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"시간별 차트 업데이트 오류: {ex.Message}");
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
                button.FontWeight = FontWeights.SemiBold;
            }

            // 활성 버튼 스타일 설정 (더 명확한 시각적 피드백)
            activeButton.Background = FindResource("AccentBrush") as Brush;
            activeButton.Foreground = Brushes.White;
            activeButton.FontWeight = FontWeights.Bold;

            // 필터된 결과 개수 표시
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var filteredCount = filteredView?.Cast<object>().Count() ?? 0;
                var totalCount = BlockedConnections.Count;

                // Toast로 필터 적용 결과 알림
                toastService?.ShowInfoAsync($"필터 적용",
                    $"{activeButton.Content} 필터: {filteredCount}/{totalCount}개 표시");
            }));
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
                            success = firewallManager != null && await firewallManager.RemoveBlockRuleAsync(ruleName);

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
                            success = autoBlockService != null && await autoBlockService.UnblockConnectionAsync(connection.RemoteAddress);
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
                        if (autoBlockService != null)
                            await autoBlockService.AddAddressToWhitelistAsync(connection.RemoteAddress, $"사용자 추가 - {DateTime.Now:yyyy-MM-dd HH:mm}");

                        // 차단도 함께 해제
                        if (connection.IsPermanentlyBlocked)
                        {
                            // 영구 차단 해제 - 방화벽 규칙 제거
                            var ruleName = $"LogCheck_Block_{connection.ProcessName}_{connection.RemoteAddress}";
                            if (firewallManager != null)
                                await firewallManager.RemoveBlockRuleAsync(ruleName);
                        }
                        else
                        {
                            // 임시 차단 해제
                            if (autoBlockService != null)
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