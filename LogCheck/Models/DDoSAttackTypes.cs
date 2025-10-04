using System;

namespace LogCheck.Models
{
    /// <summary>
    /// DDoS 공격 유형 열거형
    /// </summary>
    public enum DDoSAttackType
    {
        /// <summary>
        /// SYN Flood 공격
        /// </summary>
        SynFlood,

        /// <summary>
        /// UDP Flood 공격
        /// </summary>
        UdpFlood,

        /// <summary>
        /// UDP 증폭 공격
        /// </summary>
        UdpAmplification,

        /// <summary>
        /// ICMP Flood 공격
        /// </summary>
        IcmpFlood,

        /// <summary>
        /// HTTP Flood 공격
        /// </summary>
        HttpFlood,

        /// <summary>
        /// Slowloris 공격
        /// </summary>
        SlowLoris,

        /// <summary>
        /// Bandwidth Flood 공격
        /// </summary>
        BandwidthFlood,

        /// <summary>
        /// Connection Flood 공격
        /// </summary>
        ConnectionFlood,

        /// <summary>
        /// TCP RST Flood 공격
        /// </summary>
        TcpRstFlood,

        /// <summary>
        /// TCP ACK Flood 공격
        /// </summary>
        TcpAckFlood,

        /// <summary>
        /// TCP FIN Flood 공격
        /// </summary>
        TcpFinFlood,

        /// <summary>
        /// TCP Flood 공격
        /// </summary>
        TcpFlood,

        /// <summary>
        /// Request Flood 공격
        /// </summary>
        RequestFlood,

        /// <summary>
        /// Packet Flood 공격
        /// </summary>
        PacketFlood,

        /// <summary>
        /// Volumetric 공격
        /// </summary>
        VolumetricAttack,

        /// <summary>
        /// Ping of Death 공격
        /// </summary>
        PingOfDeath,

        /// <summary>
        /// IP 단편화 공격
        /// </summary>
        FragmentationAttack,

        /// <summary>
        /// 봇넷 기반 공격
        /// </summary>
        BotnetAttack,

        /// <summary>
        /// 연결 고갈 공격
        /// </summary>
        ConnectionExhaustion,

        /// <summary>
        /// 대역폭 고갈 공격
        /// </summary>
        BandwidthExhaustion,

        /// <summary>
        /// DNS 증폭 공격
        /// </summary>
        DnsAmplification,

        /// <summary>
        /// NTP 증폭 공격
        /// </summary>
        NtpAmplification,

        /// <summary>
        /// SSDP 증폭 공격
        /// </summary>
        SsdpAmplification,

        /// <summary>
        /// 알 수 없는 공격 유형
        /// </summary>
        Unknown
    }

    /// <summary>
    /// DDoS 공격 심각도 레벨
    /// </summary>
    public enum DDoSSeverity
    {
        /// <summary>
        /// 낮음 - 경미한 트래픽 증가
        /// </summary>
        Low = 1,

        /// <summary>
        /// 보통 - 서비스에 영향을 줄 수 있는 수준
        /// </summary>
        Medium = 2,

        /// <summary>
        /// 높음 - 서비스 성능 저하
        /// </summary>
        High = 3,

        /// <summary>
        /// 심각 - 서비스 중단 위험
        /// </summary>
        Critical = 4,

        /// <summary>
        /// 긴급 - 즉시 대응 필요
        /// </summary>
        Emergency = 5
    }

    /// <summary>
    /// TCP 플래그 열거형 (비트 플래그)
    /// </summary>
    [Flags]
    public enum TcpFlags : uint
    {
        /// <summary>
        /// 플래그 없음
        /// </summary>
        None = 0,

        /// <summary>
        /// FIN (Finish) - 연결 종료
        /// </summary>
        FIN = 1,

        /// <summary>
        /// SYN (Synchronize) - 연결 시작
        /// </summary>
        SYN = 2,

        /// <summary>
        /// RST (Reset) - 연결 재설정
        /// </summary>
        RST = 4,

        /// <summary>
        /// PSH (Push) - 즉시 전송
        /// </summary>
        PSH = 8,

        /// <summary>
        /// ACK (Acknowledge) - 수신 확인
        /// </summary>
        ACK = 16,

        /// <summary>
        /// URG (Urgent) - 긴급 데이터
        /// </summary>
        URG = 32,

        /// <summary>
        /// ECE (ECN Echo) - ECN 에코
        /// </summary>
        ECE = 64,

        /// <summary>
        /// CWR (Congestion Window Reduced) - 혼잡 윈도우 감소
        /// </summary>
        CWR = 128
    }

    /// <summary>
    /// DDoS 공격 감지 상태
    /// </summary>
    public enum DDoSDetectionState
    {
        /// <summary>
        /// 정상 상태
        /// </summary>
        Normal,

        /// <summary>
        /// 의심스러운 활동 감지
        /// </summary>
        Suspicious,

        /// <summary>
        /// 공격 감지됨
        /// </summary>
        AttackDetected,

        /// <summary>
        /// 공격 차단됨
        /// </summary>
        AttackBlocked,

        /// <summary>
        /// 시스템 과부하
        /// </summary>
        SystemOverload
    }

    /// <summary>
    /// 방어 조치 유형
    /// </summary>
    public enum DefenseActionType
    {
        /// <summary>
        /// 조치 없음
        /// </summary>
        None,

        /// <summary>
        /// 모니터링 강화
        /// </summary>
        EnhancedMonitoring,

        /// <summary>
        /// 속도 제한
        /// </summary>
        RateLimit,

        /// <summary>
        /// IP 차단
        /// </summary>
        IpBlock,

        /// <summary>
        /// 연결 제한
        /// </summary>
        ConnectionLimit,

        /// <summary>
        /// 트래픽 필터링
        /// </summary>
        TrafficFiltering,

        /// <summary>
        /// 자동 차단
        /// </summary>
        AutoBlock,

        /// <summary>
        /// 긴급 차단
        /// </summary>
        EmergencyBlock,

        /// <summary>
        /// 관리자 알림
        /// </summary>
        AdminAlert
    }

    /// <summary>
    /// 네트워크 프로토콜 유형 (기존 ProtocolKind 확장)
    /// </summary>
    public enum ProtocolKind
    {
        /// <summary>
        /// 알 수 없는 프로토콜
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// TCP 프로토콜
        /// </summary>
        TCP = 6,

        /// <summary>
        /// UDP 프로토콜
        /// </summary>
        UDP = 17,

        /// <summary>
        /// ICMP 프로토콜
        /// </summary>
        ICMP = 1,

        /// <summary>
        /// IGMP 프로토콜
        /// </summary>
        IGMP = 2,

        /// <summary>
        /// IPv6 프로토콜
        /// </summary>
        IPv6 = 41,

        /// <summary>
        /// GRE 프로토콜
        /// </summary>
        GRE = 47,

        /// <summary>
        /// ESP 프로토콜
        /// </summary>
        ESP = 50,

        /// <summary>
        /// AH 프로토콜
        /// </summary>
        AH = 51,

        /// <summary>
        /// 기타 프로토콜
        /// </summary>
        Other = 255
    }
}