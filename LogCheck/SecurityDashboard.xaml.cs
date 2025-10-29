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
                DataContext = SecurityDashboardViewModel.Instance;
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

        private void AddTestEventButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                if (DataContext is SecurityDashboardViewModel viewModel)
                {
                    viewModel.AddTestDDoSEvent();
                    System.Diagnostics.Debug.WriteLine("✅ 테스트 이벤트 추가 버튼 클릭됨");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("❌ ViewModel이 SecurityDashboardViewModel이 아님");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 테스트 이벤트 추가 중 오류: {ex.Message}");
            }
        }

        private void SimulateDDoSButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                if (DataContext is SecurityDashboardViewModel viewModel)
                {
                    // 다양한 DDoS 공격 유형 시뮬레이션
                    viewModel.SimulateDDoSAttack("TCP SYN Flood", "192.168.1.100", 500);
                    viewModel.SimulateDDoSAttack("UDP Flood", "192.168.1.101", 800);
                    viewModel.SimulateDDoSAttack("ICMP Flood", "192.168.1.102", 300);
                    
                    System.Diagnostics.Debug.WriteLine("🚨 DDoS 시뮬레이션 실행됨 - 3가지 공격 유형");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ DDoS 시뮬레이션 중 오류: {ex.Message}");
            }
        }

        private void GenerateTrafficButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                if (DataContext is SecurityDashboardViewModel viewModel)
                {
                    // 대량 트래픽 생성 시뮬레이션
                    Task.Run(async () =>
                    {
                        for (int i = 0; i < 50; i++)
                        {
                            viewModel.AddTestNetworkTraffic($"192.168.1.{10 + (i % 40)}", 1024 + (i * 100));
                            await Task.Delay(100); // 100ms 간격
                        }
                    });
                    
                    System.Diagnostics.Debug.WriteLine("📈 대량 트래픽 생성 시작 - 50개 패킷");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 트래픽 생성 중 오류: {ex.Message}");
            }
        }

        private void MockAttackButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                // MainWindows에서 DDoS 시스템 가져오기 (static 속성 사용)
                var ddosSystem = LogCheck.MainWindows.SharedDDoSDefenseSystem;

                if (ddosSystem != null)
                {
                    var mockGenerator = new LogCheck.Services.MockTrafficGenerator(ddosSystem);
                    
                    // 백그라운드에서 Mock 공격 실행
                    Task.Run(async () =>
                    {
                        System.Diagnostics.Debug.WriteLine("🔥 Mock 공격 테스트 시작");
                        await mockGenerator.QuickAttackTestAsync();
                        
                        // UI 업데이트를 위해 잠시 대기 후 추가 시뮬레이션
                        await Task.Delay(1000);
                        
                        if (DataContext is SecurityDashboardViewModel viewModel)
                        {
                            // 추가적인 UI 이벤트 생성
                            viewModel.SimulateDDoSAttack("Mock TCP Attack", "192.168.1.99", 150);
                        }
                    });
                    
                    System.Diagnostics.Debug.WriteLine("🚀 Mock 공격 테스트 실행됨");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("❌ DDoS 방어 시스템을 찾을 수 없음");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Mock 공격 테스트 중 오류: {ex.Message}");
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