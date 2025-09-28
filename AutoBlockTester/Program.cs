using LogCheck.Services;

namespace AutoBlockTester
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("AutoBlock 시스템 테스트 프로그램");
            Console.WriteLine("================================");
            Console.WriteLine();

            try
            {
                // 1. 기본 기능 테스트
                Console.WriteLine("1. 기본 차단 규칙 테스트 실행 중...");
                await AutoBlockTestHelper.RunAllTestsAsync();
                Console.WriteLine();

                // 2. 성능 테스트 (선택사항)
                Console.Write("성능 테스트를 실행하시겠습니까? (y/N): ");
                var input = Console.ReadLine();
                if (input?.ToLower() == "y" || input?.ToLower() == "yes")
                {
                    Console.Write("테스트할 연결 수를 입력하세요 (기본값: 1000): ");
                    var countInput = Console.ReadLine();
                    int connectionCount = 1000;
                    if (int.TryParse(countInput, out var parsedCount))
                    {
                        connectionCount = parsedCount;
                    }

                    await AutoBlockTestHelper.RunPerformanceTestAsync(connectionCount);
                }

                Console.WriteLine();
                Console.WriteLine("모든 테스트가 완료되었습니다!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"테스트 실행 중 오류 발생: {ex.Message}");
                Console.WriteLine($"상세 정보: {ex}");
            }

            Console.WriteLine("아무 키나 눌러서 종료하세요...");
            Console.ReadKey();
        }
    }
}