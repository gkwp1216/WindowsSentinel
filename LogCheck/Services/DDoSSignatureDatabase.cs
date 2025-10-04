using System;
using System.Collections.Generic;
using System.Linq;
using LogCheck.Models;

namespace LogCheck.Services
{
    /// <summary>
    /// DDoS 공격 시그니처 데이터베이스
    /// 알려진 DDoS 공격 패턴들을 저장하고 실시간 매칭 수행
    /// </summary>
    public class DDoSSignatureDatabase
    {
        private readonly List<DDoSSignature> _signatures;
        private readonly Dictionary<string, DDoSSignature> _signatureIndex;

        public DDoSSignatureDatabase()
        {
            _signatures = new List<DDoSSignature>();
            _signatureIndex = new Dictionary<string, DDoSSignature>();
        }

        /// <summary>
        /// 기본 DDoS 시그니처들 로드
        /// </summary>
        public void LoadDefaultSignatures()
        {
            // 1. Classic SYN Flood 시그니처
            AddSignature(new DDoSSignature
            {
                Id = "SYN_FLOOD_001",
                Name = "Classic SYN Flood",
                AttackType = DDoSAttackType.SynFlood,
                Severity = DDoSSeverity.High,
                Description = "전형적인 SYN Flood 공격 패턴",
                RecommendedAction = "SYN 쿠키 활성화 및 IP 차단",
                Patterns = new List<SignaturePattern>
                {
                    new TcpFlagPattern
                    {
                        RequiredFlags = TcpFlags.SYN,
                        ForbiddenFlags = TcpFlags.ACK,
                        MinPacketCount = 50,
                        TimeWindowSeconds = 10
                    },
                    new PacketRatePattern
                    {
                        MinPacketsPerSecond = 100,
                        ProtocolFilter = ProtocolKind.TCP
                    }
                }
            });

            // 2. UDP Amplification 시그니처
            AddSignature(new DDoSSignature
            {
                Id = "UDP_AMP_001",
                Name = "UDP Amplification Attack",
                AttackType = DDoSAttackType.UdpAmplification,
                Severity = DDoSSeverity.Critical,
                Description = "UDP 증폭 공격 패턴",
                RecommendedAction = "UDP 응답 크기 제한 및 소스 검증",
                Patterns = new List<SignaturePattern>
                {
                    new UdpAmplificationPattern
                    {
                        MinAmplificationRatio = 5.0,
                        MaxRequestSize = 100,
                        MinResponseSize = 500,
                        TargetPorts = new[] { 53, 123, 161, 1900, 5353 } // DNS, NTP, SNMP, SSDP, mDNS
                    },
                    new PacketSizePattern
                    {
                        MinPacketSize = 500,
                        MaxPacketSize = 65535,
                        MinPacketCount = 20
                    }
                }
            });

            // 3. Slowloris 시그니처
            AddSignature(new DDoSSignature
            {
                Id = "SLOWLORIS_001",
                Name = "Slowloris Attack",
                AttackType = DDoSAttackType.SlowLoris,
                Severity = DDoSSeverity.Medium,
                Description = "Slowloris 슬로우 공격 패턴",
                RecommendedAction = "연결 타임아웃 단축 및 연결 수 제한",
                Patterns = new List<SignaturePattern>
                {
                    new SlowConnectionPattern
                    {
                        MinConnectionDuration = 30, // 30초 이상
                        MaxDataRate = 1, // 1 byte/sec 이하
                        MinConcurrentConnections = 10
                    },
                    new HttpHeaderPattern
                    {
                        IncompleteHeaders = true,
                        SlowHeaderTransmission = true
                    }
                }
            });

            // 4. ICMP Flood 시그니처
            AddSignature(new DDoSSignature
            {
                Id = "ICMP_FLOOD_001",
                Name = "ICMP Flood Attack",
                AttackType = DDoSAttackType.IcmpFlood,
                Severity = DDoSSeverity.Medium,
                Description = "ICMP 패킷 플러드 공격",
                RecommendedAction = "ICMP 트래픽 제한",
                Patterns = new List<SignaturePattern>
                {
                    new PacketRatePattern
                    {
                        MinPacketsPerSecond = 50,
                        ProtocolFilter = ProtocolKind.ICMP
                    },
                    new PacketSizePattern
                    {
                        MinPacketSize = 64,
                        MaxPacketSize = 1500,
                        MinPacketCount = 100
                    }
                }
            });

            // 5. TCP RST Flood 시그니처
            AddSignature(new DDoSSignature
            {
                Id = "RST_FLOOD_001",
                Name = "TCP RST Flood",
                AttackType = DDoSAttackType.TcpRstFlood,
                Severity = DDoSSeverity.High,
                Description = "TCP RST 패킷 플러드",
                RecommendedAction = "TCP 연결 상태 검증 강화",
                Patterns = new List<SignaturePattern>
                {
                    new TcpFlagPattern
                    {
                        RequiredFlags = TcpFlags.RST,
                        MinPacketCount = 30,
                        TimeWindowSeconds = 5
                    }
                }
            });

            // 6. Fragmentation Attack 시그니처  
            AddSignature(new DDoSSignature
            {
                Id = "FRAG_ATTACK_001",
                Name = "IP Fragmentation Attack",
                AttackType = DDoSAttackType.FragmentationAttack,
                Severity = DDoSSeverity.High,
                Description = "IP 단편화 공격",
                RecommendedAction = "단편화된 패킷 필터링",
                Patterns = new List<SignaturePattern>
                {
                    new FragmentationPattern
                    {
                        MinFragmentationRatio = 0.5, // 50% 이상 단편화
                        MinFragmentCount = 50,
                        TimeWindowSeconds = 10
                    }
                }
            });

            // 7. Botnet Pattern 시그니처
            AddSignature(new DDoSSignature
            {
                Id = "BOTNET_001",
                Name = "Botnet Attack Pattern",
                AttackType = DDoSAttackType.BotnetAttack,
                Severity = DDoSSeverity.Critical,
                Description = "봇넷 기반 분산 공격",
                RecommendedAction = "다중 IP 차단 및 패턴 분석",
                Patterns = new List<SignaturePattern>
                {
                    new TimingPattern
                    {
                        MaxTimingVariance = 5.0, // 매우 규칙적인 패턴
                        MinPacketCount = 20,
                        SynchronizedBehavior = true
                    },
                    new PacketUniformityPattern
                    {
                        MaxSizeVariance = 10,
                        MinUniformPackets = 50
                    }
                }
            });
        }

        /// <summary>
        /// 새로운 시그니처 추가
        /// </summary>
        public void AddSignature(DDoSSignature signature)
        {
            _signatures.Add(signature);
            _signatureIndex[signature.Id] = signature;
        }

        /// <summary>
        /// 시그니처 제거
        /// </summary>
        public bool RemoveSignature(string signatureId)
        {
            if (_signatureIndex.TryGetValue(signatureId, out var signature))
            {
                _signatures.Remove(signature);
                _signatureIndex.Remove(signatureId);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 활성 시그니처 목록 조회
        /// </summary>
        public List<DDoSSignature> GetActiveSignatures()
        {
            return _signatures.Where(s => s.IsActive).ToList();
        }

        /// <summary>
        /// 특정 공격 유형 시그니처 조회
        /// </summary>
        public List<DDoSSignature> GetSignaturesByAttackType(DDoSAttackType attackType)
        {
            return _signatures.Where(s => s.AttackType == attackType && s.IsActive).ToList();
        }

        /// <summary>
        /// 시그니처 통계 조회
        /// </summary>
        public SignatureDatabaseStats GetStatistics()
        {
            return new SignatureDatabaseStats
            {
                TotalSignatures = _signatures.Count,
                ActiveSignatures = _signatures.Count(s => s.IsActive),
                SignaturesByType = _signatures.GroupBy(s => s.AttackType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                LastUpdated = DateTime.Now
            };
        }
    }

    #region Signature Classes

    /// <summary>
    /// DDoS 공격 시그니처
    /// </summary>
    public class DDoSSignature
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DDoSAttackType AttackType { get; set; }
        public DDoSSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        public string RecommendedAction { get; set; } = string.Empty;
        public List<SignaturePattern> Patterns { get; set; } = new();
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        public int MatchCount { get; set; } = 0;

        /// <summary>
        /// 패킷 리스트와 시그니처 매칭
        /// </summary>
        public SignatureMatchResult Match(List<PacketDto> packets)
        {
            var result = new SignatureMatchResult
            {
                IsMatch = false,
                MatchScore = 0.0,
                MatchedPatterns = new List<string>(),
                Description = string.Empty,
                SourceIP = string.Empty
            };

            try
            {
                var matchedPatternCount = 0;
                var totalScore = 0.0;
                var matchedPatternNames = new List<string>();

                foreach (var pattern in Patterns)
                {
                    var patternResult = pattern.Match(packets);
                    if (patternResult.IsMatch)
                    {
                        matchedPatternCount++;
                        totalScore += patternResult.MatchScore;
                        matchedPatternNames.Add(pattern.GetType().Name);

                        // 첫 번째 매칭에서 소스 IP 설정
                        if (string.IsNullOrEmpty(result.SourceIP) && !string.IsNullOrEmpty(patternResult.SourceIP))
                        {
                            result.SourceIP = patternResult.SourceIP;
                        }
                    }
                }

                // 모든 패턴이 매칭되어야 시그니처 매칭 성공
                if (matchedPatternCount == Patterns.Count && Patterns.Count > 0)
                {
                    result.IsMatch = true;
                    result.MatchScore = totalScore / Patterns.Count; // 평균 점수
                    result.MatchedPatterns = matchedPatternNames;
                    result.Description = $"시그니처 '{Name}' 매칭: {matchedPatternCount}/{Patterns.Count} 패턴";

                    // 매칭 카운트 증가
                    MatchCount++;
                }
            }
            catch (Exception ex)
            {
                result.Description = $"시그니처 매칭 중 오류: {ex.Message}";
            }

            return result;
        }
    }

    /// <summary>
    /// 시그니처 매칭 결과
    /// </summary>
    public class SignatureMatchResult
    {
        public bool IsMatch { get; set; }
        public double MatchScore { get; set; }
        public List<string> MatchedPatterns { get; set; } = new();
        public string Description { get; set; } = string.Empty;
        public string SourceIP { get; set; } = string.Empty;
    }

    /// <summary>
    /// 시그니처 데이터베이스 통계
    /// </summary>
    public class SignatureDatabaseStats
    {
        public int TotalSignatures { get; set; }
        public int ActiveSignatures { get; set; }
        public Dictionary<DDoSAttackType, int> SignaturesByType { get; set; } = new();
        public DateTime LastUpdated { get; set; }
    }

    #endregion

    #region Pattern Classes

    /// <summary>
    /// 시그니처 패턴 기본 클래스
    /// </summary>
    public abstract class SignaturePattern
    {
        public abstract PatternMatchResult Match(List<PacketDto> packets);
    }

    /// <summary>
    /// 패턴 매칭 결과
    /// </summary>
    public class PatternMatchResult
    {
        public bool IsMatch { get; set; }
        public double MatchScore { get; set; }
        public string SourceIP { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }

    /// <summary>
    /// TCP 플래그 패턴
    /// </summary>
    public class TcpFlagPattern : SignaturePattern
    {
        public TcpFlags RequiredFlags { get; set; }
        public TcpFlags ForbiddenFlags { get; set; }
        public int MinPacketCount { get; set; }
        public int TimeWindowSeconds { get; set; } = 10;

        public override PatternMatchResult Match(List<PacketDto> packets)
        {
            var result = new PatternMatchResult();

            var recentTime = DateTime.Now.AddSeconds(-TimeWindowSeconds);
            var tcpPackets = packets
                .Where(p => p.Protocol == ProtocolKind.TCP && p.Timestamp >= recentTime)
                .ToList();

            var matchingPackets = tcpPackets
                .Where(p => (p.Flags & (uint)RequiredFlags) == (uint)RequiredFlags &&
                           (p.Flags & (uint)ForbiddenFlags) == 0)
                .GroupBy(p => p.SrcIp);

            foreach (var group in matchingPackets)
            {
                if (group.Count() >= MinPacketCount)
                {
                    result.IsMatch = true;
                    result.MatchScore = Math.Min(100.0, (double)group.Count() / MinPacketCount * 100);
                    result.SourceIP = group.Key;
                    result.Details = $"TCP 플래그 패턴 매칭: {group.Count()}개 패킷";
                    break;
                }
            }

            return result;
        }
    }

    /// <summary>
    /// 패킷 속도 패턴
    /// </summary>
    public class PacketRatePattern : SignaturePattern
    {
        public int MinPacketsPerSecond { get; set; }
        public ProtocolKind? ProtocolFilter { get; set; }

        public override PatternMatchResult Match(List<PacketDto> packets)
        {
            var result = new PatternMatchResult();

            var oneSecondAgo = DateTime.Now.AddSeconds(-1);
            var recentPackets = packets.Where(p => p.Timestamp >= oneSecondAgo);

            if (ProtocolFilter.HasValue)
            {
                recentPackets = recentPackets.Where(p => p.Protocol == ProtocolFilter.Value);
            }

            var packetsByIP = recentPackets.GroupBy(p => p.SrcIp);

            foreach (var group in packetsByIP)
            {
                if (group.Count() >= MinPacketsPerSecond)
                {
                    result.IsMatch = true;
                    result.MatchScore = Math.Min(100.0, (double)group.Count() / MinPacketsPerSecond * 100);
                    result.SourceIP = group.Key;
                    result.Details = $"높은 패킷 전송률: {group.Count()} packets/sec";
                    break;
                }
            }

            return result;
        }
    }

    /// <summary>
    /// UDP 증폭 패턴
    /// </summary>
    public class UdpAmplificationPattern : SignaturePattern
    {
        public double MinAmplificationRatio { get; set; }
        public int MaxRequestSize { get; set; }
        public int MinResponseSize { get; set; }
        public int[] TargetPorts { get; set; } = Array.Empty<int>();

        public override PatternMatchResult Match(List<PacketDto> packets)
        {
            var result = new PatternMatchResult();

            var udpPackets = packets.Where(p => p.Protocol == ProtocolKind.UDP).ToList();

            // 포트별로 요청-응답 분석
            foreach (var port in TargetPorts)
            {
                var requestPackets = udpPackets.Where(p => p.DstPort == port && p.Length <= MaxRequestSize);
                var responsePackets = udpPackets.Where(p => p.SrcPort == port && p.Length >= MinResponseSize);

                if (requestPackets.Any() && responsePackets.Any())
                {
                    var totalRequestSize = requestPackets.Sum(p => p.Length);
                    var totalResponseSize = responsePackets.Sum(p => p.Length);

                    if (totalRequestSize > 0)
                    {
                        var amplificationRatio = (double)totalResponseSize / totalRequestSize;
                        if (amplificationRatio >= MinAmplificationRatio)
                        {
                            result.IsMatch = true;
                            result.MatchScore = Math.Min(100.0, amplificationRatio * 10);
                            result.SourceIP = requestPackets.First().SrcIp;
                            result.Details = $"UDP 증폭 공격: 비율 {amplificationRatio:F1}x";
                            break;
                        }
                    }
                }
            }

            return result;
        }
    }

    /// <summary>
    /// 패킷 크기 패턴
    /// </summary>
    public class PacketSizePattern : SignaturePattern
    {
        public int MinPacketSize { get; set; }
        public int MaxPacketSize { get; set; }
        public int MinPacketCount { get; set; }

        public override PatternMatchResult Match(List<PacketDto> packets)
        {
            var result = new PatternMatchResult();

            var matchingPackets = packets
                .Where(p => p.Length >= MinPacketSize && p.Length <= MaxPacketSize)
                .GroupBy(p => p.SrcIp);

            foreach (var group in matchingPackets)
            {
                if (group.Count() >= MinPacketCount)
                {
                    result.IsMatch = true;
                    result.MatchScore = Math.Min(100.0, (double)group.Count() / MinPacketCount * 100);
                    result.SourceIP = group.Key;
                    result.Details = $"패킷 크기 패턴: {group.Count()}개 ({MinPacketSize}-{MaxPacketSize} bytes)";
                    break;
                }
            }

            return result;
        }
    }

    /// <summary>
    /// 슬로우 연결 패턴
    /// </summary>
    public class SlowConnectionPattern : SignaturePattern
    {
        public int MinConnectionDuration { get; set; } // seconds
        public int MaxDataRate { get; set; } // bytes per second
        public int MinConcurrentConnections { get; set; }

        public override PatternMatchResult Match(List<PacketDto> packets)
        {
            var result = new PatternMatchResult();

            // 실제 구현에서는 연결 상태를 추적해야 하지만,
            // 여기서는 단순화된 버전으로 구현
            var connectionGroups = packets
                .GroupBy(p => new { p.SrcIp, p.DstIp, p.DstPort })
                .Where(g => g.Count() < MaxDataRate * MinConnectionDuration) // 낮은 데이터 전송률
                .GroupBy(g => g.Key.SrcIp);

            foreach (var ipGroup in connectionGroups)
            {
                if (ipGroup.Count() >= MinConcurrentConnections)
                {
                    result.IsMatch = true;
                    result.MatchScore = Math.Min(100.0, (double)ipGroup.Count() / MinConcurrentConnections * 100);
                    result.SourceIP = ipGroup.Key;
                    result.Details = $"슬로우 연결 패턴: {ipGroup.Count()}개 연결";
                    break;
                }
            }

            return result;
        }
    }

    /// <summary>
    /// HTTP 헤더 패턴
    /// </summary>
    public class HttpHeaderPattern : SignaturePattern
    {
        public bool IncompleteHeaders { get; set; }
        public bool SlowHeaderTransmission { get; set; }

        public override PatternMatchResult Match(List<PacketDto> packets)
        {
            // 단순화된 구현 - 실제로는 HTTP 페이로드 분석이 필요
            var result = new PatternMatchResult();

            var httpPackets = packets
                .Where(p => p.DstPort == 80 || p.DstPort == 443)
                .Where(p => p.Length < 100) // 작은 크기의 HTTP 요청
                .GroupBy(p => p.SrcIp);

            foreach (var group in httpPackets)
            {
                if (group.Count() >= 10) // 많은 수의 작은 HTTP 요청
                {
                    result.IsMatch = true;
                    result.MatchScore = Math.Min(100.0, (double)group.Count() / 10 * 100);
                    result.SourceIP = group.Key;
                    result.Details = $"의심스러운 HTTP 패턴: {group.Count()}개 작은 요청";
                    break;
                }
            }

            return result;
        }
    }

    /// <summary>
    /// 단편화 패턴
    /// </summary>
    public class FragmentationPattern : SignaturePattern
    {
        public double MinFragmentationRatio { get; set; }
        public int MinFragmentCount { get; set; }
        public int TimeWindowSeconds { get; set; }

        public override PatternMatchResult Match(List<PacketDto> packets)
        {
            var result = new PatternMatchResult();

            var recentTime = DateTime.Now.AddSeconds(-TimeWindowSeconds);
            var recentPackets = packets.Where(p => p.Timestamp >= recentTime).GroupBy(p => p.SrcIp);

            foreach (var group in recentPackets)
            {
                var totalPackets = group.Count();
                var fragmentedPackets = group.Count(p => p.Length < 500); // 단편화로 가정

                if (totalPackets > 0)
                {
                    var fragmentationRatio = (double)fragmentedPackets / totalPackets;
                    if (fragmentationRatio >= MinFragmentationRatio && fragmentedPackets >= MinFragmentCount)
                    {
                        result.IsMatch = true;
                        result.MatchScore = Math.Min(100.0, fragmentationRatio * 100);
                        result.SourceIP = group.Key;
                        result.Details = $"높은 단편화율: {fragmentationRatio:P} ({fragmentedPackets}/{totalPackets})";
                        break;
                    }
                }
            }

            return result;
        }
    }

    /// <summary>
    /// 타이밍 패턴
    /// </summary>
    public class TimingPattern : SignaturePattern
    {
        public double MaxTimingVariance { get; set; }
        public int MinPacketCount { get; set; }
        public bool SynchronizedBehavior { get; set; }

        public override PatternMatchResult Match(List<PacketDto> packets)
        {
            var result = new PatternMatchResult();

            var packetsByIP = packets.GroupBy(p => p.SrcIp);

            foreach (var group in packetsByIP)
            {
                var sortedPackets = group.OrderBy(p => p.Timestamp).ToList();
                if (sortedPackets.Count < MinPacketCount) continue;

                var intervals = new List<double>();
                for (int i = 1; i < sortedPackets.Count; i++)
                {
                    var interval = (sortedPackets[i].Timestamp - sortedPackets[i - 1].Timestamp).TotalMilliseconds;
                    intervals.Add(interval);
                }

                if (intervals.Count > 1)
                {
                    var avgInterval = intervals.Average();
                    var variance = intervals.Select(x => Math.Pow(x - avgInterval, 2)).Average();
                    var stdDev = Math.Sqrt(variance);

                    if (stdDev <= MaxTimingVariance)
                    {
                        result.IsMatch = true;
                        result.MatchScore = Math.Min(100.0, (MaxTimingVariance - stdDev) / MaxTimingVariance * 100);
                        result.SourceIP = group.Key;
                        result.Details = $"규칙적인 타이밍: 표준편차 {stdDev:F1}ms";
                        break;
                    }
                }
            }

            return result;
        }
    }

    /// <summary>
    /// 패킷 균일성 패턴
    /// </summary>
    public class PacketUniformityPattern : SignaturePattern
    {
        public int MaxSizeVariance { get; set; }
        public int MinUniformPackets { get; set; }

        public override PatternMatchResult Match(List<PacketDto> packets)
        {
            var result = new PatternMatchResult();

            var packetsByIP = packets.GroupBy(p => p.SrcIp);

            foreach (var group in packetsByIP)
            {
                if (group.Count() < MinUniformPackets) continue;

                var sizes = group.Select(p => p.Length).ToList();
                var avgSize = sizes.Average();
                var variance = sizes.Select(x => Math.Pow(x - avgSize, 2)).Average();
                var stdDev = Math.Sqrt(variance);

                if (stdDev <= MaxSizeVariance)
                {
                    result.IsMatch = true;
                    result.MatchScore = Math.Min(100.0, (MaxSizeVariance - stdDev) / MaxSizeVariance * 100);
                    result.SourceIP = group.Key;
                    result.Details = $"균일한 패킷 크기: 표준편차 {stdDev:F1} bytes";
                    break;
                }
            }

            return result;
        }
    }

    #endregion
}