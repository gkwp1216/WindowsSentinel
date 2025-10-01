using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using LogCheck.Services;

namespace LogCheck.ViewModels
{
    /// <summary>
    /// 페이지 공통 ViewModel 베이스 클래스
    /// INotifyPropertyChanged, 로그 관리, 통계 관리 등 공통 기능 제공
    /// </summary>
    public abstract class BasePageViewModel : INotifyPropertyChanged, IDisposable
    {
        protected readonly LogMessageService _logService;
        protected readonly IStatisticsProvider _statisticsService;
        protected readonly Dispatcher _dispatcher;
        protected bool _disposed = false;

        // 공통 프로퍼티
        private bool _isInitialized = false;
        private bool _isLoading = false;
        private string _statusMessage = "대기 중";

        /// <summary>
        /// 초기화 완료 여부
        /// </summary>
        public bool IsInitialized
        {
            get => _isInitialized;
            protected set { _isInitialized = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 로딩 상태
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            protected set { _isLoading = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 상태 메시지
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            protected set { _statusMessage = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 로그 메시지 서비스
        /// </summary>
        public LogMessageService LogService => _logService;

        /// <summary>
        /// 통계 서비스
        /// </summary>
        public IStatisticsProvider StatisticsService => _statisticsService;

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="logService">로그 메시지 서비스</param>
        /// <param name="statisticsService">통계 서비스</param>
        /// <param name="dispatcher">UI 디스패처</param>
        protected BasePageViewModel(
            LogMessageService? logService = null,
            IStatisticsProvider? statisticsService = null,
            Dispatcher? dispatcher = null)
        {
            _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
            _logService = logService ?? new LogMessageService(_dispatcher);
            _statisticsService = statisticsService ?? new NetworkStatisticsService();
        }

        /// <summary>
        /// 페이지 초기화 (추상 메서드)
        /// </summary>
        public abstract void Initialize();

        /// <summary>
        /// 페이지 정리 (가상 메서드)
        /// </summary>
        public virtual void Cleanup()
        {
            // 기본 정리 작업
            LogService.AddLogMessage("페이지 정리 중...");
        }

        /// <summary>
        /// 비동기 초기화 (가상 메서드)
        /// </summary>
        public virtual async System.Threading.Tasks.Task InitializeAsync()
        {
            if (IsInitialized) return;

            IsLoading = true;
            StatusMessage = "초기화 중...";

            try
            {
                await System.Threading.Tasks.Task.Run(() => Initialize());
                IsInitialized = true;
                StatusMessage = "초기화 완료";
                LogService.LogSuccess("페이지 초기화 완료");
            }
            catch (Exception ex)
            {
                StatusMessage = "초기화 실패";
                LogService.LogError($"페이지 초기화 실패: {ex.Message}");
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 안전한 UI 업데이트 헬퍼 메서드
        /// </summary>
        /// <param name="action">UI 업데이트 액션</param>
        protected void SafeInvokeUI(Action action)
        {
            if (_disposed) return;

            try
            {
                if (_dispatcher.CheckAccess())
                {
                    action();
                }
                else
                {
                    _dispatcher.InvokeAsync(action);
                }
            }
            catch (Exception ex)
            {
                LogService.LogError($"UI 업데이트 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 안전한 비동기 UI 업데이트 헬퍼 메서드
        /// </summary>
        /// <param name="action">UI 업데이트 액션</param>
        protected async System.Threading.Tasks.Task SafeInvokeUIAsync(Action action)
        {
            if (_disposed) return;

            try
            {
                if (_dispatcher.CheckAccess())
                {
                    action();
                }
                else
                {
                    await _dispatcher.InvokeAsync(action);
                }
            }
            catch (Exception ex)
            {
                LogService.LogError($"비동기 UI 업데이트 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 오류 처리 헬퍼 메서드
        /// </summary>
        /// <param name="ex">예외</param>
        /// <param name="userMessage">사용자에게 표시할 메시지</param>
        /// <param name="logMessage">로그에 기록할 메시지</param>
        protected virtual void HandleError(Exception ex, string? userMessage = null, string? logMessage = null)
        {
            var errorMessage = logMessage ?? userMessage ?? ex.Message;
            LogService.LogError(errorMessage);

            // 추가적인 오류 처리 로직은 파생 클래스에서 구현
            OnErrorOccurred(ex, userMessage);
        }

        /// <summary>
        /// 오류 발생 이벤트 (가상 메서드)
        /// </summary>
        /// <param name="ex">예외</param>
        /// <param name="userMessage">사용자 메시지</param>
        protected virtual void OnErrorOccurred(Exception ex, string? userMessage = null)
        {
            // 파생 클래스에서 오버라이드하여 커스텀 오류 처리 구현
        }

        #region 이벤트 관리 패턴

        private readonly List<(object source, string eventName, Delegate handler)> _subscribedEvents = new();

        /// <summary>
        /// 이벤트 구독 및 자동 추적
        /// </summary>
        protected void SubscribeEvent<T>(T source, string eventName, Delegate handler) where T : class
        {
            if (source == null || handler == null) return;

            try
            {
                var eventInfo = typeof(T).GetEvent(eventName);
                if (eventInfo != null)
                {
                    eventInfo.AddEventHandler(source, handler);
                    _subscribedEvents.Add((source, eventName, handler));
                    LogService.AddLogMessage($"이벤트 구독: {typeof(T).Name}.{eventName}");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError($"이벤트 구독 실패: {typeof(T).Name}.{eventName} - {ex.Message}");
            }
        }

        /// <summary>
        /// 특정 이벤트 해제
        /// </summary>
        protected void UnsubscribeEvent<T>(T source, string eventName, Delegate handler) where T : class
        {
            if (source == null || handler == null) return;

            try
            {
                var eventInfo = typeof(T).GetEvent(eventName);
                if (eventInfo != null)
                {
                    eventInfo.RemoveEventHandler(source, handler);
                    _subscribedEvents.RemoveAll(e => ReferenceEquals(e.source, source) &&
                                                     e.eventName == eventName &&
                                                     e.handler == handler);
                    LogService.AddLogMessage($"이벤트 해제: {typeof(T).Name}.{eventName}");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError($"이벤트 해제 실패: {typeof(T).Name}.{eventName} - {ex.Message}");
            }
        }

        /// <summary>
        /// 모든 구독된 이벤트 자동 해제
        /// </summary>
        protected virtual void UnsubscribeAllEvents()
        {
            var eventsToRemove = new List<(object source, string eventName, Delegate handler)>(_subscribedEvents);

            foreach (var (source, eventName, handler) in eventsToRemove)
            {
                try
                {
                    var eventInfo = source.GetType().GetEvent(eventName);
                    if (eventInfo != null)
                    {
                        eventInfo.RemoveEventHandler(source, handler);
                    }
                }
                catch (Exception ex)
                {
                    LogService.LogError($"이벤트 자동 해제 실패: {source.GetType().Name}.{eventName} - {ex.Message}");
                }
            }

            _subscribedEvents.Clear();
            LogService.AddLogMessage("모든 이벤트 구독 해제 완료");
        }

        #endregion

        /// <summary>
        /// PropertyChanged 이벤트
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// PropertyChanged 이벤트 발생
        /// </summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 리소스 정리
        /// </summary>
        public virtual void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                UnsubscribeAllEvents(); // 모든 이벤트 자동 해제
                Cleanup();
                _logService?.Dispose();
            }
        }
    }

    /// <summary>
    /// 네트워크 모니터링 페이지용 베이스 ViewModel
    /// </summary>
    public abstract class NetworkPageViewModel : BasePageViewModel
    {
        protected bool _isMonitoring = false;

        /// <summary>
        /// 모니터링 상태
        /// </summary>
        public bool IsMonitoring
        {
            get => _isMonitoring;
            protected set { _isMonitoring = value; OnPropertyChanged(); }
        }

        protected NetworkPageViewModel(
            LogMessageService? logService = null,
            IStatisticsProvider? statisticsService = null,
            Dispatcher? dispatcher = null)
            : base(logService, statisticsService ?? new NetworkStatisticsService(), dispatcher)
        {
        }

        /// <summary>
        /// 모니터링 시작 (추상 메서드)
        /// </summary>
        public abstract System.Threading.Tasks.Task StartMonitoringAsync();

        /// <summary>
        /// 모니터링 중지 (추상 메서드)  
        /// </summary>
        public abstract System.Threading.Tasks.Task StopMonitoringAsync();

        /// <summary>
        /// 데이터 새로고침 (추상 메서드)
        /// </summary>
        public abstract System.Threading.Tasks.Task RefreshDataAsync();
    }
}