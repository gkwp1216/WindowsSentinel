using System;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LogCheck.Services;

namespace LogCheck.Controls
{
    /// <summary>
    /// 간단한 Toast 알림 컨트롤
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class SimpleToastControl : System.Windows.Controls.UserControl
    {
        private DispatcherTimer? _autoHideTimer;

        public ToastNotification? ToastData { get; private set; }
        public event EventHandler? ToastClosed;

        public SimpleToastControl(ToastNotification toastData)
        {
            InitializeComponent();
            ToastData = toastData;
            SetupToast();
        }

        private void SetupToast()
        {
            if (ToastData == null) return;

            // 텍스트 설정
            TitleText.Text = ToastData.Title;
            MessageText.Text = ToastData.Message;
            IconText.Text = ToastData.Icon;

            // 색상 설정
            var backgroundColor = GetBackgroundColor(ToastData.Type);
            var borderColor = GetBorderColor(ToastData.Type);

            ToastBorder.Background = new SolidColorBrush(backgroundColor);
            ToastBorder.BorderBrush = new SolidColorBrush(borderColor);

            // 자동 숨김 타이머 설정
            SetupAutoHideTimer();
        }

        private System.Windows.Media.Color GetBackgroundColor(ToastType type)
        {
            return type switch
            {
                ToastType.Success => (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4CAF50")!,
                ToastType.Info => (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2196F3")!,
                ToastType.Warning => (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF9800")!,
                ToastType.Error => (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F44336")!,
                ToastType.Security => (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9C27B0")!,
                _ => (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2196F3")!
            };
        }

        private System.Windows.Media.Color GetBorderColor(ToastType type)
        {
            return type switch
            {
                ToastType.Success => (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#388E3C")!,
                ToastType.Info => (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1976D2")!,
                ToastType.Warning => (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F57C00")!,
                ToastType.Error => (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D32F2F")!,
                ToastType.Security => (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#7B1FA2")!,
                _ => (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1976D2")!
            };
        }

        private void SetupAutoHideTimer()
        {
            if (ToastData == null) return;

            _autoHideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(ToastData.DisplayDurationMs)
            };

            _autoHideTimer.Tick += (s, e) =>
            {
                _autoHideTimer?.Stop();
                CloseToast();
            };

            _autoHideTimer.Start();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseToast();
        }

        private void CloseToast()
        {
            _autoHideTimer?.Stop();
            _autoHideTimer = null;
            ToastClosed?.Invoke(this, EventArgs.Empty);
        }
    }
}