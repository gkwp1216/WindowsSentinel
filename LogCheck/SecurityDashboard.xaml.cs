using System;
using System.Runtime.Versioning;
using System.Windows.Controls;
using LogCheck.ViewModels;

namespace LogCheck
{
    /// <summary>
    /// SecurityDashboard.xaml에 대한 상호 작용 논리
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class SecurityDashboard : Page
    {
        public SecurityDashboard()
        {
            try
            {
                InitializeComponent();
                DataContext = new SecurityDashboardViewModel();
            }
            catch (Exception ex)
            {
                // 오류 발생 시 디버그 정보 출력 및 기본 처리
                System.Diagnostics.Debug.WriteLine($"SecurityDashboard 초기화 오류: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"스택 트레이스: {ex.StackTrace}");

                // 사용자에게 오류 알림
                System.Windows.MessageBox.Show($"보안 대시보드 로딩 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "초기화 오류", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);

                InitializeComponent(); // 최소한 UI만 초기화
            }
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // ViewModel 초기화 및 실시간 데이터 로딩 시작
            if (DataContext is SecurityDashboardViewModel viewModel)
            {
                viewModel.StartRealTimeUpdates();
            }
        }

        private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // 리소스 정리 및 실시간 업데이트 중지
            if (DataContext is SecurityDashboardViewModel viewModel)
            {
                viewModel.StopRealTimeUpdates();
            }
        }

        private void ChartPeriodSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is SecurityDashboardViewModel viewModel && ChartPeriodSelector.SelectedItem is ComboBoxItem selectedItem)
            {
                var period = selectedItem.Content.ToString() switch
                {
                    "시간별" => ChartPeriod.Hourly,
                    "일별" => ChartPeriod.Daily,
                    "주별" => ChartPeriod.Weekly,
                    _ => ChartPeriod.Hourly
                };
                viewModel.UpdateChartPeriod(period);
            }
        }
    }

    public enum ChartPeriod
    {
        Hourly,
        Daily,
        Weekly
    }
}