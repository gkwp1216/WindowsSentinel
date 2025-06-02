using System;
using System.Management;
using System.Security.Principal;
using System.Threading.Tasks;

namespace WindowsSentinel
{
    public static class WmiHelper
    {
        // Windows Defender 관련 메서드들
        #region Windows Defender
        
        // Windows Defender 상태 확인
        public static async Task<bool> CheckDefenderStatusAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var scope = new ManagementScope("\\\\\\.\\\\root\\\\Microsoft\\\\Windows\\\\Defender");
                    var query = new ObjectQuery("SELECT * FROM MSFT_MpPreference");
                    var searcher = new ManagementObjectSearcher(scope, query);
                    
                    foreach (ManagementObject item in searcher.Get())
                    {
                        if (item["DisableRealtimeMonitoring"] != null)
                        {
                            return !(bool)item["DisableRealtimeMonitoring"];
                        }
                    }
                    
                    return false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"WMI를 통한 Windows Defender 상태 확인 오류: {ex.Message}");
                    throw;
                }
            });
        }

        // Windows Defender 활성화
        public static async Task<bool> EnableDefenderAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var scope = new ManagementScope("\\\\\\.\\\\root\\\\Microsoft\\\\Windows\\\\Defender");
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
        // Windows Defender 상태 확인
        public static async Task<bool> CheckDefenderStatusAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var scope = new ManagementScope("\\\\.\\root\\Microsoft\\Windows\\Defender");
                    var query = new ObjectQuery("SELECT * FROM MSFT_MpPreference");
                    var searcher = new ManagementObjectSearcher(scope, query);
                    
                    foreach (ManagementObject item in searcher.Get())
                    {
                        if (item["DisableRealtimeMonitoring"] != null)
                        {
                            return !(bool)item["DisableRealtimeMonitoring"];
                        }
                    }
                    
                    return false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"WMI를 통한 Windows Defender 상태 확인 오류: {ex.Message}");
                    throw;
                }
            });
        }

        // Windows Defender 활성화
        public static async Task<bool> EnableDefenderAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var scope = new ManagementScope("\\\\.\\root\\Microsoft\\Windows\\Defender");
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

        // Windows Firewall 상태 확인
        public static async Task<bool> CheckFirewallStatusAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var scope = new ManagementScope("\\\\.\\root\\StandardCimv2");
                    var query = new ObjectQuery("SELECT * FROM MSFT_NetFirewallProfile");
                    var searcher = new ManagementObjectSearcher(scope, query);
                    
                    foreach (ManagementObject item in searcher.Get())
                    {
                        // 모든 프로필(Domain, Private, Public)이 활성화되어 있는지 확인
                        if (item["Enabled"] != null && (uint)item["Enabled"] == 0) // 0 = Disabled
                        {
                            return false;
                        }
                    }
                    
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"WMI를 통한 Windows Firewall 상태 확인 오류: {ex.Message}");
                    throw;
                }
            });
        }

        // Windows Firewall 활성화
        public static async Task<bool> EnableFirewallAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var scope = new ManagementScope("\\\\.\\root\\StandardCimv2");
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
                    var scope = new ManagementScope("\\\\.\\root\\SecurityCenter2");
                    var query = new ObjectQuery("SELECT * FROM AntiVirusProduct");
                    var searcher = new ManagementObjectSearcher(scope, query);
                    
                    var results = searcher.Get();
                    if (results.Count == 0)
                    {
                        return "설치되지 않음";
                    }
                    
                    // 첫 번째 안티바이러스 제품의 상태 확인
                    foreach (ManagementObject item in results)
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
    }
}
