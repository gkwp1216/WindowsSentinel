using System;

namespace WindowsSentinel.Models
{
    public class NetworkUsageRecord
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string InterfaceName { get; set; } = string.Empty;
        public string SourceIP { get; set; } = string.Empty;
        public string DestinationIP { get; set; } = string.Empty;
        public string Protocol { get; set; } = string.Empty;
        public int SourcePort { get; set; }
        public int DestinationPort { get; set; }
        public long PacketSize { get; set; }
        public string Direction { get; set; } = string.Empty; // "Inbound" / "Outbound"
        public string ProcessName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class NetworkUsageSummary
    {
        public DateTime Date { get; set; }
        public string InterfaceName { get; set; } = string.Empty;
        public long TotalBytesReceived { get; set; }
        public long TotalBytesSent { get; set; }
        public long TotalPacketsReceived { get; set; }
        public long TotalPacketsSent { get; set; }
        public int UniqueConnections { get; set; }
        public string TopProtocol { get; set; } = string.Empty;
        public string TopDestination { get; set; } = string.Empty;
    }
} 