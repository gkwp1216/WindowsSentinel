using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using LogCheck.Services;

namespace AutoBlockTestApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== AutoBlock 기능 테스트 ===");
            Console.WriteLine("1. 의심스러운 포트 연결 테스트");
            Console.WriteLine("2. 지속적인 HTTP 요청 테스트");
            Console.WriteLine("3. Raw Socket 연결 테스트");
            Console.WriteLine("4. AbuseIPDB 의심스러운 IP 테스트");
            Console.Write("선택 (1-4): ");

            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    await TestSuspiciousPorts();
                    break;
                case "2":
                    await TestHttpRequests();
                    break;
                case "3":
                    await TestRawSockets();
                    break;
                case "4":
                    await TestAbuseIPDBConnections();
                    break;
                default:
                    Console.WriteLine("잘못된 선택입니다.");
                    break;
            }

            Console.WriteLine("테스트 완료. 아무 키나 누르세요...");
            Console.ReadKey();
        }

        static async Task TestSuspiciousPorts()
        {
            var suspiciousPorts = new[] { 1337, 31337, 12345, 6666, 13337 };
            var targets = new[] { "8.8.8.8", "1.1.1.1", "google.com" };

            foreach (var target in targets)
            {
                foreach (var port in suspiciousPorts)
                {
                    try
                    {
                        Console.WriteLine($"연결 시도: {target}:{port}");

                        using var client = new TcpClient();
                        var connectTask = client.ConnectAsync(target, port);

                        // 5초 타임아웃으로 충분한 패킷 캡처 시간 확보
                        if (await Task.WhenAny(connectTask, Task.Delay(5000)) == connectTask)
                        {
                            Console.WriteLine($"연결 성공: {target}:{port}");

                            // 실제 데이터 송신으로 트래픽 생성
                            var stream = client.GetStream();
                            var data = Encoding.UTF8.GetBytes("GET / HTTP/1.1\r\nHost: test\r\n\r\n");
                            await stream.WriteAsync(data, 0, data.Length);

                            await Task.Delay(2000); // 2초 대기
                        }
                        else
                        {
                            Console.WriteLine($"연결 타임아웃: {target}:{port}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"연결 실패: {target}:{port} - {ex.Message}");
                    }

                    await Task.Delay(1000); // 각 연결 시도 사이에 1초 대기
                }
            }
        }

        static async Task TestHttpRequests()
        {
            var urls = new[]
            {
                "http://httpbin.org/delay/3", // 3초 지연으로 충분한 캡처 시간 확보
                "http://httpbin.org/status/200",
                "http://httpbin.org/get",
                "https://www.google.com",
                "https://www.github.com"
            };

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            foreach (var url in urls)
            {
                try
                {
                    Console.WriteLine($"HTTP 요청: {url}");
                    var response = await client.GetAsync(url);
                    Console.WriteLine($"응답 코드: {response.StatusCode}");

                    // 응답 내용도 읽어서 실제 데이터 전송 발생
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"응답 크기: {content.Length} bytes");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"HTTP 요청 실패: {url} - {ex.Message}");
                }

                await Task.Delay(2000); // 2초 대기
            }
        }

        static async Task TestRawSockets()
        {
            var targets = new[]
            {
                ("8.8.8.8", 53),    // DNS
                ("1.1.1.1", 53),    // Cloudflare DNS
                ("google.com", 80),  // HTTP
                ("github.com", 443)  // HTTPS
            };

            foreach (var (host, port) in targets)
            {
                try
                {
                    Console.WriteLine($"Raw Socket 연결: {host}:{port}");

                    using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    socket.ReceiveTimeout = 5000;
                    socket.SendTimeout = 5000;

                    await socket.ConnectAsync(host, port);
                    Console.WriteLine($"연결 성공: {host}:{port}");

                    // 실제 데이터 송신
                    var data = Encoding.UTF8.GetBytes("TEST DATA\r\n");
                    await socket.SendAsync(data, SocketFlags.None);

                    // 응답 수신 시도
                    var buffer = new byte[1024];
                    var received = await socket.ReceiveAsync(buffer, SocketFlags.None);
                    Console.WriteLine($"수신된 데이터: {received} bytes");

                    await Task.Delay(2000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Socket 연결 실패: {host}:{port} - {ex.Message}");
                }

                await Task.Delay(1000);
            }
        }

        static async Task TestAbuseIPDBConnections()
        {
            Console.WriteLine("=== AbuseIPDB 의심스러운 IP 연결 테스트 ===");

            // API 키가 없는 경우 알려진 악성 IP 사용
            var abuseService = new AbuseIPTestService("");
            var suspiciousIPs = await abuseService.GetSuspiciousIPsAsync(5);

            Console.WriteLine($"테스트할 의심스러운 IP 개수: {suspiciousIPs.Count}");

            // ⭐ 중요: BlockRuleEngine에 AbuseIPDB IP들을 악성 목록에 추가
            BlockRuleEngine.AddMaliciousIPs(suspiciousIPs);
            Console.WriteLine($"🛡️ {suspiciousIPs.Count}개 악성 IP가 차단 목록에 추가되었습니다."); var testPorts = new[] { 80, 443, 8080, 22, 25 }; // 일반적인 포트들

            foreach (var ip in suspiciousIPs)
            {
                Console.WriteLine($"\n--- {ip} 연결 테스트 시작 ---");

                // IP 정보 확인
                var ipInfo = await abuseService.CheckIPAsync(ip);
                Console.WriteLine($"위험도: {ipInfo.AbuseConfidencePercentage}%, 국가: {ipInfo.CountryCode}");

                foreach (var port in testPorts)
                {
                    await TestSingleIPConnection(ip, port);
                }

                Console.WriteLine("다음 IP 테스트까지 3초 대기...");
                await Task.Delay(3000);
            }

            abuseService.Dispose();
        }

        static async Task TestSingleIPConnection(string ip, int port)
        {
            try
            {
                Console.WriteLine($"연결 시도: {ip}:{port}");

                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(ip, port);

                // 10초 타임아웃으로 충분한 패킷 캡처 시간 확보
                if (await Task.WhenAny(connectTask, Task.Delay(10000)) == connectTask)
                {
                    if (client.Connected)
                    {
                        Console.WriteLine($"✅ 연결 성공: {ip}:{port}");

                        // 실제 데이터 송신으로 AutoBlock 트리거
                        try
                        {
                            var stream = client.GetStream();
                            var data = Encoding.UTF8.GetBytes("GET / HTTP/1.1\r\nHost: test\r\nUser-Agent: AutoBlockTest/1.0\r\n\r\n");
                            await stream.WriteAsync(data, 0, data.Length);

                            // 응답 읽기 시도
                            var buffer = new byte[1024];
                            stream.ReadTimeout = 5000;
                            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                            if (bytesRead > 0)
                            {
                                Console.WriteLine($"📦 응답 수신: {bytesRead} bytes");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠️ 데이터 송수신 오류: {ex.Message}");
                        }

                        // 연결 유지로 더 많은 패킷 생성
                        await Task.Delay(3000);
                    }
                }
                else
                {
                    Console.WriteLine($"⏱️ 연결 타임아웃: {ip}:{port}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 연결 실패: {ip}:{port} - {ex.Message}");
            }

            await Task.Delay(1000); // 각 연결 시도 사이에 1초 대기
        }
    }
}