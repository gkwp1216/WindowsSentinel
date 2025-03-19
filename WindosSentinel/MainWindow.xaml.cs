using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;

namespace WindowsSentinel
{
    public partial class MainWindow : Window
    {
        private TextBox LogTextBox;
        public MainWindow()
        {
            //InitializeComponent();
            LogTextBox = new TextBox(); // Initialize LogTextBox
            CollectEventLogs();
            AnalyzeRecentFiles();
        }

        private void CollectEventLogs()
        {
            // 1. Event Viewer에서 프로그램 설치 이벤트 로그 수집
            AppendLog("=== 프로그램 설치 이벤트 로그 ===");
            string queryString = "*[System/EventID=11707]";
            try
            {
                EventLogQuery query = new EventLogQuery("Application", PathType.LogName, queryString);
                using (EventLogReader reader = new EventLogReader(query))
                {
                    EventRecord record;
                    while ((record = reader.ReadEvent()) != null)
                    {
                        AppendLog($"Event ID: {record.Id}");
                        AppendLog($"Time Created: {record.TimeCreated}");
                        AppendLog($"Message: {record.FormatDescription()}");
                        AppendLog("");
                    }
                }
            }
            catch (EventLogException ex)
            {
                AppendLog($"Error reading event logs: {ex.Message}");
            }
        }

        private void AnalyzeRecentFiles()
        {
            // 2. PowerShell을 사용하여 최근 생성된 파일 분석
            AppendLog("=== 최근 생성된 파일 ===");
            string script = @"
                        Get-ChildItem -Path C:\ -Recurse -File |
                        Where-Object { $_.CreationTime -gt (Get-Date).AddDays(-7) } |
                        Select-Object FullName, CreationTime |
                        Sort-Object CreationTime -Descending |
                        Select-Object -First 10
                    ";

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "powershell.exe";
            psi.Arguments = $"-NoProfile -ExecutionPolicy unrestricted -Command \"{script}\"";
            psi.RedirectStandardOutput = true;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;

            try
            {
                using (Process process = Process.Start(psi))
                {
                    using (StreamReader outputReader = process.StandardOutput)
                    {
                        string result = outputReader.ReadToEnd();
                        AppendLog(result);
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Error executing PowerShell script: {ex.Message}");
            }
        }

        private void AppendLog(string message)
        {
            // 로그를 TextBox에 추가
            if (LogTextBox != null)
            {
                LogTextBox.AppendText(message + Environment.NewLine);
                LogTextBox.ScrollToEnd();
            }
        }
    }
}
