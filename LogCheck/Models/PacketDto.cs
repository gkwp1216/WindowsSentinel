namespace LogCheck.Models
{
    // ProtocolKind은 DDoSAttackTypes.cs에 정의됨

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
