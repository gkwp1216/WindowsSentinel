using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Runtime.Versioning;

namespace WindowsSentinel
{
    /// <summary>
    /// HomePage.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class HomePage : Page
    {
        public HomePage()
        {
            InitializeComponent();
            Loaded += HomePage_Loaded;
        }

        private async void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            await CheckSecurityStatus();
        }

        [SupportedOSPlatform("windows")]
        private async Task CheckSecurityStatus()
        {
            try
            {
                // Windows Defender 상태 확인
                await CheckDefenderStatus();
                
                // Windows Firewall 상태 확인
                await CheckFirewallStatus();
                
                // Security Center 상태 확인
                await CheckSecurityCenterStatus();
                
                // BitLocker 상태 확인
                await CheckBitLockerStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"보안 상태 확인 중 오류가 발생했습니다: {ex.Message}", 
                              "오류", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
            }
        }

        [SupportedOSPlatform("windows")]
        private async Task CheckDefenderStatus()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-Command \"Get-MpComputerStatus | Select-Object AntivirusEnabled, RealTimeProtectionEnabled\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    bool isEnabled = output.Contains("True");
                    UpdateStatus(DefenderStatus, isEnabled ? "정상" : "비활성화", isEnabled);
                }
                else
                {
                    UpdateStatus(DefenderStatus, "확인 실패", false);
                }
            }
            catch
            {
                UpdateStatus(DefenderStatus, "확인 실패", false);
            }
        }

        [SupportedOSPlatform("windows")]
        private async Task CheckFirewallStatus()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-Command \"Get-NetFirewallProfile | Select-Object Name, Enabled\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    bool isEnabled = !output.Contains("False");
                    UpdateStatus(FirewallStatus, isEnabled ? "정상" : "비활성화", isEnabled);
                }
                else
                {
                    UpdateStatus(FirewallStatus, "확인 실패", false);
                }
            }
            catch
            {
                UpdateStatus(FirewallStatus, "확인 실패", false);
            }
        }

        [SupportedOSPlatform("windows")]
        private async Task CheckSecurityCenterStatus()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-Command \"Get-Service SecurityHealthService | Select-Object Status\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    bool isRunning = output.Contains("Running");
                    UpdateStatus(SecurityCenterStatus, isRunning ? "정상" : "중지됨", isRunning);
                }
                else
                {
                    UpdateStatus(SecurityCenterStatus, "확인 실패", false);
                }
            }
            catch
            {
                UpdateStatus(SecurityCenterStatus, "확인 실패", false);
            }
        }

        [SupportedOSPlatform("windows")]
        private async Task CheckBitLockerStatus()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-Command \"Get-BitLockerVolume | Select-Object VolumeStatus\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    bool isEnabled = output.Contains("FullyEncrypted");
                    UpdateStatus(BitLockerStatus, isEnabled ? "정상" : "비활성화", isEnabled);
                }
                else
                {
                    UpdateStatus(BitLockerStatus, "확인 실패", false);
                }
            }
            catch
            {
                UpdateStatus(BitLockerStatus, "확인 실패", false);
            }
        }

        private void UpdateStatus(TextBlock statusText, string status, bool isNormal)
        {
            if (statusText != null)
            {
                statusText.Text = status;
                statusText.Foreground = new SolidColorBrush(isNormal ? 
                    Color.FromRgb(78, 201, 176) : // #4EC9B0
                    Color.FromRgb(255, 87, 87));  // #FF5757
            }
        }

        private void NavigateToPrograms(object sender, MouseButtonEventArgs e)
        {
            NavigateToPage(new InstalledPrograms());
        }

        private void NavigateToNetwork(object sender, MouseButtonEventArgs e)
        {
            NavigateToPage(new Network());
        }

        private void NavigateToLog(object sender, MouseButtonEventArgs e)
        {
            NavigateToPage(new Log());
        }

        private void SidebarHome_Click(object sender, RoutedEventArgs e)
        {
            // 이미 HomePage에 있으므로 아무것도 하지 않습니다.
        }

        private void SidebarPrograms_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new InstalledPrograms());
        }

        private void SidebarModification_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Network());
        }

        private void SidebarLog_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Log());
        }

        private void SidebarRecovery_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Recovery());
        }

        private void NavigateToPage(Page page)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.NavigateToPage(page);
        }
    }
} 