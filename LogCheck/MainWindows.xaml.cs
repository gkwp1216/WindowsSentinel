using System;
using System.ComponentModel;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
// using System.Windows.Forms; // Tray handled by App

namespace LogCheck
{
    [SupportedOSPlatform("windows")]
    public partial class MainWindows : Window
    {
        private bool isExplicitClose = false;

        [SupportedOSPlatform("windows")]
        public MainWindows()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                NavigateToPage(new NetWorks_New());
            };

        }

        [SupportedOSPlatform("windows")]
        public void NavigateToPage(Page page)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }
            // 안전하게 mainContentArea를 찾아서 사용
            var frame = FindName("mainContentArea") as System.Windows.Controls.Frame;
            if (frame != null)
            {
                frame.Navigate(page);
            }
            else
            {
                // 대체: 새 Frame을 만들어 창에 추가
                var newFrame = new System.Windows.Controls.Frame { NavigationUIVisibility = System.Windows.Navigation.NavigationUIVisibility.Hidden };
                newFrame.Navigate(page);
                if (FindName("mainGrid") is System.Windows.Controls.Grid mainGrid)
                {
                    mainGrid.Children.Add(newFrame);
                }
            }
        }
        protected override async void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            if (isExplicitClose) return;

            e.Cancel = true; // 기본 종료 취소

            var result = System.Windows.MessageBox.Show(
                "프로그램을 완전히 종료하시겠습니까?\n'아니오'를 선택하면 시스템 트레이로 이동합니다.",
                "종료 확인",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                isExplicitClose = true;
                try
                {
                    await LogCheck.Services.MonitoringHub.Instance.StopAsync();
                }
                catch { }

                System.Windows.Application.Current.Shutdown();
            }
            else if (result == MessageBoxResult.No)
            {
                Hide(); // 트레이로 이동
            }
            // Cancel이면 아무것도 하지 않음
        }
    }
}
