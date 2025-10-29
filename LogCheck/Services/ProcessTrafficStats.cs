using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LogCheck.Models;

namespace LogCheck.Services
{
    /// <summary>
    /// 프로세스별 네트워크 트래픽 통계를 관리하는 클래스
    /// </summary>
    public class ProcessTrafficStats
    {
        public int ProcessId { get; }
        public string ProcessName { get; }

        private readonly object _lock = new object();
        private readonly Queue<DateTime> _packetTimestamps = new Queue<DateTime>();
        private long _totalPackets = 0;
        private long _totalBytes = 0;

        public DateTime LastAlertTime { get; set; } = DateTime.MinValue;

        public ProcessTrafficStats(int processId, string processName)
        {
            ProcessId = processId;
            ProcessName = processName;
        }

        public void AddPacket(PacketDto packet)
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                _packetTimestamps.Enqueue(now);
                _totalPackets++;
                _totalBytes += packet.Length;

                // 1초 이상된 타임스탬프 제거
                while ((now - _packetTimestamps.Peek()).TotalSeconds > 1)
                {
                    _packetTimestamps.Dequeue();
                }
            }
        }

        public double GetPacketsPerSecond()
        {
            lock (_lock)
            {
                // 큐에 남아있는 타임스탬프가 현재 1초 이내의 패킷 수
                return _packetTimestamps.Count;
            }
        }

        public long TotalPackets => _totalPackets;
        public long TotalBytes => _totalBytes;
    }
}
