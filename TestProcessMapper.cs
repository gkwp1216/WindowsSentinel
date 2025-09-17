using System;
using System.Threading.Tasks;
using LogCheck.Services;

namespace LogCheck.Test
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("ProcessNetworkMapper 테스트 시작...");

            var mapper = new ProcessNetworkMapper();

            Console.WriteLine("GetProcessNetworkDataAsync 호출 중...");
            var data = await mapper.GetProcessNetworkDataAsync();

            Console.WriteLine($"결과: {data?.Count ?? 0}개의 프로세스 정보를 가져왔습니다.");

            if (data != null && data.Count > 0)
            {
                Console.WriteLine("첫 5개 프로세스 정보:");
                for (int i = 0; i < Math.Min(5, data.Count); i++)
                {
                    var item = data[i];
                    Console.WriteLine($"  {i + 1}. {item.ProcessName} (PID: {item.ProcessId})");
                    Console.WriteLine($"     로컬 주소: {item.LocalAddress}");
                    Console.WriteLine($"     원격 주소: {item.RemoteAddress}");
                    Console.WriteLine($"     프로토콜: {item.Protocol}");
                    Console.WriteLine();
                }
            }

            // temp 폴더의 디버그 파일 확인
            var tempPath = System.IO.Path.GetTempPath();
            var debugFiles = System.IO.Directory.GetFiles(tempPath, "ProcessNetworkMapper_*");

            if (debugFiles.Length > 0)
            {
                Console.WriteLine($"\n디버그 파일 생성됨: {debugFiles.Length}개");
                foreach (var file in debugFiles)
                {
                    Console.WriteLine($"  - {file}");
                    if (System.IO.File.Exists(file))
                    {
                        var content = await System.IO.File.ReadAllTextAsync(file);
                        Console.WriteLine($"    내용 (처음 500자): {content.Substring(0, Math.Min(500, content.Length))}");
                    }
                }
            }
            else
            {
                Console.WriteLine("\n디버그 파일이 생성되지 않았습니다.");
            }

            Console.WriteLine("\n테스트 완료. 엔터를 누르면 종료합니다.");
            Console.ReadLine();
        }
    }
}