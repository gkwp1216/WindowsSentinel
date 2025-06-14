using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Runtime.Versioning;
using System.Management;
using System.Security.Principal;
using MessageBox = System.Windows.MessageBox;

namespace WindowsSentinel
{
    /// <summary>
    /// HomePage.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class HomePage : Page
    {
        private bool _isDefenderEnabled;
        private bool _isFirewallEnabled;
        private bool _isBitLockerEnabled;

        public HomePage()
        {
            InitializeComponent();
            LoadSecurityStatus();
        }

        private async void LoadSecurityStatus()
        {
            await Task.Run(() =>
            {
                CheckWindowsDefenderStatus();
                CheckWindowsFirewallStatus();
                CheckBitLockerStatus();
            });
        }

        private void CheckWindowsDefenderStatus()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("root\\SecurityCenter2", "SELECT * FROM AntiVirusProduct"))
                {
                    foreach (var item in searcher.Get())
                    {
                        var displayName = item["displayName"]?.ToString();
                        if (displayName?.Contains("Windows Defender") == true)
                        {
                            _isDefenderEnabled = true;
                            Dispatcher.Invoke(() =>
                            {
                                DefenderStatusText.Text = "정상 작동 중";
                                DefenderStatusText.Foreground = new SolidColorBrush(Colors.Green);
                                DefenderActionButton.Content = "상태 확인";
                            });
                            return;
                        }
                    }
                }

                _isDefenderEnabled = false;
                Dispatcher.Invoke(() =>
                {
                    DefenderStatusText.Text = "비활성화됨";
                    DefenderStatusText.Foreground = new SolidColorBrush(Colors.Red);
                    DefenderActionButton.Content = "활성화";
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    DefenderStatusText.Text = $"오류: {ex.Message}";
                    DefenderStatusText.Foreground = new SolidColorBrush(Colors.Red);
                    DefenderActionButton.Content = "재시도";
                });
            }
        }

        private void CheckWindowsFirewallStatus()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("root\\SecurityCenter2", "SELECT * FROM FirewallProduct"))
                {
                    foreach (var item in searcher.Get())
                    {
                        var displayName = item["displayName"]?.ToString();
                        if (displayName?.Contains("Windows Firewall") == true)
                        {
                            _isFirewallEnabled = true;
                            Dispatcher.Invoke(() =>
                            {
                                FirewallStatusText.Text = "정상 작동 중";
                                FirewallStatusText.Foreground = new SolidColorBrush(Colors.Green);
                                FirewallActionButton.Content = "상태 확인";
                            });
                            return;
                        }
                    }
                }

                _isFirewallEnabled = false;
                Dispatcher.Invoke(() =>
                {
                    FirewallStatusText.Text = "비활성화됨";
                    FirewallStatusText.Foreground = new SolidColorBrush(Colors.Red);
                    FirewallActionButton.Content = "활성화";
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    FirewallStatusText.Text = $"오류: {ex.Message}";
                    FirewallStatusText.Foreground = new SolidColorBrush(Colors.Red);
                    FirewallActionButton.Content = "재시도";
                });
            }
        }

        private void CheckBitLockerStatus()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("root\\CIMV2\\Security\\MicrosoftVolumeEncryption", "SELECT * FROM Win32_EncryptableVolume"))
                {
                    foreach (var item in searcher.Get())
                    {
                        var protectionStatus = Convert.ToInt32(item["ProtectionStatus"]);
                        if (protectionStatus == 1)
                        {
                            _isBitLockerEnabled = true;
                            Dispatcher.Invoke(() =>
                            {
                                BitLockerStatusText.Text = "활성화됨";
                                BitLockerStatusText.Foreground = new SolidColorBrush(Colors.Green);
                                BitLockerActionButton.Content = "상태 확인";
                            });
                            return;
                        }
                    }
                }

                _isBitLockerEnabled = false;
                Dispatcher.Invoke(() =>
                {
                    BitLockerStatusText.Text = "비활성화됨";
                    BitLockerStatusText.Foreground = new SolidColorBrush(Colors.Red);
                    BitLockerActionButton.Content = "활성화";
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    BitLockerStatusText.Text = $"오류: {ex.Message}";
                    BitLockerStatusText.Foreground = new SolidColorBrush(Colors.Red);
                    BitLockerActionButton.Content = "재시도";
                });
            }
        }

        private void DefenderAction_Click(object sender, RoutedEventArgs e)
        {
            if (!_isDefenderEnabled)
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "Set-MpPreference -DisableRealtimeMonitoring $false",
                        Verb = "runas",
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                    MessageBox.Show("Windows Defender가 활성화되었습니다. 상태를 다시 확인해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Windows Defender 활성화 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            LoadSecurityStatus();
        }

        private void FirewallAction_Click(object sender, RoutedEventArgs e)
        {
            if (!_isFirewallEnabled)
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = "advfirewall set allprofiles state on",
                        Verb = "runas",
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                    MessageBox.Show("Windows 방화벽이 활성화되었습니다. 상태를 다시 확인해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Windows 방화벽 활성화 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            LoadSecurityStatus();
        }

        private void BitLockerAction_Click(object sender, RoutedEventArgs e)
        {
            if (!_isBitLockerEnabled)
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "manage-bde.exe",
                        Arguments = "-on C:",
                        Verb = "runas",
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                    MessageBox.Show("BitLocker가 활성화되었습니다. 상태를 다시 확인해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"BitLocker 활성화 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            LoadSecurityStatus();
        }

        private void SidebarHome_Click(object sender, RoutedEventArgs e)
        {
            // 이미 홈 페이지에 있으므로 아무 작업도 하지 않음
        }

        private void SidebarPrograms_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new Uri("Page1.xaml", UriKind.Relative));
        }

        private void SidebarModification_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new Uri("Page1.xaml", UriKind.Relative));
        }

        private void SidebarLog_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new Uri("Log.xaml", UriKind.Relative));
        }

        private void SidebarRecovery_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new Uri("Recovery.xaml", UriKind.Relative));
        }
    }
} 