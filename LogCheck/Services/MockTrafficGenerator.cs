using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using LogCheck.Models;

namespace LogCheck.Services
{
    /// <summary>
    /// Mock 트래픽 생성기 - PowerShell 스크립트 대체용
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class MockTrafficGenerator
    {
        private readonly IntegratedDDoSDefenseSystem? _ddosSystem;
        private readonly Random _random = new Random();

        public MockTrafficGenerator(IntegratedDDoSDefenseSystem? ddosSystem)
        {
            _ddosSystem = ddosSystem;
        }

        /// <summary>
        /// 현실적인 네트워크 트래픽 생성
        /// </summary>
        public async Task GenerateRealisticTrafficAsync()
        {
            System.Diagnostics.Debug.WriteLine("🚀 현실적인 트래픽 생성 시작");

            // 1. 정상 트래픽 생성 (20개)
            await GenerateNormalTrafficAsync();
            await Task.Delay(2000);

            // 2. 의심스러운 트래픽 생성 (50개)
            await GenerateSuspiciousTrafficAsync();
            await Task.Delay(1000);

            // 3. 명확한 공격 트래픽 생성 (200개)
            await GenerateAttackTrafficAsync();

            System.Diagnostics.Debug.WriteLine("✅ 트래픽 생성 완료");
        }

        /// <summary>
        /// 정상 트래픽 생성
        /// </summary>
        private async Task GenerateNormalTrafficAsync()
        {
            System.Diagnostics.Debug.WriteLine("📡 정상 트래픽 생성 중...");

            var normalSources = new[] { "192.168.1.10", "192.168.1.11", "192.168.1.12", "10.0.0.5" };
            var normalDestinations = new[] { "8.8.8.8", "1.1.1.1", "208.67.222.222" };
            var normalPorts = new[] { 80, 443, 53, 25, 993 };

            for (int i = 0; i < 20; i++)
            {
                var packet = new PacketDto
                {
                    SrcIp = normalSources[_random.Next(normalSources.Length)],
                    DstIp = normalDestinations[_random.Next(normalDestinations.Length)],
                    Protocol = _random.NextDouble() > 0.7 ? ProtocolKind.UDP : ProtocolKind.TCP,
                    SrcPort = _random.Next(1024, 65535),
                    DstPort = normalPorts[_random.Next(normalPorts.Length)],
                    Length = _random.Next(64, 2048),
                    Timestamp = DateTime.Now
                };

                _ddosSystem?.AddPacket(packet);
                await Task.Delay(200); // 정상적인 간격 (200ms)
            }

            System.Diagnostics.Debug.WriteLine("✅ 정상 트래픽 20개 생성 완료");
        }

        /// <summary>
        /// 의심스러운 트래픽 생성
        /// </summary>
        private async Task GenerateSuspiciousTrafficAsync()
        {
            System.Diagnostics.Debug.WriteLine("⚠️ 의심스러운 트래픽 생성 중...");

            // 동일 IP에서 반복적인 요청 (아직 임계값 미달)
            var suspiciousIP = "192.168.1.50";
            var targetPorts = new[] { 80, 443, 22, 3389, 445 };

            for (int i = 0; i < 50; i++)
            {
                var packet = new PacketDto
                {
                    SrcIp = suspiciousIP,
                    DstIp = "127.0.0.1",
                    Protocol = ProtocolKind.TCP,
                    SrcPort = _random.Next(1024, 65535),
                    DstPort = targetPorts[_random.Next(targetPorts.Length)],
                    Length = _random.Next(64, 512),
                    Timestamp = DateTime.Now,
                    Flags = 0x02 // SYN 플래그
                };

                _ddosSystem?.AddPacket(packet);
                await Task.Delay(100); // 빠른 간격 (100ms)
            }

            System.Diagnostics.Debug.WriteLine("✅ 의심스러운 트래픽 50개 생성 완료");
        }

        /// <summary>
        /// 명확한 공격 트래픽 생성 (DDoS)
        /// </summary>
        private async Task GenerateAttackTrafficAsync()
        {
            System.Diagnostics.Debug.WriteLine("🚨 공격 트래픽 생성 중...");

            // 1. TCP SYN Flood (100개)
            await GenerateTcpSynFloodAsync();

            // 2. UDP Flood (100개)
            await GenerateUdpFloodAsync();

            System.Diagnostics.Debug.WriteLine("✅ 공격 트래픽 200개 생성 완료");
        }

        /// <summary>
        /// TCP SYN Flood 공격 생성
        /// </summary>
        private async Task GenerateTcpSynFloodAsync()
        {
            System.Diagnostics.Debug.WriteLine("🔥 TCP SYN Flood 생성 중...");

            var attackerIP = "192.168.1.100";
            var targetPorts = new[] { 80, 443, 8080, 3389, 445, 135, 139 };

            for (int i = 0; i < 100; i++)
            {
                var packet = new PacketDto
                {
                    SrcIp = attackerIP,
                    DstIp = "127.0.0.1",
                    Protocol = ProtocolKind.TCP,
                    SrcPort = _random.Next(1024, 65535),
                    DstPort = targetPorts[_random.Next(targetPorts.Length)],
                    Length = 64, // 작은 SYN 패킷
                    Timestamp = DateTime.Now,
                    Flags = 0x02 // SYN 플래그
                };

                _ddosSystem?.AddPacket(packet);
                await Task.Delay(10); // 매우 빠른 간격 (10ms) - 공격 특성
            }

            System.Diagnostics.Debug.WriteLine("✅ TCP SYN Flood 100개 생성 완료");
        }

        /// <summary>
        /// UDP Flood 공격 생성
        /// </summary>
        private async Task GenerateUdpFloodAsync()
        {
            System.Diagnostics.Debug.WriteLine("🔥 UDP Flood 생성 중...");

            var attackerIP = "192.168.1.101";
            var targetPorts = new[] { 53, 123, 161, 500, 1900, 5060 }; // DNS, NTP, SNMP, IKE, SSDP, SIP

            for (int i = 0; i < 100; i++)
            {
                var packet = new PacketDto
                {
                    SrcIp = attackerIP,
                    DstIp = "127.0.0.1",
                    Protocol = ProtocolKind.UDP,
                    SrcPort = _random.Next(1024, 65535),
                    DstPort = targetPorts[_random.Next(targetPorts.Length)],
                    Length = 512, // 중간 크기 UDP 패킷
                    Timestamp = DateTime.Now
                };

                _ddosSystem?.AddPacket(packet);
                await Task.Delay(10); // 매우 빠른 간격 (10ms) - 공격 특성
            }

            System.Diagnostics.Debug.WriteLine("✅ UDP Flood 100개 생성 완료");
        }

        /// <summary>
        /// 빠른 테스트용 단순 공격 생성
        /// </summary>
        public async Task QuickAttackTestAsync(CancellationToken cancellationToken = default)
        {
            System.Diagnostics.Debug.WriteLine("⚡ 빠른 공격 테스트 시작");

            var attackerIP = "192.168.1.99";

            // 200개의 공격 패킷을 빠르게 생성 (임계값 50을 초과)
            for (int i = 0; i < 200 && !cancellationToken.IsCancellationRequested; i++)
            {
                // 취소 토큰 더 자주 체크
                if (cancellationToken.IsCancellationRequested)
                {
                    System.Diagnostics.Debug.WriteLine($"⏹️ QuickAttack 테스트 취소됨 (패킷 {i}개 생성)");
                    break;
                }

                var packet = new PacketDto
                {
                    SrcIp = attackerIP,
                    DstIp = "127.0.0.1",
                    Protocol = ProtocolKind.TCP,
                    SrcPort = _random.Next(1024, 65535),
                    DstPort = 80,
                    Length = 64,
                    Timestamp = DateTime.Now,
                    Flags = 0x02, // SYN
                    ProcessId = 1234,
                    ProcessName = "TestProcess"
                };

                _ddosSystem?.AddPacket(packet);

                // 매 10개마다 취소 체크와 지연
                if (i % 10 == 0)
                {
                    await Task.Delay(5, cancellationToken);
                }
            }

            System.Diagnostics.Debug.WriteLine("✅ 빠른 공격 테스트 완료 - 200개 패킷");
        }        /// <summary>
                 /// UDP 플러드 테스트 (E2E 호환)
                 /// </summary>
        public async Task UDFFloodTestAsync(CancellationToken cancellationToken = default)
        {
            System.Diagnostics.Debug.WriteLine("[MockTrafficGenerator] Running UDP Flood Test...");

            for (int i = 0; i < 300 && !cancellationToken.IsCancellationRequested; i++)
            {
                // 취소 토큰 더 자주 체크
                if (cancellationToken.IsCancellationRequested)
                {
                    System.Diagnostics.Debug.WriteLine($"⏹️ UDP Flood 테스트 취소됨 (패킷 {i}개 생성)");
                    break;
                }

                var packet = new PacketDto
                {
                    SrcIp = "192.168.1.101",
                    DstIp = "10.0.0.2",
                    Protocol = ProtocolKind.UDP,
                    SrcPort = _random.Next(1024, 65535),
                    DstPort = 53, // DNS 포트
                    Length = 1024,
                    ProcessId = 5678,
                    ProcessName = "UDPFloodProcess",
                    Timestamp = DateTime.Now
                };
                _ddosSystem?.AddPacket(packet);

                if (i % 15 == 0)
                {
                    await Task.Delay(5, cancellationToken);
                }
            }
            System.Diagnostics.Debug.WriteLine("✅ UDP Flood Test 완료 - 300개 패킷");
        }

        /// <summary>
        /// 혼합 트래픽 테스트 (E2E 호환)
        /// </summary>
        public async Task MixedTrafficTestAsync(CancellationToken cancellationToken = default)
        {
            System.Diagnostics.Debug.WriteLine("[MockTrafficGenerator] Running Mixed Traffic Test...");

            for (int i = 0; i < 250 && !cancellationToken.IsCancellationRequested; i++)
            {
                // 취소 토큰 더 자주 체크
                if (cancellationToken.IsCancellationRequested)
                {
                    System.Diagnostics.Debug.WriteLine($"⏹️ Mixed Traffic 테스트 취소됨 (패킷 {i}개 생성)");
                    break;
                }

                var packet = new PacketDto
                {
                    SrcIp = i % 2 == 0 ? "192.168.1.102" : "192.168.1.103",
                    DstIp = "10.0.0.3",
                    Protocol = i % 2 == 0 ? ProtocolKind.TCP : ProtocolKind.UDP,
                    SrcPort = _random.Next(1024, 65535),
                    DstPort = i % 2 == 0 ? 443 : 123,
                    Length = i % 2 == 0 ? 256 : 512,
                    ProcessId = i % 2 == 0 ? 9999 : 8888,
                    ProcessName = i % 2 == 0 ? "TCPProcess" : "UDPProcess",
                    Timestamp = DateTime.Now
                };
                _ddosSystem?.AddPacket(packet);

                if (i % 12 == 0)
                {
                    await Task.Delay(5, cancellationToken);
                }
            }
            System.Diagnostics.Debug.WriteLine("✅ Mixed Traffic Test 완료 - 250개 패킷");
        }

        /// <summary>
        /// 혼합 트래픽 생성 (데모용)
        /// </summary>
        public async Task GenerateDemoTrafficAsync()
        {
            System.Diagnostics.Debug.WriteLine("🎭 데모용 혼합 트래픽 생성 시작");

            // 1단계: 정상 트래픽으로 시작
            System.Diagnostics.Debug.WriteLine("1️⃣ 정상 상태 시뮬레이션...");
            await GenerateNormalTrafficAsync();

            await Task.Delay(3000); // 3초 대기

            // 2단계: 점진적으로 의심스러운 활동 증가
            System.Diagnostics.Debug.WriteLine("2️⃣ 의심스러운 활동 감지...");
            await GenerateSuspiciousTrafficAsync();

            await Task.Delay(2000); // 2초 대기

            // 3단계: 명확한 공격 패턴
            System.Diagnostics.Debug.WriteLine("3️⃣ 공격 패턴 감지!");
            await QuickAttackTestAsync();

            System.Diagnostics.Debug.WriteLine("🎯 데모용 트래픽 생성 완료");
        }
    }
}