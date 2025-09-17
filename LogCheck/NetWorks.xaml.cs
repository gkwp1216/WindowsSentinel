// using 지시문을 파일 맨 위로 이동 및 중복 제거
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.IO;
using System.Linq;
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
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Threading;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LogCheck.Models;
using LogCheck.Services;
using MaterialDesignThemes.Wpf;
using PacketDotNet;
using SharpPcap;
using SkiaSharp;
using Application = System.Windows.Application;
using Cursors = System.Windows.Input.Cursors;
using MessageBox = System.Windows.MessageBox;
using Point = System.Windows.Point;
using SecurityAlert = LogCheck.Models.SecurityAlert;

namespace LogCheck
{
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
    public partial class NetWorks : Page
    {
        // XAML 이벤트 핸들러 Stub (클래스 내부로 이동)
        // 필드 선언부
        private DispatcherTimer? loadingTextTimer;
        private void SidebarButton_Click(object sender, RoutedEventArgs e) { }
        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
        private void StartCapture_Click(object sender, RoutedEventArgs e) { }
        private void StopCapture_Click(object sender, RoutedEventArgs e) { }
        private void Clear_Click(object sender, RoutedEventArgs e) { }
        private void Filter_TextChanged(object sender, TextChangedEventArgs e) { }
        private void ProtocolFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
        private void DateFilter_Changed(object sender, SelectionChangedEventArgs e) { }
        private void InterfaceFilter_Changed(object sender, SelectionChangedEventArgs e) { }
        private void HistoryProtocolFilter_Changed(object sender, SelectionChangedEventArgs e) { }
        private void DirectionFilter_Changed(object sender, SelectionChangedEventArgs e) { }
        private void RefreshHistory_Click(object sender, RoutedEventArgs e) { }
        private void GenerateTestData_Click(object sender, RoutedEventArgs e) { }
        private void SecurityScan_Click(object sender, RoutedEventArgs e) { }
        private void GenerateSecurityTest_Click(object sender, RoutedEventArgs e) { }
        private void SecuritySeverityFilter_Changed(object sender, SelectionChangedEventArgs e) { }
        private void SecurityAlertTypeFilter_Changed(object sender, SelectionChangedEventArgs e) { }
        private void SecurityDateFilter_Changed(object sender, SelectionChangedEventArgs e) { }
        // ...기존 NetWorks 클래스 멤버 및 메서드...
        private int dotCount = 0;
        private const int maxDots = 3;
        private string baseText = "검사 중";
        private ObservableCollection<EventLogEntryModel> eventLogEntries = new ObservableCollection<EventLogEntryModel>();
        private readonly ObservableCollection<PacketInfo> _packets = new ObservableCollection<PacketInfo>();
        private ICollectionView? _packetsView;
        private Models.WSPacketCapture? _packetCapture;
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
            // InitializeHistoryTab(); // 구현 없음, 주석 처리
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
                    // GenerateTestData(); // 구현 없음, 주석 처리
                }
                LogHelper.LogInfo("Network 페이지 로드 완료 초기화 완료");
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"Network 페이지 로드 이벤트 처리 중 오류: {ex.Message}");
            }
        }
        // Npcap 설치 확인 (간단한 더미 구현)
        private bool CheckNpcapInstallation()
        {
            // 실제 구현 필요시 Npcap 설치 여부 확인 로직 작성
            return true;
        }

        // 가상 인터페이스 판별 (간단한 더미 구현)
        private bool IsVirtualInterface(string description)
        {
            if (string.IsNullOrEmpty(description)) return false;
            string[] virtualKeywords = { "WAN Miniport", "Loopback", "Virtual", "TAP", "VPN" };
            return virtualKeywords.Any(v => description.Contains(v, StringComparison.OrdinalIgnoreCase));
        }

        // 장치 접근성 테스트 (간단한 더미 구현)
        private bool TestDeviceAccess(ICaptureDevice device)
        {
            // 실제 접근성 테스트 필요시 구현
            return true;
        }

        // 우선순위 계산 (간단한 더미 구현)
        private int CalculatePriority(NetworkInterfaceItem item)
        {
            int priority = 0;
            if (item.IsActive) priority += 50;
            if (item.HasIPAddress) priority += 30;
            if (item.InterfaceType == NetworkInterfaceType.Ethernet || item.InterfaceType == NetworkInterfaceType.Wireless80211) priority += 20;
            return priority;
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
                                   "Npcap이 올바르게 설치되었는지 확인해주세요.",
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
                    // 접근 불가능한 어댑터는 회색으로 표시하지만 비활성화하지 않음
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
            try
            {
                // Get all network interfaces that are up and not loopback
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                                ni.NetworkInterfaceType != NetworkInterfaceType.Unknown)
                    .ToList();

                // Log the found interfaces for debugging
                foreach (var ni in interfaces)
                {
                    var ipProperties = ni.GetIPProperties();
                    var ipv4Addresses = ipProperties.UnicastAddresses
                        .Where(addr => addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        .Select(addr => addr.Address)
                        .ToList();

                    LogHelper.LogInfo($"Interface: {ni.Name} ({ni.Description}), Type: {ni.NetworkInterfaceType}, " +
                                    $"Status: {ni.OperationalStatus}, IPv4: {string.Join(", ", ipv4Addresses)}");
                }

                return interfaces;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"네트워크 인터페이스 조회 중 오류: {ex.Message}");
                return new List<NetworkInterface>();
            }
        }

        private string GetInterfaceTooltip(NetworkInterfaceItem item)
        {
            try
            {
                if (item.InterfaceInfo == null)
                    return "인터페이스 정보가 없습니다.";

                var ipProperties = item.InterfaceInfo.GetIPProperties();
                var ipv4Addresses = ipProperties.UnicastAddresses
                    .Where(addr => addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    .Select(addr => addr.Address)
                    .ToList();

                var ipv6Addresses = ipProperties.UnicastAddresses
                    .Where(addr => addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                    .Select(addr => addr.Address)
                    .ToList();

                var tooltip = new System.Text.StringBuilder();
                tooltip.AppendLine($"이름: {item.Name}");
                tooltip.AppendLine($"설명: {item.InterfaceInfo.Description}");
                tooltip.AppendLine($"유형: {item.InterfaceInfo.NetworkInterfaceType}");
                tooltip.AppendLine($"상태: {item.InterfaceInfo.OperationalStatus}");
                tooltip.AppendLine($"물리 주소: {BitConverter.ToString(item.InterfaceInfo.GetPhysicalAddress().GetAddressBytes())}");
                tooltip.AppendLine($"속도: {item.InterfaceInfo.Speed / 1000000} Mbps");

                if (ipv4Addresses.Count > 0)
                {
                    tooltip.AppendLine("\nIPv4 주소:");
                    foreach (var ip in ipv4Addresses)
                    {
                        tooltip.AppendLine($"- {ip}");
                    }
                }

                if (ipv6Addresses.Count > 0)
                {
                    tooltip.AppendLine("\nIPv6 주소:");
                    foreach (var ip in ipv6Addresses)
                    {
                        tooltip.AppendLine($"- {ip}");
                    }
                }

                return tooltip.ToString();
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"인터페이스 툴팁 생성 중 오류: {ex.Message}");
                return $"인터페이스 정보를 가져올 수 없습니다: {ex.Message}";
            }
        }

        private string FormatInterfaceName(NetworkInterfaceItem item)
        {
            try
            {
                if (item.InterfaceInfo == null)
                    return item.Name;

                var ipProperties = item.InterfaceInfo.GetIPProperties();
                var ipv4Address = ipProperties.UnicastAddresses
                    .FirstOrDefault(addr => addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.Address;

                var interfaceName = string.IsNullOrEmpty(item.InterfaceInfo.Description) ? item.InterfaceInfo.Name : item.InterfaceInfo.Description;

                if (ipv4Address != null)
                {
                    return $"{interfaceName} ({ipv4Address})";
                }

                return interfaceName;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"인터페이스 이름 포맷 중 오류: {ex.Message}");
                return item.InterfaceInfo != null ? item.InterfaceInfo.Name : item.Name;
            }
        }
    }
}