using LogCheck.Models;
using PacketDotNet;
using SharpPcap;

namespace LogCheck.Services
{
    public class CaptureService : ICaptureService, IDisposable
    {
        private ICaptureDevice? _device;
        private CancellationTokenSource? _cts;
        private string? _bpf;
        private string? _nicId;
        private bool _disposing;

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
            while (!token.IsCancellationRequested && !_disposing)
            {
                try
                {
                    if (!TryOpenDevice())
                    {
                        Thread.Sleep(Math.Min(backoffMs, 5000));
                        backoffMs = Math.Min(backoffMs * 2, 5000);
                        continue;
                    }

                    backoffMs = 500; // reset backoff after success
                    _device!.OnPacketArrival += OnPacketArrival;
                    _device.StartCapture();

                    // Block until canceled
                    while (!token.IsCancellationRequested)
                    {
                        Thread.Sleep(200);
                    }
                }
                catch (Exception ex)
                {
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
                            _device.OnPacketArrival -= OnPacketArrival;
                            _device.StopCapture();
                            _device.Close();
                        }
                    }
                    catch { }
                    finally { _device = null; }
                }
            }
        }

        private bool TryOpenDevice()
        {
            var devices = CaptureDeviceList.Instance;
            if (devices == null || devices.Count == 0)
                return false;

            _device = _nicId != null
                ? devices.FirstOrDefault(d => d.Name?.Contains(_nicId, StringComparison.OrdinalIgnoreCase) == true)
                : devices.First();

            if (_device == null)
                return false;

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

                var dto = new PacketDto
                {
                    Timestamp = DateTime.UtcNow,
                    Protocol = proto,
                    SrcIp = ip?.SourceAddress?.ToString() ?? string.Empty,
                    DstIp = ip?.DestinationAddress?.ToString() ?? string.Empty,
                    SrcPort = sport,
                    DstPort = dport,
                    Length = raw.Data.Length,
                    Flags = flags
                };

                OnPacket?.Invoke(this, dto);
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);
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
