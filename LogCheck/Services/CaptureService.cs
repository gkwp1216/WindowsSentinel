using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using LogCheck.Models;
using PacketDotNet;
using SharpPcap;

namespace LogCheck.Services
{
    [SupportedOSPlatform("windows")]
    public class CaptureService : ICaptureService, IDisposable
    {
        private ICaptureDevice? _device;
        private CancellationTokenSource? _cts;
        private string? _bpf;
        private string? _nicId;
        private bool _disposing;
        private long _packetsReceived;
        private DateTime _lastMetricsTime = DateTime.Now;

        private readonly ProcessNetworkMapper _processMapper = new ProcessNetworkMapper();

        public bool IsRunning { get; private set; }

        public event EventHandler<PacketDto>? OnPacket;
        public event EventHandler<Exception>? OnError;
        public event EventHandler<CaptureMetrics>? OnMetrics;

        public async Task StartAsync(string? bpfFilter = null, string? nicId = null)
        {
            if (IsRunning) return;
            _bpf = bpfFilter;
            _nicId = nicId;

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            IsRunning = true;
            _ = Task.Run(() => RunLoop(token), token);
            await Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            if (!IsRunning) return;
            IsRunning = false;
            try
            {
                _cts?.Cancel();
                _device?.StopCapture();
                _device?.Close();
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);
            }
            finally
            {
                _device = null;
                _cts?.Dispose();
                _cts = null;
            }
            await Task.CompletedTask;
        }

        private void RunLoop(CancellationToken token)
        {
            var backoffMs = 500; // exponential backoff base
            System.Diagnostics.Debug.WriteLine("🔍 [CaptureService] RunLoop 시작");


            while (!token.IsCancellationRequested && !_disposing)
            {
                try
                {
                    if (!TryOpenDevice())
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ [CaptureService] 디바이스 열기 실패 - {backoffMs}ms 후 재시도");
                        Thread.Sleep(Math.Min(backoffMs, 5000));
                        backoffMs = Math.Min(backoffMs * 2, 5000);
                        continue;
                    }

                    System.Diagnostics.Debug.WriteLine($"✅ [CaptureService] 디바이스 열기 성공: {_device?.Name}");
                    backoffMs = 500; // reset backoff after success
                    _device!.OnPacketArrival += OnPacketArrival;
                    _device.StartCapture();
                    System.Diagnostics.Debug.WriteLine("🎯 [CaptureService] 패킷 캡처 시작됨");

                    // Block until canceled
                    while (!token.IsCancellationRequested)
                    {
                        Thread.Sleep(200);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ [CaptureService] 오류: {ex.Message}");
                    OnError?.Invoke(this, ex);
                    Thread.Sleep(Math.Min(backoffMs, 5000));
                    backoffMs = Math.Min(backoffMs * 2, 5000);
                }
                finally
                {
                    try
                    {
                        if (_device != null)
                        {
                            try
                            {
                                _device.OnPacketArrival -= OnPacketArrival;
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"⚠️ 패킷 이벤트 해제 오류: {ex.Message}");
                            }

                            try
                            {
                                _device.StopCapture();
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"⚠️ 캡처 중지 오류: {ex.Message}");
                            }

                            try
                            {
                                _device.Close();
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"⚠️ 디바이스 닫기 오류: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ CaptureService 정리 중 오류: {ex.Message}");
                    }
                    finally 
                    { 
                        _device = null; 
                    }
                }
            }
        }

        private bool TryOpenDevice()
        {
            var devices = CaptureDeviceList.Instance;
            System.Diagnostics.Debug.WriteLine($"🔍 [CaptureService] 사용 가능한 디바이스 수: {devices?.Count ?? 0}");


            if (devices == null || devices.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("❌ [CaptureService] 네트워크 디바이스를 찾을 수 없습니다. Npcap이 설치되어 있는지 확인하세요.");
                return false;
            }

            _device = _nicId != null
                ? devices.FirstOrDefault(d => d.Name?.Contains(_nicId, StringComparison.OrdinalIgnoreCase) == true)
                : devices.FirstOrDefault(d => !d.Name?.Contains("Loopback", StringComparison.OrdinalIgnoreCase) == true && !d.Name?.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase) == true)
                  ?? devices.First(); // 루프백이 아닌 인터페이스가 없으면 첫 번째 사용

            if (_device == null)
                return false;

            System.Diagnostics.Debug.WriteLine($"🎯 [CaptureService] 선택된 디바이스: {_device.Name} - 설명: {_device.Description}");

            _device.Open();
            try
            {
                if (!string.IsNullOrWhiteSpace(_bpf))
                    _device.Filter = _bpf;
            }
            catch
            {
                // ignore filter errors – capture without filter
            }
            return true;
        }

        private void OnPacketArrival(object? sender, PacketCapture e)
        {
            try
            {
                var raw = e.GetPacket();
                if (raw == null) return;

                // 디버그: 패킷 캡처 확인 (처음 10개만)

                if (_packetsReceived < 10)
                {
                    System.Diagnostics.Debug.WriteLine($"📥 [CaptureService] 패킷 캡처됨 #{_packetsReceived + 1}");
                }

                var packet = Packet.ParsePacket(raw.LinkLayerType, raw.Data);
                var ip = packet.Extract<IPPacket>();
                var tcp = packet.Extract<TcpPacket>();
                var udp = packet.Extract<UdpPacket>();
                var icmp = packet.Extract<IcmpV4Packet>();

                ProtocolKind proto = ProtocolKind.Other;
                int? sport = null, dport = null;
                uint flags = 0;
                if (tcp != null)
                {
                    proto = ProtocolKind.TCP;
                    sport = tcp.SourcePort;
                    dport = tcp.DestinationPort;
                    flags = (uint)((tcp.Synchronize ? 1 : 0) | (tcp.Acknowledgment ? 2 : 0) | (tcp.Finished ? 4 : 0) | (tcp.Reset ? 8 : 0) | (tcp.Push ? 16 : 0) | (tcp.Urgent ? 32 : 0));
                }
                else if (udp != null)
                {
                    proto = ProtocolKind.UDP;
                    sport = udp.SourcePort;
                    dport = udp.DestinationPort;
                }
                else if (icmp != null)
                {
                    proto = ProtocolKind.ICMP;
                }

                int? processId = null;
                string? processName = null;

                if (ip?.SourceAddress != null && ip.DestinationAddress != null)
                {
                    (processId, processName) = _processMapper.GetProcessForConnection(proto, ip.SourceAddress, sport ?? 0, ip.DestinationAddress, dport ?? 0);
                }

                var dto = new PacketDto
                {
                    Timestamp = DateTime.UtcNow,
                    Protocol = proto,
                    SrcIp = ip?.SourceAddress?.ToString() ?? string.Empty,
                    DstIp = ip?.DestinationAddress?.ToString() ?? string.Empty,
                    SrcPort = sport,
                    DstPort = dport,
                    Length = raw.Data.Length,
                    Flags = flags,
                    ProcessId = processId,
                    ProcessName = processName ?? "Unknown"
                };

                OnPacket?.Invoke(this, dto);

                // 메트릭 업데이트
                Interlocked.Increment(ref _packetsReceived);
                CheckAndSendMetrics();
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);
            }
        }

        private void CheckAndSendMetrics()
        {
            var now = DateTime.Now;
            if ((now - _lastMetricsTime).TotalSeconds >= 5) // 5초마다 메트릭 전송
            {
                var packetsReceived = Interlocked.Read(ref _packetsReceived);
                var throughput = packetsReceived / Math.Max(1, (now - _lastMetricsTime).TotalSeconds);

                var metrics = new CaptureMetrics(
                    Dropped: 0, // 드롭된 패킷 수 (현재 추적하지 않음)
                    QueueLength: 0, // 큐 길이 (현재 추적하지 않음)
                    ThroughputPps: throughput
                );

                OnMetrics?.Invoke(this, metrics);
                _lastMetricsTime = now;
            }
        }

        public void Dispose()
        {
            _disposing = true;
            try { _cts?.Cancel(); } catch { }
            try { _device?.StopCapture(); } catch { }
            try { _device?.Close(); } catch { }
            _cts?.Dispose();
            _cts = null;
            _device = null;
        }
    }
}
