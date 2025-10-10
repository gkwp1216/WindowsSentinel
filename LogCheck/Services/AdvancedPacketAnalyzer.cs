using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LogCheck.Models;

namespace LogCheck.Services
{
    /// <summary>
    /// 고급 패킷 레벨 DDoS 분석기
    /// TCP 플래그, 패킷 크기 분포, 요청-응답 비율 등을 상세 분석하여
    /// 정교한 DDoS 공격 탐지 및 시그니처 매칭 수행
    /// </summary>
    public class AdvancedPacketAnalyzer
    {
        #region Private Fields

        private readonly ConcurrentDictionary<string, PacketFlowTracker> _flowTrackers;
        private readonly ConcurrentDictionary<string, TcpConnectionAnalyzer> _tcpAnalyzers;
        private readonly ConcurrentDictionary<string, UdpSessionAnalyzer> _udpAnalyzers;
        private readonly PacketSizeDistributionAnalyzer _sizeAnalyzer;
        private readonly DDoSSignatureDatabase _signatureDatabase;
        private readonly AdvancedPacketConfig _config;

        #endregion

        #region Events

        public event EventHandler<string>? ErrorOccurred;

        #endregion

        #region Constructor

        public AdvancedPacketAnalyzer()
        {
            _flowTrackers = new ConcurrentDictionary<string, PacketFlowTracker>();
            _tcpAnalyzers = new ConcurrentDictionary<string, TcpConnectionAnalyzer>();
            _udpAnalyzers = new ConcurrentDictionary<string, UdpSessionAnalyzer>();
            _sizeAnalyzer = new PacketSizeDistributionAnalyzer();
            _signatureDatabase = new DDoSSignatureDatabase();
            _config = new AdvancedPacketConfig();

            // 기본 시그니처 로드
            InitializeSignatureDatabase();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 고급 패킷 분석 실행 (SharpPcap 패킷 데이터용)
        /// </summary>
        public List<AdvancedDDoSAlert> AnalyzePacketBatch(List<PacketDto> packets)
        {
            var alerts = new List<AdvancedDDoSAlert>();

            try
            {
                // 1. TCP 플래그 상세 분석
                var tcpFlagAlerts = AnalyzeTcpFlags(packets);
                alerts.AddRange(tcpFlagAlerts);

                // 2. 패킷 크기 분포 분석
                var sizeDistributionAlerts = AnalyzePacketSizeDistribution(packets);
                alerts.AddRange(sizeDistributionAlerts);

                // 3. 요청-응답 비율 분석
                var requestResponseAlerts = AnalyzeRequestResponseRatio(packets);
                alerts.AddRange(requestResponseAlerts);

                // 4. 프로토콜별 특화 탐지
                var protocolSpecificAlerts = AnalyzeProtocolSpecificPatterns(packets);
                alerts.AddRange(protocolSpecificAlerts);

                // 5. DDoS 시그니처 매칭
                var signatureAlerts = MatchDDoSSignatures(packets);
                alerts.AddRange(signatureAlerts);

                // 6. 패킷 타이밍 분석
                var timingAlerts = AnalyzePacketTiming(packets);
                alerts.AddRange(timingAlerts);

                return alerts;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"고급 패킷 분석 중 오류: {ex.Message}");
                return alerts;
            }
        }

        /// <summary>
        /// 실시간 패킷 분석 (단일 패킷)
        /// </summary>
        public Task<List<PacketAnomalyAlert>> AnalyzeSinglePacketAsync(PacketDto packet)
        {
            var alerts = new List<PacketAnomalyAlert>();

            try
            {
                var flowKey = GenerateFlowKey(packet);
                var tracker = _flowTrackers.GetOrAdd(flowKey, _ => new PacketFlowTracker(flowKey));

                // 패킷을 플로우에 추가
                tracker.AddPacket(packet);

                // 실시간 이상 탐지
                var anomalies = DetectPacketAnomaliesAsync(tracker, packet).Result;
                alerts.AddRange(anomalies);

                return Task.FromResult(alerts);
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"실시간 패킷 분석 중 오류: {ex.Message}");
                return Task.FromResult(alerts);
            }
        }

        /// <summary>
        /// 패킷 분석 통계 조회
        /// </summary>
        public Task<PacketAnalysisStatistics> GetAnalysisStatisticsAsync()
        {
            try
            {
                var stats = new PacketAnalysisStatistics
                {
                    GeneratedAt = DateTime.Now,
                    ActiveFlows = _flowTrackers.Count,
                    ActiveTcpConnections = _tcpAnalyzers.Count,
                    ActiveUdpSessions = _udpAnalyzers.Count
                };

                // TCP 연결 통계
                stats.TcpStatistics = _tcpAnalyzers.Values.Select(analyzer => new TcpConnectionStatistics
                {
                    ConnectionKey = analyzer.ConnectionKey,
                    State = analyzer.CurrentState,
                    SynCount = analyzer.SynPacketCount,
                    AckCount = analyzer.AckPacketCount,
                    FinCount = analyzer.FinPacketCount,
                    RstCount = analyzer.RstPacketCount,
                    IsAnomalous = analyzer.IsAnomalous,
                    LastActivity = analyzer.LastActivity
                }).ToList();

                // 패킷 크기 분포 통계
                stats.PacketSizeDistribution = _sizeAnalyzer.GetDistributionStatistics();

                return Task.FromResult(stats);
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"패킷 분석 통계 조회 중 오류: {ex.Message}");
                return Task.FromResult(new PacketAnalysisStatistics { GeneratedAt = DateTime.Now });
            }
        }

        #endregion

        #region Private Analysis Methods

        /// <summary>
        /// TCP 플래그 상세 분석
        /// </summary>
        private List<AdvancedDDoSAlert> AnalyzeTcpFlags(List<PacketDto> packets)
        {
            var alerts = new List<AdvancedDDoSAlert>();

            try
            {
                var tcpPackets = packets.Where(p => p.Protocol == ProtocolKind.TCP);

                foreach (var packet in tcpPackets)
                {
                    var connectionKey = $"{packet.SrcIp}:{packet.SrcPort}->{packet.DstIp}:{packet.DstPort}";
                    var analyzer = _tcpAnalyzers.GetOrAdd(connectionKey, _ => new TcpConnectionAnalyzer(connectionKey));

                    // TCP 플래그 분석
                    var flagAnalysis = analyzer.AnalyzeFlags(packet);

                    // 이상 패턴 탐지
                    if (flagAnalysis.IsAnomalous)
                    {
                        alerts.Add(new AdvancedDDoSAlert
                        {
                            AttackType = DetermineAttackTypeFromFlags(flagAnalysis),
                            SourceIP = packet.SrcIp,
                            DestinationIP = packet.DstIp,
                            Severity = flagAnalysis.SeverityLevel,
                            Description = flagAnalysis.Description,
                            DetectedAt = DateTime.Now,
                            PacketDetails = new PacketAnalysisDetails
                            {
                                TcpFlags = packet.Flags,
                                PacketSize = packet.Length,
                                Protocol = "TCP"
                            },
                            RecommendedAction = flagAnalysis.RecommendedAction
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"TCP 플래그 분석 중 오류: {ex.Message}");
            }

            return alerts;
        }

        /// <summary>
        /// 패킷 크기 분포 분석
        /// </summary>
        private List<AdvancedDDoSAlert> AnalyzePacketSizeDistribution(List<PacketDto> packets)
        {
            var alerts = new List<AdvancedDDoSAlert>();

            try
            {
                // 패킷 크기 데이터 수집
                foreach (var packet in packets)
                {
                    _sizeAnalyzer.AddPacket(packet.SrcIp, packet.Length, packet.Protocol);
                }

                // 비정상적인 크기 분포 탐지
                var sizeAnomalies = _sizeAnalyzer.DetectAnomalies();

                foreach (var anomaly in sizeAnomalies)
                {
                    alerts.Add(new AdvancedDDoSAlert
                    {
                        AttackType = DetermineAttackTypeFromSizeAnomaly(anomaly),
                        SourceIP = anomaly.SourceIP,
                        Severity = anomaly.Severity,
                        Description = $"비정상적인 패킷 크기 분포: {anomaly.Description}",
                        DetectedAt = DateTime.Now,
                        PacketDetails = new PacketAnalysisDetails
                        {
                            AveragePacketSize = anomaly.AverageSize,
                            MinPacketSize = anomaly.MinSize,
                            MaxPacketSize = anomaly.MaxSize,
                            PacketCount = anomaly.PacketCount
                        },
                        RecommendedAction = anomaly.RecommendedAction
                    });
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"패킷 크기 분포 분석 중 오류: {ex.Message}");
            }

            return alerts;
        }

        /// <summary>
        /// 요청-응답 비율 분석
        /// </summary>
        private List<AdvancedDDoSAlert> AnalyzeRequestResponseRatio(List<PacketDto> packets)
        {
            var alerts = new List<AdvancedDDoSAlert>();

            try
            {
                // IP별 송신/수신 패킷 비율 분석
                var flowAnalysis = packets
                    .GroupBy(p => p.SrcIp)
                    .Select(g => new
                    {
                        IP = g.Key,
                        OutboundPackets = g.Count(),
                        InboundPackets = packets.Count(p => p.DstIp == g.Key),
                        TotalBytes = g.Sum(p => p.Length)
                    })
                    .Where(flow => flow.OutboundPackets > 0 || flow.InboundPackets > 0);

                foreach (var flow in flowAnalysis)
                {
                    var ratio = flow.InboundPackets > 0 ? (double)flow.OutboundPackets / flow.InboundPackets : double.MaxValue;

                    // 비정상적인 송신/수신 비율 탐지
                    if (ratio > _config.MaxOutboundInboundRatio && flow.OutboundPackets > _config.MinPacketsForRatioAnalysis)
                    {
                        alerts.Add(new AdvancedDDoSAlert
                        {
                            AttackType = DDoSAttackType.RequestFlood,
                            SourceIP = flow.IP,
                            Severity = DetermineSeverityFromRatio(ratio),
                            Description = $"비정상적인 요청-응답 비율: 송신 {flow.OutboundPackets}, 수신 {flow.InboundPackets} (비율: {ratio:F2})",
                            DetectedAt = DateTime.Now,
                            PacketDetails = new PacketAnalysisDetails
                            {
                                PacketCount = flow.OutboundPackets + flow.InboundPackets,
                                RequestResponseRatio = ratio,
                                TotalBytes = flow.TotalBytes
                            },
                            RecommendedAction = "요청 빈도 제한 및 연결 모니터링"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"요청-응답 비율 분석 중 오류: {ex.Message}");
            }

            return alerts;
        }

        /// <summary>
        /// 프로토콜별 특화 탐지 로직
        /// </summary>
        private List<AdvancedDDoSAlert> AnalyzeProtocolSpecificPatterns(List<PacketDto> packets)
        {
            var alerts = new List<AdvancedDDoSAlert>();

            try
            {
                // TCP 특화 분석
                var tcpAlerts = AnalyzeTcpSpecificPatternsAsync(packets.Where(p => p.Protocol == ProtocolKind.TCP).ToList()).Result;
                alerts.AddRange(tcpAlerts);

                // UDP 특화 분석
                var udpAlerts = AnalyzeUdpSpecificPatternsAsync(packets.Where(p => p.Protocol == ProtocolKind.UDP).ToList()).Result;
                alerts.AddRange(udpAlerts);

                // ICMP 특화 분석
                var icmpAlerts = AnalyzeIcmpSpecificPatternsAsync(packets.Where(p => p.Protocol == ProtocolKind.ICMP).ToList()).Result;
                alerts.AddRange(icmpAlerts);

            }
            catch (Exception ex)
            {
                OnErrorOccurred($"프로토콜별 특화 분석 중 오류: {ex.Message}");
            }

            return alerts;
        }

        /// <summary>
        /// DDoS 시그니처 매칭
        /// </summary>
        private List<AdvancedDDoSAlert> MatchDDoSSignatures(List<PacketDto> packets)
        {
            var alerts = new List<AdvancedDDoSAlert>();

            try
            {
                foreach (var signature in _signatureDatabase.GetActiveSignatures())
                {
                    var matchResult = signature.Match(packets);
                    if (matchResult.IsMatch)
                    {
                        alerts.Add(new AdvancedDDoSAlert
                        {
                            AttackType = signature.AttackType,
                            SourceIP = matchResult.SourceIP,
                            Severity = signature.Severity,
                            Description = $"DDoS 시그니처 매칭: {signature.Name} - {matchResult.Description}",
                            DetectedAt = DateTime.Now,
                            SignatureMatch = new SignatureMatchDetails
                            {
                                SignatureName = signature.Name,
                                MatchScore = matchResult.MatchScore,
                                MatchedPatterns = matchResult.MatchedPatterns
                            },
                            RecommendedAction = signature.RecommendedAction
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"DDoS 시그니처 매칭 중 오류: {ex.Message}");
            }

            return alerts;
        }

        /// <summary>
        /// 패킷 타이밍 분석
        /// </summary>
        private List<AdvancedDDoSAlert> AnalyzePacketTiming(List<PacketDto> packets)
        {
            var alerts = new List<AdvancedDDoSAlert>();

            try
            {
                // IP별 패킷 간격 분석
                var packetsByIP = packets.GroupBy(p => p.SrcIp);

                foreach (var group in packetsByIP)
                {
                    var sortedPackets = group.OrderBy(p => p.Timestamp).ToList();
                    if (sortedPackets.Count < 3) continue;

                    var intervals = new List<double>();
                    for (int i = 1; i < sortedPackets.Count; i++)
                    {
                        var interval = (sortedPackets[i].Timestamp - sortedPackets[i - 1].Timestamp).TotalMilliseconds;
                        intervals.Add(interval);
                    }

                    // 규칙적인 패턴 탐지 (봇넷 특성)
                    var avgInterval = intervals.Average();
                    var variance = intervals.Select(x => Math.Pow(x - avgInterval, 2)).Average();
                    var stdDev = Math.Sqrt(variance);

                    // 표준편차가 너무 작으면 인공적인 패턴으로 판단
                    if (stdDev < _config.MinTimingVariance && avgInterval < _config.MaxSuspiciousInterval)
                    {
                        alerts.Add(new AdvancedDDoSAlert
                        {
                            AttackType = DDoSAttackType.BotnetAttack,
                            SourceIP = group.Key,
                            Severity = DDoSSeverity.High,
                            Description = $"의심스러운 규칙적 패킷 전송 패턴: 평균간격 {avgInterval:F1}ms, 표준편차 {stdDev:F1}ms",
                            DetectedAt = DateTime.Now,
                            PacketDetails = new PacketAnalysisDetails
                            {
                                PacketCount = sortedPackets.Count,
                                AverageInterval = avgInterval,
                                TimingVariance = variance
                            },
                            RecommendedAction = "봇넷 활동 의심 - IP 차단 및 상세 분석"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"패킷 타이밍 분석 중 오류: {ex.Message}");
            }

            return alerts;
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// TCP 특화 패턴 분석
        /// </summary>
        private Task<List<AdvancedDDoSAlert>> AnalyzeTcpSpecificPatternsAsync(List<PacketDto> tcpPackets)
        {
            var alerts = new List<AdvancedDDoSAlert>();

            // TCP RST Flood 탐지
            var rstPackets = tcpPackets.Where(p => (p.Flags & 0x04) != 0).GroupBy(p => p.SrcIp);
            foreach (var group in rstPackets)
            {
                if (group.Count() > _config.TcpRstFloodThreshold)
                {
                    alerts.Add(new AdvancedDDoSAlert
                    {
                        AttackType = DDoSAttackType.TcpRstFlood,
                        SourceIP = group.Key,
                        Severity = DDoSSeverity.High,
                        Description = $"TCP RST Flood 탐지: {group.Count()}개의 RST 패킷",
                        DetectedAt = DateTime.Now,
                        RecommendedAction = "IP 차단 및 TCP 연결 모니터링"
                    });
                }
            }

            // TCP ACK Flood 탐지
            var ackPackets = tcpPackets.Where(p => (p.Flags & 0x10) != 0 && (p.Flags & 0x02) == 0).GroupBy(p => p.SrcIp);
            foreach (var group in ackPackets)
            {
                if (group.Count() > _config.TcpAckFloodThreshold)
                {
                    alerts.Add(new AdvancedDDoSAlert
                    {
                        AttackType = DDoSAttackType.TcpAckFlood,
                        SourceIP = group.Key,
                        Severity = DDoSSeverity.Medium,
                        Description = $"TCP ACK Flood 탐지: {group.Count()}개의 ACK 패킷",
                        DetectedAt = DateTime.Now,
                        RecommendedAction = "연결 상태 검증 및 필터링"
                    });
                }
            }

            return Task.FromResult(alerts);
        }

        /// <summary>
        /// UDP 특화 패턴 분석
        /// </summary>
        private Task<List<AdvancedDDoSAlert>> AnalyzeUdpSpecificPatternsAsync(List<PacketDto> udpPackets)
        {
            var alerts = new List<AdvancedDDoSAlert>();

            // UDP 증폭 공격 탐지 (작은 요청, 큰 응답)
            var udpFlows = udpPackets.GroupBy(p => new { p.SrcIp, p.DstIp, p.DstPort });
            foreach (var flow in udpFlows)
            {
                var requestSize = flow.Where(p => p.Length < _config.UdpAmplificationRequestMaxSize).Sum(p => p.Length);
                var responseSize = flow.Where(p => p.Length > _config.UdpAmplificationRequestMaxSize).Sum(p => p.Length);

                if (requestSize > 0 && responseSize > 0)
                {
                    var amplificationRatio = (double)responseSize / requestSize;
                    if (amplificationRatio > _config.UdpAmplificationRatioThreshold)
                    {
                        alerts.Add(new AdvancedDDoSAlert
                        {
                            AttackType = DDoSAttackType.UdpAmplification,
                            SourceIP = flow.Key.SrcIp,
                            DestinationIP = flow.Key.DstIp,
                            Severity = DDoSSeverity.High,
                            Description = $"UDP 증폭 공격 탐지: 증폭비율 {amplificationRatio:F1}x (요청: {requestSize}bytes, 응답: {responseSize}bytes)",
                            DetectedAt = DateTime.Now,
                            RecommendedAction = "UDP 응답 크기 제한 및 소스 IP 검증"
                        });
                    }
                }
            }

            return Task.FromResult(alerts);
        }

        /// <summary>
        /// ICMP 특화 패턴 분석
        /// </summary>
        private Task<List<AdvancedDDoSAlert>> AnalyzeIcmpSpecificPatternsAsync(List<PacketDto> icmpPackets)
        {
            var alerts = new List<AdvancedDDoSAlert>();

            // ICMP Flood 탐지
            var icmpByIP = icmpPackets.GroupBy(p => p.SrcIp);
            foreach (var group in icmpByIP)
            {
                if (group.Count() > _config.IcmpFloodThreshold)
                {
                    alerts.Add(new AdvancedDDoSAlert
                    {
                        AttackType = DDoSAttackType.IcmpFlood,
                        SourceIP = group.Key,
                        Severity = DDoSSeverity.Medium,
                        Description = $"ICMP Flood 탐지: {group.Count()}개의 ICMP 패킷",
                        DetectedAt = DateTime.Now,
                        RecommendedAction = "ICMP 트래픽 제한 및 IP 차단"
                    });
                }
            }

            // Ping of Death 탐지 (비정상적으로 큰 ICMP 패킷)
            var largePingPackets = icmpPackets.Where(p => p.Length > _config.PingOfDeathSizeThreshold);
            foreach (var packet in largePingPackets)
            {
                alerts.Add(new AdvancedDDoSAlert
                {
                    AttackType = DDoSAttackType.PingOfDeath,
                    SourceIP = packet.SrcIp,
                    Severity = DDoSSeverity.High,
                    Description = $"Ping of Death 탐지: 비정상적으로 큰 ICMP 패킷 ({packet.Length}bytes)",
                    DetectedAt = DateTime.Now,
                    RecommendedAction = "즉시 IP 차단 및 ICMP 패킷 크기 제한"
                });
            }

            return Task.FromResult(alerts);
        }

        /// <summary>
        /// 실시간 패킷 이상 탐지
        /// </summary>
        private Task<List<PacketAnomalyAlert>> DetectPacketAnomaliesAsync(PacketFlowTracker tracker, PacketDto packet)
        {
            var alerts = new List<PacketAnomalyAlert>();

            try
            {
                // 패킷 속도 이상 탐지
                if (tracker.GetPacketsPerSecond() > _config.MaxPacketsPerSecondPerFlow)
                {
                    alerts.Add(new PacketAnomalyAlert
                    {
                        AnomalyType = PacketAnomalyType.HighPacketRate,
                        SourceIP = packet.SrcIp,
                        Description = $"높은 패킷 전송률: {tracker.GetPacketsPerSecond():F1} packets/sec",
                        DetectedAt = DateTime.Now,
                        Severity = PacketAnomalySeverity.Medium
                    });
                }

                // 패킷 크기 이상 탐지
                if (packet.Length > _config.MaxSinglePacketSize)
                {
                    alerts.Add(new PacketAnomalyAlert
                    {
                        AnomalyType = PacketAnomalyType.OversizedPacket,
                        SourceIP = packet.SrcIp,
                        Description = $"비정상적으로 큰 패킷: {packet.Length} bytes",
                        DetectedAt = DateTime.Now,
                        Severity = PacketAnomalySeverity.High
                    });
                }

                // 프래그먼테이션 공격 탐지
                if (tracker.GetFragmentationRatio() > _config.MaxFragmentationRatio)
                {
                    alerts.Add(new PacketAnomalyAlert
                    {
                        AnomalyType = PacketAnomalyType.FragmentationAttack,
                        SourceIP = packet.SrcIp,
                        Description = $"높은 패킷 단편화율: {tracker.GetFragmentationRatio():P}",
                        DetectedAt = DateTime.Now,
                        Severity = PacketAnomalySeverity.High
                    });
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"실시간 패킷 이상 탐지 중 오류: {ex.Message}");
            }

            return Task.FromResult(alerts);
        }

        /// <summary>
        /// 플로우 키 생성
        /// </summary>
        private string GenerateFlowKey(PacketDto packet)
        {
            return $"{packet.SrcIp}:{packet.SrcPort ?? 0}-{packet.DstIp}:{packet.DstPort ?? 0}-{packet.Protocol}";
        }

        /// <summary>
        /// TCP 플래그 기반 공격 유형 결정
        /// </summary>
        private DDoSAttackType DetermineAttackTypeFromFlags(TcpFlagAnalysisResult flagAnalysis)
        {
            return flagAnalysis.PrimaryFlag switch
            {
                TcpFlags.SYN => DDoSAttackType.SynFlood,
                TcpFlags.RST => DDoSAttackType.TcpRstFlood,
                TcpFlags.ACK => DDoSAttackType.TcpAckFlood,
                TcpFlags.FIN => DDoSAttackType.TcpFinFlood,
                _ => DDoSAttackType.TcpFlood
            };
        }

        /// <summary>
        /// 패킷 크기 이상 기반 공격 유형 결정
        /// </summary>
        private DDoSAttackType DetermineAttackTypeFromSizeAnomaly(PacketSizeAnomaly anomaly)
        {
            return anomaly.AnomalyType switch
            {
                SizeAnomalyType.UniformSize => DDoSAttackType.BotnetAttack,
                SizeAnomalyType.ExcessivelyLarge => DDoSAttackType.PingOfDeath,
                SizeAnomalyType.ExcessivelySmall => DDoSAttackType.PacketFlood,
                _ => DDoSAttackType.VolumetricAttack
            };
        }

        /// <summary>
        /// 송신/수신 비율 기반 심각도 결정
        /// </summary>
        private DDoSSeverity DetermineSeverityFromRatio(double ratio)
        {
            return ratio switch
            {
                >= 100 => DDoSSeverity.Critical,
                >= 50 => DDoSSeverity.High,
                >= 20 => DDoSSeverity.Medium,
                _ => DDoSSeverity.Low
            };
        }

        /// <summary>
        /// 시그니처 데이터베이스 초기화
        /// </summary>
        private void InitializeSignatureDatabase()
        {
            try
            {
                // 기본 DDoS 시그니처들을 로드
                _signatureDatabase.LoadDefaultSignatures();
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"시그니처 데이터베이스 초기화 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 오류 이벤트 발생
        /// </summary>
        private void OnErrorOccurred(string message)
        {
            ErrorOccurred?.Invoke(this, message);
        }

        #endregion
    }

    #region Supporting Classes and Enums

    /// <summary>
    /// 고급 패킷 분석 설정
    /// </summary>
    public class AdvancedPacketConfig
    {
        public double MaxOutboundInboundRatio { get; set; } = 10.0;
        public int MinPacketsForRatioAnalysis { get; set; } = 20;
        public double MinTimingVariance { get; set; } = 5.0; // ms
        public double MaxSuspiciousInterval { get; set; } = 100.0; // ms
        public int TcpRstFloodThreshold { get; set; } = 50;
        public int TcpAckFloodThreshold { get; set; } = 100;
        public int UdpAmplificationRequestMaxSize { get; set; } = 100; // bytes
        public double UdpAmplificationRatioThreshold { get; set; } = 10.0;
        public int IcmpFloodThreshold { get; set; } = 30;
        public int PingOfDeathSizeThreshold { get; set; } = 65507; // bytes
        public int MaxPacketsPerSecondPerFlow { get; set; } = 1000;
        public int MaxSinglePacketSize { get; set; } = 1500; // bytes
        public double MaxFragmentationRatio { get; set; } = 0.3; // 30%
    }

    /// <summary>
    /// 고급 DDoS 경고
    /// </summary>
    public class AdvancedDDoSAlert
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DDoSAttackType AttackType { get; set; }
        public string SourceIP { get; set; } = string.Empty;
        public string DestinationIP { get; set; } = string.Empty;
        public DDoSSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; }
        public PacketAnalysisDetails? PacketDetails { get; set; }
        public SignatureMatchDetails? SignatureMatch { get; set; }
        public string RecommendedAction { get; set; } = string.Empty;
        public bool IsResolved { get; set; } = false;
    }

    /// <summary>
    /// 패킷 분석 상세 정보
    /// </summary>
    public class PacketAnalysisDetails
    {
        public uint TcpFlags { get; set; }
        public int PacketSize { get; set; }
        public string Protocol { get; set; } = string.Empty;
        public double AveragePacketSize { get; set; }
        public int MinPacketSize { get; set; }
        public int MaxPacketSize { get; set; }
        public int PacketCount { get; set; }
        public double RequestResponseRatio { get; set; }
        public long TotalBytes { get; set; }
        public double AverageInterval { get; set; }
        public double TimingVariance { get; set; }
    }

    /// <summary>
    /// 시그니처 매칭 상세 정보
    /// </summary>
    public class SignatureMatchDetails
    {
        public string SignatureName { get; set; } = string.Empty;
        public double MatchScore { get; set; }
        public List<string> MatchedPatterns { get; set; } = new();
    }

    /// <summary>
    /// 패킷 이상 경고
    /// </summary>
    public class PacketAnomalyAlert
    {
        public PacketAnomalyType AnomalyType { get; set; }
        public string SourceIP { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; }
        public PacketAnomalySeverity Severity { get; set; }
    }

    /// <summary>
    /// 패킷 분석 통계
    /// </summary>
    public class PacketAnalysisStatistics
    {
        public DateTime GeneratedAt { get; set; }
        public int ActiveFlows { get; set; }
        public int ActiveTcpConnections { get; set; }
        public int ActiveUdpSessions { get; set; }
        public List<TcpConnectionStatistics> TcpStatistics { get; set; } = new();
        public PacketSizeDistributionStats PacketSizeDistribution { get; set; } = new();
    }

    /// <summary>
    /// TCP 연결 통계
    /// </summary>
    public class TcpConnectionStatistics
    {
        public string ConnectionKey { get; set; } = string.Empty;
        public TcpConnectionState State { get; set; }
        public int SynCount { get; set; }
        public int AckCount { get; set; }
        public int FinCount { get; set; }
        public int RstCount { get; set; }
        public bool IsAnomalous { get; set; }
        public DateTime LastActivity { get; set; }
    }

    /// <summary>
    /// 패킷 크기 분포 통계
    /// </summary>
    public class PacketSizeDistributionStats
    {
        public double AverageSize { get; set; }
        public int MinSize { get; set; }
        public int MaxSize { get; set; }
        public double StandardDeviation { get; set; }
        public Dictionary<string, int> SizeRanges { get; set; } = new();
    }

    // DDoSAttackType은 Models/DDoSAttackTypes.cs에 정의됨

    /// <summary>
    /// 패킷 이상 유형
    /// </summary>
    public enum PacketAnomalyType
    {
        HighPacketRate,
        OversizedPacket,
        FragmentationAttack,
        UnusualProtocol,
        SuspiciousTiming,
        AnomalousFlags
    }

    /// <summary>
    /// 패킷 이상 심각도
    /// </summary>
    public enum PacketAnomalySeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    #endregion
}
