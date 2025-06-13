using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Runtime.Versioning;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net.NetworkInformation;

namespace LogCheck
{
    public class PacketConnectionModel
    {
        public DateTime Time { get; set; }
        public string Protocol { get; set; }
        public string Source { get; set; }
        public int? SourcePort { get; set; }
        public string Destination { get; set; }
        public int? DestinationPort { get; set; }
    }

    [SupportedOSPlatform("windows")]
    public partial class Network : Page
    {
        private DispatcherTimer? loadingTextTimer;
        private int dotCount = 0;
        private const int maxDots = 3;
        private string baseText = "검사 중";
        private ObservableCollection<PacketConnectionModel> packetConnections = new ObservableCollection<PacketConnectionModel>();
        private ObservableCollection<PacketConnectionModel> filteredConnections = new ObservableCollection<PacketConnectionModel>();
        private ICaptureDevice? captureDevice;
        private bool isCapturing = false;

        // 필터 설정
        private string? protocolFilter;
        private string? sourceIPFilter;
        private string? destinationIPFilter;
        private string? sourcePortFilter;
        private string? destinationPortFilter;
        private bool isFilterActive = false;

        // 필터링된 패킷 통계
        private int totalPackets = 0;
        private int filteredPackets = 0;

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

        private async void BtnCheck_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                ShowLoadingOverlay();
                packetConnections.Clear();
                StartPacketCapture();
                await Task.Delay(5000); // 5초간 캡처
                StopPacketCapture();
                eventDataGrid.ItemsSource = null;
                eventDataGrid.ItemsSource = packetConnections;
                if (packetConnections.Count == 0)
                {
                    MessageBox.Show("캡처된 네트워크 패킷이 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"패킷 캡처 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                HideLoadingOverlay();
            }
        }

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

        private void StartPacketCapture()
        {
            if (isCapturing) return;
            var devices = CaptureDeviceList.Instance;
            if (devices.Count == 0)
            {
                MessageBox.Show("네트워크 인터페이스를 찾을 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // 실제 네트워크 어댑터 찾기
                ICaptureDevice selectedDevice = null;
                string deviceList = "사용 가능한 네트워크 인터페이스:\n\n";
                
                for (int i = 0; i < devices.Count; i++)
                {
                    var currentDevice = devices[i] as LibPcapLiveDevice;
                    deviceList += $"{i}: {currentDevice?.Description}\n";
                    
                    // 실제 네트워크 어댑터 선택 (WAN miniport 제외)
                    if (currentDevice != null && 
                        !currentDevice.Description.Contains("WAN Miniport") && 
                        !currentDevice.Description.Contains("Loopback") &&
                        !currentDevice.Description.Contains("Bluetooth"))
                    {
                        selectedDevice = currentDevice;
                        break;
                    }
                }

                if (selectedDevice == null)
                {
                    MessageBox.Show($"적절한 네트워크 어댑터를 찾을 수 없습니다.\n\n{deviceList}", 
                                  "오류", 
                                  MessageBoxButton.OK, 
                                  MessageBoxImage.Warning);
                    return;
                }

                captureDevice = selectedDevice;
                
                // 캡처 모드 설정
                var liveDevice = captureDevice as LibPcapLiveDevice;
                if (liveDevice != null)
                {
                    // 프로미스큐어스 모드로 설정
                    liveDevice.Open(DeviceModes.Promiscuous);
                    
                    // 디버그 메시지 추가
                    MessageBox.Show($"캡처 시작: {liveDevice.Description}\n" +
                                  $"MAC 주소: {liveDevice.MacAddress}\n" +
                                  $"IP 주소: {string.Join(", ", liveDevice.Addresses.Select(a => a.Addr))}", 
                                  "캡처 정보");
                }
                else
                {
                    captureDevice.Open();
                }

                captureDevice.OnPacketArrival += CaptureDevice_OnPacketArrival;
                captureDevice.StartCapture();
                isCapturing = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"캡처 시작 중 오류: {ex.Message}\n\n" +
                               $"스택 트레이스: {ex.StackTrace}", 
                               "오류", 
                               MessageBoxButton.OK, 
                               MessageBoxImage.Error);
            }
        }

        private void StopPacketCapture()
        {
            if (captureDevice != null && isCapturing)
            {
                captureDevice.StopCapture();
                captureDevice.Dispose();
                captureDevice.OnPacketArrival -= CaptureDevice_OnPacketArrival;
                isCapturing = false;
            }
        }

        private void CaptureDevice_OnPacketArrival(object sender, PacketCapture e)
        {
            try
            {
                var raw = e.GetPacket();
                var time = raw.Timeval.Date;
                var packet = Packet.ParsePacket(raw.LinkLayerType, raw.Data);
                
                // 디버그 메시지 추가
                Debug.WriteLine($"패킷 수신: {packet.GetType().Name}");
                
                var ipPacket = packet.Extract<IPPacket>();
                if (ipPacket != null)
                {
                    string protocol = ipPacket.Protocol.ToString();
                    int? srcPort = null, dstPort = null;

                    if (ipPacket.Protocol == PacketDotNet.ProtocolType.Tcp)
                    {
                        var tcpPacket = packet.Extract<TcpPacket>();
                        if (tcpPacket != null)
                        {
                            srcPort = tcpPacket.SourcePort;
                            dstPort = tcpPacket.DestinationPort;
                        }
                    }
                    else if (ipPacket.Protocol == PacketDotNet.ProtocolType.Udp)
                    {
                        var udpPacket = packet.Extract<UdpPacket>();
                        if (udpPacket != null)
                        {
                            srcPort = udpPacket.SourcePort;
                            dstPort = udpPacket.DestinationPort;
                        }
                    }

                    var newPacket = new PacketConnectionModel
                    {
                        Time = time,
                        Protocol = protocol,
                        Source = ipPacket.SourceAddress.ToString(),
                        SourcePort = srcPort,
                        Destination = ipPacket.DestinationAddress.ToString(),
                        DestinationPort = dstPort
                    };

                    Dispatcher.Invoke(() =>
                    {
                        packetConnections.Add(newPacket);
                        totalPackets++;

                        if (isFilterActive)
                        {
                            if (IsPacketMatchFilter(newPacket))
                            {
                                filteredConnections.Add(newPacket);
                                filteredPackets++;
                            }
                            eventDataGrid.ItemsSource = filteredConnections;
                        }
                        else
                        {
                            eventDataGrid.ItemsSource = packetConnections;
                        }
                        UpdateFilterStatus();
                    });
                }
                else
                {
                    // ARP나 다른 패킷 타입도 로깅
                    Debug.WriteLine($"IP가 아닌 패킷 수신: {packet.GetType().Name}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"패킷 처리 중 오류: {ex.Message}");
            }
        }

        private void ProtocolFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProtocolFilter.SelectedItem is ComboBoxItem selectedItem)
            {
                protocolFilter = selectedItem.Content.ToString() == "모두" ? null : selectedItem.Content.ToString();
                UpdateFilterStatus();
            }
        }

        private void IPFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                string text = textBox.Text.Trim();
                if (textBox.Name == "SourceIPFilter")
                {
                    sourceIPFilter = string.IsNullOrEmpty(text) ? null : text;
                }
                else if (textBox.Name == "DestinationIPFilter")
                {
                    destinationIPFilter = string.IsNullOrEmpty(text) ? null : text;
                }
                UpdateFilterStatus();
            }
        }

        private void PortFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                string text = textBox.Text.Trim();
                if (textBox.Name == "SourcePortFilter")
                {
                    sourcePortFilter = string.IsNullOrEmpty(text) ? null : text;
                }
                else if (textBox.Name == "DestinationPortFilter")
                {
                    destinationPortFilter = string.IsNullOrEmpty(text) ? null : text;
                }
                UpdateFilterStatus();
            }
        }

        private void UpdateFilterStatus()
        {
<<<<<<< HEAD
            if (FilterStatusText == null || FilterStatusBorder == null) return;

            var filterConditions = new List<string>();

            if (!string.IsNullOrWhiteSpace(protocolFilter))
                filterConditions.Add($"프로토콜: {protocolFilter}");

            if (!string.IsNullOrWhiteSpace(sourceIPFilter))
                filterConditions.Add($"소스 IP: {sourceIPFilter}");

            if (!string.IsNullOrWhiteSpace(destinationIPFilter))
                filterConditions.Add($"목적지 IP: {destinationIPFilter}");

            if (!string.IsNullOrWhiteSpace(sourcePortFilter))
                filterConditions.Add($"소스 포트: {sourcePortFilter}");

            if (!string.IsNullOrWhiteSpace(destinationPortFilter))
                filterConditions.Add($"목적지 포트: {destinationPortFilter}");

            if (filterConditions.Count > 0)
            {
                FilterStatusText.Text = $"현재 필터: {string.Join(", ", filterConditions)}";
                FilterStatusBorder.Visibility = Visibility.Visible;
                isFilterActive = true;
            }
            else
            {
                FilterStatusBorder.Visibility = Visibility.Collapsed;
                isFilterActive = false;
            }

            // 필터링된 패킷 수 업데이트
            filteredPackets = filteredConnections.Count;
            totalPackets = packetConnections.Count;
=======
            var filterConditions = new List<string>();
            
            if (!string.IsNullOrEmpty(protocolFilter))
                filterConditions.Add($"프로토콜: {protocolFilter}");
            if (!string.IsNullOrEmpty(sourceIPFilter))
                filterConditions.Add($"소스 IP: {sourceIPFilter}");
            if (!string.IsNullOrEmpty(destinationIPFilter))
                filterConditions.Add($"목적지 IP: {destinationIPFilter}");
            if (!string.IsNullOrEmpty(sourcePortFilter))
                filterConditions.Add($"소스 포트: {sourcePortFilter}");
            if (!string.IsNullOrEmpty(destinationPortFilter))
                filterConditions.Add($"목적지 포트: {destinationPortFilter}");

            if (filterConditions.Any())
            {
                FilterStatusText.Text = $"현재 필터: {string.Join(", ", filterConditions)}\n" +
                                      $"총 패킷: {totalPackets}, 필터링된 패킷: {filteredPackets} " +
                                      $"({(totalPackets > 0 ? (filteredPackets * 100.0 / totalPackets).ToString("F1") : "0")}%)";
                FilterStatusBorder.Visibility = Visibility.Visible;
            }
            else
            {
                FilterStatusText.Text = string.Empty;
                FilterStatusBorder.Visibility = Visibility.Collapsed;
            }
>>>>>>> origin/main
        }

        private void ApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private void ResetFilter_Click(object sender, RoutedEventArgs e)
        {
            // 필터 초기화
            ProtocolFilter.SelectedIndex = 0;
            SourceIPFilter.Text = string.Empty;
            DestinationIPFilter.Text = string.Empty;
            SourcePortFilter.Text = string.Empty;
            DestinationPortFilter.Text = string.Empty;

            // 필터 상태 초기화
            protocolFilter = null;
            sourceIPFilter = null;
            destinationIPFilter = null;
            sourcePortFilter = null;
            destinationPortFilter = null;
            isFilterActive = false;

            // 모든 패킷 표시
            eventDataGrid.ItemsSource = packetConnections;
            FilterStatusText.Text = string.Empty;
        }

        private void ApplyFilters()
        {
            if (!isFilterActive && 
                string.IsNullOrEmpty(protocolFilter) && 
                string.IsNullOrEmpty(sourceIPFilter) && 
                string.IsNullOrEmpty(destinationIPFilter) && 
                string.IsNullOrEmpty(sourcePortFilter) && 
                string.IsNullOrEmpty(destinationPortFilter))
            {
                return;
            }

            filteredConnections.Clear();
            filteredPackets = 0;
            totalPackets = packetConnections.Count;

            foreach (var packet in packetConnections)
            {
                if (IsPacketMatchFilter(packet))
                {
                    filteredConnections.Add(packet);
                    filteredPackets++;
                }
            }

            eventDataGrid.ItemsSource = filteredConnections;
            isFilterActive = true;
            UpdateFilterStatus();
        }

        private bool IsPacketMatchFilter(PacketConnectionModel packet)
        {
            // 프로토콜 필터
            if (!string.IsNullOrEmpty(protocolFilter) && 
                !packet.Protocol.Equals(protocolFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // 소스 IP 필터
            if (!string.IsNullOrEmpty(sourceIPFilter) && 
                !packet.Source.Contains(sourceIPFilter))
            {
                return false;
            }

            // 목적지 IP 필터
            if (!string.IsNullOrEmpty(destinationIPFilter) && 
                !packet.Destination.Contains(destinationIPFilter))
            {
                return false;
            }

            // 소스 포트 필터
            if (!string.IsNullOrEmpty(sourcePortFilter) && 
                packet.SourcePort.HasValue)
            {
                if (!int.TryParse(sourcePortFilter, out int filterPort) || 
                    packet.SourcePort.Value != filterPort)
                {
                    return false;
                }
            }

            // 목적지 포트 필터
            if (!string.IsNullOrEmpty(destinationPortFilter) && 
                packet.DestinationPort.HasValue)
            {
                if (!int.TryParse(destinationPortFilter, out int filterPort) || 
                    packet.DestinationPort.Value != filterPort)
                {
                    return false;
                }
            }

            return true;
        }
    }
}

