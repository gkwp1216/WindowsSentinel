using LogCheck.Models;
using LogCheck.Services;

namespace LogCheck.Services
{
    /// <summary>
    /// 자동 차단 시스템 테스트를 위한 유틸리티 클래스
    /// 개발 및 디버깅 목적으로 사용
    /// </summary>
    public static class AutoBlockTestHelper
    {
        /// <summary>
        /// 테스트용 ProcessNetworkInfo 생성
        /// </summary>
        public static ProcessNetworkInfo CreateTestProcess(
            string processName = "test.exe",
            string processPath = @"C:\temp\test.exe",
            int processId = 1234,
            string remoteAddress = "192.168.1.100",
            int remotePort = 1337,
            int localPort = 12345,
            string protocol = "TCP",
            long dataTransferred = 0)
        {
            return new ProcessNetworkInfo
            {
                ProcessName = processName,
                ProcessPath = processPath,
                ProcessId = processId,
                RemoteAddress = remoteAddress,
                RemotePort = remotePort,
                LocalPort = localPort,
                Protocol = protocol,
                DataTransferred = dataTransferred,
                ConnectionStartTime = DateTime.Now.AddMinutes(-5)
            };
        }

        /// <summary>
        /// System Idle Process 위장 테스트 케이스들
        /// </summary>
        public static ProcessNetworkInfo[] GetSystemIdleProcessForgeryTests()
        {
            return new ProcessNetworkInfo[]
            {
                // 1. PID가 0이 아닌 경우
                CreateTestProcess("System Idle Process", @"C:\Windows\System32\svchost.exe", 1234, "8.8.8.8", 443),
                
                // 2. .exe 확장자가 있는 경우  
                CreateTestProcess("System Idle Process.exe", @"C:\temp\System Idle Process.exe", 0, "1.1.1.1", 80),
                
                // 3. 네트워크 연결이 있는 경우
                CreateTestProcess("System Idle Process", "", 0, "127.0.0.1", 9999),
                
                // 4. 이름 변형 (l 대신 I)
                CreateTestProcess("System ldle Process", @"C:\malware\fake.exe", 5678, "192.168.1.1", 4444),
                
                // 5. 공백 변형
                CreateTestProcess("System  Idle Process", @"C:\temp\suspicious.exe", 9999, "10.0.0.1", 1337)
            };
        }

        /// <summary>
        /// 악성 IP 테스트 케이스들
        /// </summary>
        public static ProcessNetworkInfo[] GetMaliciousIPTests()
        {
            return new ProcessNetworkInfo[]
            {
                CreateTestProcess("chrome.exe", @"C:\Program Files\Google\Chrome\Application\chrome.exe", 1111, "192.168.1.100", 443),
                CreateTestProcess("notepad.exe", @"C:\Windows\notepad.exe", 2222, "10.0.0.50", 80),
                CreateTestProcess("calc.exe", @"C:\Windows\System32\calc.exe", 3333, "127.0.0.2", 8080)
            };
        }

        /// <summary>
        /// 의심스러운 포트 테스트 케이스들
        /// </summary>
        public static ProcessNetworkInfo[] GetSuspiciousPortTests()
        {
            return new ProcessNetworkInfo[]
            {
                CreateTestProcess("suspicious.exe", @"C:\temp\suspicious.exe", 4444, "8.8.8.8", 1337),
                CreateTestProcess("malware.exe", @"C:\temp\malware.exe", 5555, "1.1.1.1", 31337),
                CreateTestProcess("trojan.exe", @"C:\users\test\trojan.exe", 6666, "208.67.222.222", 12345),
                CreateTestProcess("backdoor.exe", @"C:\programdata\backdoor.exe", 7777, "9.9.9.9", 54321)
            };
        }

        /// <summary>
        /// 정상 연결 테스트 케이스들
        /// </summary>
        public static ProcessNetworkInfo[] GetLegitimateTests()
        {
            return new ProcessNetworkInfo[]
            {
                // 정상적인 System Idle Process (PID 0, 경로 없음)
                CreateTestProcess("System Idle Process", "", 0, "", 0, 0, "TCP", 0),

                CreateTestProcess("chrome.exe", @"C:\Program Files\Google\Chrome\Application\chrome.exe", 1001, "142.250.185.142", 443), // Google
                CreateTestProcess("outlook.exe", @"C:\Program Files\Microsoft Office\root\Office16\OUTLOOK.EXE", 1002, "52.97.148.46", 993), // Microsoft
                CreateTestProcess("steam.exe", @"C:\Program Files (x86)\Steam\steam.exe", 1003, "103.28.54.10", 80), // Steam
                CreateTestProcess("discord.exe", @"C:\Users\user\AppData\Local\Discord\app-1.0.9013\Discord.exe", 1004, "162.159.133.233", 443) // Discord
            };
        }

        /// <summary>
        /// 정상적인 System Idle Process 테스트 케이스
        /// </summary>
        public static ProcessNetworkInfo[] GetLegitimateSystemIdleProcessTests()
        {
            return new ProcessNetworkInfo[]
            {
                // 1. 완전히 정상적인 System Idle Process (PID 0, 경로 없음, 연결 없음)
                CreateTestProcess("System Idle Process", "", 0, "", 0, 0, "", 0),
                
                // 2. 정상적인 System Idle Process (PID 0, 시스템 경로)
                CreateTestProcess("System Idle Process", @"", 0, "", 0, 0, "", 0)
            };
        }

        /// <summary>
        /// 대용량 데이터 전송 테스트 케이스들
        /// </summary>
        public static ProcessNetworkInfo[] GetLargeDataTransferTests()
        {
            const long oneGB = 1024 * 1024 * 1024;
            const long hundredMB = 100 * 1024 * 1024;

            return new ProcessNetworkInfo[]
            {
                CreateTestProcess("backup.exe", @"C:\temp\backup.exe", 8001, "192.168.1.200", 21, 12345, "TCP", oneGB),
                CreateTestProcess("uploader.exe", @"C:\suspicious\uploader.exe", 8002, "185.220.101.1", 9001, 54321, "TCP", hundredMB * 2),
                CreateTestProcess("sync.exe", @"C:\malware\sync.exe", 8003, "1.1.1.1", 443, 443, "TCP", hundredMB * 5)
            };
        }

        /// <summary>
        /// 모든 테스트 케이스 실행 및 결과 출력
        /// </summary>
        public static async Task RunAllTestsAsync()
        {
            var connectionString = "Data Source=autoblock_test.db";
            var autoBlockService = new AutoBlockService(connectionString);

            await autoBlockService.InitializeAsync();

            Console.WriteLine("=== AutoBlock 테스트 시작 ===\n");

            // System Idle Process 위장 테스트
            Console.WriteLine("1. System Idle Process 위장 탐지 테스트");
            Console.WriteLine("=".PadRight(50, '='));
            foreach (var testCase in GetSystemIdleProcessForgeryTests())
            {
                var result = await autoBlockService.AnalyzeConnectionAsync(testCase);
                Console.WriteLine($"프로세스: {testCase.ProcessName} (PID: {testCase.ProcessId})");
                Console.WriteLine($"결과: {result.Level} - {result.Reason}");
                Console.WriteLine($"신뢰도: {result.ConfidenceScore:P1}");
                Console.WriteLine();
            }

            // 악성 IP 테스트
            Console.WriteLine("2. 악성 IP 탐지 테스트");
            Console.WriteLine("=".PadRight(50, '='));
            foreach (var testCase in GetMaliciousIPTests())
            {
                var result = await autoBlockService.AnalyzeConnectionAsync(testCase);
                Console.WriteLine($"연결: {testCase.ProcessName} -> {testCase.RemoteAddress}:{testCase.RemotePort}");
                Console.WriteLine($"결과: {result.Level} - {result.Reason}");
                Console.WriteLine($"신뢰도: {result.ConfidenceScore:P1}");
                Console.WriteLine();
            }

            // 의심스러운 포트 테스트
            Console.WriteLine("3. 의심스러운 포트 탐지 테스트");
            Console.WriteLine("=".PadRight(50, '='));
            foreach (var testCase in GetSuspiciousPortTests())
            {
                var result = await autoBlockService.AnalyzeConnectionAsync(testCase);
                Console.WriteLine($"연결: {testCase.ProcessName} -> {testCase.RemoteAddress}:{testCase.RemotePort}");
                Console.WriteLine($"결과: {result.Level} - {result.Reason}");
                Console.WriteLine($"신뢰도: {result.ConfidenceScore:P1}");
                Console.WriteLine();
            }

            // 정상 연결 테스트
            Console.WriteLine("4. 정상 연결 테스트 (허용되어야 함)");
            Console.WriteLine("=".PadRight(50, '='));
            foreach (var testCase in GetLegitimateTests())
            {
                var result = await autoBlockService.AnalyzeConnectionAsync(testCase);
                Console.WriteLine($"연결: {testCase.ProcessName} -> {testCase.RemoteAddress}:{testCase.RemotePort}");
                Console.WriteLine($"결과: {result.Level} - {result.Reason}");
                Console.WriteLine($"신뢰도: {result.ConfidenceScore:P1}");
                Console.WriteLine();
            }

            // 정상적인 System Idle Process 테스트
            Console.WriteLine("5. 정상적인 System Idle Process 테스트 (허용되어야 함)");
            Console.WriteLine("=".PadRight(50, '='));
            foreach (var testCase in GetLegitimateSystemIdleProcessTests())
            {
                var result = await autoBlockService.AnalyzeConnectionAsync(testCase);
                Console.WriteLine($"프로세스: {testCase.ProcessName} (PID: {testCase.ProcessId})");
                Console.WriteLine($"결과: {result.Level} - {result.Reason}");
                Console.WriteLine($"신뢰도: {result.ConfidenceScore:P1}");
                Console.WriteLine();
            }

            // 화이트리스트 테스트
            Console.WriteLine("6. 화이트리스트 테스트");
            Console.WriteLine("=".PadRight(50, '='));
            var whitelistTest = GetMaliciousIPTests()[0]; // 악성 IP 테스트 케이스 재사용

            // 화이트리스트 추가 전 테스트
            var resultBefore = await autoBlockService.AnalyzeConnectionAsync(whitelistTest);
            Console.WriteLine($"화이트리스트 추가 전: {resultBefore.Level} - {resultBefore.Reason}");

            // 화이트리스트에 추가
            await autoBlockService.AddToWhitelistAsync(whitelistTest.ProcessPath!, "테스트용 화이트리스트");

            // 화이트리스트 추가 후 테스트
            var resultAfter = await autoBlockService.AnalyzeConnectionAsync(whitelistTest);
            Console.WriteLine($"화이트리스트 추가 후: {resultAfter.Level} - {resultAfter.Reason}");
            Console.WriteLine();

            await autoBlockService.CleanupAsync();

            Console.WriteLine("=== 테스트 완료 ===");
        }

        /// <summary>
        /// 성능 테스트 (대량 연결 분석)
        /// </summary>
        public static async Task RunPerformanceTestAsync(int connectionCount = 1000)
        {
            var connectionString = "Data Source=autoblock_perf_test.db";
            var autoBlockService = new AutoBlockService(connectionString);
            await autoBlockService.InitializeAsync();

            Console.WriteLine($"=== 성능 테스트 시작 (연결 수: {connectionCount}) ===");

            var random = new Random();
            var testCases = new List<ProcessNetworkInfo>();

            // 테스트 케이스 생성
            for (int i = 0; i < connectionCount; i++)
            {
                var processName = $"test_process_{i}.exe";
                var remoteAddress = $"192.168.{random.Next(1, 255)}.{random.Next(1, 255)}";
                var remotePort = random.Next(1, 65536);

                testCases.Add(CreateTestProcess(processName, $@"C:\test\{processName}",
                    1000 + i, remoteAddress, remotePort));
            }

            // 성능 측정
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var tasks = testCases.Select(async testCase =>
            {
                return await autoBlockService.AnalyzeConnectionAsync(testCase);
            });

            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            // 결과 분석
            var blockCounts = results.GroupBy(r => r.Level).ToDictionary(g => g.Key, g => g.Count());

            Console.WriteLine($"처리 시간: {stopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"초당 처리량: {connectionCount * 1000.0 / stopwatch.ElapsedMilliseconds:F2} connections/sec");
            Console.WriteLine($"평균 처리 시간: {stopwatch.ElapsedMilliseconds / (double)connectionCount:F2} ms/connection");
            Console.WriteLine();
            Console.WriteLine("차단 레벨별 분포:");
            foreach (var kvp in blockCounts)
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value}건 ({kvp.Value * 100.0 / connectionCount:F1}%)");
            }

            await autoBlockService.CleanupAsync();
            Console.WriteLine("=== 성능 테스트 완료 ===");
        }
    }
}