using System.Drawing;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Windows;
using System.Windows.Forms;
using LogCheck.Services;

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
            /*
             // 콘솔 윈도우 할당 (디버깅용)
             if (System.Diagnostics.Debugger.IsAttached == false)
             {
                 try
                 {
                     AllocConsole();
                     Console.WriteLine("LogCheck 애플리케이션 시작");
                 }
                 catch
                 {
                     //콘솔 할당 실패시 무시
                 }
             }*/

            InitializeComponent();
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 🔥 관리자 권한 체크 - 필수 보안 요구사항
            if (!IsRunningAsAdministrator())
            {
                System.Diagnostics.Debug.WriteLine("🚫 관리자 권한 없음 - 프로그램 종료");
                System.Windows.MessageBox.Show(
                    "WindowsSentinel은 시스템 보안을 위해 관리자 권한이 필요합니다.\n\n" +
                    "프로그램을 관리자 권한으로 다시 실행해주세요.\n\n" +
                    "방법: exe 파일을 마우스 오른쪽 버튼으로 클릭 → '관리자 권한으로 실행'",
                    "관리자 권한 필요",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                // 프로그램 즉시 종료
                System.Windows.Application.Current.Shutdown();
                return;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("✅ 관리자 권한 확인됨 - 프로그램 계속 실행");
            }

            ToolStripMenuItem? toggleItem = null;

            try
            {
                // 트레이 아이콘 생성
                var trayIcon = TryLoadIcon();
                _notifyIcon = new NotifyIcon
                {
                    Icon = trayIcon,
                    Visible = true,
                    Text = "Windows Sentinel - 네트워크 보안 모니터링"
                };


                System.Diagnostics.Debug.WriteLine("트레이 아이콘이 성공적으로 생성되고 표시됨");

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

                toggleItem = new ToolStripMenuItem("모니터링 중지");
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
                        if (toggleItem != null)
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
                var s = LogCheck.Properties.Settings.Default;
                var bpf = string.IsNullOrWhiteSpace(s.BpfFilter) ? "tcp or udp or icmp" : s.BpfFilter;
                string? nic = s.AutoSelectNic ? null : (string.IsNullOrWhiteSpace(s.SelectedNicId) ? null : s.SelectedNicId);

                // 방화벽 규칙 복구 (애플리케이션 시작 시)
                try
                {
                    var persistentFirewallManager = new PersistentFirewallManager();
                    var restoredCount = await persistentFirewallManager.RestoreBlockRulesFromDatabase();

                    if (restoredCount > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"시작 시 {restoredCount}개의 방화벽 규칙을 복구했습니다.");
                        _notifyIcon?.ShowBalloonTip(3000, "Windows Sentinel",
                            $"{restoredCount}개의 차단 규칙이 복구되었습니다.", ToolTipIcon.Info);
                    }

                    // 정기 동기화 시작
                    persistentFirewallManager.StartPeriodicSynchronization();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"방화벽 규칙 복구 실패: {ex.Message}");
                    // 복구 실패는 애플리케이션 시작을 방해하지 않음
                }

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
                System.Windows.MessageBox.Show($"시작 오류: {ex.Message}\n{ex.StackTrace}", "오류");
                Shutdown();
                return;
            }

            // UI는 시작 시 자동 표시되지 않음 → 백그라운드 상주
        }

        /// <summary>
        /// 트레이 아이콘에서 벌룬 팁 표시
        /// </summary>
        public void ShowBalloonTip(string title, string text, ToolTipIcon icon)
        {
            try
            {
                _notifyIcon?.ShowBalloonTip(3000, title, text, icon);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"트레이 알림 표시 실패: {ex.Message}");
            }
        }

        private Icon TryLoadIcon()
        {
            try
            {
                // 여러 경로에서 아이콘을 찾아서 로드
                var possiblePaths = new[]
                {
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WindowsSentinel.ico"),
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IconTexture", "WindowsSentinel.ico"),
                    System.IO.Path.Combine(Environment.CurrentDirectory, "WindowsSentinel.ico"),
                    System.IO.Path.Combine(Environment.CurrentDirectory, "IconTexture", "WindowsSentinel.ico")
                };

                foreach (var path in possiblePaths)
                {
                    if (System.IO.File.Exists(path))
                    {
                        System.Diagnostics.Debug.WriteLine($"아이콘 로드 성공: {path}");
                        return new Icon(path);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"아이콘 파일 없음: {path}");
                    }
                }

                System.Diagnostics.Debug.WriteLine("모든 경로에서 아이콘을 찾을 수 없음. 기본 아이콘 사용.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"아이콘 로드 오류: {ex.Message}");
            }
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

        /// <summary>
        /// 현재 프로세스가 관리자 권한으로 실행되고 있는지 확인
        /// </summary>
        private bool IsRunningAsAdministrator()
        {
            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        public static class ThemeManager
        {
            public static string CurrentTheme { get; set; } = "Light"; // 기본값
        }
    }
}
