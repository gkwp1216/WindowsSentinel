using System;
using System.Threading;
using System.Threading.Tasks;
using LogCheck.Models;
using LogCheck.Services;

namespace LogCheck.Services
{
    /// <summary>
    /// 앱 전체에서 공유되는 모니터링 허브. 캡처와 프로세스 매핑을 함께 시작/중지한다.
    /// 싱글톤 패턴으로 간단히 제공한다.
    /// </summary>
    public sealed class MonitoringHub
    {
        private static readonly Lazy<MonitoringHub> _instance = new(() => new MonitoringHub());
        public static MonitoringHub Instance => _instance.Value;

        private readonly ICaptureService _captureService;
        private readonly ProcessNetworkMapper _processMapper;
        private volatile int _isRunning; // 0:false 1:true

        // Events
        public event EventHandler<bool>? MonitoringStateChanged; // true: started, false: stopped
        public event EventHandler<Exception>? ErrorOccurred;
        public event EventHandler<CaptureMetrics>? MetricsUpdated;
        public event EventHandler<PacketDto>? PacketArrived; // optional, can be heavy

        private MonitoringHub()
        {
            _captureService = new CaptureService();
            _processMapper = new ProcessNetworkMapper();

            // Wire inner service events
            _captureService.OnError += (s, ex) => ErrorOccurred?.Invoke(this, ex);
            _captureService.OnMetrics += (s, m) => MetricsUpdated?.Invoke(this, m);
            _captureService.OnPacket += (s, p) => PacketArrived?.Invoke(this, p);
        }

        public ICaptureService Capture => _captureService;
        public ProcessNetworkMapper ProcessMapper => _processMapper;
        public bool IsRunning => _isRunning == 1;

        public async Task StartAsync(string? bpf = null, string? nicId = null)
        {
            if (Interlocked.Exchange(ref _isRunning, 1) == 1)
                return; // 이미 실행 중

            try { await _processMapper.StartMonitoringAsync().ConfigureAwait(false); }
            catch (Exception ex)
            {
                // Report but continue
                ErrorOccurred?.Invoke(this, ex);
            }

            try
            {
                await _captureService.StartAsync(bpf, nicId).ConfigureAwait(false);
            }
            catch
            {
                Interlocked.Exchange(ref _isRunning, 0);
                throw;
            }

            // Notify running
            _ = Task.Run(() => MonitoringStateChanged?.Invoke(this, true));
        }

        public async Task StopAsync()
        {
            if (Interlocked.Exchange(ref _isRunning, 0) == 0)
                return; // 이미 정지

            Exception? firstEx = null;
            try { await _captureService.StopAsync().ConfigureAwait(false); } catch (Exception ex) { firstEx ??= ex; }
            try { await _processMapper.StopMonitoringAsync().ConfigureAwait(false); } catch (Exception ex) { firstEx ??= ex; }

            // Notify stopped
            _ = Task.Run(() => MonitoringStateChanged?.Invoke(this, false));

            if (firstEx != null) throw firstEx;
        }
    }
}
