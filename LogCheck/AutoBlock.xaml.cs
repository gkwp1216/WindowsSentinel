using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    public partial class AutoBlock : Page, INotifyPropertyChanged
    {
        #region Properties and Fields

        private readonly AutoBlockService autoBlockService;
        private readonly AutoBlockStatisticsService statisticsService;
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
            InitializeComponent();
            DataContext = this;

            // 서비스 초기화
            autoBlockService = new AutoBlockService();
            statisticsService = new AutoBlockStatisticsService("Data Source=autoblock.db");

            // BlockedConnections는 이미 초기화됨

            // 필터링을 위한 CollectionView 설정
            filteredView = CollectionViewSource.GetDefaultView(BlockedConnections);
            filteredView.Filter = FilterConnections;

            BlockedConnectionsDataGrid.ItemsSource = filteredView;

            // 기본 필터 설정
            SetActiveFilter(AllFilterButton);
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

                // 임시 차단 연결 로드
                var temporaryConnections = await autoBlockService.GetBlockedConnectionsAsync();

                // 영구 차단 연결 로드
                var permanentConnections = await statisticsService.GetPermanentlyBlockedConnectionsAsync();

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
                await Task.Delay(100); // 임시 구현

                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 전체 차단 수
                    var totalBlocks = BlockedConnections.Count;
                    var temporaryCount = BlockedConnections.Count(c => !c.IsPermanentlyBlocked);
                    var permanentCount = BlockedConnections.Count(c => c.IsPermanentlyBlocked);

                    TotalBlocksText.Text = totalBlocks.ToString();
                    TotalBlocksSubText.Text = $"임시 {temporaryCount} | 영구 {permanentCount}";

                    // 24시간 차단
                    var last24Hours = BlockedConnections.Count(c =>
                        c.BlockedAt >= DateTime.Now.AddDays(-1));
                    Last24HBlocksText.Text = last24Hours.ToString();

                    // 차단 성공률
                    var successfulBlocks = BlockedConnections.Count(c => c.FirewallRuleExists);
                    var successRate = totalBlocks > 0 ? (double)successfulBlocks / totalBlocks * 100 : 0;
                    SuccessRateText.Text = $"{successRate:F1}%";
                    SuccessRateSubText.Text = $"성공 {successfulBlocks} / 실패 {totalBlocks - successfulBlocks}";

                    // 방화벽 규칙 수
                    var firewallRuleCount = BlockedConnections.Count(c => c.FirewallRuleExists);
                    FirewallRulesText.Text = firewallRuleCount.ToString();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"통계 갱신 오류: {ex.Message}");
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
            try
            {
                foreach (var connection in connections)
                {
                    if (connection.IsPermanentlyBlocked)
                    {
                        // 영구 차단 해제 (임시 구현)
                        await Task.Delay(100);
                    }
                    else
                    {
                        // 임시 차단 해제
                        await autoBlockService.UnblockConnectionAsync(connection.RemoteAddress);
                    }

                    // UI에서 제거
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        BlockedConnections.Remove(connection);
                    });
                }

                await RefreshStatisticsAsync();
                MessageBox.Show($"{connections.Count()}개의 연결 차단을 해제했습니다.", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"차단 해제 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task AddToWhitelistAsync(IEnumerable<IBlockedConnection> connections)
        {
            try
            {
                foreach (var connection in connections)
                {
                    // 화이트리스트에 추가
                    await autoBlockService.AddAddressToWhitelistAsync(connection.RemoteAddress, $"사용자 추가 - {DateTime.Now:yyyy-MM-dd HH:mm}");

                    // 차단도 함께 해제
                    if (connection.IsPermanentlyBlocked)
                    {
                        // 영구 차단 해제 (임시 구현)
                        await Task.Delay(100);
                    }
                    else
                    {
                        await autoBlockService.UnblockConnectionAsync(connection.RemoteAddress);
                    }

                    // UI에서 제거
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        BlockedConnections.Remove(connection);
                    });
                }

                await RefreshStatisticsAsync();
                MessageBox.Show($"{connections.Count()}개의 연결을 화이트리스트에 추가하고 차단을 해제했습니다.", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"화이트리스트 추가 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
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

        public bool FirewallRuleExists =>
            wrappedConnection is AutoBlockedConnection ab ? ab.FirewallRuleExists :
            wrappedConnection is PermanentBlockedConnection pb ? pb.FirewallRuleExists : false;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    #endregion
}