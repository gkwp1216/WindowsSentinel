using System;
using System.ComponentModel;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

namespace LogCheck
{
    [SupportedOSPlatform("windows")]
    public partial class MainWindows : Window
    {
        private NotifyIcon? notifyIcon;
        private bool isExplicitClose = false;

        [SupportedOSPlatform("windows")]
        public MainWindows()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                NavigateToPage(new NetWorks_New());
            };

            InitializeNotifyIcon();
        }

        private void InitializeNotifyIcon()
        {
            try
            {
                notifyIcon = new NotifyIcon
                {
                    Icon = new System.Drawing.Icon(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IconTexture", "WindowsSentinel.ico")),
                    Text = "Windows Sentinel",
                    Visible = false
                };

                notifyIcon.DoubleClick += (s, e) =>
                {
                    ShowFromTray();
                };

                var contextMenu = new ContextMenuStrip();
                var openItem = new ToolStripMenuItem("열기");
                openItem.Click += (s, e) => ShowFromTray();
                var exitItem = new ToolStripMenuItem("종료");
                exitItem.Click += (s, e) =>
                {
                    isExplicitClose = true;
                    System.Windows.Application.Current.Dispatcher.Invoke(() => System.Windows.Application.Current.Shutdown());
                };

                contextMenu.Items.Add(openItem);
                contextMenu.Items.Add(exitItem);
                notifyIcon.ContextMenuStrip = contextMenu;
            }
            catch (Exception ex)
            {
                // 로그로 남기거나 무시
                System.Diagnostics.Debug.WriteLine($"NotifyIcon init failed: {ex.Message}");
            }
        }

        private void ShowFromTray()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
                if (notifyIcon != null)
                {
                    notifyIcon.Visible = false;
                }
            });
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

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            if (isExplicitClose)
            {
                // 정상 종료
                notifyIcon?.Dispose();
                notifyIcon = null;
                return;
            }

            // 기본 동작 취소하고 사용자에게 묻기
            e.Cancel = true;
            var result = System.Windows.MessageBox.Show(
                "프로그램을 완전히 종료하시겠습니까?\n'아니오'를 선택하면 시스템 트레이로 이동합니다.",
                "종료 확인",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                isExplicitClose = true;
                notifyIcon?.Dispose();
                notifyIcon = null;
                System.Windows.Application.Current.Shutdown();
            }
            else if (result == MessageBoxResult.No)
            {
                // 트레이로 이동
                Hide();
                if (notifyIcon != null)
                {
                    notifyIcon.Visible = true;
                }
            }
            // Cancel이면 아무것도 하지 않음
        }
    }
}
