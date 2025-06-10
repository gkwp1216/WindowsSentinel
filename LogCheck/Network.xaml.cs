using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Runtime.Versioning;

namespace WindowsSentinel
{
    public class EventLogEntryModel
    {
        public DateTime TimeGenerated { get; set; }
        public string ApplicationName { get; set; }
        public int ProcessId { get; set; }
        public string Protocol { get; set; }
        public string Direction { get; set; }
        public string Source { get; set; }
        public string Destination { get; set; }
        public string Result { get; set; }
        public int EventId { get; set; }
    }

    [SupportedOSPlatform("windows")]
    public partial class Network : Page
    {
        private DispatcherTimer? loadingTextTimer;
        private int dotCount = 0;
        private const int maxDots = 3;
        private string baseText = "검사 중";
        private ObservableCollection<EventLogEntryModel> eventLogEntries = new ObservableCollection<EventLogEntryModel>();

        public Network()
        {
            if (!IsRunningAsAdmin())
            {
                MessageBox.Show("이 프로그램은 관리자 권한으로 실행해야 합니다.",
                                "권한 필요",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                Application.Current.Shutdown();
                return;
            }

            if (!IsAuditPolicyEnabled())
            {
                EnableAuditPolicy();
                MessageBox.Show("TCP 연결 추적을 위해 감사 정책이 일시적으로 활성화되었습니다.\n이 설정은 시스템에 영향을 줄 수 있습니다.",
                                "감사 정책 활성화됨",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
            }

            InitializeComponent();
            SetupLoadingTextAnimation();
            SpinnerItems.ItemsSource = CreateSpinnerPoints(40, 50, 50);
            StartRotation();
        }

        private bool IsRunningAsAdmin()
        {
            try
            {
                using WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        private bool IsAuditPolicyEnabled()
        {
            try
            {
                ProcessStartInfo psi = new()
                {
                    FileName = "auditpol",
                    Arguments = "/get /subcategory:\"Filtering Platform Connection\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using Process process = Process.Start(psi);
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return output.Contains("Success              Enable") || output.Contains("Failure              Enable");
            }
            catch { return false; }
        }

        private void EnableAuditPolicy()
        {
            string[] commands =
            {
                "auditpol /set /subcategory:\"Filtering Platform Connection\" /success:enable /failure:enable",
                "auditpol /set /subcategory:\"Filtering Platform Packet Drop\" /success:enable /failure:enable"
            };

            foreach (var cmd in commands)
            {
                ProcessStartInfo psi = new()
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {cmd}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(psi))
                {
                    process.WaitForExit();
                }
            }
        }

        private async void BtnCheck_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                ShowLoadingOverlay();

                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

                await Task.Run(() => { LoadFirewallEventLogs(); });

                Dispatcher.Invoke(() =>
                {
                    eventDataGrid.ItemsSource = null;
                    eventDataGrid.ItemsSource = eventLogEntries;

                    if (eventLogEntries.Count == 0)
                    {
                        MessageBox.Show("검색된 이벤트 로그 데이터가 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"프로그램 검사 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                HideLoadingOverlay();
            }
        }

        private void LoadFirewallEventLogs()
        {
            string query = "*[System[(EventID=5156 or EventID=5157 or EventID=5158)]]";
            var logQuery = new EventLogQuery("Security", PathType.LogName, query) { ReverseDirection = true };

            try
            {
                using var reader = new EventLogReader(logQuery);
                EventRecord record;
                while ((record = reader.ReadEvent()) != null)
                {
                    using (record)
                    {
                        string xml = record.ToXml();
                        var parsed = ParseEventRecord(record, xml);
                        if (parsed != null)
                            eventLogEntries.Add(parsed);
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"이벤트 로그 수집 중 오류 발생: {ex.Message}", "로그 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private EventLogEntryModel? ParseEventRecord(EventRecord record, string xml)
        {
            try
            {
                string app = GetXmlValue(xml, "ApplicationName");
                string pidStr = GetXmlValue(xml, "ProcessId");
                string proto = GetProtocol(GetXmlValue(xml, "Protocol"));
                string srcIp = GetXmlValue(xml, "SourceAddress");
                string srcPort = GetXmlValue(xml, "SourcePort");
                string dstIp = GetXmlValue(xml, "DestinationAddress");
                string dstPort = GetXmlValue(xml, "DestinationPort");
                string direction = GetDirection(GetXmlValue(xml, "Direction"));
                string result = GetResultFromId(record.Id);

                return new EventLogEntryModel
                {
                    TimeGenerated = record.TimeCreated ?? DateTime.MinValue,
                    ApplicationName = app,
                    ProcessId = int.TryParse(pidStr, out int pid) ? pid : 0,
                    Protocol = proto,
                    Direction = direction,
                    Source = $"{srcIp}:{srcPort}",
                    Destination = $"{dstIp}:{dstPort}",
                    Result = result,
                    EventId = record.Id
                };
            }
            catch { return null; }
        }

        private string GetXmlValue(string xml, string tag)
        {
            var match = Regex.Match(xml, $"<{tag}>(.*?)</{tag}>");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private string GetProtocol(string code) => code switch
        {
            "6" => "TCP",
            "17" => "UDP",
            _ => code
        };

        private string GetDirection(string code) => code switch
        {
            "%%14592" => "Inbound",
            "%%14593" => "Outbound",
            _ => "Unknown"
        };

        private string GetResultFromId(int eventId) => eventId switch
        {
            5156 => "허용",
            5157 => "차단",
            5158 => "수신 허용",
            _ => "알 수 없음"
        };

        private void SetupLoadingTextAnimation()
        {
            loadingTextTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            loadingTextTimer.Tick += (s, e) =>
            {
                dotCount = (dotCount + 1) % (maxDots + 1);
                LoadingText.Text = baseText + new string('.', dotCount);
            };
        }

        private void ShowLoadingOverlay()
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            loadingTextTimer?.Start();
        }

        private void HideLoadingOverlay()
        {
            loadingTextTimer?.Stop();
            LoadingOverlay.Visibility = Visibility.Collapsed;
            LoadingText.Text = baseText;
        }

        private void StartRotation()
        {
            var rotateAnimation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = new Duration(TimeSpan.FromSeconds(2.0)),
                RepeatBehavior = RepeatBehavior.Forever
            };
            SpinnerRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, rotateAnimation);
        }

        private List<Point> CreateSpinnerPoints(double radius, double centerX, double centerY)
        {
            var points = new List<Point>();
            for (int i = 0; i < 8; i++)
            {
                double angle = i * 360.0 / 8 * Math.PI / 180.0;
                double x = centerX + radius * Math.Cos(angle) - 5;
                double y = centerY + radius * Math.Sin(angle) - 5;
                points.Add(new Point(x, y));
            }
            return points;
        }
        [SupportedOSPlatform("windows")]
        private void SidebarPrograms_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new InstalledPrograms());
        }
        [SupportedOSPlatform("windows")]
        private void SidebarModification_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Network());
        }
        [SupportedOSPlatform("windows")]
        private void SidebarLog_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Log());
        }
        [SupportedOSPlatform("windows")]
        private void SidebarRecovery_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Recovery());
        }
        [SupportedOSPlatform("windows")]
        private void NavigateToPage(Page page)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.NavigateToPage(page);
        }
    }
}
