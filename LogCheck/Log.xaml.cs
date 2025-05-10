using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
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
using WindowsSentinel;

namespace WindowsSentinel
{
    /// <summary>
    /// Log.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class Log : Page
    {
        public Log()
        {
            InitializeComponent();
        }

        private void BtnShowChangeLogs_Click(object sender, RoutedEventArgs e)
        {          
            logsSection.Visibility = Visibility.Visible; // 로그 섹션 표시

            listBoxLogs.Items.Clear();

            // 이벤트 ID와 로그 채널을 튜플로 묶어 순회
            var eventSources = new (int Id, string LogName)[]
            {
                (5007, "Microsoft-Windows-Windows Defender/Operational"),
                (2004, "Microsoft-Windows-Windows Firewall With Advanced Security/Firewall"),
                (2006, "Microsoft-Windows-Windows Firewall With Advanced Security/Firewall"),
                (2033, "Microsoft-Windows-Windows Firewall With Advanced Security/Firewall"),
                (775,  "Microsoft-Windows-BitLocker/Operational")
            };

            DateTime oneYearAgo = DateTime.Now.AddYears(-1);

            foreach (var es in eventSources)
            {
                try
                {
                    string query = $"*[System[(EventID={es.Id})]]";
                    var eventQuery = new EventLogQuery(es.LogName, PathType.LogName, query)
                    {
                        ReverseDirection = true  // 최신 로그부터
                    };

                    using (var reader = new EventLogReader(eventQuery))
                    {
                        var record = reader.ReadEvent();

                        // null‑안전 검증 & 1년 이내 검사
                        if (record?.TimeCreated > oneYearAgo)
                        {
                            string time = record.TimeCreated?.ToString("yyyy-MM-dd HH:mm:ss") ?? "시간 없음";
                            string message = record.FormatDescription() ?? "(설명 없음)";
                            listBoxLogs.Items.Add($"[{time}] {record.Id} - {message}");
                        }
                        else
                        {
                            listBoxLogs.Items.Add($"[{es.LogName} / ID {es.Id}] 최근 1년 내 이벤트 없음");
                        }
                    }
                }
                catch (Exception ex)
                {
                    listBoxLogs.Items.Add($"[{es.LogName} / ID {es.Id}] 로그 읽기 실패: {ex.Message}");
                }
            }
        }

        private void SidebarPrograms_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Page1());
        }

        private void SidebarModification_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Page2());
        }

        private void SidebarLog_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Log());
        }

        private void SidebarRecovery_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("보안 프로그램 복구 기능이 곧 구현될 예정입니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void NavigateToPage(Page page)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.NavigateToPage(page);
        }
    }
}
