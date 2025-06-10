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

    public class CurrentConnectionModel
    {
        public string Protocol { get; set; }
        public string LocalAddress { get; set; }
        public string ForeignAddress { get; set; }
        public string State { get; set; }
        public int? PID { get; set; }
    }

    internal static class NetworkLogConstants
    {
        public const string ProtocolTcp = "TCP";
        public const string ProtocolUdp = "UDP";
        public const string DirectionInbound = "Inbound";
        public const string DirectionOutbound = "Outbound";
        public const string ResultAllowed = "허용";
        public const string ResultBlocked = "차단";
        public const string ResultListenAllowed = "수신 허용";
        public const string Unknown = "알 수 없음";
    }

    [SupportedOSPlatform("windows")]
    public partial class Network : Page
    {
        private DispatcherTimer? loadingTextTimer;
        private int dotCount = 0;
        private const int maxDots = 3;
        private string baseText = "검사 중";
        private ObservableCollection<EventLogEntryModel> eventLogEntries = new ObservableCollection<EventLogEntryModel>();
        private bool auditPolicyJustEnabled = false;

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

            InitializeComponent(); // InitializeComponent를 먼저 호출

            // DatePicker 기본값 설정
            EndDatePicker.SelectedDate = DateTime.Today;

            if (!IsAuditPolicyEnabled())
            {
                EnableAuditPolicy();
                auditPolicyJustEnabled = true; // 플래그 설정
            }

            SetupLoadingTextAnimation();
            SpinnerItems.ItemsSource = CreateSpinnerPoints(40, 50, 50);
            StartRotation();

            if (auditPolicyJustEnabled) // UI 초기화 후 메시지 박스 표시
            {
                MessageBox.Show("TCP 연결 추적을 위해 감사 정책이 일시적으로 활성화되었습니다.\n이 설정은 시스템에 영향을 줄 수 있습니다.",
                                "감사 정책 활성화됨",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
            }
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

                await LoadFirewallEventLogsAsync();

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

        private async Task LoadFirewallEventLogsAsync()
        {
            string query = "*[System[(EventID=5156 or EventID=5157 or EventID=5158)]]";
            var logQuery = new EventLogQuery("Security", PathType.LogName, query) { ReverseDirection = true };
            var newEntries = new List<EventLogEntryModel>(); // 임시 컬렉션

            try
            {
                using var reader = new EventLogReader(logQuery);
                EventRecord record;
                while ((record = reader.ReadEvent()) != null)
                {
                    using (record)
                    {
                        string xml = record.ToXml();
                        var parsed = await Task.Run(() => ParseEventRecord(record, xml)); // 백그라운드에서 파싱
                        if (parsed != null)
                            newEntries.Add(parsed);
                    }
                }

                // UI 스레드에서 ObservableCollection 업데이트
                await Dispatcher.InvokeAsync(() =>
                {
                    eventLogEntries.Clear();
                    foreach (var entry in newEntries)
                    {
                        eventLogEntries.Add(entry);
                    }
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => MessageBox.Show($"이벤트 로그 수집 중 오류 발생: {ex.Message}", "로그 오류", MessageBoxButton.OK, MessageBoxImage.Error));
            }
        }

        private EventLogEntryModel? ParseEventRecord(EventRecord record, string xml)
        {
            try
            {
                string app = GetXmlValue(xml, "Application") ?? GetXmlValue(xml, "ApplicationName"); // Application 태그도 확인
                string pidStr = GetXmlValue(xml, "ProcessID") ?? GetXmlValue(xml, "ProcessId"); // ProcessID 태그도 확인
                string proto = GetProtocol(GetXmlValue(xml, "Protocol") ?? string.Empty);
                string srcIp = GetXmlValue(xml, "SourceAddress") ?? string.Empty;
                string srcPort = GetXmlValue(xml, "SourcePort") ?? string.Empty;
                string dstIp = GetXmlValue(xml, "DestAddress") ?? GetXmlValue(xml, "DestinationAddress"); // DestAddress 태그도 확인
                string dstPort = GetXmlValue(xml, "DestPort") ?? GetXmlValue(xml, "DestinationPort"); // DestPort 태그도 확인
                string direction = GetDirection(GetXmlValue(xml, "Direction") ?? string.Empty);
                string result = GetResultFromId(record.Id); // Id는 null이 될 수 없음

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

        private string? GetXmlValue(string xml, string tag)
        {
            var match = Regex.Match(xml, $"<{tag}>(.*?)</{tag}>");
            return match.Success ? match.Groups[1].Value : null;
        }

        private string GetProtocol(string code) => code switch
        {
            "6" => NetworkLogConstants.ProtocolTcp,
            "17" => NetworkLogConstants.ProtocolUdp,
            _ => string.IsNullOrEmpty(code) ? NetworkLogConstants.Unknown : code
        };

        private string GetDirection(string code) => code switch
        {
            "%%14592" => NetworkLogConstants.DirectionInbound,
            "%%14593" => NetworkLogConstants.DirectionOutbound,
            _ => NetworkLogConstants.Unknown
        };

        private string GetResultFromId(int eventId) => eventId switch
        {
            5156 => NetworkLogConstants.ResultAllowed,
            5157 => NetworkLogConstants.ResultBlocked,
            5158 => NetworkLogConstants.ResultListenAllowed,
            _ => NetworkLogConstants.Unknown
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

        // 현재 접속 상태 조회
        private async Task<List<CurrentConnectionModel>> GetCurrentConnectionsAsync()
        {
            var connections = new List<CurrentConnectionModel>();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "netstat",
                    Arguments = "-ano",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Verb = "runas" // 관리자 권한으로 실행
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    throw new Exception("netstat 프로세스를 시작할 수 없습니다.");
                }

                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"netstat 명령어 실행 실패 (종료 코드: {process.ExitCode})");
                }

                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                bool isHeaderFound = false;

                foreach (var line in lines)
                {
                    // 헤더 라인 찾기
                    if (line.Contains("Proto") && line.Contains("Local Address") && line.Contains("Foreign Address"))
                    {
                        isHeaderFound = true;
                        continue;
                    }

                    // 헤더를 찾은 후에만 데이터 처리
                    if (isHeaderFound)
                    {
                        var trimmedLine = line.Trim();
                        if (trimmedLine.StartsWith("TCP", StringComparison.OrdinalIgnoreCase) || 
                            trimmedLine.StartsWith("UDP", StringComparison.OrdinalIgnoreCase))
                        {
                            var tokens = System.Text.RegularExpressions.Regex.Split(trimmedLine, @"\s+");
                            if (tokens.Length >= 4)
                            {
                                try
                                {
                                    var model = new CurrentConnectionModel
                                    {
                                        Protocol = tokens[0],
                                        LocalAddress = tokens[1],
                                        ForeignAddress = tokens[2],
                                        State = tokens[0].Equals("UDP", StringComparison.OrdinalIgnoreCase) ? "" : tokens[3],
                                        PID = int.TryParse(tokens[^1], out int pid) ? pid : (int?)null
                                    };
                                    connections.Add(model);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"라인 파싱 오류: {line}, 오류: {ex.Message}");
                                }
                            }
                        }
                    }
                }

                if (!isHeaderFound)
                {
                    throw new Exception("netstat 출력에서 헤더를 찾을 수 없습니다.");
                }

                if (connections.Count == 0)
                {
                    Debug.WriteLine("현재 활성화된 네트워크 연결이 없습니다.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"현재 접속 상태 조회 중 오류 발생: {ex.Message}");
                MessageBox.Show($"현재 접속 상태 조회 중 오류가 발생했습니다: {ex.Message}", 
                              "오류", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
            }
            return connections;
        }

        // 기간별 조회 버튼 클릭 이벤트 핸들러 (XAML에서 연결 필요)
        private async void BtnPeriodSearch_Click(object sender, RoutedEventArgs e)
        {
            DateTime? start = StartDatePicker.SelectedDate;
            DateTime? end = EndDatePicker.SelectedDate?.AddDays(1).AddSeconds(-1); // 종료일의 23:59:59까지 포함

            if (start == null || end == null)
            {
                MessageBox.Show("시작일과 종료일을 모두 선택하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await LoadFirewallEventLogsAsync(start.Value, end.Value);
        }

        // 기간별 이벤트 로그 조회 (오버로드)
        private async Task LoadFirewallEventLogsAsync(DateTime start, DateTime end)
        {
            string query = $"*[System[(EventID=5156 or EventID=5157 or EventID=5158) and TimeCreated[@SystemTime>='{start.ToUniversalTime():o}' and @SystemTime<='{end.ToUniversalTime():o}']]]";
            var logQuery = new EventLogQuery("Security", PathType.LogName, query) { ReverseDirection = true };
            var newEntries = new List<EventLogEntryModel>();
            try
            {
                using var reader = new EventLogReader(logQuery);
                EventRecord record;
                while ((record = reader.ReadEvent()) != null)
                {
                    using (record)
                    {
                        string xml = record.ToXml();
                        var parsed = await Task.Run(() => ParseEventRecord(record, xml));
                        if (parsed != null)
                            newEntries.Add(parsed);
                    }
                }
                await Dispatcher.InvokeAsync(() =>
                {
                    eventLogEntries.Clear();
                    foreach (var entry in newEntries)
                        eventLogEntries.Add(entry);
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => MessageBox.Show($"이벤트 로그 수집 중 오류 발생: {ex.Message}", "로그 오류", MessageBoxButton.OK, MessageBoxImage.Error));
            }
        }

        // 현재 접속 상태 보기 버튼 클릭 이벤트 핸들러 (XAML에서 연결 필요)
        private async void BtnCurrentConnections_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                ShowLoadingOverlay();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                var connections = await GetCurrentConnectionsAsync();
                Dispatcher.Invoke(() =>
                {
                    currentConnectionsDataGrid.ItemsSource = null;
                    currentConnectionsDataGrid.ItemsSource = connections;
                    if (connections.Count == 0)
                    {
                        MessageBox.Show("현재 네트워크 연결이 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"현재 접속 상태 조회 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                HideLoadingOverlay();
            }
        }
    }
}

