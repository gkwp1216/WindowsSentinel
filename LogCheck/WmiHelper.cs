using System;
using System.Linq;  // LINQ 사용을 위한 네임스페이스
using System.Management;
using System.Security.Principal;
using System.ServiceProcess;  // ServiceController 클래스를 사용하기 위한 네임스페이스
using System.Threading.Tasks;

namespace WindowsSentinel
{
    public static class WmiHelper
    {
        // 로깅을 위한 상수
        private const string LOG_SOURCE = "WmiHelper";

        #region Windows Defender
        
        /// <summary>
        /// Windows Defender 실시간 보호 활성화/비활성화
        /// </summary>
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
        public static async Task<bool> CheckDefenderStatusAsync()
        {
            return await Task.Run(() =>
            {
                LogHelper.LogInfo($"{LOG_SOURCE}: Windows Defender 상태 확인 시도");
                // 1. 레지스트리를 통한 기본적인 Defender 상태 확인 시도
                try
                {
                    using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Defender"))
                    {
                        if (key != null)
                        {
                            var disableAntiSpyware = key.GetValue("DisableAntiSpyware");
                            if (disableAntiSpyware != null && (int)disableAntiSpyware == 1)
                            {
                                LogHelper.LogInfo($"{LOG_SOURCE}: 레지스트리에서 Defender 비활성화 상태 확인");
                                return false; // 비활성화됨
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.LogError($"{LOG_SOURCE}: 레지스트리 확인 중 오류", ex);
                }

                // 2. WMI를 통한 정확한 상태 확인 시도
                var wmiQueries = new[]
                {
                    // Windows 10/11용 Defender 네임스페이스 (가장 신뢰성 높은 방법)
                    new { Namespace = "\\\\\\.\\root\\Microsoft\\Windows\\Defender", ClassName = "MSFT_MpComputerStatus" },
                    new { Namespace = "\\\\\\.\\root\\Microsoft\\Windows\\WindowsDefender", ClassName = "MSFT_MpComputerStatus" },
                    
                    // Windows 보안 센터 (구버전 Windows 호환성)
                    new { Namespace = "\\\\\\.\\root\\SecurityCenter", ClassName = "AntiVirusProduct" },
                    
                    // Windows 보안 센터 2 (문제가 있는 경우가 많아 마지막으로 시도)
                    new { Namespace = "\\\\\\.\\root\\SecurityCenter2", ClassName = "AntiVirusProduct" },
                    
                    // 대체 방법: Win32_ComputerSystem을 통한 기본 정보 확인
                    new { Namespace = "\\\\\\.\\root\\CIMV2", ClassName = "Win32_ComputerSystem" }
                };
                
                LogHelper.LogInfo($"{LOG_SOURCE}: WMI를 통한 Defender 상태 확인 시작");

                foreach (var query in wmiQueries)
                {
                    try
                    {
                        LogHelper.LogInfo($"{LOG_SOURCE}: WMI 쿼리 시도 - {query.Namespace}\\{query.ClassName}");
                        
                        var scope = new ManagementScope(query.Namespace);
                        scope.Connect(); // 명시적으로 연결 시도
                        var objectQuery = new ObjectQuery($"SELECT * FROM {query.ClassName}");
                        using (var searcher = new ManagementObjectSearcher(scope, objectQuery))
                        using (var collection = searcher.Get())
                        {
                            LogHelper.LogInfo($"{LOG_SOURCE}: WMI 쿼리 성공 - {collection.Count}개 항목 발견");
                            
                            foreach (ManagementObject item in collection)
                            {
                                // MSFT_MpComputerStatus인 경우
                                if (query.ClassName == "MSFT_MpComputerStatus")
                                {
                                    if (item["AntivirusEnabled"] != null)
                                    {
                                        bool status = (bool)item["AntivirusEnabled"];
                                        LogHelper.LogInfo($"{LOG_SOURCE}: MSFT_MpComputerStatus - AntivirusEnabled: {status}");
                                        return status;
                                    }
                                }
                                // AntiVirusProduct인 경우
                                else if (query.ClassName.Contains("AntiVirusProduct"))
                                {
                                    // productState 값에서 바이러스 백신 상태 추출
                                    if (item["productState"] != null)
                                    {
                                        uint productState = (uint)item["productState"];
                                        bool isEnabled = (productState & 0x1000) != 0x1000;
                                        LogHelper.LogInfo($"{LOG_SOURCE}: AntiVirusProduct - productState: 0x{productState:X8}, 활성화: {isEnabled}");
                                        return isEnabled;
                                    }
                                }
                            }
                        }
                    }
                    catch (ManagementException mex)
                    {
                        LogHelper.LogWarning($"{LOG_SOURCE}: WMI 쿼리 실패 - {query.Namespace}\\{query.ClassName}, 오류: {mex.Message}");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogError($"{LOG_SOURCE}: WMI 오류 - {query.Namespace}\\{query.ClassName}", ex);
                        continue;
                    }
                }

                LogHelper.LogWarning($"{LOG_SOURCE}: Windows Defender 상태를 확인할 수 없습니다. 지원되지 않는 시스템이거나 Defender가 설치되어 있지 않을 수 있습니다.");
                return false;
            });
        }

        // Windows Defender 활성화
        public static async Task<bool> EnableDefenderAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var scope = new ManagementScope("\\\\\\.\\root\\Microsoft\\Windows\\Defender");
                    var path = new ManagementPath("MSFT_MpPreference");
                    var options = new ObjectGetOptions();
                    
                    using (var managementClass = new ManagementClass(scope, path, options))
                    {
                        var inParams = managementClass.GetMethodParameters("SetDisableRealtimeMonitoring");
                        inParams["DisableRealtimeMonitoring"] = false;
                        
                        var outParams = managementClass.InvokeMethod("SetDisableRealtimeMonitoring", inParams, null);
                        return (uint)outParams["ReturnValue"] == 0; // 0은 성공을 의미
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Windows Defender 활성화 오류: {ex.Message}");
                    return false;
                }
            });
        }
        #endregion

        // Windows Firewall 관련 메서드들
        #region Windows Firewall
        
        // Windows Firewall 상태 확인
        public static async Task<bool> CheckFirewallStatusAsync()
        {
            return await Task.Run(() =>
            {
                string[] namespaces = new[]
                {
                    "\\\\\\.\\root\\StandardCimv2",
                    "\\\\\\.\\root\\SecurityCenter2",
                    "\\\\\\.\\root\\microsoft\\homeNet"
                };

                // 다양한 WMI 쿼리 시도
                var queries = new[]
                {
                    new { Namespace = "\\\\\\.\\root\\StandardCimv2", Query = "SELECT * FROM MSFT_NetFirewallProfile" },
                    new { Namespace = "\\\\\\.\\root\\StandardCimv2", Query = "SELECT * FROM MSFT_NetFirewallProfile WHERE Name = 'Domain' OR Name = 'Private' OR Name = 'Public'" },
                    new { Namespace = "\\\\\\.\\root\\SecurityCenter2", Query = "SELECT * FROM FirewallProduct" },
                    new { Namespace = "\\\\\\.\\root\\microsoft\\homeNet", Query = "SELECT * FROM HNet_ConnectionProperties" }
                };

                foreach (var q in queries)
                {
                    try
                    {
                        var scope = new ManagementScope(q.Namespace);
                        var query = new ObjectQuery(q.Query);
                        var searcher = new ManagementObjectSearcher(scope, query);
                        var collection = searcher.Get();
                        var items = collection.Cast<ManagementObject>();

                        if (items.Any())
                        {
                            // SecurityCenter2의 FirewallProduct인 경우
                            if (q.Namespace.Contains("SecurityCenter2"))
                            {
                                foreach (var item in items)
                                {
                                    if (item["enabled"] != null)
                                    {
                                        return (bool)item["enabled"];
                                    }
                                }
                            }
                            // StandardCimv2의 MSFT_NetFirewallProfile인 경우
                            else if (q.Query.Contains("MSFT_NetFirewallProfile"))
                            {
                                bool allEnabled = true;
                                foreach (var item in items)
                                {
                                    // EnabledState 또는 Enabled 속성 확인
                                    uint enabledState = 0;
                                    if (item["EnabledState"] != null)
                                    {
                                        enabledState = (uint)item["EnabledState"];
                                    }
                                    else if (item["Enabled"] != null)
                                    {
                                        enabledState = (uint)item["Enabled"];
                                    }

                                    // 0 = Disabled, 1 = Enabled, 2 = NotConfigured
                                    if (enabledState == 0)
                                    {
                                        allEnabled = false;
                                        break;
                                    }
                                }
                                return allEnabled;
                            }
                        }
                    }
                    catch (ManagementException mex)
                    {
                        Console.WriteLine($"WMI 쿼리 실패 - 네임스페이스: {q.Namespace}, 쿼리: {q.Query}, 오류: {mex.Message}");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"WMI 오류 - 네임스페이스: {q.Namespace}, 쿼리: {q.Query}, 오류: {ex.Message}");
                        continue;
                    }
                }

                Console.WriteLine("Windows 방화벽 상태를 확인할 수 없습니다. 지원되지 않는 시스템이거나 방화벽이 설치되어 있지 않을 수 있습니다.");
                return false;
            });
        }

        // Windows Firewall 활성화
        public static async Task<bool> EnableFirewallAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var scope = new ManagementScope("\\\\\\.\\root\\StandardCimv2");
                    var path = new ManagementPath("MSFT_NetFirewallProfile");
                    var options = new ObjectGetOptions();
                    
                    using (var managementClass = new ManagementClass(scope, path, options))
                    {
                        // 모든 프로필(Domain, Private, Public)에 대해 활성화
                        var inParams = managementClass.GetMethodParameters("EnableFirewall");
                        inParams["Profile"] = 0x7FFFFFFF; // 모든 프로필 (DOMAIN | PRIVATE | PUBLIC)
                        inParams["Enable"] = true;
                        
                        var outParams = managementClass.InvokeMethod("EnableFirewall", inParams, null);
                        return (uint)outParams["ReturnValue"] == 0; // 0은 성공을 의미
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Windows Firewall 활성화 오류: {ex.Message}");
                    return false;
                }
            });
        }
        #endregion

        // Windows Security Center 관련 메서드들
        #region Windows Security Center
        
        // Windows Security Center 상태 확인
        public static async Task<string> CheckSecurityCenterStatusAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var scope = new ManagementScope("\\\\\\.\\root\\SecurityCenter2");
                    var query = new ObjectQuery("SELECT * FROM AntiVirusProduct");
                    var searcher = new ManagementObjectSearcher(scope, query);
                    
                    var results = searcher.Get();
                    var items = results.Cast<ManagementObject>();
                    if (!items.Any())
                    {
                        return "설치되지 않음";
                    }
                    
                    // 첫 번째 안티바이러스 제품의 상태 확인
                    foreach (var item in items)
                    {
                        if (item["productState"] != null)
                        {
                            uint productState = (uint)item["productState"];
                            // productState 값에 따라 상태 판단 (하위 8비트가 0x10이면 활성)
                            if ((productState & 0x1000) == 0x1000)
                            {
                                return "비활성";
                            }
                            return "활성";
                        }
                    }
                    
                    return "상태 확인 불가";
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"WMI를 통한 Windows Security Center 상태 확인 오류: {ex.Message}");
                    throw;
                }
            });
        }

        // Windows Security Center 활성화 (서비스 재시작)
        public static async Task<bool> EnableSecurityCenterAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // 보안 센터 서비스 재시작
                    using (var service = new System.ServiceProcess.ServiceController("wscsvc"))
                    {
                        if (service.Status != System.ServiceProcess.ServiceControllerStatus.Running)
                        {
                            service.Start();
                            service.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                        }
                        else
                        {
                            service.Stop();
                            service.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                            service.Start();
                            service.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                        }
                        return service.Status == System.ServiceProcess.ServiceControllerStatus.Running;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Windows Security Center 활성화 오류: {ex.Message}");
                    return false;
                }
            });
        }
        #endregion

        // BitLocker 관련 메서드들
        #region BitLocker
        
        // BitLocker 상태 확인
        public static async Task<string> CheckBitLockerStatusAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var scope = new ManagementScope("\\\\localhost\\root\\cimv2\\Security\\MicrosoftVolumeEncryption");
                    var query = new ObjectQuery("SELECT * FROM Win32_EncryptableVolume WHERE DriveLetter = 'C:'");
                    var searcher = new ManagementObjectSearcher(scope, query);
                    
                    var results = searcher.Get();
                    if (results.Count == 0)
                    {
                        return "미지원 또는 설치되지 않음";
                    }
                    
                    foreach (ManagementObject item in results)
                    {
                        if (item["ProtectionStatus"] != null)
                        {
                            uint protectionStatus = (uint)item["ProtectionStatus"];
                            
                            // ProtectionStatus 값에 따라 상태 판단
                            switch (protectionStatus)
                            {
                                case 0: // UNPROTECTED
                                    return "비활성 (암호화되지 않음)";
                                case 1: // PROTECTED
                                    return "활성 (암호화 완료)";
                                case 2: // PROTECTION_OFF
                                    return "비활성 (보호 꺼짐)";
                                case 3: // PROTECTION_PAUSED
                                    return "일시 중지됨";
                                default:
                                    return "알 수 없는 상태";
                            }
                        }
                    }
                    
                    return "상태 확인 불가";
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"WMI를 통한 BitLocker 상태 확인 오류: {ex.Message}");
                    return "확인 중 오류 발생";
                }
            });
        }

        // BitLocker 활성화
        public static async Task<bool> EnableBitLockerAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // BitLocker 활성화를 위한 관리자 권한 확인
                    if (!IsUserAdministrator())
                    {
                        Console.WriteLine("BitLocker를 활성화하려면 관리자 권한이 필요합니다.");
                        return false;
                    }

                    // BitLocker 볼륨 정보 가져오기 (C: 드라이브)
                    var scope = new ManagementScope("\\\\localhost\\root\\cimv2\\Security\\MicrosoftVolumeEncryption");
                    var query = new ObjectQuery("SELECT * FROM Win32_EncryptableVolume WHERE DriveLetter = 'C:'");
                    var searcher = new ManagementObjectSearcher(scope, query);
                    
                    foreach (ManagementObject volume in searcher.Get())
                    {
                        // BitLocker 보호기 추가 (TPM + PIN)
                        string volumePath = volume["DeviceID"].ToString();
                        
                        // TPM 보호기 활성화
                        using (var tpmParams = volume.GetMethodParameters("ProtectKeyWithTPM"))
                        using (var pinParams = volume.GetMethodParameters("ProtectKeyWithTPMAndPIN"))
                        {
                            // TPM 보호기 추가
                            var tpmResult = volume.InvokeMethod("ProtectKeyWithTPM", tpmParams, null);
                            
                            // TPM + PIN 보호기 추가 (선택사항, 필요에 따라 주석 해제)
                            // pinParams["ProtectorSecret"] = "사용자_설정_PIN_코드"; // 실제로는 안전한 방식으로 PIN을 설정해야 함
                            // var pinResult = volume.InvokeMethod("ProtectKeyWithTPMAndPIN", pinParams, null);
                            
                            // 볼륨 암호화 시작
                            using (var encryptParams = volume.GetMethodParameters("Encrypt"))
                            {
                                encryptParams["EncryptionMethod"] = 1; // AES_128_WITH_DIFFUSER
                                encryptParams["EncryptUsedSpaceOnly"] = false;
                                encryptParams["EncryptionFlags"] = 1; // SYSTEM_VOLUME
                                
                                var encryptResult = volume.InvokeMethod("Encrypt", encryptParams, null);
                                return (uint)encryptResult["ReturnValue"] == 0; // 0은 성공을 의미
                            }
                        }
                    }
                    
                    return false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"BitLocker 활성화 오류: {ex.Message}");
                    return false;
                }
            });
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
    }
}
