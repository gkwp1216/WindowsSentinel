using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Security.Principal;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Threading;
using System.Windows.Navigation;
using System.Runtime.Versioning;
using System.Windows.Media;

namespace WindowsSentinel
{
    /// <summary>
    /// Recovery.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class Recovery : Page
    {
        private readonly ObservableCollection<RecoverySecurityStatusItem> securityStatusItems = new();
        private readonly DispatcherTimer loadingTextTimer = new();
        private int dotCount = 0;
        private const int maxDots = 3;
        private string baseText = "처리 중";

        // UI 요소 참조
        private TextBlock _defenderStatusText;
        private TextBlock _firewallStatusText;
        private TextBlock _securityCenterStatusText;
        private TextBlock _bitLockerStatusText;
        private ProgressBar _defenderProgressBar;
        private ProgressBar _firewallProgressBar;
        private ProgressBar _securityCenterProgressBar;
        private ProgressBar _bitLockerProgressBar;
        private Button _startRecoveryButton;

        // 복구 상태 필드
        private TimeSpan _defenderRecoveryTime;
        private TimeSpan _firewallRecoveryTime;
        private TimeSpan _securityCenterRecoveryTime;
        private TimeSpan _bitLockerRecoveryTime;
        private string _defenderRecoveryErrorMessage;
        private string _firewallRecoveryErrorMessage;
        private string _securityCenterRecoveryErrorMessage;
        private string _bitLockerRecoveryErrorMessage;

        public Recovery()
        {
            InitializeComponent();
            InitializeUI();
            SetupLoadingTextTimer();
        }

        private void InitializeUI()
        {
            // UI 요소 초기화
            _defenderStatusText = (TextBlock)FindName("DefenderStatusText");
            _firewallStatusText = (TextBlock)FindName("FirewallStatusText");
            _securityCenterStatusText = (TextBlock)FindName("SecurityCenterStatusText");
            _bitLockerStatusText = (TextBlock)FindName("BitLockerStatusText");
            _defenderProgressBar = (ProgressBar)FindName("DefenderProgressBar");
            _firewallProgressBar = (ProgressBar)FindName("FirewallProgressBar");
            _securityCenterProgressBar = (ProgressBar)FindName("SecurityCenterProgressBar");
            _bitLockerProgressBar = (ProgressBar)FindName("BitLockerProgressBar");
            _startRecoveryButton = (Button)FindName("StartRecoveryButton");

            // 초기 상태 설정
            UpdateStatusText(_defenderStatusText, "대기 중");
            UpdateStatusText(_firewallStatusText, "대기 중");
            UpdateStatusText(_securityCenterStatusText, "대기 중");
            UpdateStatusText(_bitLockerStatusText, "대기 중");

            // 프로그레스 바 초기화
            _defenderProgressBar.Value = 0;
            _firewallProgressBar.Value = 0;
            _securityCenterProgressBar.Value = 0;
            _bitLockerProgressBar.Value = 0;
        }

        private void SetupLoadingTextTimer()
        {
            loadingTextTimer.Interval = TimeSpan.FromMilliseconds(500);
            loadingTextTimer.Tick += LoadingTextTimer_Tick;
        }

        private void LoadingTextTimer_Tick(object sender, EventArgs e)
        {
            dotCount = (dotCount + 1) % (maxDots + 1);
            string dots = new string('.', dotCount);
            UpdateLoadingText($"{baseText}{dots}");
        }

        private void UpdateLoadingText(string text)
        {
            if (LoadingText != null)
            {
                LoadingText.Text = text;
            }
        }

        private void UpdateStatusText(TextBlock statusText, string status)
        {
            if (statusText != null)
            {
                statusText.Text = status;
            }
        }

        private void UpdateProgressBar(ProgressBar progressBar, double value)
        {
            if (progressBar != null)
            {
                progressBar.Value = value;
            }
        }

        [SupportedOSPlatform("windows")]
        private async void StartRecovery_Click(object sender, RoutedEventArgs e)
        {
            if (!IsAdministrator())
            {
                MessageBox.Show("이 작업을 수행하려면 관리자 권한이 필요합니다.", "권한 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (_startRecoveryButton != null)
            {
                _startRecoveryButton.IsEnabled = false;
            }
            loadingTextTimer.Start();

            try
            {
                await Task.WhenAll(
                    RecoverWindowsDefender(),
                    RecoverWindowsFirewall(),
                    RecoverSecurityCenter(),
                    RecoverBitLocker()
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show($"복구 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                loadingTextTimer.Stop();
                if (_startRecoveryButton != null)
                {
                    _startRecoveryButton.IsEnabled = true;
                }
                UpdateLoadingText("복구 완료");
            }
        }

        private bool IsAdministrator()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private async Task RecoverWindowsDefender()
        {
            var startTime = DateTime.Now;
            try
            {
                UpdateStatusText(_defenderStatusText, "실행 중");
                UpdateProgressBar(_defenderProgressBar, 0);

                // Windows Defender 복구 로직
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-Command \"Set-MpPreference -DisableRealtimeMonitoring $false; Start-MpScan -ScanType QuickScan\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                        Verb = "runas"
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    UpdateStatusText(_defenderStatusText, "완료");
                    UpdateProgressBar(_defenderProgressBar, 100);
                }
                else
                {
                    throw new Exception($"Windows Defender 복구 실패 (종료 코드: {process.ExitCode})");
                }
            }
            catch (Exception ex)
            {
                _defenderRecoveryErrorMessage = ex.Message;
                UpdateStatusText(_defenderStatusText, "실패");
                UpdateProgressBar(_defenderProgressBar, 0);
            }
            finally
            {
                _defenderRecoveryTime = DateTime.Now - startTime;
            }
        }

        private async Task RecoverWindowsFirewall()
        {
            var startTime = DateTime.Now;
            try
            {
                UpdateStatusText(_firewallStatusText, "실행 중");
                UpdateProgressBar(_firewallProgressBar, 0);

                // Windows Firewall 복구 로직
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "netsh.exe",
                        Arguments = "advfirewall set allprofiles state on",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                        Verb = "runas"
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    UpdateStatusText(_firewallStatusText, "완료");
                    UpdateProgressBar(_firewallProgressBar, 100);
                }
                else
                {
                    throw new Exception($"Windows Firewall 복구 실패 (종료 코드: {process.ExitCode})");
                }
            }
            catch (Exception ex)
            {
                _firewallRecoveryErrorMessage = ex.Message;
                UpdateStatusText(_firewallStatusText, "실패");
                UpdateProgressBar(_firewallProgressBar, 0);
            }
            finally
            {
                _firewallRecoveryTime = DateTime.Now - startTime;
            }
        }

        private async Task RecoverSecurityCenter()
        {
            var startTime = DateTime.Now;
            try
            {
                UpdateStatusText(_securityCenterStatusText, "실행 중");
                UpdateProgressBar(_securityCenterProgressBar, 0);

                // Security Center 복구 로직
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-Command \"Restart-Service -Name SecurityHealthService -Force\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                        Verb = "runas"
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    UpdateStatusText(_securityCenterStatusText, "완료");
                    UpdateProgressBar(_securityCenterProgressBar, 100);
                }
                else
                {
                    throw new Exception($"Security Center 복구 실패 (종료 코드: {process.ExitCode})");
                }
            }
            catch (Exception ex)
            {
                _securityCenterRecoveryErrorMessage = ex.Message;
                UpdateStatusText(_securityCenterStatusText, "실패");
                UpdateProgressBar(_securityCenterProgressBar, 0);
            }
            finally
            {
                _securityCenterRecoveryTime = DateTime.Now - startTime;
            }
        }

        private async Task RecoverBitLocker()
        {
            var startTime = DateTime.Now;
            try
            {
                UpdateStatusText(_bitLockerStatusText, "실행 중");
                UpdateProgressBar(_bitLockerProgressBar, 0);

                // BitLocker 복구 로직
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-Command \"Resume-BitLocker -MountPoint C:\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                        Verb = "runas"
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    UpdateStatusText(_bitLockerStatusText, "완료");
                    UpdateProgressBar(_bitLockerProgressBar, 100);
                }
                else
                {
                    throw new Exception($"BitLocker 복구 실패 (종료 코드: {process.ExitCode})");
                }
            }
            catch (Exception ex)
            {
                _bitLockerRecoveryErrorMessage = ex.Message;
                UpdateStatusText(_bitLockerStatusText, "실패");
                UpdateProgressBar(_bitLockerProgressBar, 0);
            }
            finally
            {
                _bitLockerRecoveryTime = DateTime.Now - startTime;
            }
        }

        private void GenerateReport_Click(object sender, RoutedEventArgs e)
        {
            var report = new StringBuilder();
            report.AppendLine("=== Windows 보안 복구 보고서 ===");
            report.AppendLine($"생성 시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine();

            report.AppendLine("=== 상세 복구 결과 ===");
            report.AppendLine();

            // Windows Defender 결과 상세
            report.AppendLine($"• Windows Defender: {_defenderStatusText.Text}");
            report.AppendLine($"  소요 시간: {_defenderRecoveryTime.TotalSeconds:F1}초");
            if (_defenderStatusText.Text == "완료")
            {
                report.AppendLine("  - 실시간 보호 활성화");
                report.AppendLine("  - 빠른 검사 완료");
            }
            else if (!string.IsNullOrEmpty(_defenderRecoveryErrorMessage))
            {
                report.AppendLine($"  오류 상세: {_defenderRecoveryErrorMessage}");
            }
            report.AppendLine();

            // Windows Firewall 결과 상세
            report.AppendLine($"• Windows Firewall: {_firewallStatusText.Text}");
            report.AppendLine($"  소요 시간: {_firewallRecoveryTime.TotalSeconds:F1}초");
            if (_firewallStatusText.Text == "완료")
                report.AppendLine("  - 모든 프로필 방화벽 활성화");
            else if (!string.IsNullOrEmpty(_firewallRecoveryErrorMessage))
            {
                report.AppendLine($"  오류 상세: {_firewallRecoveryErrorMessage}");
            }
            report.AppendLine();

            // Windows Security Center 결과 상세
            report.AppendLine($"• Windows Security Center: {_securityCenterStatusText.Text}");
            report.AppendLine($"  소요 시간: {_securityCenterRecoveryTime.TotalSeconds:F1}초");
            if (_securityCenterStatusText.Text == "완료")
                report.AppendLine("  - 보안 센터 서비스 재시작 완료");
            else if (!string.IsNullOrEmpty(_securityCenterRecoveryErrorMessage))
            {
                report.AppendLine($"  오류 상세: {_securityCenterRecoveryErrorMessage}");
            }
            report.AppendLine();

            // BitLocker 결과 상세
            report.AppendLine($"• BitLocker: {_bitLockerStatusText.Text}");
            report.AppendLine($"  소요 시간: {_bitLockerRecoveryTime.TotalSeconds:F1}초");
            if (_bitLockerStatusText.Text == "완료")
                report.AppendLine("  - BitLocker 서비스 재개 완료");
            else if (!string.IsNullOrEmpty(_bitLockerRecoveryErrorMessage))
            {
                report.AppendLine($"  오류 상세: {_bitLockerRecoveryErrorMessage}");
            }

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "텍스트 파일 (*.txt)|*.txt",
                FileName = $"Windows 보안 복구 보고서_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                File.WriteAllText(saveFileDialog.FileName, report.ToString());
                MessageBox.Show("보고서가 성공적으로 저장되었습니다.", "저장 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SidebarHome_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new HomePage());
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
            // 이미 Recovery 페이지에 있으므로 아무것도 하지 않습니다.
            // NavigateToPage(new Recovery()); 
        }

        [SupportedOSPlatform("windows")]
        private async void StartDiagnosticWizard_Click(object sender, RoutedEventArgs e)
        {
            if (!IsAdministrator())
            {
                MessageBox.Show("이 작업을 수행하려면 관리자 권한이 필요합니다.", "권한 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // PowerShell 출력 영역 표시
                if (PowerShellOutputBorder != null)
                {
                    PowerShellOutputBorder.Visibility = Visibility.Visible;
                }

                // 현재 작업 상태 업데이트
                if (CurrentOperation != null)
                {
                    CurrentOperation.Text = "보안 진단 시작...";
                }

                // Windows Defender 진단
                await RunDiagnosticCommand("Windows Defender", "Get-MpComputerStatus | Select-Object AntivirusEnabled, RealTimeProtectionEnabled, AntispywareEnabled, AntivirusSignatureLastUpdated");
                
                // Windows Firewall 진단
                await RunDiagnosticCommand("Windows Firewall", "Get-NetFirewallProfile | Select-Object Name, Enabled");
                
                // Security Center 진단
                await RunDiagnosticCommand("Security Center", "Get-Service SecurityHealthService | Select-Object Name, Status, StartType");
                
                // BitLocker 진단
                await RunDiagnosticCommand("BitLocker", "Get-BitLockerVolume | Select-Object MountPoint, VolumeStatus, ProtectionStatus");

                // 상태 업데이트
                if (StatusText != null)
                {
                    StatusText.Text = "진단 완료";
                }
                if (TimestampText != null)
                {
                    TimestampText.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"진단 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [SupportedOSPlatform("windows")]
        private async void OptimizeSecuritySettings_Click(object sender, RoutedEventArgs e)
        {
            if (!IsAdministrator())
            {
                MessageBox.Show("이 작업을 수행하려면 관리자 권한이 필요합니다.", "권한 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // PowerShell 출력 영역 표시
                if (PowerShellOutputBorder != null)
                {
                    PowerShellOutputBorder.Visibility = Visibility.Visible;
                }

                // 현재 작업 상태 업데이트
                if (CurrentOperation != null)
                {
                    CurrentOperation.Text = "보안 설정 최적화 시작...";
                }

                // Windows Defender 설정 최적화
                await RunOptimizationCommand("Windows Defender", 
                    "Set-MpPreference -DisableRealtimeMonitoring $false; " +
                    "Set-MpPreference -DisableIOAVProtection $false; " +
                    "Set-MpPreference -DisableBehaviorMonitoring $false");

                // Windows Firewall 설정 최적화
                await RunOptimizationCommand("Windows Firewall",
                    "Set-NetFirewallProfile -Profile Domain,Public,Private -Enabled True");

                // Security Center 서비스 최적화
                await RunOptimizationCommand("Security Center",
                    "Set-Service -Name SecurityHealthService -StartupType Automatic; " +
                    "Start-Service -Name SecurityHealthService");

                // 상태 업데이트
                if (StatusText != null)
                {
                    StatusText.Text = "최적화 완료";
                }
                if (TimestampText != null)
                {
                    TimestampText.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                }

                MessageBox.Show("보안 설정이 성공적으로 최적화되었습니다.", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"최적화 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task RunDiagnosticCommand(string component, string command)
        {
            try
            {
                if (CurrentOperation != null)
                {
                    CurrentOperation.Text = $"{component} 진단 중...";
                }

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                        Arguments = $"-Command \"{command}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                        Verb = "runas"
                    }
                };

                var output = new StringBuilder();
                process.OutputDataReceived += (s, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        output.AppendLine(e.Data);
                        UpdateOutput($"{component} 진단 결과:", e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"{component} 진단 실패 (종료 코드: {process.ExitCode})");
                }
            }
            catch (Exception ex)
            {
                UpdateOutput($"{component} 진단 오류:", ex.Message);
                throw;
            }
        }

        private async Task RunOptimizationCommand(string component, string command)
        {
            try
            {
                if (CurrentOperation != null)
                {
                    CurrentOperation.Text = $"{component} 최적화 중...";
                }

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-Command \"{command}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        Verb = "runas"
                    }
                };

                var output = new StringBuilder();
                process.OutputDataReceived += (s, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        output.AppendLine(e.Data);
                        UpdateOutput($"{component} 최적화 결과:", e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"{component} 최적화 실패 (종료 코드: {process.ExitCode})");
                }
            }
            catch (Exception ex)
            {
                UpdateOutput($"{component} 최적화 오류:", ex.Message);
                throw;
            }
        }

        private void UpdateOutput(string header, string message)
        {
            Dispatcher.Invoke(() =>
            {
                if (UserFriendlyOutput != null)
                {
                    var textBlock = new TextBlock
                    {
                        Text = $"{header}\n{message}",
                        Foreground = Brushes.White,
                        Margin = new Thickness(0, 0, 0, 10),
                        TextWrapping = TextWrapping.Wrap
                    };
                    UserFriendlyOutput.Children.Add(textBlock);
                }

                if (PowerShellOutput != null)
                {
                    PowerShellOutput.Text += $"{header}\n{message}\n";
                }

                if (UserFriendlyOutputScroll != null)
                {
                    UserFriendlyOutputScroll.ScrollToBottom();
                }

                if (PowerShellOutputScroll != null)
                {
                    PowerShellOutputScroll.ScrollToBottom();
                }
            });
        }

        private void NavigateToPage(Page page)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.NavigateToPage(page);
        }
    }

    public class RecoverySecurityStatusItem
    {
        public required string Icon { get; set; }
        public required string Title { get; set; }
        public required string Description { get; set; }
        public required string Status { get; set; }
    }

    public enum RecoveryMessageType
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class RecoveryProgressInfo
    {
        public string Operation { get; set; }
        public string Status { get; set; }
        public double Progress { get; set; }
    }
} 