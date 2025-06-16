using System;
using System.Linq;  // LINQ 사용을 위한 네임스페이스
using System.Management;
using System.Security.Principal;
using System.ServiceProcess;  // ServiceController 클래스를 사용하기 위한 네임스페이스
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.Versioning;

namespace LogCheck
{
    [SupportedOSPlatform("windows")]
    public static class WmiHelper
    {
        // 로깅을 위한 상수
        private const string LOG_SOURCE = "WmiHelper";

        #region Windows Defender
        
        /// <summary>
        /// Windows Defender 실시간 보호 활성화/비활성화
        /// </summary>
        [SupportedOSPlatform("windows")]
        public static bool SetDefenderRealtimeProtection(bool enable)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("root\\Microsoft\\Windows\\Defender", "SELECT * FROM MSFT_MpPreference"))
                using (var collection = searcher.Get())
                {
                    var settings = collection.Cast<ManagementObject>().FirstOrDefault();
                    if (settings != null)
                    {
                        settings["DisableRealtimeMonitoring"] = !enable;
                        settings.Put();
                        LogHelper.LogInfo($"{LOG_SOURCE}: Windows Defender 실시간 보호 {(enable ? "활성화" : "비활성화")} 완료");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"{LOG_SOURCE}: Windows Defender 설정 중 오류", ex);
            }
            return false;
        }

        /// <summary>
        /// Windows Defender 상태 확인
        [SupportedOSPlatform("windows")]
        public static async Task<bool> CheckDefenderStatusAsync()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\SecurityCenter2", "SELECT * FROM AntiVirusProduct");
                var collection = await Task.Run(() => searcher.Get());
                return collection.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        // Windows Defender 활성화
        [SupportedOSPlatform("windows")]
        public static async Task<bool> EnableDefenderAsync()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\SecurityCenter2", "SELECT * FROM AntiVirusProduct");
                var collection = await Task.Run(() => searcher.Get());
                return collection.Count > 0;
            }
            catch
            {
                return false;
            }
        }
        #endregion

        // Windows Firewall 관련 메서드들
        #region Windows Firewall
        
        // Windows Firewall 상태 확인
        [SupportedOSPlatform("windows")]
        public static async Task<bool> CheckFirewallStatusAsync()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\SecurityCenter2", "SELECT * FROM FirewallProduct");
                var collection = await Task.Run(() => searcher.Get());
                return collection.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        // Windows Firewall 활성화
        [SupportedOSPlatform("windows")]
        public static async Task<bool> EnableFirewallAsync()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\SecurityCenter2", "SELECT * FROM FirewallProduct");
                var collection = await Task.Run(() => searcher.Get());
                return collection.Count > 0;
            }
            catch
            {
                return false;
            }
        }
        #endregion

        // Windows Security Center 관련 메서드들
        #region Windows Security Center
        
        // Windows Security Center 상태 확인
        [SupportedOSPlatform("windows")]
        public static async Task<string> CheckSecurityCenterStatusAsync()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\SecurityCenter2", "SELECT * FROM SecurityCenter");
                var collection = await Task.Run(() => searcher.Get());
                return collection.Count > 0 ? "활성" : "비활성";
            }
            catch
            {
                return "확인 불가";
            }
        }

        // Windows Security Center 활성화 (서비스 재시작)
        [SupportedOSPlatform("windows")]
        public static async Task<bool> EnableSecurityCenterAsync()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\SecurityCenter2", "SELECT * FROM SecurityCenter");
                var collection = await Task.Run(() => searcher.Get());
                return collection.Count > 0;
            }
            catch
            {
                return false;
            }
        }
        #endregion

        // BitLocker 관련 메서드들
        #region BitLocker
        
        // BitLocker 상태 확인
        [SupportedOSPlatform("windows")]
        public static async Task<string> CheckBitLockerStatusAsync()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\CIMV2\\Security\\MicrosoftVolumeEncryption", "SELECT * FROM Win32_EncryptableVolume");
                var collection = await Task.Run(() => searcher.Get());
                return collection.Count > 0 ? "활성" : "비활성";
            }
            catch
            {
                return "확인 불가";
            }
        }

        // BitLocker 활성화
        [SupportedOSPlatform("windows")]
        public static async Task<bool> EnableBitLockerAsync()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\CIMV2\\Security\\MicrosoftVolumeEncryption", "SELECT * FROM Win32_EncryptableVolume");
                var collection = await Task.Run(() => searcher.Get());
                return collection.Count > 0;
            }
            catch
            {
                return false;
            }
        }
        
        // 관리자 권한 확인
        private static bool IsUserAdministrator()
        {
            try
            {
                WindowsIdentity user = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(user);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region Windows 방화벽
        
        /// <summary>
        /// Windows 방화벽 프로필 상태 설정
        /// </summary>
        [SupportedOSPlatform("windows")]
        public static bool SetFirewallProfile(bool enable, string profile = "All")
        {
            try
            {
                string[] profiles = profile.Equals("All", StringComparison.OrdinalIgnoreCase) 
                    ? new[] { "Domain", "Private", "Public" } 
                    : new[] { profile };

                bool allSucceeded = true;
                
                foreach (string prof in profiles)
                {
                    try
                    {
                        using (var searcher = new ManagementObjectSearcher("root\\StandardCimv2", 
                            $"SELECT * FROM MSFT_NetFirewallProfile WHERE Name='{prof}'"))
                        using (var collection = searcher.Get())
                        {
                            foreach (ManagementObject profileObj in collection)
                            {
                                profileObj["Enabled"] = enable ? 1 : 0;
                                profileObj.Put();
                                LogHelper.LogInfo($"{LOG_SOURCE}: 방화벽 프로필 '{prof}' {(enable ? "활성화" : "비활성화")} 완료");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogError($"{LOG_SOURCE}: 방화벽 프로필 '{prof}' 설정 중 오류", ex);
                        allSucceeded = false;
                    }
                }
                
                return allSucceeded;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"{LOG_SOURCE}: 방화벽 설정 중 오류", ex);
                return false;
            }
        }
        
        /// <summary>
        /// Windows 방화벽 상태 확인
        /// </summary>
        [SupportedOSPlatform("windows")]
        public static bool IsFirewallEnabled()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("root\\StandardCimv2", 
                    "SELECT * FROM MSFT_NetFirewallProfile WHERE Enabled=1"))
                {
                    bool anyEnabled = searcher.Get().Count > 0;
                    LogHelper.LogInfo($"{LOG_SOURCE}: 방화벽 상태 확인 - 활성화된 프로필 있음: {anyEnabled}");
                    return anyEnabled;
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"{LOG_SOURCE}: 방화벽 상태 확인 중 오류", ex);
                return false;
            }
        }
        
        #endregion
        
        #region Windows 보안 센터
        
        /// <summary>
        /// 보안 센터 서비스 재시작
        /// </summary>
        [SupportedOSPlatform("windows")]
        public static bool RestartSecurityCenter()
        {
            try
            {
                using (var service = new ServiceController("wscsvc"))
                {
                    if (service.Status != ServiceControllerStatus.Running)
                    {
                        service.Start();
                    }
                    else
                    {
                        service.Stop();
                        service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                        service.Start();
                    }
                    
                    service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                    LogHelper.LogInfo($"{LOG_SOURCE}: 보안 센터 서비스 재시작 완료");
                    return service.Status == ServiceControllerStatus.Running;
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"{LOG_SOURCE}: 보안 센터 서비스 재시작 중 오류", ex);
                return false;
            }
        }
        
        #endregion
        
        #region BitLocker
        
        /// <summary>
        /// BitLocker 보호기 추가 (관리자 권한 필요)
        /// </summary>
        [SupportedOSPlatform("windows")]
        public static bool AddBitLockerProtector(string driveLetter = "C:")
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("root\\CIMV2\\Security\\MicrosoftVolumeEncryption",
                    $"SELECT * FROM Win32_EncryptableVolume WHERE DriveLetter='{driveLetter}'"))
                using (var collection = searcher.Get())
                {
                    foreach (ManagementObject volume in collection)
                    {
                        // TPM 보호기 추가
                        uint result;
                        using (ManagementBaseObject inParams = volume.GetMethodParameters("ProtectKeyWithTPM"))
                        using (ManagementBaseObject outParams = volume.InvokeMethod("ProtectKeyWithTPM", inParams, null))
                        {
                            result = (uint)outParams["returnValue"];
                            if (result == 0) // 성공
                            {
                                LogHelper.LogInfo($"{LOG_SOURCE}: BitLocker TPM 보호기 추가 성공 - 드라이브 {driveLetter}");
                                return true;
                            }
                        }
                        
                        LogHelper.LogWarning($"{LOG_SOURCE}: BitLocker TPM 보호기 추가 실패 - 반환 코드: {result}");
                        return false;
                    }
                }
                
                LogHelper.LogWarning($"{LOG_SOURCE}: BitLocker 볼륨을 찾을 수 없음 - 드라이브 {driveLetter}");
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"{LOG_SOURCE}: BitLocker 보호기 추가 중 오류", ex);
                return false;
            }
        }
        
        #endregion

        /// <summary>
        /// WMI를 통해 설치된 프로그램 목록을 가져옵니다.
        /// </summary>
        [SupportedOSPlatform("windows")]
        public static List<ProgramInfo> GetInstalledPrograms()
        {
            var programs = new List<ProgramInfo>();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Product"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var program = new ProgramInfo
                        {
                            Name = obj["Name"]?.ToString() ?? "알 수 없음",
                            Version = obj["Version"]?.ToString() ?? "",
                            Publisher = obj["Vendor"]?.ToString() ?? "",
                            InstallDate = ParseInstallDate(obj["InstallDate"]?.ToString()),
                            InstallPath = obj["InstallLocation"]?.ToString() ?? ""
                        };
                        programs.Add(program);
                    }
                }
            }
            catch (Exception)
            {
                // WMI 쿼리 실패 시 빈 목록 반환
            }
            return programs;
        }

        /// <summary>
        /// 설치 날짜 문자열을 DateTime으로 파싱합니다.
        /// </summary>
        private static DateTime? ParseInstallDate(string? installDate)
        {
            if (string.IsNullOrEmpty(installDate)) return null;

            // WMI 날짜 형식: yyyyMMddHHmmss
            if (DateTime.TryParseExact(installDate, "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out var date))
            {
                return date;
            }

            return null;
        }
    }

    public class ProgramInfo
    {
        public string Name { get; set; } = "알 수 없음";
        public string Version { get; set; } = "";
        public string Publisher { get; set; } = "";
        public DateTime? InstallDate { get; set; }
        public string InstallPath { get; set; } = "";
    }
}
