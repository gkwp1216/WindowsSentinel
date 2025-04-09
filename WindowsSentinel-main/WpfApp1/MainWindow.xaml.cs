using System;
using System.IO;
using System.Management;
using System.Net;
using System.ServiceProcess;
using System.Windows;

namespace ServiceStatusLogger
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // 보안 관련 서비스 목록
            string[] serviceNames = {
                "WinDefend",    // Windows Defender Antivirus Service
                "MpsSvc",       // Windows Defender Firewall
                "wuauserv",     // Windows Update
                "PolicyAgent",  // IPsec Policy Agent
                "SecurityHealthService" // Windows Security Center
            };

            // 결과를 저장할 경로 (바탕화면의 결과.txt)
            string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "결과.txt");

            // 파일을 새로 생성하고 결과를 기록
            using (StreamWriter writer = new StreamWriter(filePath, false))
            {
                writer.WriteLine("서비스 이름\t상태\t상태 설정 날짜\t정지된 시간\tIP 종류");

                foreach (var serviceName in serviceNames)
                {
                    string status = GetServiceStatus(serviceName);
                    DateTime? statusChangeDate = GetServiceStatusChangeTime(serviceName);
                    string stopTime = (status == "Stopped" && statusChangeDate.HasValue) ? statusChangeDate.Value.ToString("yyyy-MM-dd HH:mm:ss") : "없음";

                    string ipType = CheckIpType(statusChangeDate);

                    string statusDate = statusChangeDate.HasValue ? statusChangeDate.Value.ToString("yyyy-MM-dd HH:mm:ss") : "알 수 없음";
                    writer.WriteLine($"{serviceName}\t{status}\t{statusDate}\t{stopTime}\t{ipType}");
                }
            }

            MessageBox.Show($"결과가 바탕화면의 '결과.txt'에 저장되었습니다.", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private string GetServiceStatus(string serviceName)
        {
            try
            {
                ServiceController sc = new ServiceController(serviceName);
                return sc.Status.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"서비스 '{serviceName}' 상태 확인 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return "알 수 없음";
            }
        }

        private DateTime? GetServiceStatusChangeTime(string serviceName)
        {
            try
            {
                string query = $"SELECT * FROM Win32_Service WHERE Name = '{serviceName}'";
                using ManagementObjectSearcher searcher = new(query);
                using ManagementObjectCollection services = searcher.Get();

                foreach (ManagementObject service in services)
                {
                    if (service["InstallDate"] != null)
                    {
                        return ManagementDateTimeConverter.ToDateTime(service["InstallDate"].ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"서비스 상태 확인 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return null;
        }

        private string CheckIpType(DateTime? statusChangeDate)
        {
            try
            {
                string externalIp = GetExternalIp();
                if (externalIp == "알 수 없음")
                    return "알 수 없음";
                    
                return IsPrivateIp(externalIp) ? "내부 IP" : "외부 IP";
            }
            catch
            {
                return "알 수 없음";
            }
        }

        private string GetExternalIp()
        {
            try
            {
                using WebClient client = new WebClient();
                return client.DownloadString("https://api.ipify.org").Trim();
            }
            catch
            {
                return "알 수 없음";
            }
        }

        private bool IsPrivateIp(string ip)
        {
            IPAddress ipAddr = IPAddress.Parse(ip);
            return IPAddress.IsLoopback(ipAddr) ||
                   ipAddr.ToString().StartsWith("10.") ||
                   ipAddr.ToString().StartsWith("192.168.") ||
                   ipAddr.ToString().StartsWith("172.");
        }
    }
}