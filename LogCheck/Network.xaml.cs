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

namespace WindowsSentinel
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
        private ICaptureDevice? captureDevice;
        private bool isCapturing = false;

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
            captureDevice = devices[0];
            captureDevice.OnPacketArrival += CaptureDevice_OnPacketArrival;
            captureDevice.Open();
            captureDevice.StartCapture();
            isCapturing = true;
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
            var raw = e.GetPacket();
            var time = raw.Timeval.Date;
            var packet = Packet.ParsePacket(raw.LinkLayerType, raw.Data);
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

                Dispatcher.Invoke(() =>
                {
                    packetConnections.Add(new PacketConnectionModel
                    {
                        Time = time,
                        Protocol = protocol,
                        Source = ipPacket.SourceAddress.ToString(),
                        SourcePort = srcPort,
                        Destination = ipPacket.DestinationAddress.ToString(),
                        DestinationPort = dstPort
                    });
                });
            }
        }
    }
}

