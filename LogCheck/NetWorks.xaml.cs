using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LogCheck.Models;
using LogCheck.Services;
using MaterialDesignThemes.Wpf;
using Microsoft.VisualBasic.Logging;
using SharpPcap;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Threading;
using WindowsSentinel;
using WindowsSentinel.Models;
using Application = System.Windows.Application;
using Cursors = System.Windows.Input.Cursors;
using MessageBox = System.Windows.MessageBox;
using Point = System.Windows.Point;

namespace LogCheck
{

    // 파일 크기를 동적 단위로 변환하는 ValueConverter
    [SupportedOSPlatform("windows")]
    public class FileSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long bytes)
            {
                return NetWorks.FormatFileSize(bytes);
            }
            return "0 B";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class EventLogEntryModel
    {
        public DateTime TimeGenerated { get; set; }
        public string ApplicationName { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public string Protocol { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public int EventId { get; set; }
    }

    [SupportedOSPlatform("windows")]
    public partial class NetWorks : Page, INavigable
    {
        private ToggleButton _selectedButton;
        private DispatcherTimer? loadingTextTimer;
        private int dotCount = 0;
        private const int maxDots = 3;
        private string baseText = "검사 중";
        private ObservableCollection<EventLogEntryModel> eventLogEntries = new ObservableCollection<EventLogEntryModel>();
        private readonly ObservableCollection<PacketInfo> _packets = new ObservableCollection<PacketInfo>();
        private ICollectionView _packetsView;
        private Models.PacketCapture? _packetCapture;
        private bool _isCapturing;
        private readonly ObservableCollection<NetworkUsageRecord> _historyRecords = new ObservableCollection<NetworkUsageRecord>();

        // 무한 팝업 방지를 위한 플래그들
        private bool _isInitializing = false;
        private bool _showCompletionMessage = false;

        // 보안 분석 관련 변수들
        private SecurityAnalyzer _securityAnalyzer = new SecurityAnalyzer();
        private readonly ObservableCollection<SecurityAlert> _securityAlerts = new ObservableCollection<SecurityAlert>();

        // 대안 모니터링 관련 변수들
        private DispatcherTimer? _alternativeMonitoringTimer;
        private bool _isAlternativeMonitoring = false;

        // 실시간 이벤트 로그 모니터링 관련 변수들
        private EventLogWatcher? _eventLogWatcher;
        private readonly Dictionary<string, DateTime> _lastSeenConnections = new Dictionary<string, DateTime>();
        private readonly object _connectionLock = new object();

        public NetWorks()
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
                LogHelper.LogInfo("TCP 연결 추적을 위해 감사 정책이 일시적으로 활성화되었습니다.");
            }

            InitializeComponent();

            SideNetworksButton.IsChecked = true;

            // Loaded 이벤트 핸들러 추가 (차트 초기화 보장)
            this.Loaded += Network_Loaded;

            SetupLoadingTextAnimation();
            StartRotation();
            PacketDataGrid.ItemsSource = _packets;
            _packetsView = CollectionViewSource.GetDefaultView(_packets);
            PacketDataGrid.ItemsSource = _packetsView;
            HistoryDataGrid.ItemsSource = _historyRecords;

            LoadNetworkInterfaces();
            InitializeSpinner();
            InitializeHistoryTab();
            // InitializeSecurityTab(); // 탭이 선택될 때까지 지연
        }

        // 페이지 로드 완료 시 초기화
        private void Network_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LogHelper.LogInfo("Network 페이지 로드 완료");

                // 테스트 데이터 생성
                if (_historyRecords.Count == 0)
                {
                    GenerateTestData();
                }

                LogHelper.LogInfo("Network 페이지 로드 완료 초기화 완료");
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"Network 페이지 로드 이벤트 처리 중 오류: {ex.Message}");
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

                using Process? process = Process.Start(psi);
                if (process == null) return false;
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

                using (Process? process = Process.Start(psi))
                {
                    process?.WaitForExit();
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
                    PacketDataGrid.ItemsSource = null;
                    PacketDataGrid.ItemsSource = eventLogEntries;

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

            // SpinnerRotate가 XAML에 정의되어 있지 않은 경우 스킵
            try
            {
                var spinnerRotate = this.FindName("SpinnerRotate") as RotateTransform;
                spinnerRotate?.BeginAnimation(RotateTransform.AngleProperty, rotateAnimation);
            }
            catch
            {
                // SpinnerRotate를 찾을 수 없는 경우 무시
            }
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

        private void LoadNetworkInterfaces()
        {
            try
            {
                NetworkInterfaceComboBox.Items.Clear();

                // 1. Npcap 설치 및 서비스 상태 확인
                if (!CheckNpcapInstallation())
                {
                    return;
                }

                var devices = CaptureDeviceList.Instance;

                // 디버깅: 발견된 장치 수 로그
                LogHelper.LogInfo($"발견된 캡처 장치 수: {devices.Count}");

                // 2. 사용 가능한 캡처 장치가 있는지 확인
                if (devices.Count == 0)
                {
                    MessageBox.Show("사용 가능한 네트워크 캡처 장치가 없습니다.\n" +
                                   "Npcap이 올바르게 설치되었는지 확인해주세요.\n\n" +
                                   "해결 방법:\n" +
                                   "1. Npcap을 재설치하세요\n" +
                                   "2. 'WinPcap API 호환 모드'로 설치하세요\n" +
                                   "3. 관리자 권한으로 실행하세요",
                                   "Npcap 오류",
                                   MessageBoxButton.OK,
                                   MessageBoxImage.Warning);
                    return;
                }

                // 디버깅: 모든 장치 정보 로그
                for (int i = 0; i < devices.Count; i++)
                {
                    var device = devices[i];
                    LogHelper.LogInfo($"장치 {i}: {device.Description} - {device.Name}");
                }

                var activeInterfaces = GetActiveNetworkInterfaces();
                var interfaceItems = new List<NetworkInterfaceItem>();

                foreach (var device in devices)
                {
                    // 디버깅: 각 장치 처리 과정 로그
                    LogHelper.LogInfo($"처리 중인 장치: {device.Description}");

                    // WAN Miniport, Loopback 등 가상 인터페이스 필터링
                    if (IsVirtualInterface(device.Description))
                    {
                        LogHelper.LogInfo($"가상 인터페이스로 필터링됨: {device.Description}");
                        continue;
                    }

                    // 3. 각 어댑터의 접근 가능성 미리 테스트
                    bool isAccessible = TestDeviceAccess(device);
                    LogHelper.LogInfo($"장치 접근 가능성: {device.Description} = {isAccessible}");

                    var interfaceInfo = activeInterfaces.FirstOrDefault(ni =>
                        device.Description.Contains(ni.Name) ||
                        ni.Description.Contains(device.Description) ||
                        device.Name.Contains(ni.Id));

                    var item = new NetworkInterfaceItem
                    {
                        Device = device,
                        Name = device.Description,
                        IsActive = interfaceInfo?.OperationalStatus == OperationalStatus.Up,
                        HasIPAddress = interfaceInfo?.GetIPProperties().UnicastAddresses.Any(ip =>
                            ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                            !IPAddress.IsLoopback(ip.Address)) ?? false,
                        Speed = interfaceInfo?.Speed ?? 0,
                        InterfaceType = interfaceInfo?.NetworkInterfaceType ?? NetworkInterfaceType.Unknown
                    };

                    // 우선순위 계산 (활성 상태, IP 주소 보유, 이더넷/WiFi 우선)
                    item.Priority = CalculatePriority(item);

                    // 접근 불가능한 어댑터는 우선순위를 낮추지만 완전히 비활성화하지는 않음
                    if (!isAccessible)
                    {
                        item.Priority -= 20; // 100에서 20으로 완화
                        LogHelper.LogInfo($"장치 접근 불가하지만 표시함: {device.Description}");
                    }

                    LogHelper.LogInfo($"장치 상태: {device.Description} - 활성: {item.IsActive}, IP: {item.HasIPAddress}, 우선순위: {item.Priority}");

                    interfaceItems.Add(item);
                }

                // 디버깅: 최종 인터페이스 상태 로그
                var activeCount = interfaceItems.Count(i => i.IsActive);
                var accessibleCount = interfaceItems.Count(i => i.Priority >= 0);
                LogHelper.LogInfo($"총 인터페이스: {interfaceItems.Count}, 활성: {activeCount}, 접근가능: {accessibleCount}");

                // 4. 접근 가능한 인터페이스가 없는 경우 경고 (임시 비활성화)
                if (!interfaceItems.Any(i => i.Priority >= 0) && interfaceItems.Count == 0)
                {
                    // 완전히 장치가 없는 경우만 경고 표시
                    MessageBox.Show("네트워크 캡처 장치를 찾을 수 없습니다.\n\n" +
                                   "Npcap이 올바르게 설치되었는지 확인해주세요.",
                                   "네트워크 장치 없음",
                                   MessageBoxButton.OK,
                                   MessageBoxImage.Warning);
                }

                // 우선순위 순으로 정렬
                var sortedInterfaces = interfaceItems
                    .OrderByDescending(i => i.Priority)
                    .ThenByDescending(i => i.Speed)
                    .ToList();

                foreach (var item in sortedInterfaces)
                {
                    var displayName = FormatInterfaceName(item);
                    var comboItem = new ComboBoxItem
                    {
                        Content = displayName,
                        Tag = item.Device,
                        ToolTip = GetInterfaceTooltip(item)
                    };

                    // 활성 인터페이스는 굵게 표시
                    if (item.IsActive && item.HasIPAddress)
                    {
                        comboItem.FontWeight = FontWeights.Bold;
                        comboItem.Foreground = new SolidColorBrush(Colors.DarkGreen);
                    }
                    // 접근 불가능한 인터페이스는 회색으로 표시하지만 비활성화하지 않음
                    else if (item.Priority < 0)
                    {
                        comboItem.Foreground = new SolidColorBrush(Colors.Orange);
                        // comboItem.IsEnabled = false; // 제거하여 선택 가능하게 함
                    }

                    NetworkInterfaceComboBox.Items.Add(comboItem);
                }

                // 가장 우선순위가 높은 활성 인터페이스를 기본 선택
                var defaultInterface = sortedInterfaces.FirstOrDefault(i => i.IsActive && i.HasIPAddress);
                if (defaultInterface != null)
                {
                    var defaultIndex = sortedInterfaces.IndexOf(defaultInterface);
                    NetworkInterfaceComboBox.SelectedIndex = defaultIndex;
                }
                else if (NetworkInterfaceComboBox.Items.Count > 0)
                {
                    NetworkInterfaceComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"네트워크 인터페이스를 불러오는 중 오류가 발생했습니다:\n{ex.Message}\n\n" +
                               "상세 정보:\n{ex.StackTrace}",
                              "오류",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
        }

        private List<NetworkInterface> GetActiveNetworkInterfaces()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                .ToList();
        }

        private bool IsVirtualInterface(string description)
        {
            var virtualKeywords = new[]
            {
                "WAN Miniport",
                "Loopback",
                "Microsoft KM-TEST",
                "Teredo",
                "ISATAP",
                "6to4",
                "Virtual",
                "VMware",
                "VirtualBox",
                "Hyper-V",
                "TAP-Windows",
                "OpenVPN",
                "Npcap Loopback"
            };

            return virtualKeywords.Any(keyword =>
                description.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private int CalculatePriority(NetworkInterfaceItem item)
        {
            int priority = 0;

            // 활성 상태 (+50점)
            if (item.IsActive) priority += 50;

            // IP 주소 보유 (+30점)
            if (item.HasIPAddress) priority += 30;

            // 인터페이스 타입별 점수
            priority += item.InterfaceType switch
            {
                NetworkInterfaceType.Ethernet => 20,
                NetworkInterfaceType.Wireless80211 => 15,
                NetworkInterfaceType.GigabitEthernet => 25,
                NetworkInterfaceType.FastEthernetT => 18,
                NetworkInterfaceType.Ppp => 5,
                _ => 0
            };

            // 속도별 점수 (Mbps 기준)
            if (item.Speed > 0)
            {
                priority += item.Speed switch
                {
                    >= 1000000000 => 10, // 1Gbps 이상
                    >= 100000000 => 8,   // 100Mbps 이상
                    >= 10000000 => 5,    // 10Mbps 이상
                    _ => 2
                };
            }

            return priority;
        }

        private string FormatInterfaceName(NetworkInterfaceItem item)
        {
            var name = item.Name;

            // 긴 이름 축약
            if (name.Length > 50)
            {
                name = name.Substring(0, 47) + "...";
            }

            // 상태 표시
            var status = "";
            if (item.IsActive && item.HasIPAddress)
            {
                status = " ✓ [활성]";
            }
            else if (item.IsActive)
            {
                status = " [연결됨]";
            }
            else
            {
                status = " [비활성]";
            }

            // 인터페이스 타입 표시
            var typeIcon = item.InterfaceType switch
            {
                NetworkInterfaceType.Ethernet => " 🔌",
                NetworkInterfaceType.Wireless80211 => " 📶",
                NetworkInterfaceType.GigabitEthernet => " ⚡",
                _ => ""
            };

            return $"{name}{typeIcon}{status}";
        }

        private string GetInterfaceTooltip(NetworkInterfaceItem item)
        {
            var tooltip = $"인터페이스: {item.Name}\n";
            tooltip += $"상태: {(item.IsActive ? "활성" : "비활성")}\n";
            tooltip += $"IP 주소: {(item.HasIPAddress ? "있음" : "없음")}\n";
            tooltip += $"타입: {GetInterfaceTypeDescription(item.InterfaceType)}\n";

            if (item.Speed > 0)
            {
                tooltip += $"속도: {FormatSpeed(item.Speed)}";
            }

            return tooltip;
        }

        private bool CheckNpcapInstallation()
        {
            try
            {
                // 1. Npcap 서비스 상태 확인 (최신 버전은 npcap 서비스만 사용)
                var npcapServiceStatus = CheckServiceStatus("npcap");

                if (npcapServiceStatus == "NotFound")
                {
                    MessageBox.Show("Npcap이 설치되지 않았습니다.\n\n" +
                                   "Npcap을 설치해주세요:\n" +
                                   "1. https://npcap.com 에서 다운로드\n" +
                                   "2. 'WinPcap API 호환 모드' 체크하여 설치\n" +
                                   "3. 설치 후 재부팅",
                                   "Npcap 미설치",
                                   MessageBoxButton.OK,
                                   MessageBoxImage.Error);
                    return false;
                }

                if (npcapServiceStatus == "Stopped")
                {
                    var result = MessageBox.Show("Npcap 서비스가 중지되어 있습니다.\n" +
                                                "서비스를 시작하시겠습니까?\n\n" +
                                                "수동으로 시작하려면:\n" +
                                                "1. 'services.msc' 실행\n" +
                                                "2. 'Npcap Packet Capture' 서비스 시작",
                                                "Npcap 서비스 중지",
                                                MessageBoxButton.YesNo,
                                                MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        StartNpcapService();
                    }
                    else
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Npcap 상태 확인 중 오류: {ex.Message}",
                               "오류",
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);
                return false;
            }
        }

        private string CheckServiceStatus(string serviceName)
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = "sc";
                process.StartInfo.Arguments = $"query {serviceName}";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    return "NotFound";
                }

                if (output.Contains("RUNNING"))
                    return "Running";
                else if (output.Contains("STOPPED"))
                    return "Stopped";
                else
                    return "Unknown";
            }
            catch
            {
                return "Error";
            }
        }

        private void StartNpcapService()
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = "net";
                process.StartInfo.Arguments = "start npcap";
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.Verb = "runas"; // 관리자 권한으로 실행
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                process.WaitForExit();

                MessageBox.Show("Npcap 서비스를 시작했습니다.",
                               "서비스 시작",
                               MessageBoxButton.OK,
                               MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Npcap 서비스 시작 실패: {ex.Message}\n\n" +
                               "수동으로 시작해주세요:\n" +
                               "1. 'services.msc' 실행\n" +
                               "2. 'Npcap Packet Capture' 서비스 시작",
                               "서비스 시작 실패",
                               MessageBoxButton.OK,
                               MessageBoxImage.Warning);
            }
        }

        private bool TestDeviceAccess(ICaptureDevice device)
        {
            try
            {
                // 기본적으로 장치가 사용 가능하다고 가정
                // 실제 열기는 캡처 시작 시에만 수행하여 성능 향상
                if (string.IsNullOrEmpty(device.Description))
                {
                    return false;
                }

                // 가상 인터페이스나 특수 인터페이스 필터링
                var description = device.Description.ToLower();
                if (description.Contains("loopback") ||
                    description.Contains("microsoft") ||
                    description.Contains("teredo") ||
                    description.Contains("isatap"))
                {
                    LogHelper.LogInfo($"특수 인터페이스 제외: {device.Description}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"장치 정보 확인 실패 ({device.Description}): {ex.Message}");
                return false;
            }
        }

        private string GetInterfaceTypeDescription(NetworkInterfaceType type)
        {
            return type switch
            {
                NetworkInterfaceType.Ethernet => "이더넷",
                NetworkInterfaceType.Wireless80211 => "무선 LAN (WiFi)",
                NetworkInterfaceType.GigabitEthernet => "기가비트 이더넷",
                NetworkInterfaceType.FastEthernetT => "고속 이더넷",
                NetworkInterfaceType.Ppp => "PPP 연결",
                NetworkInterfaceType.Loopback => "루프백",
                _ => type.ToString()
            };
        }

        private string FormatSpeed(long speed)
        {
            if (speed >= 1000000000)
                return $"{speed / 1000000000.0:F1} Gbps";
            else if (speed >= 1000000)
                return $"{speed / 1000000.0:F1} Mbps";
            else if (speed >= 1000)
                return $"{speed / 1000.0:F1} Kbps";
            else
                return $"{speed} bps";
        }

        // 헬퍼 클래스
        public class NetworkInterfaceItem
        {
            public ICaptureDevice Device { get; set; }
            public string Name { get; set; } = string.Empty;
            public bool IsActive { get; set; }
            public bool HasIPAddress { get; set; }
            public long Speed { get; set; }
            public NetworkInterfaceType InterfaceType { get; set; }
            public int Priority { get; set; }
        }

        private void InitializeSpinner()
        {
            try
            {
                // SpinnerItems가 존재하는지 확인
                if (SpinnerItems != null)
                {
                    // 기존 아이템들 제거
                    SpinnerItems.Items.Clear();

                    // 스피너 아이템 생성
                    for (int i = 0; i < 8; i++)
                    {
                        var angle = i * 45;
                        var x = 25 + 20 * Math.Cos(angle * Math.PI / 180);
                        var y = 25 + 20 * Math.Sin(angle * Math.PI / 180);

                        var ellipse = new System.Windows.Shapes.Ellipse
                        {
                            Width = 6,
                            Height = 6,
                            Fill = System.Windows.Media.Brushes.White,
                            Opacity = 1.0 - (i * 0.1)
                        };

                        Canvas.SetLeft(ellipse, x - 3);
                        Canvas.SetTop(ellipse, y - 3);

                        SpinnerItems.Items.Add(ellipse);
                    }
                }

                // 회전 애니메이션 설정
                var animation = new DoubleAnimation
                {
                    From = 0,
                    To = 360,
                    Duration = TimeSpan.FromSeconds(1),
                    RepeatBehavior = RepeatBehavior.Forever
                };

                // SpinnerRotate 리소스를 찾아서 애니메이션 적용
                try
                {
                    var spinnerRotate = this.FindResource("SpinnerRotate") as RotateTransform;
                    spinnerRotate?.BeginAnimation(RotateTransform.AngleProperty, animation);
                }
                catch
                {
                    // SpinnerRotate 리소스를 찾을 수 없는 경우 무시
                }
            }
            catch (InvalidOperationException)
            {
                // ItemsSource가 설정되어 있는 경우, 대신 ItemsSource를 사용
                try
                {
                    var spinnerPoints = CreateSpinnerPoints(40, 50, 50);
                    SpinnerItems.ItemsSource = spinnerPoints;
                }
                catch
                {
                    // 스피너 초기화 실패 시 무시
                }
            }
            catch
            {
                // 기타 예외 발생 시 무시
            }
        }

        private async void StartCapture_Click(object sender, RoutedEventArgs e)
        {
            if (NetworkInterfaceComboBox.SelectedItem == null)
            {
                MessageBox.Show("네트워크 인터페이스를 선택해주세요.",
                              "알림",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
                return;
            }

            try
            {
                var selectedComboItem = NetworkInterfaceComboBox.SelectedItem as ComboBoxItem;
                var selectedDevice = selectedComboItem?.Tag as ICaptureDevice;
                var interfaceName = selectedComboItem.Content.ToString() ?? "Unknown Interface";

                if (selectedDevice == null)
                {
                    MessageBox.Show("선택된 네트워크 인터페이스가 유효하지 않습니다.",
                                  "오류",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Error);
                    return;
                }

                LogHelper.LogInfo($"네트워크 모니터링 시작 시도: {interfaceName}");

                // 1단계: 패킷 캡처 시도
                bool packetCaptureSuccess = await TryStartPacketCapture(selectedDevice, interfaceName);

                if (!packetCaptureSuccess)
                {
                    // 2단계: 대안 모니터링 방법 사용
                    var result = MessageBox.Show(
                        $"패킷 캡처를 시작할 수 없습니다.\n\n" +
                        $"대신 다음 방법으로 네트워크 모니터링을 계속하시겠습니까?\n\n" +
                        $"✅ Windows 이벤트 로그 분석\n" +
                        $"✅ 활성 연결 상태 모니터링 (netstat)\n" +
                        $"✅ 네트워크 통계 수집 (WMI)\n" +
                        $"✅ 실시간 연결 추적\n\n" +
                        $"이 방법들은 보안 설정을 변경하지 않고도\n" +
                        $"효과적인 네트워크 모니터링을 제공합니다.",
                        "대안 모니터링 방법 사용",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        await StartAlternativeMonitoring(interfaceName);
                    }
                    return;
                }

                // 패킷 캡처 성공시 UI 업데이트
                UpdateCaptureUI(true, interfaceName);
                LogHelper.LogInfo($"패킷 캡처가 시작되었습니다: {interfaceName}");
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"네트워크 모니터링 시작 실패: {ex.Message}");

                // 예외 발생시에도 대안 방법 제공
                var result = MessageBox.Show(
                    $"네트워크 모니터링 시작 중 오류가 발생했습니다:\n\n{ex.Message}\n\n" +
                    $"대안 모니터링 방법을 사용하시겠습니까?\n" +
                    $"(보안 설정 변경 없이 네트워크 활동 추적 가능)",
                    "대안 모니터링 방법",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    await StartAlternativeMonitoring("시스템 전체");
                }
            }
        }

        private async Task<bool> TryStartPacketCapture(ICaptureDevice device, string interfaceName)
        {
            try
            {
                // 장치 접근 테스트
                if (!TestDeviceAccessForCapture(device))
                {
                    LogHelper.LogInfo($"장치 접근 테스트 실패: {interfaceName}");
                    return false;
                }

                // PacketCapture 인스턴스 생성 시도
                _packetCapture = new Models.PacketCapture(device, interfaceName);

                // 이벤트 핸들러 설정
                _packetCapture.PacketCaptured += (s, packet) =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _packets.Insert(0, packet); // 상단에 추가
                        if (_packets.Count > 1000)
                        {
                            _packets.RemoveAt(_packets.Count - 1); // 하단에서 제거
                        }
                        UpdateCaptureStatus();
                    });
                };

                _packetCapture.ErrorOccurred += (s, error) =>
                {
                    Application.Current.Dispatcher.Invoke(async () =>
                    {
                        LogHelper.LogError($"패킷 캡처 오류: {error}");
                        StopCapture();

                        // 오류 발생시 대안 모니터링으로 전환
                        var result = MessageBox.Show(
                            $"패킷 캡처 중 오류가 발생했습니다:\n{error}\n\n" +
                            $"대안 모니터링 방법으로 전환하시겠습니까?",
                            "대안 모니터링 전환",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (result == MessageBoxResult.Yes)
                        {
                            await StartAlternativeMonitoring(interfaceName);
                        }
                    });
                };

                // 캡처 시작
                await Task.Run(() => _packetCapture.StartCapture());
                _isCapturing = true;

                return true;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"패킷 캡처 시작 실패: {ex.Message}");

                // 리소스 정리
                if (_packetCapture != null)
                {
                    try
                    {
                        _packetCapture.Dispose();
                        _packetCapture = null;
                    }
                    catch { }
                }

                return false;
            }
        }

        private bool TestDeviceAccessForCapture(ICaptureDevice device)
        {
            // 기본적인 장치 정보 확인
            if (device == null || string.IsNullOrEmpty(device.Description))
            {
                return false;
            }

            // 루프백 인터페이스는 항상 접근 가능하다고 가정
            if (device.Description.ToLower().Contains("loopback"))
            {
                return true;
            }

            // 가상 인터페이스 필터링
            if (IsVirtualInterface(device.Description))
            {
                return false;
            }

            // 실제 장치 접근 테스트 (더 관대한 조건)
            try
            {
                // 더 긴 타임아웃으로 테스트 (500ms)
                device.Open(DeviceMode.Normal, 500);
                device.Close();
                return true;
            }
            catch
            {
                try
                {
                    // Promiscuous 모드로 재시도 (더 긴 타임아웃)
                    device.Open(DeviceMode.Promiscuous, 500);
                    device.Close();
                    return true;
                }
                catch
                {
                    // 테스트 실패해도 경고만 하고 사용자가 선택하도록 함
                    LogHelper.LogInfo($"장치 접근 테스트 실패 (하지만 시도 가능): {device.Description}");

                    // Realtek Gaming 어댑터의 경우 특별 처리
                    if (device.Description.ToLower().Contains("realtek") &&
                        (device.Description.ToLower().Contains("gaming") || device.Description.ToLower().Contains("2.5g")))
                    {
                        // Realtek Gaming 어댑터는 테스트 실패해도 접근 가능하다고 가정
                        LogHelper.LogInfo($"Realtek Gaming 어댑터 감지 - 접근 가능하다고 가정: {device.Description}");
                        return true;
                    }

                    return false;
                }
            }
        }

        private void UpdateCaptureUI(bool isCapturing, string interfaceName, bool isAlternativeMode = false)
        {
            if (isCapturing)
            {
                StartCaptureButton.Visibility = Visibility.Collapsed;
                StopCaptureButton.Visibility = Visibility.Visible;
                NetworkInterfaceComboBox.IsEnabled = false;

                // 상태 표시 업데이트
                var statusText = isAlternativeMode
                    ? $"대안 모니터링 중: {interfaceName}"
                    : $"패킷 캡처 중: {interfaceName}";

                ShowCaptureStatus(statusText);

                // 상태 표시를 위한 추가 UI 업데이트
                if (isAlternativeMode)
                {
                    // 대안 모니터링임을 시각적으로 표시
                    LoadingText.Text = "대안 모니터링 활성";
                    LoadingText.Foreground = new SolidColorBrush(Colors.Orange);
                }

                if (CaptureStatusText != null)
                {
                    CaptureStatusText.Text = statusText;
                }
            }
            else
            {
                StartCaptureButton.Visibility = Visibility.Visible;
                StopCaptureButton.Visibility = Visibility.Collapsed;
                NetworkInterfaceComboBox.IsEnabled = true;
                LoadingOverlay.Visibility = Visibility.Collapsed;

                // 기본 상태로 복원
                LoadingText.Text = baseText;
                LoadingText.Foreground = new SolidColorBrush(Colors.White);
            }
        }

        private void ShowCaptureStatus(string interfaceName)
        {
            // 상태 표시 UI 업데이트 (필요시 구현)
            this.Title = $"Network - {interfaceName}";
        }

        private void UpdateCaptureStatus()
        {
            // 실시간 패킷 수 표시 등 (필요시 구현)
        }

        private void StopCapture_Click(object sender, RoutedEventArgs e)
        {
            StopCapture();
        }

        private void StopCapture()
        {
            try
            {
                // 패킷 캡처 정지
                if (_packetCapture != null)
                {
                    _packetCapture.StopCapture();
                    _packetCapture.Dispose();
                    _packetCapture = null;
                }

                // 실시간 이벤트 로그 모니터링 정지
                if (_eventLogWatcher != null)
                {
                    _eventLogWatcher.Enabled = false;
                    _eventLogWatcher.EventRecordWritten -= OnNewNetworkEvent;
                    _eventLogWatcher.Dispose();
                    _eventLogWatcher = null;
                    LogHelper.LogInfo("실시간 이벤트 로그 모니터링 정지됨");
                }

                // 대안 모니터링 정지
                if (_alternativeMonitoringTimer != null)
                {
                    _alternativeMonitoringTimer.Stop();
                    _alternativeMonitoringTimer = null;
                }

                // 연결 기록 정리
                lock (_connectionLock)
                {
                    _lastSeenConnections.Clear();
                }

                _isCapturing = false;
                _isAlternativeMonitoring = false;

                UpdateCaptureUI(false, "");

                LogHelper.LogInfo("네트워크 모니터링이 정지되었습니다.");
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"모니터링 정지 중 오류: {ex.Message}");
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            if (_isCapturing)
            {
                MessageBox.Show("패킷 캡처가 진행 중일 때는 목록을 지울 수 없습니다.",
                              "알림",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
                return;
            }

            _packets.Clear();
        }

        private bool PacketFilter(object? obj)
        {
            if (obj is not PacketInfo p) return false;

            var filterText = FilterTextBox.Text.ToLower();
            var protocolFilter = (ProtocolFilterComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();

            bool matchesText = string.IsNullOrEmpty(filterText) ||
                               p.SourceIP.ToLower().Contains(filterText) ||
                               p.DestinationIP.ToLower().Contains(filterText) ||
                               p.Protocol.ToLower().Contains(filterText);

            bool matchesProto = protocolFilter == "모든 프로토콜" || p.Protocol == protocolFilter;

            return matchesText && matchesProto;
        }

        private void Filter_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_packetsView == null) return;
            _packetsView.Filter = PacketFilter;
            _packetsView.Refresh();
        }

        private void ProtocolFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Filter_TextChanged(sender, null);
        }

        public void OnNavigatedTo()
        {
            // 페이지가 활성화될 때 호출됨
            try
            {
                LogHelper.LogInfo("Network 페이지 활성화됨");

                // 히스토리 탭 초기화
                InitializeHistoryTab();

                // 보안 탭 초기화
                InitializeSecurityTab();

                LogHelper.LogInfo("Network 페이지 초기화 완료");
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"Network 페이지 활성화 중 오류: {ex.Message}");
            }
        }

        public void OnNavigatedFrom()
        {
            StopCapture();
        }

        // 사용 기록 관련 메서드들
        private void InitializeHistoryTab()
        {
            try
            {
                _isInitializing = true; // 초기화 시작

                // 날짜 필터 초기화
                StartDatePicker.SelectedDate = DateTime.Today.AddDays(-7);
                EndDatePicker.SelectedDate = DateTime.Today;

                // 프로토콜 필터 초기화
                if (HistoryProtocolFilterComboBox.Items.Count > 0)
                {
                    HistoryProtocolFilterComboBox.SelectedIndex = 0;
                }

                // 방향 필터 초기화
                if (DirectionFilterComboBox.Items.Count > 0)
                {
                    DirectionFilterComboBox.SelectedIndex = 0;
                }

                // DataGrid ItemsSource 설정
                HistoryDataGrid.ItemsSource = _historyRecords;

                // 인터페이스 필터 로드
                LoadInterfaceFilter();

                // 초기 데이터가 없으면 테스트 데이터 생성
                if (_historyRecords.Count == 0)
                {
                    LogHelper.LogInfo("초기 데이터가 없어서 테스트 데이터 생성");
                    GenerateTestData();
                }

                _isInitializing = false; // 초기화 완료
                LogHelper.LogInfo("히스토리 탭 초기화 완료");
            }
            catch (Exception ex)
            {
                _isInitializing = false;
                LogHelper.LogError($"히스토리 탭 초기화 중 오류: {ex.Message}");


            }
        }

        private void LoadInterfaceFilter()
        {
            try
            {
                InterfaceFilterComboBox.Items.Clear();
                InterfaceFilterComboBox.Items.Add(new ComboBoxItem { Content = "모든 인터페이스", Tag = "" });

                var devices = CaptureDeviceList.Instance;
                foreach (var device in devices)
                {
                    if (!IsVirtualInterface(device.Description))
                    {
                        var item = new ComboBoxItem
                        {
                            Content = device.Description,
                            Tag = device.Description
                        };
                        InterfaceFilterComboBox.Items.Add(item);
                    }
                }

                InterfaceFilterComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"인터페이스 필터 로드 실패: {ex.Message}");
            }
        }

        private async void LoadNetworkHistory()
        {
            try
            {
                LogHelper.LogInfo("LoadNetworkHistory 메서드 시작");

                var startDate = StartDatePicker.SelectedDate ?? DateTime.Today.AddDays(-7);
                var endDate = EndDatePicker.SelectedDate ?? DateTime.Today;
                var selectedInterface = "";

                if (InterfaceFilterComboBox.SelectedItem is ComboBoxItem selectedItem)
                {
                    selectedInterface = selectedItem.Tag?.ToString() ?? "";
                }

                LogHelper.LogInfo($"사용 기록 조회 시작 - 시작일: {startDate:yyyy-MM-dd}, 종료일: {endDate:yyyy-MM-dd}, 인터페이스: '{selectedInterface}'");

                // 기존 데이터 백업 (실제 데이터가 없을 때 복원용)
                var backupRecords = _historyRecords.ToList();

                _historyRecords.Clear();
                LogHelper.LogInfo("기존 기록 클리어 완료");

                // 1. Windows 이벤트 로그에서 네트워크 연결 기록 조회
                LogHelper.LogInfo("이벤트 로그 조회 시작");
                var eventLogRecords = await GetNetworkRecordsFromEventLog(startDate, endDate);
                LogHelper.LogInfo($"이벤트 로그 조회 완료: {eventLogRecords.Count}개");

                // 2. netstat 명령어로 현재 활성 연결 조회
                LogHelper.LogInfo("netstat 조회 시작");
                var netstatRecords = await GetCurrentNetworkConnections();
                LogHelper.LogInfo($"netstat 조회 완료: {netstatRecords.Count}개");

                // 3. WMI로 네트워크 통계 조회
                LogHelper.LogInfo("WMI 조회 시작");
                var wmiRecords = await GetNetworkStatisticsFromWMI();
                LogHelper.LogInfo($"WMI 조회 완료: {wmiRecords.Count}개");

                // 모든 기록 합치기
                var allRecords = new List<NetworkUsageRecord>();
                allRecords.AddRange(eventLogRecords);
                allRecords.AddRange(netstatRecords);
                allRecords.AddRange(wmiRecords);
                LogHelper.LogInfo($"전체 기록 합계: {allRecords.Count}개");

                // 인터페이스 필터링
                if (!string.IsNullOrEmpty(selectedInterface))
                {
                    var beforeFilter = allRecords.Count;
                    allRecords = allRecords.Where(r => r.InterfaceName.Contains(selectedInterface, StringComparison.OrdinalIgnoreCase)).ToList();
                    LogHelper.LogInfo($"인터페이스 필터링 완료: {beforeFilter}개 → {allRecords.Count}개");
                }

                // 날짜 필터링
                var beforeDateFilter = allRecords.Count;
                allRecords = allRecords.Where(r => r.Timestamp >= startDate && r.Timestamp <= endDate.AddDays(1)).ToList();
                LogHelper.LogInfo($"날짜 필터링 완료: {beforeDateFilter}개 → {allRecords.Count}개");

                // 실제 데이터가 없으면 기존 데이터 복원 (테스트 데이터 유지)
                if (allRecords.Count == 0 && backupRecords.Count > 0)
                {
                    LogHelper.LogInfo("실제 데이터가 없어서 기존 데이터를 복원합니다.");
                    allRecords = backupRecords;
                }

                // 시간순 정렬
                allRecords = allRecords.OrderByDescending(r => r.Timestamp).ToList();
                LogHelper.LogInfo("시간순 정렬 완료");

                foreach (var record in allRecords)
                {
                    _historyRecords.Add(record);
                }
                LogHelper.LogInfo($"ObservableCollection에 {allRecords.Count}개 기록 추가 완료");

                // DataGrid 직접 업데이트
                if (HistoryDataGrid != null)
                {
                    HistoryDataGrid.ItemsSource = null;
                    HistoryDataGrid.ItemsSource = _historyRecords;
                    LogHelper.LogInfo("HistoryDataGrid ItemsSource 업데이트 완료");
                }

                // 통계 업데이트 (UI 컨트롤이 초기화된 경우에만)
                try
                {
                    UpdateNetworkStatistics(allRecords);
                    LogHelper.LogInfo("통계 업데이트 완료");
                }
                catch (Exception statEx)
                {
                    LogHelper.LogError($"통계 업데이트 실패: {statEx.Message}");
                }



                LogHelper.LogInfo($"네트워크 사용 기록 로드 완료: {allRecords.Count}개");

                // 수동 조회인 경우에만 완료 메시지 표시
                if (_showCompletionMessage)
                {
                    if (allRecords.Count == 0 || allRecords == backupRecords)
                    {
                        MessageBox.Show("선택한 조건에 해당하는 실제 네트워크 사용 기록이 없습니다.\n\n" +
                                       "• Windows 이벤트 로그\n" +
                                       "• 현재 활성 연결 (netstat)\n" +
                                       "• WMI 네트워크 통계\n\n" +
                                       "위 소스들에서 데이터를 조회했지만 결과가 없어서\n" +
                                       "기존 테스트 데이터를 유지합니다.",
                                       "정보",
                                       MessageBoxButton.OK,
                                       MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"네트워크 사용 기록 조회가 완료되었습니다.\n총 {allRecords.Count}개의 실제 기록을 찾았습니다.",
                                       "조회 완료",
                                       MessageBoxButton.OK,
                                       MessageBoxImage.Information);
                    }
                    _showCompletionMessage = false; // 플래그 리셋
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"네트워크 사용 기록을 불러오는 중 오류가 발생했습니다:\n{ex.Message}\n\n스택 트레이스:\n{ex.StackTrace}";
                MessageBox.Show(errorMessage,
                               "오류",
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);
                LogHelper.LogError($"네트워크 사용 기록 로드 실패: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void DateFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitializing)
            {
                LoadNetworkHistory();
            }
        }

        private void InterfaceFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitializing)
            {
                LoadNetworkHistory();
            }
        }

        private void HistoryProtocolFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitializing)
            {
                ApplyHistoryFilters();
            }
        }

        private void DirectionFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitializing)
            {
                ApplyHistoryFilters();
            }
        }

        private void ApplyHistoryFilters()
        {
            try
            {
                // UI 컨트롤이 초기화되었는지 확인
                if (HistoryProtocolFilterComboBox == null || DirectionFilterComboBox == null ||
                    HistoryDataGrid == null)
                {
                    return;
                }

                var allRecords = _historyRecords.ToList();
                var filteredRecords = allRecords.AsEnumerable();

                // 프로토콜 필터링
                var selectedProtocol = (HistoryProtocolFilterComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
                if (!string.IsNullOrEmpty(selectedProtocol) && selectedProtocol != "모든 프로토콜")
                {
                    filteredRecords = filteredRecords.Where(r =>
                        r.Protocol.Equals(selectedProtocol, StringComparison.OrdinalIgnoreCase) ||
                        (selectedProtocol == "HTTP" && (r.SourcePort == 80 || r.DestinationPort == 80)) ||
                        (selectedProtocol == "HTTPS" && (r.SourcePort == 443 || r.DestinationPort == 443))
                    );
                }

                // 방향 필터링
                var selectedDirection = (DirectionFilterComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
                if (!string.IsNullOrEmpty(selectedDirection) && selectedDirection != "모든 방향")
                {
                    filteredRecords = filteredRecords.Where(r =>
                        r.Direction.Equals(selectedDirection, StringComparison.OrdinalIgnoreCase)
                    );
                }

                // DataGrid 업데이트
                HistoryDataGrid.ItemsSource = filteredRecords.OrderByDescending(r => r.Timestamp).ToList();

                // 통계 업데이트
                UpdateNetworkStatistics(filteredRecords.ToList());
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"필터 적용 중 오류: {ex.Message}");
            }
        }

        private void UpdateNetworkStatistics(List<NetworkUsageRecord> records)
        {
            try
            {
                // UI 컨트롤이 초기화되었는지 확인
                if (TotalConnectionsText == null || TotalDataText == null ||
                    TopProtocolText == null || PeakTimeText == null)
                {
                    return;
                }

                // 총 연결 수
                TotalConnectionsText.Text = records.Count.ToString("N0");

                // 총 데이터량 계산
                var totalBytes = records.Sum(r => r.PacketSize);
                TotalDataText.Text = FormatBytes(totalBytes);

                // 가장 많이 사용된 프로토콜
                var protocolGroups = records.GroupBy(r => r.Protocol)
                                           .OrderByDescending(g => g.Count())
                                           .FirstOrDefault();
                TopProtocolText.Text = protocolGroups?.Key ?? "N/A";

                // 피크 시간대 계산 (시간별 연결 수가 가장 많은 시간)
                var hourlyGroups = records.GroupBy(r => r.Timestamp.Hour)
                                         .OrderByDescending(g => g.Count())
                                         .FirstOrDefault();
                if (hourlyGroups != null)
                {
                    PeakTimeText.Text = $"{hourlyGroups.Key:D2}:00";
                }
                else
                {
                    PeakTimeText.Text = "--:--";
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"통계 업데이트 중 오류: {ex.Message}");
            }
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        // 패킷 크기를 동적 단위로 포맷팅하는 정적 메서드 (XAML에서 사용 가능)
        [SupportedOSPlatform("windows")]
        public static string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "0 B";

            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = Math.Abs(bytes);
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            // 소수점 자릿수 조정: 1024 미만은 정수, 그 이상은 소수점 1자리
            string format = order == 0 ? "0" : "0.#";
            return $"{len.ToString(format)} {sizes[order]}";
        }

        private void RefreshHistory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogHelper.LogInfo("네트워크 사용 기록 조회 버튼 클릭됨");

                _showCompletionMessage = true; // 수동 조회이므로 완료 메시지 표시
                LoadNetworkHistory();
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"조회 버튼 클릭 처리 중 오류: {ex.Message}");
                MessageBox.Show($"조회 버튼 처리 중 오류가 발생했습니다:\n{ex.Message}",
                               "오류",
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);
            }
        }

        private void GenerateTestData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogHelper.LogInfo("테스트 데이터 생성 버튼 클릭됨");

                // 테스트 데이터 생성
                GenerateTestData();

                LogHelper.LogInfo("테스트 데이터 생성 완료");
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"테스트 데이터 생성 버튼 처리 중 오류: {ex.Message}");
            }
        }

        // 1. Windows 이벤트 로그에서 네트워크 연결 기록 조회
        private async Task<List<NetworkUsageRecord>> GetNetworkRecordsFromEventLog(DateTime startDate, DateTime endDate)
        {
            var records = new List<NetworkUsageRecord>();

            try
            {
                await Task.Run(() =>
                {
                    string query = "*[System[(EventID=5156 or EventID=5157) and TimeCreated[@SystemTime >= '" +
                                  startDate.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") + "' and @SystemTime <= '" +
                                  endDate.AddDays(1).ToString("yyyy-MM-ddTHH:mm:ss.fffZ") + "']]]";

                    var logQuery = new EventLogQuery("Security", PathType.LogName, query) { ReverseDirection = true };

                    using var reader = new EventLogReader(logQuery);
                    EventRecord record;
                    int count = 0;

                    while ((record = reader.ReadEvent()) != null && count < 100) // 최대 100개 제한
                    {
                        using (record)
                        {
                            var networkRecord = ParseEventLogToNetworkRecord(record);
                            if (networkRecord != null)
                            {
                                records.Add(networkRecord);
                                count++;
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"이벤트 로그 조회 실패: {ex.Message}");
            }

            return records;
        }

        // 2. netstat 명령어로 현재 활성 연결 조회
        private async Task<List<NetworkUsageRecord>> GetCurrentNetworkConnections()
        {
            var records = new List<NetworkUsageRecord>();

            try
            {
                await Task.Run(() =>
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "netstat",
                        Arguments = "-ano",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(psi);
                    if (process != null)
                    {
                        var output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();

                        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                        foreach (var line in lines.Skip(4)) // 헤더 스킵
                        {
                            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 5)
                            {
                                var record = ParseNetstatLine(parts);
                                if (record != null)
                                {
                                    records.Add(record);
                                }
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"netstat 조회 실패: {ex.Message}");
            }

            return records;
        }

        // 3. WMI로 네트워크 통계 조회
        private async Task<List<NetworkUsageRecord>> GetNetworkStatisticsFromWMI()
        {
            var records = new List<NetworkUsageRecord>();

            try
            {
                await Task.Run(() =>
                {
                    using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PerfRawData_Tcpip_NetworkInterface");
                    using var results = searcher.Get();

                    foreach (ManagementObject obj in results)
                    {
                        var interfaceName = obj["Name"]?.ToString();
                        if (!string.IsNullOrEmpty(interfaceName) && !IsVirtualInterface(interfaceName))
                        {
                            var record = new NetworkUsageRecord
                            {
                                Timestamp = DateTime.Now,
                                InterfaceName = interfaceName,
                                SourceIP = "Local",
                                DestinationIP = "Various",
                                Protocol = "Mixed",
                                SourcePort = 0,
                                DestinationPort = 0,
                                PacketSize = Convert.ToInt64(obj["BytesReceivedPerSec"] ?? 0) + Convert.ToInt64(obj["BytesSentPerSec"] ?? 0),
                                Direction = "Both",
                                ProcessName = "System",
                                Description = $"WMI 네트워크 통계 - {interfaceName}"
                            };
                            records.Add(record);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"WMI 조회 실패: {ex.Message}");
            }

            return records;
        }

        // 이벤트 로그 레코드를 NetworkUsageRecord로 변환
        private NetworkUsageRecord? ParseEventLogToNetworkRecord(EventRecord eventRecord)
        {
            try
            {
                var xml = eventRecord.ToXml();

                return new NetworkUsageRecord
                {
                    Timestamp = eventRecord.TimeCreated ?? DateTime.Now,
                    InterfaceName = "Windows Firewall",
                    SourceIP = GetXmlValue(xml, "SourceAddress"),
                    DestinationIP = GetXmlValue(xml, "DestinationAddress"),
                    Protocol = GetProtocol(GetXmlValue(xml, "Protocol")),
                    SourcePort = int.TryParse(GetXmlValue(xml, "SourcePort"), out int srcPort) ? srcPort : 0,
                    DestinationPort = int.TryParse(GetXmlValue(xml, "DestinationPort"), out int dstPort) ? dstPort : 0,
                    PacketSize = 0,
                    Direction = GetDirection(GetXmlValue(xml, "Direction")),
                    ProcessName = GetXmlValue(xml, "ApplicationName"),
                    Description = $"Windows 방화벽 로그 - EventID: {eventRecord.Id}"
                };
            }
            catch
            {
                return null;
            }
        }

        // netstat 출력 라인을 NetworkUsageRecord로 변환
        private NetworkUsageRecord? ParseNetstatLine(string[] parts)
        {
            try
            {
                var protocol = parts[0];
                var localAddress = parts[1];
                var foreignAddress = parts[2];
                var state = parts[3];
                var pid = parts.Length > 4 ? parts[4] : "0";

                var localParts = localAddress.Split(':');
                var foreignParts = foreignAddress.Split(':');

                return new NetworkUsageRecord
                {
                    Timestamp = DateTime.Now,
                    InterfaceName = "Active Connection",
                    SourceIP = localParts.Length > 1 ? string.Join(":", localParts.Take(localParts.Length - 1)) : localParts[0],
                    DestinationIP = foreignParts.Length > 1 ? string.Join(":", foreignParts.Take(foreignParts.Length - 1)) : foreignParts[0],
                    Protocol = protocol,
                    SourcePort = localParts.Length > 1 && int.TryParse(localParts.Last(), out int srcPort) ? srcPort : 0,
                    DestinationPort = foreignParts.Length > 1 && int.TryParse(foreignParts.Last(), out int dstPort) ? dstPort : 0,
                    PacketSize = 0,
                    Direction = "Outbound",
                    ProcessName = $"PID: {pid}",
                    Description = $"netstat - {state}"
                };
            }
            catch
            {
                return null;
            }
        }

        // 테스트용 샘플 데이터 생성 메서드 (메모리 기반)
        private void GenerateTestData()
        {
            var random = new Random();
            var protocols = new[] { "TCP", "UDP", "HTTP", "HTTPS", "ICMP" };
            var directions = new[] { "송신", "수신", "내부" };
            var interfaces = new[] { "Ethernet", "Wi-Fi", "Loopback" };

            for (int i = 0; i < 50; i++)
            {
                var record = new NetworkUsageRecord
                {
                    Timestamp = DateTime.Now.AddHours(-random.Next(0, 168)), // 지난 7일
                    InterfaceName = interfaces[random.Next(interfaces.Length)],
                    SourceIP = $"192.168.1.{random.Next(1, 255)}",
                    SourcePort = random.Next(1024, 65535),
                    DestinationIP = $"10.0.0.{random.Next(1, 255)}",
                    DestinationPort = random.Next(80, 8080),
                    Protocol = protocols[random.Next(protocols.Length)],
                    Direction = directions[random.Next(directions.Length)],
                    PacketSize = random.Next(64, 1500)
                };

                _historyRecords.Add(record);
            }

            // 통계 업데이트
            UpdateNetworkStatistics(_historyRecords.ToList());

            LogHelper.LogInfo($"테스트 데이터 {_historyRecords.Count}개 생성 완료");
        }

        // 보안 기능 관련 메서드들

        // 보안 탭 초기화
        private void InitializeSecurityTab()
        {
            try
            {
                LogHelper.LogInfo("보안 탭 초기화 시작");

                // 보안 경고 DataGrid 바인딩 (null 체크 추가)
                if (SecurityAlertsDataGrid != null)
                {
                    SecurityAlertsDataGrid.ItemsSource = _securityAlerts;
                    LogHelper.LogInfo("SecurityAlertsDataGrid 바인딩 완료");
                }
                else
                {
                    LogHelper.LogWarning("SecurityAlertsDataGrid가 아직 로드되지 않음 - 지연 초기화");
                    // 탭이 선택될 때까지 초기화를 지연
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (SecurityAlertsDataGrid != null)
                        {
                            SecurityAlertsDataGrid.ItemsSource = _securityAlerts;
                            UpdateSecurityStatistics();
                            LogHelper.LogInfo("지연된 보안 탭 초기화 완료");
                        }
                    }), DispatcherPriority.Loaded);
                    return;
                }

                // 보안 통계 업데이트
                UpdateSecurityStatistics();

                LogHelper.LogInfo("보안 탭 초기화 완료");
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"보안 탭 초기화 중 오류: {ex.Message}");
            }
        }

        // 보안 스캔 버튼 클릭
        private async void SecurityScan_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogHelper.LogInfo("보안 스캔 시작");

                // 기존 경고 목록 초기화 (테스트 경고 및 이전 스캔 결과 제거)
                _securityAlerts.Clear();
                LogHelper.LogInfo("기존 보안 경고 목록 초기화 완료");

                // 현재 네트워크 기록에 대해 보안 분석 수행
                var analysisResults = new List<SecurityAlert>();

                foreach (var record in _historyRecords)
                {
                    var alerts = await _securityAnalyzer.AnalyzePacketAsync(record);
                    analysisResults.AddRange(alerts);
                }

                // 새로운 실시간 분석 결과를 컬렉션에 추가
                foreach (var alert in analysisResults)
                {
                    _securityAlerts.Add(alert);
                }

                UpdateSecurityStatistics();
                ApplySecurityFilters();

                // 스캔 결과에 따른 메시지 표시
                if (analysisResults.Count == 0)
                {
                    MessageBox.Show("실시간 보안 스캔 완료!\n\n현재 네트워크 기록에서 보안 위협이 발견되지 않았습니다.\n시스템이 안전한 상태입니다.",
                                  "보안 스캔 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var highRiskCount = analysisResults.Count(a => a.Severity == "High" || a.Severity == "Critical");
                    var mediumRiskCount = analysisResults.Count(a => a.Severity == "Medium");
                    var lowRiskCount = analysisResults.Count(a => a.Severity == "Low");

                    var riskBreakdown = "";
                    if (highRiskCount > 0) riskBreakdown += $"고위험: {highRiskCount}개\n";
                    if (mediumRiskCount > 0) riskBreakdown += $"중위험: {mediumRiskCount}개\n";
                    if (lowRiskCount > 0) riskBreakdown += $"저위험: {lowRiskCount}개";

                    MessageBox.Show($"실시간 보안 스캔 완료!\n\n현재 네트워크 기록에서 {analysisResults.Count}개의 보안 경고가 발견되었습니다.\n\n{riskBreakdown}\n\n상세 내용은 아래 목록에서 확인하세요.",
                                  "보안 스캔 완료", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                LogHelper.LogInfo($"실시간 보안 스캔 완료: {analysisResults.Count}개 경고 발견");
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"보안 스캔 중 오류: {ex.Message}");
                MessageBox.Show($"보안 스캔 중 오류가 발생했습니다: {ex.Message}",
                              "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 테스트 보안 경고 생성
        private void GenerateSecurityTest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogHelper.LogInfo("테스트 보안 경고 생성 시작");

                var testAlerts = new[]
                {
                    new SecurityAlert
                    {
                        Timestamp = DateTime.Now.AddMinutes(-30),
                        AlertType = "악성 IP 감지",
                        Severity = "High",
                        SourceIP = "192.168.1.100",
                        DestinationIP = "10.0.0.1",
                        SourcePort = 4444,
                        DestinationPort = 80,
                        Protocol = "TCP",
                        Description = "알려진 악성 IP와의 통신 감지: 192.168.1.100",
                        Details = "카테고리: Botnet, 출처: Internal Detection",
                        Action = "Monitored"
                    },
                    new SecurityAlert
                    {
                        Timestamp = DateTime.Now.AddMinutes(-15),
                        AlertType = "의심스러운 포트 사용",
                        Severity = "Medium",
                        SourceIP = "172.16.0.50",
                        DestinationIP = "172.16.0.1",
                        SourcePort = 12345,
                        DestinationPort = 1433,
                        Protocol = "TCP",
                        Description = "의심스러운 포트 사용 감지: 1433",
                        Details = "SQL Server 데이터베이스 포트",
                        Action = "Monitored"
                    },
                    new SecurityAlert
                    {
                        Timestamp = DateTime.Now.AddMinutes(-5),
                        AlertType = "비정상적인 트래픽 패턴",
                        Severity = "High",
                        SourceIP = "10.0.0.25",
                        DestinationIP = "8.8.8.8",
                        Protocol = "UDP",
                        Description = "비정상적인 연결 패턴 감지: 75회 연결",
                        Details = "위험도: 8.5/10, 데이터량: 15.2 MB",
                        Action = "Monitored"
                    },
                    new SecurityAlert
                    {
                        Timestamp = DateTime.Now.AddMinutes(-2),
                        AlertType = "대용량 데이터 전송",
                        Severity = "Medium",
                        SourceIP = "192.168.1.200",
                        DestinationIP = "203.104.144.200",
                        SourcePort = 443,
                        DestinationPort = 443,
                        Protocol = "TCP",
                        Description = "대용량 데이터 전송 감지: 25.6 MB",
                        Details = "정상 범위를 초과하는 데이터 전송량",
                        Action = "Monitored"
                    },
                    new SecurityAlert
                    {
                        Timestamp = DateTime.Now.AddMinutes(-1),
                        AlertType = "ICMP 트래픽 감지",
                        Severity = "Low",
                        SourceIP = "192.168.1.50",
                        DestinationIP = "8.8.4.4",
                        SourcePort = 0,
                        DestinationPort = 0,
                        Protocol = "ICMP",
                        Description = "ICMP 핑 트래픽 감지",
                        Details = "정상적인 네트워크 연결 테스트",
                        Action = "Monitored"
                    },
                    new SecurityAlert
                    {
                        Timestamp = DateTime.Now.AddMinutes(-3),
                        AlertType = "WMI 네트워크 접근",
                        Severity = "Medium",
                        SourceIP = "192.168.1.100",
                        DestinationIP = "192.168.1.10",
                        SourcePort = 0,
                        DestinationPort = 0,
                        Protocol = "WMI",
                        Description = "WMI 네트워크 관리 접근 감지",
                        Details = "시스템 관리 도구를 통한 원격 접근",
                        Action = "Monitored"
                    },
                    new SecurityAlert
                    {
                        Timestamp = DateTime.Now.AddMinutes(-5),
                        AlertType = "비정상적인 트래픽 패턴",
                        Severity = "High",
                        SourceIP = "10.0.0.25",
                        DestinationIP = "8.8.8.8",
                        Protocol = "UDP",
                        Description = "비정상적인 연결 패턴 감지: 75회 연결",
                        Details = "위험도: 8.5/10, 데이터량: 15.2 MB",
                        Action = "Monitored"
                    },
                    new SecurityAlert
                    {
                        Timestamp = DateTime.Now.AddMinutes(-5),
                        AlertType = "비정상적인 트래픽 패턴",
                        Severity = "High",
                        SourceIP = "10.0.0.25",
                        DestinationIP = "8.8.8.8",
                        SourcePort = 53124,
                        DestinationPort = 53,
                        Protocol = "UDP",
                        Description = "비정상적인 연결 패턴 감지: 75회 연결",
                        Details = "위험도: 8.5/10, 데이터량: 15.2 MB",
                        Action = "Monitored"
                    }
                };

                foreach (var alert in testAlerts)
                {
                    _securityAlerts.Add(alert);
                }

                UpdateSecurityStatistics();
                ApplySecurityFilters();

                MessageBox.Show($"테스트 보안 경고 {testAlerts.Length}개가 생성되었습니다.\n\n※ 이는 테스트용 샘플 데이터입니다.\n실제 네트워크 분석을 원하시면 '보안 스캔' 버튼을 클릭하세요.",
                              "테스트 완료", MessageBoxButton.OK, MessageBoxImage.Information);

                LogHelper.LogInfo($"테스트 보안 경고 {testAlerts.Length}개 생성 완료");
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"테스트 보안 경고 생성 중 오류: {ex.Message}");
                MessageBox.Show($"테스트 경고 생성 중 오류가 발생했습니다: {ex.Message}",
                              "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 보안 통계 업데이트
        private void UpdateSecurityStatistics()
        {
            try
            {
                var stats = _securityAnalyzer.GetSecurityStatistics();

                // 실제 경고 수로 업데이트 (SecurityAnalyzer의 내부 데이터 + UI 컬렉션)
                var totalAlerts = _securityAlerts.Count;
                var last24Hours = _securityAlerts.Count(a => (DateTime.Now - a.Timestamp).TotalHours <= 24);
                var highRisk = _securityAlerts.Count(a => a.Severity == "High" || a.Severity == "Critical");
                var unresolved = _securityAlerts.Count(a => !a.IsResolved);

                Dispatcher.Invoke(() =>
                {
                    // UI 컨트롤들이 로드되었는지 확인
                    if (TotalAlertsText != null) TotalAlertsText.Text = totalAlerts.ToString();
                    if (RecentAlertsText != null) RecentAlertsText.Text = last24Hours.ToString();
                    if (HighRiskAlertsText != null) HighRiskAlertsText.Text = highRisk.ToString();
                    if (UnresolvedAlertsText != null) UnresolvedAlertsText.Text = unresolved.ToString();
                });

                LogHelper.LogInfo($"보안 통계 업데이트: 총 {totalAlerts}개, 24시간 {last24Hours}개, 고위험 {highRisk}개, 미해결 {unresolved}개");
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"보안 통계 업데이트 중 오류: {ex.Message}");
            }
        }

        // 보안 필터 적용
        private void ApplySecurityFilters()
        {
            try
            {
                // SecurityAlertsDataGrid가 로드되지 않은 경우 필터링 건너뛰기
                if (SecurityAlertsDataGrid == null)
                {
                    LogHelper.LogWarning("SecurityAlertsDataGrid가 아직 로드되지 않아 필터링을 건너뜁니다.");
                    return;
                }

                var filteredAlerts = _securityAlerts.AsEnumerable();

                // 위험도 필터
                var severityFilter = (SecuritySeverityFilter?.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                if (!string.IsNullOrEmpty(severityFilter) && severityFilter != "All")
                {
                    filteredAlerts = filteredAlerts.Where(a => a.Severity == severityFilter);
                }

                // 경고 유형 필터
                var alertTypeFilter = (SecurityAlertTypeFilter?.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                if (!string.IsNullOrEmpty(alertTypeFilter) && alertTypeFilter != "All")
                {
                    var filterMap = new Dictionary<string, string>
                    {
                        ["MaliciousIP"] = "악성 IP 감지",
                        ["SuspiciousPort"] = "의심스러운 포트 사용",
                        ["AbnormalTraffic"] = "비정상적인 트래픽 패턴",
                        ["LargeTransfer"] = "대용량 데이터 전송"
                    };

                    if (filterMap.ContainsKey(alertTypeFilter))
                    {
                        filteredAlerts = filteredAlerts.Where(a => a.AlertType == filterMap[alertTypeFilter]);
                    }
                }

                // 날짜 필터
                if (SecurityStartDatePicker?.SelectedDate.HasValue == true)
                {
                    filteredAlerts = filteredAlerts.Where(a => a.Timestamp.Date >= SecurityStartDatePicker.SelectedDate.Value.Date);
                }

                if (SecurityEndDatePicker?.SelectedDate.HasValue == true)
                {
                    filteredAlerts = filteredAlerts.Where(a => a.Timestamp.Date <= SecurityEndDatePicker.SelectedDate.Value.Date);
                }

                // DataGrid 업데이트
                var filteredList = filteredAlerts.OrderByDescending(a => a.Timestamp).ToList();

                Dispatcher.Invoke(() =>
                {
                    if (SecurityAlertsDataGrid != null)
                    {
                        SecurityAlertsDataGrid.ItemsSource = null;
                        SecurityAlertsDataGrid.ItemsSource = filteredList;
                    }
                });

                LogHelper.LogInfo($"보안 필터 적용 완료: {filteredList.Count}개 경고 표시");
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"보안 필터 적용 중 오류: {ex.Message}");
            }
        }

        // 보안 필터 이벤트 핸들러들
        private void SecuritySeverityFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitializing)
            {
                ApplySecurityFilters();
            }
        }

        private void SecurityAlertTypeFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitializing)
            {
                ApplySecurityFilters();
            }
        }

        private void SecurityDateFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitializing)
            {
                ApplySecurityFilters();
            }
        }

        // 탭 선택 변경 이벤트 핸들러
        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (MainTabControl?.SelectedItem is TabItem selectedTab)
                {
                    LogHelper.LogInfo($"탭 변경됨: {selectedTab.Name}");

                    // 보안 탭이 선택되었을 때 초기화
                    if (selectedTab.Name == "SecurityTab" && SecurityAlertsDataGrid != null)
                    {
                        // 보안 탭이 처음 선택되었을 때만 초기화
                        if (SecurityAlertsDataGrid.ItemsSource == null)
                        {
                            LogHelper.LogInfo("보안 탭 지연 초기화 시작");
                            SecurityAlertsDataGrid.ItemsSource = _securityAlerts;
                            UpdateSecurityStatistics();
                            LogHelper.LogInfo("보안 탭 지연 초기화 완료");
                        }
                    }


                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"탭 선택 변경 처리 중 오류: {ex.Message}");
            }
        }

        // 사이드바 네비게이션 (임시)
        [SupportedOSPlatform("windows")]
        private void SidebarButton_Click(object sender, RoutedEventArgs e)
        {
            var clicked = sender as ToggleButton;
            if (clicked == null) return;

            // 이전 선택 해제
            if (_selectedButton != null && _selectedButton != clicked)
                _selectedButton.IsChecked = false;

            // 선택 상태 유지
            clicked.IsChecked = true;
            _selectedButton = clicked;

            switch (clicked.CommandParameter?.ToString())
            {
                case "Vaccine":
                    NavigateToPage(new Vaccine());
                    break;
                case "NetWorks":
                    NavigateToPage(new NetWorks());
                    break;
                case "ProgramsList":
                    NavigateToPage(new ProgramsList());
                    break;
                case "Recoverys":
                    NavigateToPage(new Recoverys());
                    break;
                case "Logs":
                    NavigateToPage(new Logs());
                    break;
            }
        }

        private void NavigateToPage(System.Windows.Controls.Page page)
        {
            var mainWindow = System.Windows.Window.GetWindow(this) as MainWindows;
            mainWindow?.NavigateToPage(page);
        }

        // 클래스 멤버에 YourMonitoringClass 인스턴스 선언
        private YourMonitoringClass _monitoringClass = new YourMonitoringClass();
        private async Task StartAlternativeMonitoring(string interfaceName)
        {
            try
            {
                LogHelper.LogInfo($"대안 네트워크 모니터링 시작: {interfaceName}");

                // 여기에 설치된 프로그램 리스트를 준비 (실제로는 외부에서 가져와야 함)
                var installedPrograms = new List<string>
                {
                    "OldVPN",
                    "OutdatedBrowser",
                    "SafeProgram"
                };

                // 분석 및 저장 — 메시지 박스나 출력은 하지 않음
                _monitoringClass.AnalyzeAndStoreProgramScores(installedPrograms);

                // UI 상태 변경
                UpdateCaptureUI(true, interfaceName, isAlternativeMode: true);

                // 대안 모니터링 타이머 시작
                StartAlternativeMonitoringTimer();

                // 초기 데이터 로드
                await LoadInitialNetworkData();

                MessageBox.Show(
                    $"실시간 네트워크 모니터링이 시작되었습니다!\n\n" +
                    $"🔥 실시간 모니터링 기능:\n" +
                    $"• 실시간 방화벽 이벤트 로그 감지 (NEW!)\n" +
                    $"• Windows 보안 이벤트 즉시 추적\n" +
                    $"• 네트워크 연결 상태 실시간 업데이트\n" +
                    $"• 중복 제거를 통한 정확한 데이터 표시\n" +
                    $"• 프로세스별 네트워크 사용량 분석\n\n" +
                    $"⚡ 이제 새로운 네트워크 연결이 발생하는 즉시\n" +
                    $"실시간으로 데이터가 업데이트됩니다!\n\n" +
                    $"💡 보안 설정 변경 없이 안전하고 효과적인\n" +
                    $"네트워크 보안 모니터링을 제공합니다.",
                    "실시간 모니터링 시작",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                UpdateCaptureUI(true, interfaceName, isAlternativeMode: true);
                StartAlternativeMonitoringTimer();
                await LoadInitialNetworkData();
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"대안 모니터링 시작 실패: {ex.Message}");
                MessageBox.Show($"네트워크 모니터링 시작에 실패했습니다: {ex.Message}",
                               "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartAlternativeMonitoringTimer()
        {
            // 실시간 이벤트 로그 모니터링 시작
            StartRealTimeEventLogMonitoring();

            // 기존 타이머 기반 모니터링 (10초 간격으로 설정)
            _alternativeMonitoringTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10) // 10초마다 netstat 기반 업데이트
            };

            _alternativeMonitoringTimer.Tick += async (s, e) =>
            {
                try
                {
                    await UpdateAlternativeMonitoringData();
                }
                catch (Exception ex)
                {
                    LogHelper.LogError($"대안 모니터링 업데이트 실패: {ex.Message}");
                }
            };

            _isAlternativeMonitoring = true;
            _alternativeMonitoringTimer.Start();
            LogHelper.LogInfo("대안 모니터링 (이벤트 로그 + 10초 타이머) 시작됨");
        }

        // 실시간 이벤트 로그 모니터링 시작
        private void StartRealTimeEventLogMonitoring()
        {
            try
            {
                // Security 로그에서 네트워크 관련 이벤트 실시간 감지
                var query = new EventLogQuery("Security", PathType.LogName, 
                    "*[System[(EventID=5156 or EventID=5157)]]");

                _eventLogWatcher = new EventLogWatcher(query);
                _eventLogWatcher.EventRecordWritten += OnNewNetworkEvent;
                _eventLogWatcher.Enabled = true;

                LogHelper.LogInfo("실시간 이벤트 로그 모니터링 시작됨");
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"실시간 이벤트 로그 모니터링 시작 실패: {ex.Message}");
                // 실패해도 기존 타이머 기반 모니터링은 계속 동작
            }
        }

        // 새로운 네트워크 이벤트 발생 시 처리
        private void OnNewNetworkEvent(object? sender, EventRecordWrittenEventArgs e)
        {
            try
            {
                if (e.EventRecord == null) return;

                using (e.EventRecord)
                {
                    var networkRecord = ParseEventLogToNetworkRecord(e.EventRecord);
                    if (networkRecord != null && IsNewOrChangedConnection(networkRecord))
                    {
                        var packetInfo = ConvertNetworkRecordToPacketInfo(networkRecord);
                        if (packetInfo != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                _packets.Insert(0, packetInfo); // 상단에 추가
                                if (_packets.Count > 1000)
                                {
                                    _packets.RemoveAt(_packets.Count - 1); // 하단에서 제거
                                }
                                UpdateCaptureStatus();
                            });

                            LogHelper.LogInfo($"실시간 네트워크 이벤트 감지: {packetInfo.SourceIP}:{packetInfo.SourcePort} -> {packetInfo.DestinationIP}:{packetInfo.DestinationPort} ({packetInfo.Protocol})");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"네트워크 이벤트 처리 실패: {ex.Message}");
            }
        }

        // 새로운 또는 변경된 연결인지 확인
        private bool IsNewOrChangedConnection(NetworkUsageRecord record)
        {
            lock (_connectionLock)
            {
                var key = $"{record.SourceIP}:{record.SourcePort}-{record.DestinationIP}:{record.DestinationPort}-{record.Protocol}";
                
                if (!_lastSeenConnections.ContainsKey(key) || 
                    (DateTime.Now - _lastSeenConnections[key]).TotalSeconds > 10) // 10초 이후 재표시 허용
                {
                    _lastSeenConnections[key] = DateTime.Now;
                    
                    // 딕셔너리 크기 제한 (메모리 누수 방지)
                    if (_lastSeenConnections.Count > 1000)
                    {
                        var oldestKey = _lastSeenConnections.OrderBy(kvp => kvp.Value).First().Key;
                        _lastSeenConnections.Remove(oldestKey);
                    }
                    
                    return true;
                }
                return false;
            }
        }

        // NetworkUsageRecord를 PacketInfo로 변환
        private PacketInfo? ConvertNetworkRecordToPacketInfo(NetworkUsageRecord record)
        {
            try
            {
                return new PacketInfo
                {
                    Timestamp = record.Timestamp,
                    SourceIP = record.SourceIP,
                    DestinationIP = record.DestinationIP,
                    SourcePort = (ushort)Math.Max(0, Math.Min(record.SourcePort, ushort.MaxValue)),
                    DestinationPort = (ushort)Math.Max(0, Math.Min(record.DestinationPort, ushort.MaxValue)),
                    Protocol = record.Protocol,
                    PacketSize = record.PacketSize,
                    Direction = record.Direction,
                    ProcessName = record.ProcessName ?? "Unknown",
                    Description = $"실시간 이벤트: {record.Description}"
                };
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"네트워크 기록 변환 실패: {ex.Message}");
                return null;
            }
        }

        private async Task UpdateAlternativeMonitoringData()
        {
            try
            {
                // 1. 현재 네트워크 연결 상태 조회 (이제 보조적 역할)
                var currentConnections = await GetCurrentNetworkConnections();

                // 2. 새로운 연결들을 패킷 목록에 추가 (중복 필터링 적용)
                foreach (var connection in currentConnections.Take(5)) // 5개로 줄임
                {
                    if (IsNewOrChangedConnection(connection))
                    {
                        var packetInfo = ConvertConnectionToPacketInfo(connection);
                        if (packetInfo != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                _packets.Insert(0, packetInfo); // 상단에 추가
                                if (_packets.Count > 1000)
                                {
                                    _packets.RemoveAt(_packets.Count - 1); // 하단에서 제거
                                }
                            });
                        }
                    }
                }

                // 3. 상태 업데이트
                Application.Current.Dispatcher.Invoke(() =>
                {
                    UpdateCaptureStatus();
                });
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"대안 모니터링 데이터 업데이트 실패: {ex.Message}");
            }
        }

        private PacketInfo? ConvertConnectionToPacketInfo(NetworkUsageRecord connection)
        {
            try
            {
                return new PacketInfo
                {
                    Timestamp = connection.Timestamp,
                    SourceIP = connection.SourceIP,
                    DestinationIP = connection.DestinationIP,
                    SourcePort = (ushort)Math.Max(0, Math.Min(connection.SourcePort, ushort.MaxValue)),
                    DestinationPort = (ushort)Math.Max(0, Math.Min(connection.DestinationPort, ushort.MaxValue)),
                    Protocol = connection.Protocol,
                    PacketSize = connection.PacketSize,
                    Direction = connection.Direction,
                    ProcessName = connection.ProcessName ?? "Unknown",
                    Description = $"연결 모니터링: {connection.Description}"
                };
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"연결 정보 변환 실패: {ex.Message}");
                return null;
            }
        }

        private async Task LoadInitialNetworkData()
        {
            try
            {
                ShowLoadingOverlay();

                // 병렬로 데이터 수집
                var tasks = new[]
                {
                    LoadFirewallEventLogsAsync(),
                    GetCurrentNetworkConnections(),
                    GetNetworkStatisticsFromWMI()
                };

                await Task.WhenAll(tasks);

                // 방화벽 이벤트 로그를 패킷 목록에 추가 (최신 것부터 상단에)
                var eventLogPackets = ConvertEventLogsToPackets();
                var recentPackets = eventLogPackets.Take(50).ToList(); // 최신 50개
                
                // 시간순으로 정렬하여 최신 것이 위에 오도록
                recentPackets = recentPackets.OrderByDescending(p => p.Timestamp).ToList();
                
                foreach (var packet in recentPackets)
                {
                    _packets.Insert(0, packet); // 상단에 추가
                }

                LogHelper.LogInfo("초기 네트워크 데이터 로드 완료");
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"초기 데이터 로드 실패: {ex.Message}");
            }
            finally
            {
                HideLoadingOverlay();
            }
        }

        private async Task LoadFirewallEventLogsAsync()
        {
            await Task.Run(() => LoadFirewallEventLogs());
        }

        private List<PacketInfo> ConvertEventLogsToPackets()
        {
            var packets = new List<PacketInfo>();

            foreach (var logEntry in eventLogEntries.Take(50))
            {
                try
                {
                    var packet = new PacketInfo
                    {
                        Timestamp = logEntry.TimeGenerated,
                        SourceIP = logEntry.Source.Split(':')[0],
                        DestinationIP = logEntry.Destination.Split(':')[0],
                        SourcePort = (ushort)Math.Max(0, Math.Min(int.TryParse(logEntry.Source.Split(':').LastOrDefault(), out int srcPort) ? srcPort : 0, ushort.MaxValue)),
                        DestinationPort = (ushort)Math.Max(0, Math.Min(int.TryParse(logEntry.Destination.Split(':').LastOrDefault(), out int dstPort) ? dstPort : 0, ushort.MaxValue)),
                        Protocol = logEntry.Protocol,
                        PacketSize = 0, // 이벤트 로그에는 패킷 크기 정보 없음
                        Direction = logEntry.Direction,
                        ProcessName = logEntry.ApplicationName,
                        Description = $"방화벽 로그: {logEntry.Result}"
                    };

                    packets.Add(packet);
                }
                catch (Exception ex)
                {
                    LogHelper.LogError($"이벤트 로그 변환 실패: {ex.Message}");
                }
            }

            return packets;
        }

        [SupportedOSPlatform("windows")]
        private void SidebarVaccine_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Vaccine());
        }
        public class YourMonitoringClass
        {
            public List<ProgramSecurityScore> ProgramScores { get; private set; } = new List<ProgramSecurityScore>();

            public void AnalyzeAndStoreProgramScores(List<string> installedPrograms)
            {
                ProgramScores.Clear();

                foreach (var program in installedPrograms)
                {
                    var score = new ProgramSecurityScore(program);

                    if (program.Contains("OldVPN") || program.Contains("UnknownVPN"))
                        score.AddDeduction(5, "알려지지 않은 VPN 사용");

                    if (program.Contains("OutdatedBrowser"))
                        score.AddDeduction(3, "구버전 브라우저 사용");

                    if (program.Contains("FileSharingTool"))
                        score.AddDeduction(5, "파일 공유 프로그램 설치로 인한 보안 위험");

                    if (program.Contains("RemoteAccessTool"))
                        score.AddDeduction(8, "원격 접속 도구 사용 위험");

                    if (score.DeductionPoints > 0)
                    {
                        ProgramScores.Add(score);
                        ProgramSecurityManager.Name.Add(program);
                        ProgramSecurityManager.Scores.Add(score.DeductionPoints);
                    }
                }
            }
        }

        public class ProgramSecurityScore
        {
            public string ProgramName { get; set; }
            public int DeductionPoints { get; set; }
            public List<string> DeductionReasons { get; set; }

            public ProgramSecurityScore(string programName)
            {
                ProgramName = programName;
                DeductionPoints = 0;
                DeductionReasons = new List<string>();
            }

            public void AddDeduction(int points, string reason)
            {
                DeductionPoints += points;
                DeductionReasons.Add(reason);
            }
        }
        public static class ProgramSecurityManager
        {
            public static List<String> Name { get; set; } = new List<String>();
            public static List<int> Scores { get; set; } = new List<int>();
        }

    }
}