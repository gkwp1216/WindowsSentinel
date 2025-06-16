using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using SharpPcap;
using PacketDotNet;
using System.Collections.Concurrent;
using WindowsSentinel.Models;

namespace LogCheck.Models
{
    public class PacketCapture : IDisposable
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

        public PacketCapture()
        {
            packetCache = new ConcurrentDictionary<string, PacketInfo>();
        }

        public PacketCapture(ICaptureDevice captureDevice, string interfaceName = "")
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
                device.Open(DeviceMode.Promiscuous);
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

        private void Device_OnPacketArrival(object sender, CaptureEventArgs e)
        {
            try
            {
                var rawPacket = e.Packet;
                
                // PacketDotNet 버전 호환성을 위해 try-catch로 감싸서 처리
                Packet? packet = null;
                try
                {
                    // 최신 버전의 PacketDotNet에서는 다른 방법을 사용할 수 있음
                    packet = Packet.ParsePacket(LinkLayers.Ethernet, rawPacket.Data);
                }
                catch
                {
                    // 대체 방법으로 패킷 파싱 시도
                    return;
                }

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

                // 장치 정보 로깅
                System.Diagnostics.Debug.WriteLine($"시도하는 장치: {device.Description}");
                System.Diagnostics.Debug.WriteLine($"장치 이름: {device.Name}");

                // 더 안전한 장치 열기 시도 - 타임아웃을 더 길게 설정
                bool captureStarted = false;
                Exception lastException = null;

                // 방법 1: 일반 모드, 짧은 타임아웃
                if (!captureStarted)
                {
                    try
                    {
                        device.OnPacketArrival += Device_OnPacketArrival;
                        device.Open(DeviceMode.Normal, 100);
                        
                        // 간단한 필터만 설정
                        try
                        {
                            device.Filter = "tcp";
                        }
                        catch
                        {
                            // 필터 설정 실패해도 계속 진행
                        }

                        device.StartCapture();
                        isCapturing = true;
                        captureStarted = true;
                        
                        System.Diagnostics.Debug.WriteLine("일반 모드로 캡처 시작 성공");
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        try { device.Close(); } catch { }
                        System.Diagnostics.Debug.WriteLine($"일반 모드 실패: {ex.Message}");
                    }
                }

                // 방법 2: Promiscuous 모드, 짧은 타임아웃
                if (!captureStarted)
                {
                    try
                    {
                        device.Open(DeviceMode.Promiscuous, 100);
                        
                        try
                        {
                            device.Filter = "tcp";
                        }
                        catch
                        {
                            // 필터 설정 실패해도 계속 진행
                        }

                        device.StartCapture();
                        isCapturing = true;
                        captureStarted = true;
                        
                        System.Diagnostics.Debug.WriteLine("Promiscuous 모드로 캡처 시작 성공");
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        try { device.Close(); } catch { }
                        System.Diagnostics.Debug.WriteLine($"Promiscuous 모드 실패: {ex.Message}");
                    }
                }

                // 방법 3: 일반 모드, 긴 타임아웃, 필터 없음
                if (!captureStarted)
                {
                    try
                    {
                        device.Open(DeviceMode.Normal, 5000);
                        device.StartCapture();
                        isCapturing = true;
                        captureStarted = true;
                        
                        System.Diagnostics.Debug.WriteLine("일반 모드 (필터 없음)로 캡처 시작 성공");
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        try { device.Close(); } catch { }
                        System.Diagnostics.Debug.WriteLine($"일반 모드 (필터 없음) 실패: {ex.Message}");
                    }
                }

                // 방법 4: Promiscuous 모드, 긴 타임아웃, 필터 없음
                if (!captureStarted)
                {
                    try
                    {
                        device.Open(DeviceMode.Promiscuous, 5000);
                        device.StartCapture();
                        isCapturing = true;
                        captureStarted = true;
                        
                        System.Diagnostics.Debug.WriteLine("Promiscuous 모드 (필터 없음)로 캡처 시작 성공");
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        try { device.Close(); } catch { }
                        System.Diagnostics.Debug.WriteLine($"Promiscuous 모드 (필터 없음) 실패: {ex.Message}");
                    }
                }

                if (!captureStarted)
                {
                    var errorMsg = $"네트워크 어댑터를 열 수 없습니다.\n\n" +
                                  $"장치: {device.Description}\n" +
                                  $"마지막 오류: {lastException?.Message}\n\n" +
                                  $"가능한 해결 방법:\n" +
                                  $"1. Windows Defender 방화벽에서 프로그램을 허용하세요\n" +
                                  $"2. Npcap을 재설치해보세요 (WinPcap 호환 모드 체크)\n" +
                                  $"3. 다른 네트워크 어댑터를 선택해보세요\n" +
                                  $"4. 시스템을 재부팅해보세요\n" +
                                  $"5. Npcap 서비스를 재시작해보세요";
                    
                    throw new Exception(errorMsg);
                }
            }
            catch (Exception ex)
            {
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