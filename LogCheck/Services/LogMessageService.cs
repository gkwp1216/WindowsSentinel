using System;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace LogCheck.Services
{
    /// <summary>
    /// 로그 메시지 관리를 위한 공통 서비스 클래스
    /// AddLogMessage 중복 제거 및 통일된 로그 관리 제공
    /// </summary>
    public class LogMessageService : IDisposable
    {
        private readonly ObservableCollection<string> _logMessages;
        private readonly Dispatcher _dispatcher;
        private readonly int _maxLogCount;
        private bool _disposed = false;

        /// <summary>
        /// 로그 메시지 컬렉션 (읽기 전용)
        /// </summary>
        public ObservableCollection<string> LogMessages => _logMessages;

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="dispatcher">UI 스레드 Dispatcher</param>
        /// <param name="maxLogCount">최대 로그 개수 (기본값: 100)</param>
        public LogMessageService(Dispatcher? dispatcher = null, int maxLogCount = 100)
        {
            _logMessages = new ObservableCollection<string>();
            _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
            _maxLogCount = Math.Max(10, maxLogCount); // 최소 10개
        }

        /// <summary>
        /// 로그 메시지 추가
        /// </summary>
        /// <param name="message">로그 메시지</param>
        /// <param name="includeTimestamp">타임스탬프 포함 여부 (기본값: true)</param>
        public void AddLogMessage(string message, bool includeTimestamp = true)
        {
            if (string.IsNullOrWhiteSpace(message) || _disposed)
                return;

            try
            {
                var logMessage = includeTimestamp
                    ? $"[{DateTime.Now:HH:mm:ss}] {message}"
                    : message;

                // UI 스레드에서 실행
                if (_dispatcher.CheckAccess())
                {
                    AddLogMessageInternal(logMessage);
                }
                else
                {
                    _dispatcher.InvokeAsync(() => AddLogMessageInternal(logMessage));
                }
            }
            catch (Exception ex)
            {
                // 로그 추가 실패 시 콘솔에 출력 (무한 루프 방지)
                System.Diagnostics.Debug.WriteLine($"로그 메시지 추가 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 포맷된 로그 메시지 추가
        /// </summary>
        /// <param name="format">메시지 포맷</param>
        /// <param name="args">포맷 인수</param>
        public void AddLogMessage(string format, params object[] args)
        {
            if (args?.Length > 0)
            {
                AddLogMessage(string.Format(format, args));
            }
            else
            {
                AddLogMessage(format);
            }
        }

        /// <summary>
        /// 정보 레벨 로그
        /// </summary>
        /// <param name="message">메시지</param>
        public void LogInfo(string message) => AddLogMessage($"ℹ️ {message}");

        /// <summary>
        /// 경고 레벨 로그
        /// </summary>
        /// <param name="message">메시지</param>
        public void LogWarning(string message) => AddLogMessage($"⚠️ {message}");

        /// <summary>
        /// 오류 레벨 로그
        /// </summary>
        /// <param name="message">메시지</param>
        public void LogError(string message) => AddLogMessage($"❌ {message}");

        /// <summary>
        /// 성공 레벨 로그
        /// </summary>
        /// <param name="message">메시지</param>
        public void LogSuccess(string message) => AddLogMessage($"✅ {message}");

        /// <summary>
        /// 디버그 레벨 로그
        /// </summary>
        /// <param name="message">메시지</param>
        public void LogDebug(string message) => AddLogMessage($"🔄 [DEBUG] {message}");

        /// <summary>
        /// 모든 로그 메시지 삭제
        /// </summary>
        public void ClearLogs()
        {
            if (_disposed) return;

            if (_dispatcher.CheckAccess())
            {
                _logMessages.Clear();
            }
            else
            {
                _dispatcher.InvokeAsync(() => _logMessages.Clear());
            }
        }

        /// <summary>
        /// 내부 로그 추가 로직 (UI 스레드에서만 호출)
        /// </summary>
        private void AddLogMessageInternal(string logMessage)
        {
            _logMessages.Add(logMessage);

            // 로그 메시지 개수 제한
            while (_logMessages.Count > _maxLogCount)
            {
                _logMessages.RemoveAt(0);
            }
        }

        /// <summary>
        /// 리소스 정리
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                ClearLogs();
            }
        }
    }
}