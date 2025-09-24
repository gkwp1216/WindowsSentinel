using System.Management;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.ServiceProcess;  // ServiceController 클래스를 사용하기 위한 네임스페이스

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

        /// <summary>
        /// Windows Security Center 상태 확인 (개선된 버전)
        /// </summary>
        [SupportedOSPlatform("windows")]
        public static async Task<string> CheckSecurityCenterStatusAsync()
        {
            try
            {
                // 1단계: 서비스 상태 확인 (가장 확실한 방법)
                var serviceStatus = await CheckSecurityCenterService();
                if (serviceStatus != "확인 불가")
                {
                    return serviceStatus;
                }

                // 2단계: WMI를 통한 Security Center 상태 확인
                var wmiStatus = await CheckSecurityCenterViaWMI();
                if (wmiStatus != "확인 불가")
                {
                    return wmiStatus;
                }

                // 3단계: 레지스트리 기반 확인 (최후 수단)
                return CheckSecurityCenterViaRegistry();
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"{LOG_SOURCE}: Security Center 상태 확인 중 오류", ex);
                return "확인 불가";
            }
        }

        /// <summary>
        /// Windows Security Center 서비스 상태 확인
        /// </summary>
        [SupportedOSPlatform("windows")]
        private static async Task<string> CheckSecurityCenterService()
        {
            try
            {
                return await Task.Run(() =>
                {
                    using (var service = new ServiceController("wscsvc"))
                    {
                        service.Refresh();
                        bool isRunning = service.Status == ServiceControllerStatus.Running;

                        LogHelper.LogInfo($"{LOG_SOURCE}: Security Center 서비스 상태 - {service.Status}");

                        return isRunning ? "활성" : "비활성";
                    }
                });
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"{LOG_SOURCE}: Security Center 서비스 상태 확인 중 오류", ex);
                return "확인 불가";
            }
        }

        /// <summary>
        /// WMI를 통한 Security Center 상태 확인
        /// </summary>
        [SupportedOSPlatform("windows")]
        private static async Task<string> CheckSecurityCenterViaWMI()
        {
            try
            {
                // Windows Defender Security Center 상태 확인
                using var searcher = new ManagementObjectSearcher("root\\SecurityCenter2",
                    "SELECT * FROM AntiVirusProduct WHERE displayName LIKE '%Defender%' OR displayName LIKE '%Windows%'");
                var collection = await Task.Run(() => searcher.Get());

                foreach (ManagementObject obj in collection)
                {
                    var productState = obj["productState"];
                    if (productState != null)
                    {
                        // productState 값을 분석하여 활성화 상태 확인
                        uint state = Convert.ToUInt32(productState);
                        bool isActive = (state & 0x1000) == 0x1000; // 활성화 비트 확인

                        LogHelper.LogInfo($"{LOG_SOURCE}: Security Center WMI 상태 - {(isActive ? "활성" : "비활성")}");
                        return isActive ? "활성" : "비활성";
                    }
                }

                LogHelper.LogInfo($"{LOG_SOURCE}: Security Center WMI - 제품 정보 없음");
                return "확인 불가";
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"{LOG_SOURCE}: Security Center WMI 확인 중 오류", ex);
                return "확인 불가";
            }
        }

        /// <summary>
        /// 레지스트리를 통한 Security Center 상태 확인
        /// </summary>
        [SupportedOSPlatform("windows")]
        private static string CheckSecurityCenterViaRegistry()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Security Center"))
                {
                    if (key != null)
                    {
                        var antiVirusDisableNotify = key.GetValue("AntiVirusDisableNotify");
                        var firewallDisableNotify = key.GetValue("FirewallDisableNotify");

                        // 알림이 비활성화되지 않았다면 Security Center가 활성화된 상태
                        bool isActive = (antiVirusDisableNotify == null || (int)antiVirusDisableNotify == 0) &&
                                       (firewallDisableNotify == null || (int)firewallDisableNotify == 0);

                        LogHelper.LogInfo($"{LOG_SOURCE}: Security Center 레지스트리 상태 - {(isActive ? "활성" : "비활성")}");
                        return isActive ? "활성" : "비활성";
                    }
                }

                LogHelper.LogWarning($"{LOG_SOURCE}: Security Center 레지스트리 키를 찾을 수 없음");
                return "확인 불가";
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"{LOG_SOURCE}: Security Center 레지스트리 확인 중 오류", ex);
                return "확인 불가";
            }
        }

        /// <summary>
        /// Windows Security Center 활성화 (개선된 버전)
        /// </summary>
        [SupportedOSPlatform("windows")]
        public static async Task<bool> EnableSecurityCenterAsync()
        {
            try
            {
                // 1단계: 서비스 재시작을 통한 활성화
                bool serviceResult = await Task.Run(() => RestartSecurityCenter());
                if (serviceResult)
                {
                    LogHelper.LogInfo($"{LOG_SOURCE}: Security Center 서비스 재시작 성공");

                    // 재시작 후 잠시 대기
                    await Task.Delay(2000);

                    // 상태 재확인
                    var status = await CheckSecurityCenterStatusAsync();
                    return status == "활성";
                }

                // 2단계: 레지스트리를 통한 설정 복구
                bool registryResult = await Task.Run(() => EnableSecurityCenterViaRegistry());
                if (registryResult)
                {
                    LogHelper.LogInfo($"{LOG_SOURCE}: Security Center 레지스트리 설정 복구 성공");
                    return true;
                }

                LogHelper.LogWarning($"{LOG_SOURCE}: Security Center 활성화 실패");
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"{LOG_SOURCE}: Security Center 활성화 중 오류", ex);
                return false;
            }
        }

        /// <summary>
        /// 레지스트리를 통한 Security Center 활성화
        /// </summary>
        [SupportedOSPlatform("windows")]
        private static bool EnableSecurityCenterViaRegistry()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Security Center", true))
                {
                    if (key != null)
                    {
                        // 알림 활성화 (0 = 활성화, 1 = 비활성화)
                        key.SetValue("AntiVirusDisableNotify", 0, Microsoft.Win32.RegistryValueKind.DWord);
                        key.SetValue("FirewallDisableNotify", 0, Microsoft.Win32.RegistryValueKind.DWord);

                        LogHelper.LogInfo($"{LOG_SOURCE}: Security Center 레지스트리 설정 복구 완료");
                        return true;
                    }
                }

                LogHelper.LogWarning($"{LOG_SOURCE}: Security Center 레지스트리 키에 액세스할 수 없음");
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"{LOG_SOURCE}: Security Center 레지스트리 설정 중 오류", ex);
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
        /// Windows 방화벽 상태 확인 (개선된 버전)
        /// </summary>
        [SupportedOSPlatform("windows")]
        public static bool IsFirewallEnabled()
        {
            try
            {
                // COM 객체를 사용한 방화벽 상태 확인
                Type? comType = Type.GetTypeFromProgID("HNetCfg.FwMgr");
                if (comType == null)
                {
                    LogHelper.LogWarning($"{LOG_SOURCE}: Windows Firewall COM 객체를 찾을 수 없습니다.");
                    return CheckFirewallViaRegistry();
                }

                dynamic? firewall = Activator.CreateInstance(comType);
                if (firewall?.LocalPolicy?.CurrentProfile?.FirewallEnabled == true)
                {
                    LogHelper.LogInfo($"{LOG_SOURCE}: 방화벽 상태 확인 - 활성화됨 (COM)");
                    return true;
                }

                // 백업으로 WMI 방식 사용
                return CheckFirewallViaWMI();
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"{LOG_SOURCE}: 방화벽 상태 확인 중 오류 (COM)", ex);
                return CheckFirewallViaRegistry();
            }
        }

        /// <summary>
        /// WMI를 통한 방화벽 상태 확인 (백업 방법)
        /// </summary>
        [SupportedOSPlatform("windows")]
        private static bool CheckFirewallViaWMI()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("root\\StandardCimv2",
                    "SELECT Enabled FROM MSFT_NetFirewallProfile"))
                {
                    foreach (ManagementObject profile in searcher.Get())
                    {
                        if (profile["Enabled"] != null && (bool)profile["Enabled"])
                        {
                            LogHelper.LogInfo($"{LOG_SOURCE}: 방화벽 상태 확인 - 활성화된 프로필 발견 (WMI)");
                            return true;
                        }
                    }
                }

                LogHelper.LogInfo($"{LOG_SOURCE}: 방화벽 상태 확인 - 활성화된 프로필 없음 (WMI)");
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"{LOG_SOURCE}: WMI 방화벽 상태 확인 중 오류", ex);
                return false;
            }
        }

        /// <summary>
        /// 레지스트리를 통한 방화벽 상태 확인 (최후 수단)
        /// </summary>
        [SupportedOSPlatform("windows")]
        private static bool CheckFirewallViaRegistry()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile"))
                {
                    if (key?.GetValue("EnableFirewall") is int enabledValue && enabledValue == 1)
                    {
                        LogHelper.LogInfo($"{LOG_SOURCE}: 방화벽 상태 확인 - 활성화됨 (레지스트리)");
                        return true;
                    }
                }

                LogHelper.LogInfo($"{LOG_SOURCE}: 방화벽 상태 확인 - 비활성화됨 (레지스트리)");
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"{LOG_SOURCE}: 레지스트리 방화벽 상태 확인 중 오류", ex);
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
