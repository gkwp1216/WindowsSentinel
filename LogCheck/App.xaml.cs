using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Forms; // NotifyIcon
using System.Drawing;      // Icon

namespace LogCheck
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private NotifyIcon? _notifyIcon;
        private MainWindows? _mainWindows;

        public App()
        {
            InitializeComponent();
        }
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 트레이 아이콘 생성
            _notifyIcon = new NotifyIcon
            {
                Icon = new Icon("WindowsSentinel.ico"), // 필요시 .ico 교체 가능
                Visible = true,
                Text = "Windows Sentinal"
            };

            // 트레이 메뉴 구성
            var contextMenu = new ContextMenuStrip();

            var openItem = new ToolStripMenuItem("창 열기");
            openItem.Click += (_, __) => ShowWindow();
            contextMenu.Items.Add(openItem);

            var exitItem = new ToolStripMenuItem("종료");
            exitItem.Click += (_, __) => ExitApplication();
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;

            // 아이콘 더블클릭 시 창 열기
            _notifyIcon.DoubleClick += (_, __) => ShowWindow();

            // 앱 종료 시 트레이 아이콘 정리
            this.Exit += (_, __) =>
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    _notifyIcon = null;
                }
            };

            // UI는 시작 시 자동 표시되지 않음 → 백그라운드 상주
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
            Shutdown(); // 명시적 종료
        }

        public static class ThemeManager
        {
            public static string CurrentTheme { get; set; } = "Light"; // 기본값
        }
    }
}
