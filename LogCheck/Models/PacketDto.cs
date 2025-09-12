using System;

namespace LogCheck.Models
{
    public enum ProtocolKind { TCP, UDP, ICMP, Other }

    public class PacketDto
    {
        public DateTime Timestamp { get; set; }
        public ProtocolKind Protocol { get; set; }
        public string SrcIp { get; set; } = string.Empty;
        public int? SrcPort { get; set; }
        public string DstIp { get; set; } = string.Empty;
        public int? DstPort { get; set; }
        public int Length { get; set; }
        public uint Flags { get; set; }
    }
}
