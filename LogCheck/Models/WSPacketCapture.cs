using SharpPcap;
using PacketDotNet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace LogCheck.Models
{
    public class WSPacketCapture : IDisposable
    {
        private ICaptureDevice? device;
        private readonly ConcurrentDictionary<string, PacketInfo> packetCache;
        private readonly object lockObject = new object();
        private bool isCapturing;
        private Task? captureTask;
        private string _currentInterfaceName = string.Empty;

        public event EventHandler<PacketInfo>? PacketCaptured;
        public event EventHandler<string>? CaptureError;
        public event EventHandler<string>? ErrorOccurred;

        public WSPacketCapture()
        {
            packetCache = new ConcurrentDictionary<string, PacketInfo>();
        }

        public WSPacketCapture(ICaptureDevice captureDevice, string interfaceName = "")
        {
            packetCache = new ConcurrentDictionary<string, PacketInfo>();
            device = captureDevice;
            _currentInterfaceName = interfaceName;
        }

        public async Task StartCaptureAsync()
        {
            if (isCapturing) return;

            try
            {
                var devices = CaptureDeviceList.Instance;
                if (devices.Count < 1)
                {
                    OnCaptureError("사용 가능한 네트워크 인터페이스가 없습니다.");
                    return;
                }

                device = devices[0];
                device.OnPacketArrival += Device_OnPacketArrival;
                device.Open();
                device.Filter = "tcp";

                isCapturing = true;
                captureTask = Task.Run(() => device.StartCapture());
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                OnCaptureError($"패킷 캡처 시작 중 오류 발생: {ex.Message}");
            }
        }

        public async Task StopCaptureAsync()
        {
            if (!isCapturing || device == null) return;

            try
            {
                isCapturing = false;
                device.StopCapture();
                device.Close();
                device = null;

                if (captureTask != null)
                {
                    await captureTask;
                    captureTask = null;
                }
            }
            catch (Exception ex)
            {
                OnCaptureError($"패킷 캡처 중지 중 오류 발생: {ex.Message}");
            }
        }

        private void Device_OnPacketArrival(object? sender, PacketCapture e)
        {
            try
            {
                var rawPacket = e.GetPacket();
                if (rawPacket == null) return;

                var packet = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);

                var tcpPacket = packet?.Extract<TcpPacket>();
                var ipPacket = packet?.Extract<IPPacket>();

                if (tcpPacket == null || ipPacket == null) return;

                var packetInfo = new PacketInfo
                {
                    Timestamp = DateTime.Now,
                    SourceIP = ipPacket.SourceAddress.ToString(),
                    DestinationIP = ipPacket.DestinationAddress.ToString(),
                    SourcePort = tcpPacket.SourcePort,
                    DestinationPort = tcpPacket.DestinationPort,
                    Protocol = "TCP",
                    Flags = GetTcpFlags(tcpPacket),
                    Length = rawPacket.Data.Length,
                    PacketSize = rawPacket.Data.Length,
                    ProcessId = GetProcessId(ipPacket.SourceAddress, tcpPacket.SourcePort)
                };

                var key = $"{packetInfo.SourceIP}:{packetInfo.SourcePort}-{packetInfo.DestinationIP}:{packetInfo.DestinationPort}";
                packetCache.AddOrUpdate(key, packetInfo, (_, _) => packetInfo);

                PacketCaptured?.Invoke(this, packetInfo);
            }
            catch (Exception ex)
            {
                OnCaptureError($"패킷 처리 중 오류 발생: {ex.Message}");
            }
        }



        private bool IsLocalIP(string ipAddress)
        {
            try
            {
                var ip = IPAddress.Parse(ipAddress);
                
                // 로컬 IP 주소 범위 확인
                if (IPAddress.IsLoopback(ip)) return true;
                
                // 사설 IP 주소 범위 확인
                var bytes = ip.GetAddressBytes();
                if (bytes.Length == 4) // IPv4
                {
                    // 10.0.0.0/8
                    if (bytes[0] == 10) return true;
                    
                    // 172.16.0.0/12
                    if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
                    
                    // 192.168.0.0/16
                    if (bytes[0] == 192 && bytes[1] == 168) return true;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        private string GetTcpFlags(TcpPacket tcpPacket)
        {
            var flags = new List<string>();
            if (tcpPacket.Synchronize) flags.Add("SYN");
            if (tcpPacket.Acknowledgment) flags.Add("ACK");
            if (tcpPacket.Finished) flags.Add("FIN");
            if (tcpPacket.Reset) flags.Add("RST");
            if (tcpPacket.Push) flags.Add("PSH");
            if (tcpPacket.Urgent) flags.Add("URG");
            return string.Join(",", flags);
        }

        private int? GetProcessId(IPAddress localAddress, ushort localPort)
        {
            try
            {
                // .NET의 TcpConnectionInformation에는 ProcessId가 없으므로
                // 프로세스 ID를 가져오려면 다른 방법을 사용해야 합니다.
                // 여기서는 간단히 null을 반환하거나 WMI/P/Invoke를 사용할 수 있습니다.
                
                // 향후 구현을 위해 null 반환
                return null;
            }
            catch
            {
                return null;
            }
        }

        private void OnCaptureError(string message)
        {
            CaptureError?.Invoke(this, message);
        }

        public IEnumerable<PacketInfo> GetCapturedPackets()
        {
            return packetCache.Values.OrderByDescending(p => p.Timestamp);
        }

        public void ClearCache()
        {
            packetCache.Clear();
        }

        // 동기 메서드들 추가
        public void StartCapture()
        {
            if (isCapturing) return;

            try
            {
                if (device == null)
                {
                    var devices = CaptureDeviceList.Instance;
                    if (devices.Count < 1)
                    {
                        OnErrorOccurred("사용 가능한 네트워크 인터페이스가 없습니다.");
                        return;
                    }
                    device = devices[0];
                }

                System.Diagnostics.Debug.WriteLine($"시도하는 장치: {device.Description}");
                
                device.OnPacketArrival += Device_OnPacketArrival;
                device.Open(); // Simplified Open call
                
                try
                {
                    device.Filter = "tcp";
                }
                catch (Exception filterEx)
                {
                    System.Diagnostics.Debug.WriteLine($"필터 설정 실패: {filterEx.Message}");
                    // 필터 설정에 실패해도 캡처는 계속 진행
                }

                device.StartCapture();
                isCapturing = true;
                System.Diagnostics.Debug.WriteLine("캡처 시작 성공");
            }
            catch (Exception ex)
            {
                try { device?.Close(); } catch { }
                isCapturing = false;
                OnErrorOccurred($"패킷 캡처 시작 중 오류 발생: {ex.Message}");
            }
        }

        public void StopCapture()
        {
            if (!isCapturing || device == null) return;

            try
            {
                isCapturing = false;
                device.StopCapture();
                device.Close();
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"패킷 캡처 중지 중 오류 발생: {ex.Message}");
            }
        }

        private void OnErrorOccurred(string message)
        {
            ErrorOccurred?.Invoke(this, message);
            CaptureError?.Invoke(this, message);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopCapture();
                device = null;
            }
        }
    }

    public class PacketInfo
    {
        public DateTime Timestamp { get; set; }
        public string SourceIP { get; set; } = string.Empty;
        public string DestinationIP { get; set; } = string.Empty;
        public ushort SourcePort { get; set; }
        public ushort DestinationPort { get; set; }
        public string Protocol { get; set; } = string.Empty;
        public string Flags { get; set; } = string.Empty;
        public int Length { get; set; }
        public int? ProcessId { get; set; }
        
        // 추가된 속성들
        public string Direction { get; set; } = string.Empty;
        public long PacketSize { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
} 