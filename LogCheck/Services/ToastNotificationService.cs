using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using LogCheck.Controls;

namespace LogCheck.Services
{
    /// <summary>
    /// Toast 알림 타입 열거형
    /// </summary>
    public enum ToastType
    {
        Success,    // 🟢 성공 (3초)
        Info,       // 🔵 정보 (3초)
        Warning,    // 🟡 경고 (4초)
        Error,      // 🔴 오류 (5초)
        Security    // 🛡️ 보안 이벤트 (6초)
    }

    /// <summary>
    /// Toast 알림 데이터 모델
    /// </summary>
    public class ToastNotification : INotifyPropertyChanged
    {
        private bool _isVisible = true;

        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public ToastType Type { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int DisplayDurationMs { get; set; }

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible != value)
                {
                    _isVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 타입별 아이콘 반환
        /// </summary>
        public string Icon => Type switch
        {
            ToastType.Success => "✅",
            ToastType.Info => "ℹ️",
            ToastType.Warning => "⚠️",
            ToastType.Error => "❌",
            ToastType.Security => "🛡️",
            _ => "ℹ️"
        };

        /// <summary>
        /// 타입별 색상 반환
        /// </summary>
        public string BackgroundColor => Type switch
        {
            ToastType.Success => "#4CAF50",   // 초록
            ToastType.Info => "#2196F3",      // 파랑
            ToastType.Warning => "#FF9800",   // 주황
            ToastType.Error => "#F44336",     // 빨강
            ToastType.Security => "#9C27B0",  // 보라
            _ => "#2196F3"
        };

        /// <summary>
        /// 타입별 테두리 색상 반환
        /// </summary>
        public string BorderColor => Type switch
        {
            ToastType.Success => "#388E3C",
            ToastType.Info => "#1976D2",
            ToastType.Warning => "#F57C00",
            ToastType.Error => "#D32F2F",
            ToastType.Security => "#7B1FA2",
            _ => "#1976D2"
        };

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Toast 알림 서비스 - MessageBox 대체용 비침습적 알림 시스템
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class ToastNotificationService : INotifyPropertyChanged
    {
        private static ToastNotificationService? _instance;
        private readonly Dispatcher _dispatcher;
        private StackPanel? _containerStack;
        private const int MaxToasts = 4;

        public static ToastNotificationService Instance => _instance ??= new ToastNotificationService();

        public ObservableCollection<ToastNotification> ActiveToasts { get; }

        private ToastNotificationService()
        {
            _dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            ActiveToasts = new ObservableCollection<ToastNotification>();
        }

        /// <summary>
        /// Toast 컨테이너 StackPanel 설정
        /// </summary>
        public void SetContainer(StackPanel container)
        {
            _containerStack = container;
        }

        /// <summary>
        /// 성공 알림 표시 (3초)
        /// </summary>
        public async Task ShowSuccessAsync(string title, string message)
        {
            await ShowToastAsync(title, message, ToastType.Success, 3000);
        }

        /// <summary>
        /// 정보 알림 표시 (3초)
        /// </summary>
        public async Task ShowInfoAsync(string title, string message)
        {
            await ShowToastAsync(title, message, ToastType.Info, 3000);
        }

        /// <summary>
        /// 경고 알림 표시 (4초)
        /// </summary>
        public async Task ShowWarningAsync(string title, string message)
        {
            await ShowToastAsync(title, message, ToastType.Warning, 4000);
        }

        /// <summary>
        /// 오류 알림 표시 (5초)
        /// </summary>
        public async Task ShowErrorAsync(string title, string message)
        {
            await ShowToastAsync(title, message, ToastType.Error, 5000);
        }

        /// <summary>
        /// 보안 이벤트 알림 표시 (6초)
        /// </summary>
        public async Task ShowSecurityAsync(string title, string message)
        {
            await ShowToastAsync(title, message, ToastType.Security, 6000);
        }

        /// <summary>
        /// Toast 알림 표시 핵심 메서드
        /// </summary>
        private async Task ShowToastAsync(string title, string message, ToastType type, int durationMs)
        {
            await _dispatcher.InvokeAsync(() =>
            {
                var toast = new ToastNotification
                {
                    Title = title,
                    Message = message,
                    Type = type,
                    DisplayDurationMs = durationMs
                };

                // 최대 알림 수 제한
                while (ActiveToasts.Count >= MaxToasts)
                {
                    var oldestToast = ActiveToasts[0];
                    RemoveToastFromUI(oldestToast);
                    ActiveToasts.RemoveAt(0);
                }

                ActiveToasts.Add(toast);

                // UI 컨트롤 생성 및 컨테이너에 추가
                if (_containerStack != null)
                {
                    var toastControl = new SimpleToastControl(toast);

                    _containerStack.Children.Insert(0, toastControl); // 상단에 추가

                    // 닫기 이벤트 처리
                    toastControl.ToastClosed += (s, e) =>
                    {
                        _containerStack.Children.Remove(toastControl);
                        if (ActiveToasts.Contains(toast))
                        {
                            ActiveToasts.Remove(toast);
                            OnPropertyChanged(nameof(ActiveToasts));
                        }
                    };
                }

                OnPropertyChanged(nameof(ActiveToasts));

                // 자동 사라짐 타이머 설정
                _ = Task.Run(async () =>
                {
                    await Task.Delay(durationMs);
                    await _dispatcher.InvokeAsync(() =>
                    {
                        if (ActiveToasts.Contains(toast))
                        {
                            RemoveToastFromUI(toast);
                            ActiveToasts.Remove(toast);
                            OnPropertyChanged(nameof(ActiveToasts));
                        }
                    });
                });
            });
        }

        /// <summary>
        /// UI에서 Toast 제거
        /// </summary>
        private void RemoveToastFromUI(ToastNotification toast)
        {
            toast.IsVisible = false;
        }

        /// <summary>
        /// 특정 Toast 알림 수동 제거
        /// </summary>
        public async Task RemoveToastAsync(string toastId)
        {
            await _dispatcher.InvokeAsync(() =>
            {
                for (int i = ActiveToasts.Count - 1; i >= 0; i--)
                {
                    if (ActiveToasts[i].Id == toastId)
                    {
                        ActiveToasts[i].IsVisible = false;
                        ActiveToasts.RemoveAt(i);
                        OnPropertyChanged(nameof(ActiveToasts));
                        break;
                    }
                }
            });
        }

        /// <summary>
        /// 모든 Toast 알림 제거
        /// </summary>
        public async Task ClearAllToastsAsync()
        {
            await _dispatcher.InvokeAsync(() =>
            {
                foreach (var toast in ActiveToasts)
                {
                    toast.IsVisible = false;
                }
                ActiveToasts.Clear();
                OnPropertyChanged(nameof(ActiveToasts));
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}