using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using LogCheck.Models;

namespace LogCheck.Services
{
    #region Packet Flow Analysis Classes

    /// <summary>
    /// 패킷 플로우 추적기
    /// 특정 플로우의 패킷들을 추적하여 실시간 분석 수행
    /// </summary>
    public class PacketFlowTracker
    {
        private readonly string _flowKey;
        private readonly List<PacketDto> _packets;
        private readonly object _lockObject = new object();
        private DateTime _lastActivity;

        public string FlowKey => _flowKey;
        public DateTime LastActivity => _lastActivity;
        public int PacketCount { get; private set; }

        public PacketFlowTracker(string flowKey)
        {
            _flowKey = flowKey;
            _packets = new List<PacketDto>();
            _lastActivity = DateTime.Now;
            PacketCount = 0;
        }

        public void AddPacket(PacketDto packet)
        {
            lock (_lockObject)
            {
                _packets.Add(packet);
                _lastActivity = DateTime.Now;
                PacketCount++;

                // 메모리 관리: 오래된 패킷 제거 (5분 이상)
                var cutoffTime = DateTime.Now.AddMinutes(-5);
                _packets.RemoveAll(p => p.Timestamp < cutoffTime);
            }
        }

        public double GetPacketsPerSecond()
        {
            lock (_lockObject)
            {
                var now = DateTime.Now;
                var recentPackets = _packets.Where(p => (now - p.Timestamp).TotalSeconds <= 1).Count();
                return recentPackets;
            }
        }

        public double GetFragmentationRatio()
        {
            lock (_lockObject)
            {
                if (_packets.Count == 0) return 0;

                // TCP 프래그먼트 감지 로직 (간단화)
                var fragmentedPackets = _packets.Count(p => p.Length < 500); // 작은 패킷들을 프래그먼트로 가정
                return (double)fragmentedPackets / _packets.Count;
            }
        }

        public List<PacketDto> GetRecentPackets(TimeSpan timeSpan)
        {
            lock (_lockObject)
            {
                var cutoffTime = DateTime.Now - timeSpan;
                return _packets.Where(p => p.Timestamp >= cutoffTime).ToList();
            }
        }
    }

    #endregion

    #region TCP Connection Analysis

    /// <summary>
    /// TCP 연결 분석기
    /// TCP 플래그와 연결 상태를 상세 분석
    /// </summary>
    public class TcpConnectionAnalyzer
    {
        public string ConnectionKey { get; }
        public TcpConnectionState CurrentState { get; private set; }
        public DateTime LastActivity { get; private set; }
        public bool IsAnomalous { get; private set; }

        public int SynPacketCount { get; private set; }
        public int AckPacketCount { get; private set; }
        public int FinPacketCount { get; private set; }
        public int RstPacketCount { get; private set; }

        private readonly List<TcpPacketInfo> _packetHistory;
        private readonly object _lockObject = new object();

        public TcpConnectionAnalyzer(string connectionKey)
        {
            ConnectionKey = connectionKey;
            CurrentState = TcpConnectionState.Unknown;
            LastActivity = DateTime.Now;
            _packetHistory = new List<TcpPacketInfo>();
        }

        public TcpFlagAnalysisResult AnalyzeFlags(PacketDto packet)
        {
            lock (_lockObject)
            {
                var flags = (TcpFlags)packet.Flags;
                var packetInfo = new TcpPacketInfo
                {
                    Timestamp = packet.Timestamp,
                    Flags = flags,
                    Size = packet.Length
                };

                _packetHistory.Add(packetInfo);
                LastActivity = DateTime.Now;

                // 플래그별 카운트 업데이트
                UpdateFlagCounts(flags);

                // 상태 전이 분석
                var previousState = CurrentState;
                CurrentState = DetermineConnectionState(flags, CurrentState);

                // 이상 패턴 탐지
                var analysisResult = DetectFlagAnomalies(flags, previousState);

                // 전체적인 이상 상태 업데이트
                IsAnomalous = analysisResult.IsAnomalous || DetectOverallAnomalies();

                return analysisResult;
            }
        }

        private void UpdateFlagCounts(TcpFlags flags)
        {
            if (flags.HasFlag(TcpFlags.SYN)) SynPacketCount++;
            if (flags.HasFlag(TcpFlags.ACK)) AckPacketCount++;
            if (flags.HasFlag(TcpFlags.FIN)) FinPacketCount++;
            if (flags.HasFlag(TcpFlags.RST)) RstPacketCount++;
        }

        private TcpConnectionState DetermineConnectionState(TcpFlags flags, TcpConnectionState currentState)
        {
            // 간단한 TCP 상태 머신
            return currentState switch
            {
                TcpConnectionState.Unknown when flags.HasFlag(TcpFlags.SYN) && !flags.HasFlag(TcpFlags.ACK) => TcpConnectionState.SynSent,
                TcpConnectionState.SynSent when flags.HasFlag(TcpFlags.SYN) && flags.HasFlag(TcpFlags.ACK) => TcpConnectionState.SynReceived,
                TcpConnectionState.SynReceived when flags.HasFlag(TcpFlags.ACK) => TcpConnectionState.Established,
                TcpConnectionState.Established when flags.HasFlag(TcpFlags.FIN) => TcpConnectionState.FinWait,
                _ when flags.HasFlag(TcpFlags.RST) => TcpConnectionState.Closed,
                _ => currentState
            };
        }

        private TcpFlagAnalysisResult DetectFlagAnomalies(TcpFlags flags, TcpConnectionState previousState)
        {
            var result = new TcpFlagAnalysisResult
            {
                PrimaryFlag = GetPrimaryFlag(flags),
                IsAnomalous = false,
                SeverityLevel = DDoSSeverity.Low,
                Description = "정상적인 TCP 플래그 패턴",
                RecommendedAction = "모니터링 지속"
            };

            // 비정상적인 플래그 조합 탐지
            if (DetectInvalidFlagCombinations(flags))
            {
                result.IsAnomalous = true;
                result.SeverityLevel = DDoSSeverity.High;
                result.Description = $"비정상적인 TCP 플래그 조합: {flags}";
                result.RecommendedAction = "연결 차단 및 상세 분석";
            }

            // SYN Flood 패턴 탐지
            if (flags.HasFlag(TcpFlags.SYN) && !flags.HasFlag(TcpFlags.ACK) && SynPacketCount > 10)
            {
                result.IsAnomalous = true;
                result.SeverityLevel = DDoSSeverity.High;
                result.Description = $"SYN Flood 의심: {SynPacketCount}개의 SYN 패킷";
                result.RecommendedAction = "SYN 쿠키 활성화 및 IP 차단";
            }

            // RST Flood 패턴 탐지
            if (flags.HasFlag(TcpFlags.RST) && RstPacketCount > 5)
            {
                result.IsAnomalous = true;
                result.SeverityLevel = DDoSSeverity.Medium;
                result.Description = $"RST Flood 의심: {RstPacketCount}개의 RST 패킷";
                result.RecommendedAction = "연결 상태 검증 및 필터링";
            }

            return result;
        }

        private bool DetectInvalidFlagCombinations(TcpFlags flags)
        {
            // 유효하지 않은 TCP 플래그 조합들
            var invalidCombinations = new[]
            {
                TcpFlags.SYN | TcpFlags.FIN,  // SYN + FIN
                TcpFlags.SYN | TcpFlags.RST,  // SYN + RST
                TcpFlags.FIN | TcpFlags.RST   // FIN + RST
            };

            return invalidCombinations.Any(combination => (flags & combination) == combination);
        }

        private TcpFlags GetPrimaryFlag(TcpFlags flags)
        {
            if (flags.HasFlag(TcpFlags.SYN)) return TcpFlags.SYN;
            if (flags.HasFlag(TcpFlags.ACK)) return TcpFlags.ACK;
            if (flags.HasFlag(TcpFlags.FIN)) return TcpFlags.FIN;
            if (flags.HasFlag(TcpFlags.RST)) return TcpFlags.RST;
            if (flags.HasFlag(TcpFlags.PSH)) return TcpFlags.PSH;
            if (flags.HasFlag(TcpFlags.URG)) return TcpFlags.URG;
            return TcpFlags.None;
        }

        private bool DetectOverallAnomalies()
        {
            // 전체적인 패턴 이상 탐지
            var recentPackets = _packetHistory.Where(p => (DateTime.Now - p.Timestamp).TotalMinutes <= 1).ToList();

            // 1분 내 과도한 패킷 수
            if (recentPackets.Count > 100)
                return true;

            // 비정상적인 플래그 비율
            if (recentPackets.Count > 10)
            {
                var synRatio = (double)recentPackets.Count(p => p.Flags.HasFlag(TcpFlags.SYN)) / recentPackets.Count;
                if (synRatio > 0.8) // 80% 이상이 SYN 패킷
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// TCP 패킷 정보
    /// </summary>
    public class TcpPacketInfo
    {
        public DateTime Timestamp { get; set; }
        public TcpFlags Flags { get; set; }
        public int Size { get; set; }
    }

    /// <summary>
    /// TCP 플래그 분석 결과
    /// </summary>
    public class TcpFlagAnalysisResult
    {
        public TcpFlags PrimaryFlag { get; set; }
        public bool IsAnomalous { get; set; }
        public DDoSSeverity SeverityLevel { get; set; }
        public string Description { get; set; } = string.Empty;
        public string RecommendedAction { get; set; } = string.Empty;
    }

    /// <summary>
    /// TCP 플래그 열거형
    /// </summary>
    [Flags]
    public enum TcpFlags : uint
    {
        None = 0,
        FIN = 0x01,
        SYN = 0x02,
        RST = 0x04,
        PSH = 0x08,
        ACK = 0x10,
        URG = 0x20,
        ECE = 0x40,
        CWR = 0x80
    }

    /// <summary>
    /// TCP 연결 상태
    /// </summary>
    public enum TcpConnectionState
    {
        Unknown,
        SynSent,
        SynReceived,
        Established,
        FinWait,
        CloseWait,
        Closing,
        LastAck,
        TimeWait,
        Closed
    }

    #endregion

    #region UDP Session Analysis

    /// <summary>
    /// UDP 세션 분석기
    /// </summary>
    public class UdpSessionAnalyzer
    {
        public string SessionKey { get; }
        public DateTime LastActivity { get; private set; }
        public int PacketCount { get; private set; }
        public long TotalBytes { get; private set; }

        private readonly List<UdpPacketInfo> _packetHistory;
        private readonly object _lockObject = new object();

        public UdpSessionAnalyzer(string sessionKey)
        {
            SessionKey = sessionKey;
            LastActivity = DateTime.Now;
            _packetHistory = new List<UdpPacketInfo>();
        }

        public void AddPacket(PacketDto packet)
        {
            lock (_lockObject)
            {
                var packetInfo = new UdpPacketInfo
                {
                    Timestamp = packet.Timestamp,
                    Size = packet.Length,
                    SourceIP = packet.SrcIp,
                    DestinationIP = packet.DstIp
                };

                _packetHistory.Add(packetInfo);
                LastActivity = DateTime.Now;
                PacketCount++;
                TotalBytes += packet.Length;

                // 메모리 관리
                var cutoffTime = DateTime.Now.AddMinutes(-5);
                _packetHistory.RemoveAll(p => p.Timestamp < cutoffTime);
            }
        }

        public double GetPacketsPerSecond()
        {
            lock (_lockObject)
            {
                var now = DateTime.Now;
                var recentPackets = _packetHistory.Where(p => (now - p.Timestamp).TotalSeconds <= 1).Count();
                return recentPackets;
            }
        }
    }

    /// <summary>
    /// UDP 패킷 정보
    /// </summary>
    public class UdpPacketInfo
    {
        public DateTime Timestamp { get; set; }
        public int Size { get; set; }
        public string SourceIP { get; set; } = string.Empty;
        public string DestinationIP { get; set; } = string.Empty;
    }

    #endregion

    #region Packet Size Distribution Analysis

    /// <summary>
    /// 패킷 크기 분포 분석기
    /// </summary>
    public class PacketSizeDistributionAnalyzer
    {
        private readonly ConcurrentDictionary<string, List<PacketSizeRecord>> _sizeRecords;
        private readonly object _lockObject = new object();

        public PacketSizeDistributionAnalyzer()
        {
            _sizeRecords = new ConcurrentDictionary<string, List<PacketSizeRecord>>();
        }

        public void AddPacket(string sourceIP, int packetSize, ProtocolKind protocol)
        {
            lock (_lockObject)
            {
                var records = _sizeRecords.GetOrAdd(sourceIP, _ => new List<PacketSizeRecord>());

                records.Add(new PacketSizeRecord
                {
                    Timestamp = DateTime.Now,
                    Size = packetSize,
                    Protocol = protocol
                });

                // 메모리 관리: 5분 이상 된 기록 제거
                var cutoffTime = DateTime.Now.AddMinutes(-5);
                records.RemoveAll(r => r.Timestamp < cutoffTime);
            }
        }

        public List<PacketSizeAnomaly> DetectAnomalies()
        {
            var anomalies = new List<PacketSizeAnomaly>();

            lock (_lockObject)
            {
                foreach (var kvp in _sizeRecords)
                {
                    var sourceIP = kvp.Key;
                    var records = kvp.Value;

                    if (records.Count < 10) continue; // 충분한 샘플이 없으면 스킵

                    var sizes = records.Select(r => r.Size).ToList();
                    var avgSize = sizes.Average();
                    var minSize = sizes.Min();
                    var maxSize = sizes.Max();
                    var stdDev = CalculateStandardDeviation(sizes);

                    // 이상 패턴 탐지
                    var anomaly = DetectSizeAnomaly(sourceIP, sizes, avgSize, minSize, maxSize, stdDev);
                    if (anomaly != null)
                    {
                        anomalies.Add(anomaly);
                    }
                }
            }

            return anomalies;
        }

        public PacketSizeDistributionStats GetDistributionStatistics()
        {
            lock (_lockObject)
            {
                var allSizes = _sizeRecords.Values.SelectMany(records => records.Select(r => r.Size)).ToList();

                if (allSizes.Count == 0)
                {
                    return new PacketSizeDistributionStats();
                }

                var stats = new PacketSizeDistributionStats
                {
                    AverageSize = allSizes.Average(),
                    MinSize = allSizes.Min(),
                    MaxSize = allSizes.Max(),
                    StandardDeviation = CalculateStandardDeviation(allSizes)
                };

                // 크기 범위별 분포
                stats.SizeRanges = new Dictionary<string, int>
                {
                    ["0-100"] = allSizes.Count(s => s <= 100),
                    ["101-500"] = allSizes.Count(s => s > 100 && s <= 500),
                    ["501-1000"] = allSizes.Count(s => s > 500 && s <= 1000),
                    ["1001-1500"] = allSizes.Count(s => s > 1000 && s <= 1500),
                    ["1500+"] = allSizes.Count(s => s > 1500)
                };

                return stats;
            }
        }

        private PacketSizeAnomaly? DetectSizeAnomaly(string sourceIP, List<int> sizes, double avgSize, int minSize, int maxSize, double stdDev)
        {
            // 1. 균일한 크기 패킷 (봇넷 특성)
            if (stdDev < 10 && sizes.Count > 50)
            {
                return new PacketSizeAnomaly
                {
                    SourceIP = sourceIP,
                    AnomalyType = SizeAnomalyType.UniformSize,
                    Severity = DDoSSeverity.Medium,
                    Description = $"균일한 패킷 크기 패턴 (표준편차: {stdDev:F1})",
                    AverageSize = (int)avgSize,
                    MinSize = minSize,
                    MaxSize = maxSize,
                    PacketCount = sizes.Count,
                    RecommendedAction = "봇넷 활동 의심 - 상세 분석 필요"
                };
            }

            // 2. 비정상적으로 큰 패킷
            if (maxSize > 9000) // Jumbo frame 이상
            {
                return new PacketSizeAnomaly
                {
                    SourceIP = sourceIP,
                    AnomalyType = SizeAnomalyType.ExcessivelyLarge,
                    Severity = DDoSSeverity.High,
                    Description = $"비정상적으로 큰 패킷 감지 (최대: {maxSize} bytes)",
                    AverageSize = (int)avgSize,
                    MinSize = minSize,
                    MaxSize = maxSize,
                    PacketCount = sizes.Count,
                    RecommendedAction = "패킷 크기 제한 및 분석"
                };
            }

            // 3. 비정상적으로 작은 패킷 대량 전송
            if (avgSize < 50 && sizes.Count > 100)
            {
                return new PacketSizeAnomaly
                {
                    SourceIP = sourceIP,
                    AnomalyType = SizeAnomalyType.ExcessivelySmall,
                    Severity = DDoSSeverity.Medium,
                    Description = $"소형 패킷 대량 전송 (평균: {avgSize:F1} bytes, 개수: {sizes.Count})",
                    AverageSize = (int)avgSize,
                    MinSize = minSize,
                    MaxSize = maxSize,
                    PacketCount = sizes.Count,
                    RecommendedAction = "패킷 플러딩 의심 - 연결 제한"
                };
            }

            return null;
        }

        private double CalculateStandardDeviation(List<int> values)
        {
            if (values.Count == 0) return 0;

            var average = values.Average();
            var sumOfSquaresOfDifferences = values.Select(val => (val - average) * (val - average)).Sum();
            var standardDeviation = Math.Sqrt(sumOfSquaresOfDifferences / values.Count);

            return standardDeviation;
        }
    }

    /// <summary>
    /// 패킷 크기 기록
    /// </summary>
    public class PacketSizeRecord
    {
        public DateTime Timestamp { get; set; }
        public int Size { get; set; }
        public ProtocolKind Protocol { get; set; }
    }

    /// <summary>
    /// 패킷 크기 이상 정보
    /// </summary>
    public class PacketSizeAnomaly
    {
        public string SourceIP { get; set; } = string.Empty;
        public SizeAnomalyType AnomalyType { get; set; }
        public DDoSSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        public int AverageSize { get; set; }
        public int MinSize { get; set; }
        public int MaxSize { get; set; }
        public int PacketCount { get; set; }
        public string RecommendedAction { get; set; } = string.Empty;
    }

    /// <summary>
    /// 크기 이상 유형
    /// </summary>
    public enum SizeAnomalyType
    {
        UniformSize,
        ExcessivelyLarge,
        ExcessivelySmall,
        HighVariance
    }

    #endregion
}