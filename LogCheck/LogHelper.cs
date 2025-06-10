using System;
using System.IO;
using System.Windows;

namespace WindowsSentinel
{
    public static class LogHelper
    {
        private static readonly string LogFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            "Logs",
            $"Log_{DateTime.Now:yyyyMMdd}.log");

        static LogHelper()
        {
            try
            {
                string? logDir = Path.GetDirectoryName(LogFilePath);
                if (string.IsNullOrEmpty(logDir))
                {
                    throw new InvalidOperationException("로그 디렉토리 경로를 가져올 수 없습니다.");
                }

                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"로그 디렉토리 생성 실패: {ex.Message}");
            }
        }

        public static void LogInfo(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                throw new ArgumentNullException(nameof(message), "로그 메시지는 null이거나 비어있을 수 없습니다.");
            }
            Log("INFO", message);
        }

        public static void LogWarning(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                throw new ArgumentNullException(nameof(message), "로그 메시지는 null이거나 비어있을 수 없습니다.");
            }
            Log("WARN", message);
        }

        public static void LogError(string message, Exception? ex = null)
        {
            if (string.IsNullOrEmpty(message))
            {
                throw new ArgumentNullException(nameof(message), "로그 메시지는 null이거나 비어있을 수 없습니다.");
            }

            string errorMessage = ex != null 
                ? $"{message}\n예외: {ex.Message}\n스택 추적: {ex.StackTrace}" 
                : message;
            Log("ERROR", errorMessage);
        }

        private static void Log(string level, string message)
        {
            if (string.IsNullOrEmpty(level))
            {
                throw new ArgumentNullException(nameof(level), "로그 레벨은 null이거나 비어있을 수 없습니다.");
            }
            if (string.IsNullOrEmpty(message))
            {
                throw new ArgumentNullException(nameof(message), "로그 메시지는 null이거나 비어있을 수 없습니다.");
            }

            try
            {
                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}\n";
                
                // 콘솔에 출력
                Console.Write(logMessage);
                
                // 파일에 쓰기 (비동기로 처리)
                File.AppendAllText(LogFilePath, logMessage);
                
                // 디버그 창에 출력
                System.Diagnostics.Debug.Write(logMessage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"로그 기록 중 오류 발생: {ex.Message}");
            }
        }
    }
}
