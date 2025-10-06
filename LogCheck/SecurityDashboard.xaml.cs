using System;
using System.Windows.Controls;
using LogCheck.ViewModels;

namespace LogCheck
{
    /// <summary>
    /// SecurityDashboard.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class SecurityDashboard : Page
    {
        public SecurityDashboard()
        {
            InitializeComponent();
            DataContext = new SecurityDashboardViewModel();
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