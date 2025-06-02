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
                string logDir = Path.GetDirectoryName(LogFilePath);
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
            Log("INFO", message);
        }

        public static void LogWarning(string message)
        {
            Log("WARN", message);
        }

        public static void LogError(string message, Exception? ex = null)
        {
            string errorMessage = ex != null 
                ? $"{message}\n예외: {ex.Message}\n스택 추적: {ex.StackTrace}" 
                : message;
            Log("ERROR", errorMessage);
        }

        private static void Log(string level, string message)
        {
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
