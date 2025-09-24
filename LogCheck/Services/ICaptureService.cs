using LogCheck.Models;

namespace LogCheck.Services
{
    public record CaptureMetrics(long Dropped, int QueueLength, double ThroughputPps);

    public interface ICaptureService
    {
        bool IsRunning { get; }
        event EventHandler<PacketDto>? OnPacket;
        event EventHandler<Exception>? OnError;
        event EventHandler<CaptureMetrics>? OnMetrics;

        Task StartAsync(string? bpfFilter = null, string? nicId = null);
        Task StopAsync();
    }
}
