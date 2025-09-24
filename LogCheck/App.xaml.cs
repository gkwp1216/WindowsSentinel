using LogCheck.Services;
using System.Runtime.Versioning;
using System.Windows;

namespace LogCheck
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class App : System.Windows.Application
    {
        private NotifyIcon? _notifyIcon;
        private MainWindows? _mainWindows;

        public App()
        {
            InitializeComponent();
        }
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 트레이 아이콘 생성
            _notifyIcon = new NotifyIcon
            {
                Icon = TryLoadIcon(),
                Visible = true,
                Text = "Windows Sentinal"
            };

            // 트레이 메뉴 구성
            var contextMenu = new ContextMenuStrip();

            var openItem = new ToolStripMenuItem("창 열기");
            openItem.Click += (_, __) => ShowWindow();
            contextMenu.Items.Add(openItem);

            var settingsItem = new ToolStripMenuItem("설정");
            settingsItem.Click += (_, __) =>
            {
                // 메인 창을 띄우고 설정 페이지로 이동
                ShowWindow();
                try { _mainWindows?.NavigateToPage(new Setting()); } catch { }
            };
            contextMenu.Items.Add(settingsItem);

            var toggleItem = new ToolStripMenuItem("모니터링 중지");
            toggleItem.Click += async (_, __) =>
            {
                try
                {
                    if (MonitoringHub.Instance.IsRunning)
                    {
                        await MonitoringHub.Instance.StopAsync();
                        toggleItem.Text = "모니터링 시작";
                        _notifyIcon?.ShowBalloonTip(3000, "Windows Sentinel", "모니터링이 중지되었습니다.", ToolTipIcon.Info);
                    }
                    else
                    {
                        var s = LogCheck.Properties.Settings.Default;
                        var bpf = string.IsNullOrWhiteSpace(s.BpfFilter) ? "tcp or udp or icmp" : s.BpfFilter;
                        string? nic = s.AutoSelectNic ? null : (string.IsNullOrWhiteSpace(s.SelectedNicId) ? null : s.SelectedNicId);
                        await MonitoringHub.Instance.StartAsync(bpf, nic);
                        toggleItem.Text = "모니터링 중지";
                        _notifyIcon?.ShowBalloonTip(3000, "Windows Sentinel", "모니터링이 시작되었습니다.", ToolTipIcon.Info);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Toggle monitoring failed: {ex.Message}");
                }
            };
            contextMenu.Items.Add(toggleItem);

            // 허브 상태 변화에 따라 토글 텍스트 동기화
            MonitoringHub.Instance.MonitoringStateChanged += (_, running) =>
            {
                try
                {
                    toggleItem.Text = running ? "모니터링 중지" : "모니터링 시작";
                }
                catch { }
            };

            var exitItem = new ToolStripMenuItem("종료");
            exitItem.Click += (_, __) => ExitApplication();
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;

            // 아이콘 더블클릭 시 창 열기
            _notifyIcon.DoubleClick += (_, __) => ShowWindow();

            // 앱 종료 시 정리(동기 보장)
            this.Exit += (_, __) =>
            {
                try
                {
                    // 모니터링을 동기적으로 중단하여 캡처 스레드가 남지 않도록 보장
                    try { MonitoringHub.Instance.StopAsync().GetAwaiter().GetResult(); } catch { /* ignore on exit */ }

                    // 트레이 아이콘 정리
                    if (_notifyIcon != null)
                    {
                        try { _notifyIcon.Visible = false; } catch { }
                        try { _notifyIcon.Dispose(); } catch { }
                        _notifyIcon = null;
                    }
                }
                catch { /* 마지막 정리에서 발생하는 예외는 무시 */ }
            };

            // 설정에 따라 자동 모니터링 시작
            try
            {
                var s = LogCheck.Properties.Settings.Default;
                var bpf = string.IsNullOrWhiteSpace(s.BpfFilter) ? "tcp or udp or icmp" : s.BpfFilter;
                string? nic = s.AutoSelectNic ? null : (string.IsNullOrWhiteSpace(s.SelectedNicId) ? null : s.SelectedNicId);

                if (s.AutoStartMonitoring)
                {
                    await MonitoringHub.Instance.StartAsync(bpf, nic);
                    toggleItem.Text = "모니터링 중지";
                    _notifyIcon?.ShowBalloonTip(3000, "Windows Sentinel", "앱 시작과 함께 모니터링을 시작했습니다.", ToolTipIcon.Info);
                }
                else
                {
                    toggleItem.Text = "모니터링 시작";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto start failed: {ex.Message}");
            }

            // UI는 시작 시 자동 표시되지 않음 → 백그라운드 상주
        }

        private Icon TryLoadIcon()
        {
            try
            {
                var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IconTexture", "WindowsSentinel.ico");
                if (System.IO.File.Exists(path))
                    return new Icon(path);
            }
            catch { }
            return SystemIcons.Application;
        }

        private void ShowWindow()
        {
            if (_mainWindows == null)
            {
                _mainWindows = new MainWindows();
                _mainWindows.Closed += (_, __) =>
                {
                    _mainWindows = null; // 창 닫아도 앱은 계속 실행됨
                };
                _mainWindows.Show();
            }
            else
            {
                if (_mainWindows.WindowState == WindowState.Minimized)
                    _mainWindows.WindowState = WindowState.Normal;

                _mainWindows.Activate();
            }
        }

        private void ExitApplication()
        {
            // 가능한 한 빨리 백그라운드 작업을 중단 후 종료
            try { MonitoringHub.Instance.StopAsync().GetAwaiter().GetResult(); } catch { }
            Shutdown(); // 명시적 종료
        }

        public static class ThemeManager
        {
            public static string CurrentTheme { get; set; } = "Light"; // 기본값
        }
    }
}
