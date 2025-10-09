using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LogCheck.Models;

namespace LogCheck.Services
{
    /// <summary>
    /// DDoS 감지 엔진과 고급 패킷 분석기를 통합한 통합 DDoS 방어 시스템
    /// </summary>
    public class IntegratedDDoSDefenseSystem
    {
        private readonly DDoSDetectionEngine _detectionEngine;
        private readonly AdvancedPacketAnalyzer _packetAnalyzer;
        private readonly RateLimitingService _rateLimiter;
        private readonly DDoSSignatureDatabase _signatureDatabase;

        private readonly ConcurrentQueue<PacketDto> _packetQueue;
        private readonly ConcurrentDictionary<string, DDoSDetectionResult> _activeAttacks;
        private readonly System.Threading.Timer _analysisTimer;
        private readonly System.Threading.Timer _cleanupTimer;

        private volatile bool _isRunning = false;
        private readonly object _lockObject = new object();

        // 성능 메트릭
        private long _totalPacketsProcessed = 0;
        private long _totalAttacksDetected = 0;
        private long _totalAttacksBlocked = 0;
        private readonly ConcurrentDictionary<DDoSAttackType, int> _attackTypeStats = new();

        // 이벤트
        public event EventHandler<DDoSDetectionResult>? AttackDetected;
        public event EventHandler<DefenseActionResult>? DefenseActionExecuted;
        public event EventHandler<DDoSMonitoringMetrics>? MetricsUpdated;

        public IntegratedDDoSDefenseSystem(
            DDoSDetectionEngine detectionEngine,
            AdvancedPacketAnalyzer packetAnalyzer,
            RateLimitingService rateLimiter,
            DDoSSignatureDatabase signatureDatabase)
        {
            _detectionEngine = detectionEngine ?? throw new ArgumentNullException(nameof(detectionEngine));
            _packetAnalyzer = packetAnalyzer ?? throw new ArgumentNullException(nameof(packetAnalyzer));
            _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
            _signatureDatabase = signatureDatabase ?? throw new ArgumentNullException(nameof(signatureDatabase));

            _packetQueue = new ConcurrentQueue<PacketDto>();
            _activeAttacks = new ConcurrentDictionary<string, DDoSDetectionResult>();

            // 타이머 설정: 1초마다 분석, 30초마다 정리
            _analysisTimer = new System.Threading.Timer(PerformAnalysis, null, Timeout.Infinite, Timeout.Infinite);
            _cleanupTimer = new System.Threading.Timer(CleanupExpiredAttacks, null, Timeout.Infinite, Timeout.Infinite);

            // 시그니처 데이터베이스 초기화
            _signatureDatabase.LoadDefaultSignatures();
        }

        /// <summary>
        /// 방어 시스템 시작
        /// </summary>
        public void Start()
        {
            lock (_lockObject)
            {
                if (!_isRunning)
                {
                    _isRunning = true;
                    _analysisTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(1));
                    _cleanupTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(30));
                    LogHelper.Log($"통합 DDoS 방어 시스템 시작됨", MessageType.Information);
                }
            }
        }

        /// <summary>
        /// 방어 시스템 중지
        /// </summary>
        public void Stop()
        {
            lock (_lockObject)
            {
                if (_isRunning)
                {
                    _isRunning = false;
                    _analysisTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    _cleanupTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    LogHelper.Log($"통합 DDoS 방어 시스템 중지됨", MessageType.Information);
                }
            }
        }

        /// <summary>
        /// 패킷 데이터 추가 (실시간 처리용)
        /// </summary>
        public void AddPacket(PacketDto packet)
        {
            if (packet == null || !_isRunning) return;

            _packetQueue.Enqueue(packet);
            Interlocked.Increment(ref _totalPacketsProcessed);

            // 큐 크기 제한 (메모리 보호)
            if (_packetQueue.Count > 10000)
            {
                while (_packetQueue.Count > 8000)
                {
                    _packetQueue.TryDequeue(out _);
                }
            }
        }

        /// <summary>
        /// 패킷 배치 처리 (대량 데이터 분석용)
        /// </summary>
        public async Task<List<DDoSDetectionResult>> AnalyzePacketBatch(List<PacketDto> packets)
        {
            if (packets == null || packets.Count == 0)
                return new List<DDoSDetectionResult>();

            var results = new List<DDoSDetectionResult>();

            try
            {
                // 1. 기존 DDoS 감지 엔진으로 패킷 분석
                var basicAlerts = await _detectionEngine.AnalyzePacketsAsync(packets);
                var basicDetectionResults = ConvertAlertsToResults(basicAlerts);

                // 2. 고급 패킷 분석 수행
                var advancedAlerts = _packetAnalyzer.AnalyzePacketBatch(packets);
                var packetAnalysisResults = ConvertAdvancedAlertsToPacketResult(advancedAlerts);

                // 3. 시그니처 기반 매칭
                var signatureResults = await AnalyzeWithSignatures(packets);

                // 4. 결과 통합 및 상관 관계 분석
                results = await CorrelateAndMergeResults(
                    basicDetectionResults,
                    packetAnalysisResults,
                    signatureResults
                );

                // 5. 방어 조치 실행
                foreach (var result in results.Where(r => r.IsAttackDetected))
                {
                    await ExecuteDefenseActions(result);
                }

                Interlocked.Add(ref _totalAttacksDetected, results.Count(r => r.IsAttackDetected));
            }
            catch (Exception ex)
            {
                LogHelper.Log($"패킷 배치 분석 오류: {ex.Message}", MessageType.Error);
            }

            return results;
        }

        /// <summary>
        /// 정기적인 분석 수행 (타이머 콜백)
        /// </summary>
        private async void PerformAnalysis(object? state)
        {
            if (!_isRunning) return;

            try
            {
                var packets = DequeuePackets(1000); // 최대 1000개 패킷 처리
                if (packets.Count == 0) return;

                var results = await AnalyzePacketBatch(packets);

                // 메트릭 업데이트
                var metrics = GenerateCurrentMetrics();
                MetricsUpdated?.Invoke(this, metrics);
            }
            catch (Exception ex)
            {
                LogHelper.Log($"정기 분석 중 오류: {ex.Message}", MessageType.Error);
            }
        }

        /// <summary>
        /// 큐에서 패킷 추출
        /// </summary>
        private List<PacketDto> DequeuePackets(int maxCount)
        {
            var packets = new List<PacketDto>();

            for (int i = 0; i < maxCount && _packetQueue.TryDequeue(out var packet); i++)
            {
                packets.Add(packet);
            }

            return packets;
        }

        /// <summary>
        /// 시그니처 기반 분석
        /// </summary>
        private async Task<List<DDoSDetectionResult>> AnalyzeWithSignatures(List<PacketDto> packets)
        {
            var results = new List<DDoSDetectionResult>();

            try
            {
                var signatures = _signatureDatabase.GetActiveSignatures();

                await Task.Run(() =>
                {
                    foreach (var signature in signatures)
                    {
                        var matchResult = signature.Match(packets);
                        if (matchResult.IsMatch)
                        {
                            var detectionResult = new DDoSDetectionResult
                            {
                                IsAttackDetected = true,
                                AttackType = signature.AttackType,
                                Severity = signature.Severity,
                                AttackDescription = signature.Description,
                                SourceIP = matchResult.SourceIP,
                                AttackScore = matchResult.MatchScore,
                                DetectedAt = DateTime.Now,
                                MatchedSignatures = new List<string> { signature.Name },
                                RecommendedActions = GetRecommendedActions(signature.Severity),
                                AdditionalData = new Dictionary<string, object>
                                {
                                    ["SignatureId"] = signature.Id,
                                    ["MatchedPatterns"] = matchResult.MatchedPatterns
                                }
                            };

                            results.Add(detectionResult);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                LogHelper.Log($"시그니처 분석 오류: {ex.Message}", MessageType.Error);
            }

            return results;
        }

        /// <summary>
        /// 결과 상관 관계 분석 및 통합
        /// </summary>
        private async Task<List<DDoSDetectionResult>> CorrelateAndMergeResults(
            List<DDoSDetectionResult> basicResults,
            PacketAnalysisResult packetAnalysis,
            List<DDoSDetectionResult> signatureResults)
        {
            var mergedResults = new List<DDoSDetectionResult>();

            try
            {
                // 1. 기본 감지 결과 추가
                mergedResults.AddRange(basicResults);

                // 2. 시그니처 결과 추가 (중복 제거)
                foreach (var sigResult in signatureResults)
                {
                    var existing = mergedResults.FirstOrDefault(r =>
                        r.SourceIP == sigResult.SourceIP &&
                        r.AttackType == sigResult.AttackType);

                    if (existing != null)
                    {
                        // 기존 결과와 병합
                        existing.AttackScore = Math.Max(existing.AttackScore, sigResult.AttackScore);
                        existing.MatchedSignatures.AddRange(sigResult.MatchedSignatures);
                        existing.Severity = (DDoSSeverity)Math.Max((int)existing.Severity, (int)sigResult.Severity);
                    }
                    else
                    {
                        mergedResults.Add(sigResult);
                    }
                }

                // 3. 패킷 분석 결과를 기반으로 추가 검증
                foreach (var result in mergedResults.Where(r => r.IsAttackDetected))
                {
                    EnhanceResultWithPacketAnalysis(result, packetAnalysis);
                }

                // 4. 심각도에 따른 정렬
                mergedResults = mergedResults
                    .OrderByDescending(r => r.Severity)
                    .ThenByDescending(r => r.AttackScore)
                    .ToList();
            }
            catch (Exception ex)
            {
                LogHelper.Log($"결과 통합 중 오류: {ex.Message}", MessageType.Error);
            }

            return mergedResults;
        }

        /// <summary>
        /// 패킷 분석 결과로 감지 결과 개선
        /// </summary>
        private void EnhanceResultWithPacketAnalysis(DDoSDetectionResult result, PacketAnalysisResult packetAnalysis)
        {
            try
            {
                // TCP 플래그 분석 결과 추가
                var tcpAnalysis = packetAnalysis.TcpFlagAnalyses
                    .FirstOrDefault(t => t.SourceIP == result.SourceIP && t.IsAnomalous);

                if (tcpAnalysis != null)
                {
                    result.AdditionalData["TcpFlagAnomaly"] = tcpAnalysis.Description;
                    result.AttackScore += 10; // 추가 점수
                }

                // 이상 징후 정보 추가
                var anomalies = packetAnalysis.AnomaliesDetected
                    .Where(a => a.AffectedIP == result.SourceIP)
                    .ToList();

                if (anomalies.Count > 0)
                {
                    result.AdditionalData["DetectedAnomalies"] = anomalies.Select(a => a.Description).ToList();
                    result.AttackScore += anomalies.Sum(a => a.Severity);
                }

                // 패킷 수 정보 업데이트
                if (packetAnalysis.SourceIPCounts.ContainsKey(result.SourceIP))
                {
                    result.PacketCount = packetAnalysis.SourceIPCounts[result.SourceIP];
                }
            }
            catch (Exception ex)
            {
                LogHelper.Log($"결과 개선 중 오류: {ex.Message}", MessageType.Warning);
            }
        }

        /// <summary>
        /// 방어 조치 실행
        /// </summary>
        private async Task ExecuteDefenseActions(DDoSDetectionResult detectionResult)
        {
            try
            {
                foreach (var action in detectionResult.RecommendedActions)
                {
                    var actionResult = await ExecuteSingleDefenseAction(action, detectionResult);
                    DefenseActionExecuted?.Invoke(this, actionResult);

                    if (actionResult.Success && IsBlockingAction(action))
                    {
                        Interlocked.Increment(ref _totalAttacksBlocked);
                    }
                }

                // 활성 공격 목록에 추가
                var attackKey = $"{detectionResult.SourceIP}_{detectionResult.AttackType}";
                _activeAttacks.AddOrUpdate(attackKey, detectionResult, (k, v) => detectionResult);

                // 공격 감지 이벤트 발생
                AttackDetected?.Invoke(this, detectionResult);

                // 통계 업데이트
                _attackTypeStats.AddOrUpdate(detectionResult.AttackType, 1, (k, v) => v + 1);
            }
            catch (Exception ex)
            {
                LogHelper.Log($"방어 조치 실행 오류: {ex.Message}", MessageType.Error);
            }
        }

        /// <summary>
        /// 단일 방어 조치 실행
        /// </summary>
        private async Task<DefenseActionResult> ExecuteSingleDefenseAction(
            DefenseActionType actionType,
            DDoSDetectionResult detectionResult)
        {
            var startTime = DateTime.Now;
            var result = new DefenseActionResult
            {
                ActionType = actionType,
                TargetIP = detectionResult.SourceIP,
                ExecutedAt = startTime
            };

            try
            {
                switch (actionType)
                {
                    case DefenseActionType.RateLimit:
                        await _rateLimiter.ApplyRateLimit(detectionResult.SourceIP, null!);
                        result.Success = true;
                        result.Description = "트래픽 속도 제한 적용";
                        break;

                    case DefenseActionType.IpBlock:
                        result.Success = await BlockIP(detectionResult.SourceIP);
                        result.Description = "IP 주소 차단";
                        break;

                    case DefenseActionType.ConnectionLimit:
                        result.Success = await LimitConnections(detectionResult.SourceIP);
                        result.Description = "연결 수 제한";
                        break;

                    case DefenseActionType.AdminAlert:
                        result.Success = await SendAdminAlert(detectionResult);
                        result.Description = "관리자 알림 발송";
                        break;

                    default:
                        result.Success = false;
                        result.ErrorMessage = "지원하지 않는 방어 조치";
                        break;
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }
            finally
            {
                result.ExecutionDuration = DateTime.Now - startTime;
            }

            return result;
        }

        /// <summary>
        /// IP 차단
        /// </summary>
        private async Task<bool> BlockIP(string ipAddress)
        {
            try
            {
                // Windows 방화벽을 통한 IP 차단 (실제 구현 필요)
                await Task.Run(() =>
                {
                    // 여기에 실제 방화벽 규칙 추가 로직 구현
                    LogHelper.Log($"IP {ipAddress} 차단됨", MessageType.Information);
                });
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 연결 제한
        /// </summary>
        private async Task<bool> LimitConnections(string ipAddress)
        {
            try
            {
                await _rateLimiter.LimitConnectionsForIP(ipAddress);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 관리자 알림 발송
        /// </summary>
        private async Task<bool> SendAdminAlert(DDoSDetectionResult detectionResult)
        {
            try
            {
                await Task.Run(() =>
                {
                    LogHelper.Log($"[긴급] DDoS 공격 감지: {detectionResult}", MessageType.Critical);
                });
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 심각도별 권장 조치 결정
        /// </summary>
        private List<DefenseActionType> GetRecommendedActions(DDoSSeverity severity)
        {
            return severity switch
            {
                DDoSSeverity.Low => new List<DefenseActionType> { DefenseActionType.EnhancedMonitoring },
                DDoSSeverity.Medium => new List<DefenseActionType> { DefenseActionType.RateLimit, DefenseActionType.AdminAlert },
                DDoSSeverity.High => new List<DefenseActionType> { DefenseActionType.RateLimit, DefenseActionType.ConnectionLimit, DefenseActionType.AdminAlert },
                DDoSSeverity.Critical => new List<DefenseActionType> { DefenseActionType.IpBlock, DefenseActionType.AdminAlert },
                DDoSSeverity.Emergency => new List<DefenseActionType> { DefenseActionType.EmergencyBlock, DefenseActionType.AdminAlert },
                _ => new List<DefenseActionType> { DefenseActionType.EnhancedMonitoring }
            };
        }

        /// <summary>
        /// 차단 조치인지 확인
        /// </summary>
        private bool IsBlockingAction(DefenseActionType actionType)
        {
            return actionType is DefenseActionType.IpBlock or
                   DefenseActionType.AutoBlock or
                   DefenseActionType.EmergencyBlock;
        }

        /// <summary>
        /// 만료된 공격 정보 정리
        /// </summary>
        private void CleanupExpiredAttacks(object? state)
        {
            try
            {
                var expireTime = DateTime.Now.AddMinutes(-10); // 10분 후 만료
                var expiredKeys = _activeAttacks
                    .Where(kvp => kvp.Value.DetectedAt < expireTime)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    _activeAttacks.TryRemove(key, out _);
                }

                if (expiredKeys.Count > 0)
                {
                    LogHelper.Log($"만료된 공격 정보 {expiredKeys.Count}건 정리됨", MessageType.Information);
                }
            }
            catch (Exception ex)
            {
                LogHelper.Log($"공격 정보 정리 중 오류: {ex.Message}", MessageType.Warning);
            }
        }

        /// <summary>
        /// 현재 메트릭 생성
        /// </summary>
        private DDoSMonitoringMetrics GenerateCurrentMetrics()
        {
            var metrics = new DDoSMonitoringMetrics
            {
                Timestamp = DateTime.Now,
                TotalPacketsAnalyzed = _totalPacketsProcessed,
                PacketsPerSecond = CalculatePacketsPerSecond(),
                ActiveConnections = _activeAttacks.Count,
                SuspiciousConnections = _activeAttacks.Count(a => a.Value.Severity >= DDoSSeverity.Medium),
                BlockedIPs = _activeAttacks.Count(a => a.Value.RecommendedActions.Any(IsBlockingAction)),
                RecentAlerts = _activeAttacks.Values
                    .Where(a => a.DetectedAt > DateTime.Now.AddMinutes(-5))
                    .Select(a => a.AttackDescription)
                    .ToList()
            };

            metrics.UpdateStateFromRiskScore();
            return metrics;
        }

        /// <summary>
        /// 초당 패킷 수 계산
        /// </summary>
        private long CalculatePacketsPerSecond()
        {
            // 간단한 구현 - 실제로는 더 정교한 계산 필요
            return Math.Max(0, _packetQueue.Count);
        }

        /// <summary>
        /// 통계 정보 조회
        /// </summary>
        public DDoSDetectionStats GetStatistics()
        {
            return new DDoSDetectionStats
            {
                TotalAttacksDetected = (int)_totalAttacksDetected,
                AttacksBlocked = (int)_totalAttacksBlocked,
                UniqueAttackers = _activeAttacks.Values.Select(a => a.SourceIP).Distinct().Count(),
                AttacksByType = new Dictionary<DDoSAttackType, int>(_attackTypeStats),
                AttacksBySeverity = _activeAttacks.Values
                    .GroupBy(a => a.Severity)
                    .ToDictionary(g => g.Key, g => g.Count()),
                TopAttackerIPs = _activeAttacks.Values
                    .GroupBy(a => a.SourceIP)
                    .ToDictionary(g => g.Key, g => g.Count()),
                LastUpdated = DateTime.Now
            };
        }

        /// <summary>
        /// DDoSAlert 리스트를 DDoSDetectionResult 리스트로 변환
        /// </summary>
        private List<DDoSDetectionResult> ConvertAlertsToResults(List<DDoSAlert> alerts)
        {
            return alerts.Select(alert => new DDoSDetectionResult
            {
                AttackType = alert.AttackType,
                IsAttackDetected = true,
                SourceIP = alert.SourceIP,
                Severity = alert.Severity, // 이미 DDoSSeverity 타입
                DetectedAt = alert.DetectedAt,
                AttackScore = CalculateAttackScore(alert),
                PacketCount = alert.PacketCount
            }).ToList();
        }

        /// <summary>
        /// AdvancedDDoSAlert 리스트를 PacketAnalysisResult로 변환
        /// </summary>
        private PacketAnalysisResult ConvertAdvancedAlertsToPacketResult(List<AdvancedDDoSAlert> advancedAlerts)
        {
            return new PacketAnalysisResult
            {
                AnalysisTime = DateTime.Now,
                TotalPackets = advancedAlerts.Count,
                AveragePacketSize = 64.0, // 기본값
                PacketsPerSecond = advancedAlerts.Count / Math.Max(1.0, 1.0), // 초당 패킷 수 추정
                AnalysisDuration = TimeSpan.FromSeconds(1)
            };
        }

        /// <summary>
        /// 공격 점수 계산
        /// </summary>
        private double CalculateAttackScore(DDoSAlert alert)
        {
            // 기본 점수 계산 로직
            double score = alert.ConnectionCount * 0.1 + alert.PacketCount * 0.05;

            // 공격 타입별 가중치
            score *= alert.AttackType switch
            {
                DDoSAttackType.SynFlood => 1.5,
                DDoSAttackType.UdpFlood => 1.3,
                DDoSAttackType.HttpFlood => 1.8,
                DDoSAttackType.SlowLoris => 2.0,
                _ => 1.0
            };

            return Math.Min(score, 100.0); // 최대 100점
        }

        public void Dispose()
        {
            Stop();
            _analysisTimer?.Dispose();
            _cleanupTimer?.Dispose();
        }
    }
}