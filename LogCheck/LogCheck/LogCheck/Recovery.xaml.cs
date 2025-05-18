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

namespace WindowsSentinel
{
    public partial class Recovery : Page
    {
        private readonly ObservableCollection<SecurityStatusItem> securityStatusItems;
        private readonly DispatcherTimer loadingTextTimer;
        private int dotCount = 0;
        private const int maxDots = 3;
        private string baseText = "처리 중";

        public Recovery()
        {
            InitializeComponent();

            // 관리자 권한 확인
            if (!IsRunningAsAdmin())
            {
                MessageBox.Show("이 프로그램은 관리자 권한으로 실행해야 합니다.",
                              "권한 필요",
                              MessageBoxButton.OK,
                              MessageBoxImage.Warning);
                Application.Current.Shutdown();
                return;
            }

            // 초기화
            securityStatusItems = new ObservableCollection<SecurityStatusItem>();
            SecurityStatusItems.ItemsSource = securityStatusItems;

            // 로딩 텍스트 타이머 설정
            loadingTextTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            loadingTextTimer.Tick += LoadingTextTimer_Tick;

            // 초기 보안 상태 로드
            _ = LoadSecurityStatus();
        }

        private void LoadingTextTimer_Tick(object sender, EventArgs e)
        {
            dotCount = (dotCount + 1) % (maxDots + 1);
            LoadingText.Text = baseText + new string('.', dotCount);
        }

        private void ShowLoadingOverlay(string message)
        {
            baseText = message;
            LoadingText.Text = baseText;
            LoadingOverlay.Visibility = Visibility.Visible;
            loadingTextTimer.Start();
        }

        private void HideLoadingOverlay()
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            loadingTextTimer.Stop();
        }

        // 관리자 권한 확인 메서드
        private bool IsRunningAsAdmin()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        // 보안 상태 로드 메서드
        private async Task LoadSecurityStatus()
        {
            try
            {
                securityStatusItems.Clear(); // 기존 항목 초기화
                ShowLoadingOverlay("보안 상태 확인 중...");

                // Windows Defender 상태 확인
                var defenderStatus = await CheckDefenderStatus();
                securityStatusItems.Add(new SecurityStatusItem
                {
                    Icon = "&#xE72E;",
                    Title = "Windows Defender",
                    Description = "바이러스 및 위협 방지",
                    Status = defenderStatus
                });

                // Windows Firewall 상태 확인
                var firewallStatus = await CheckFirewallStatus();
                securityStatusItems.Add(new SecurityStatusItem
                {
                    Icon = "&#xE785;",
                    Title = "Windows Firewall",
                    Description = "네트워크 보안",
                    Status = firewallStatus
                });

                // Windows Security Center 상태 확인
                var securityCenterStatus = await CheckSecurityCenterStatus();
                securityStatusItems.Add(new SecurityStatusItem
                {
                    Icon = "&#xE946;",
                    Title = "Windows Security Center",
                    Description = "시스템 보안 상태",
                    Status = securityCenterStatus
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"보안 상태 확인 중 오류가 발생했습니다: {ex.Message}",
                              "오류",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
            finally
            {
                HideLoadingOverlay();
            }
        }

        // Windows Defender 상태 확인 (Process.Start 사용)
        private async Task<string> CheckDefenderStatus()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-Command \"Get-MpComputerStatus | Select-Object -ExpandProperty RealtimeProtectionEnabled\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    string output = await process.StandardOutput.ReadToEndAsync();
                    process.WaitForExit();
                    bool isEnabled = bool.TryParse(output.Trim(), out bool result) && result;
                    return isEnabled ? "활성" : "비활성";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Windows Defender 상태 확인 오류: {ex.Message}");
                return "확인 불가";
            }
        }

        // Windows Firewall 상태 확인 (Process.Start 사용)
        private async Task<string> CheckFirewallStatus()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-Command \"Get-NetFirewallProfile -Profile Domain,Private,Public | Select-Object -ExpandProperty Enabled\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    string output = await process.StandardOutput.ReadToEndAsync();
                    process.WaitForExit();
                    var enabledStatuses = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                                             .Select(line => bool.TryParse(line.Trim(), out bool result) && result)
                                             .ToList();
                    return enabledStatuses.All(e => e) ? "활성" : "비활성";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Windows Firewall 상태 확인 오류: {ex.Message}");
                return "확인 불가";
            }
        }

        // Windows Security Center 상태 확인 (Process.Start 사용)
        private async Task<string> CheckSecurityCenterStatus()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-Command \"Get-CimInstance -Namespace root/SecurityCenter2 -ClassName HealthService | Select-Object -ExpandProperty HealthStatus\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    string output = await process.StandardOutput.ReadToEndAsync();
                    process.WaitForExit();
                    string status = output.Trim();

                    switch (status)
                    {
                        case "0":
                            return "정상";
                        case "1":
                            return "잠재적 위험";
                        case "2":
                            return "위험";
                        default:
                            return "확인 불가";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Windows Security Center 상태 확인 오류: {ex.Message}");
                return "확인 불가";
            }
        }

        // 보안 설정 최적화 버튼 클릭 이벤트
        private async void OptimizeSecuritySettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowLoadingOverlay("보안 설정 최적화 중...");

                // Windows Defender 설정 최적화 (실시간 보호 활성화 등)
                await RunPowerShellCommand("Set-MpPreference -RealtimeProtectionEnabled $true");

                // Windows Firewall 설정 최적화 (모든 프로필 활성화)
                await RunPowerShellCommand("Set-NetFirewallProfile -Profile Domain,Private,Public -Enabled True");

                // 추가적인 보안 설정 최적화 명령 추가 가능

                MessageBox.Show("보안 설정 최적화가 완료되었습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"보안 설정 최적화 중 오류가 발생했습니다: {ex.Message}",
                              "오류",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
            finally
            {
                HideLoadingOverlay();
                _ = LoadSecurityStatus(); // 상태 새로고침
            }
        }

        // 문제 진단 마법사 시작 버튼 클릭 이벤트
        private async void StartDiagnosticWizard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 초기화
                ResetAllProgress();
                securityStatusItems.Clear();
                ResultReport.Text = "복구를 시작합니다...";

                // Windows Defender 복구
                await RecoverDefender();

                // Windows Firewall 복구
                await RecoverFirewall();

                // Windows Security Center 복구
                await RecoverSecurityCenter();

                // BitLocker 복구
                await RecoverBitLocker();

                // 최종 결과 보고서 생성
                UpdateResultReport();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"보안 진단 중 오류가 발생했습니다:\n\n{ex.Message}\n\n" +
                    "일부 기능이 정상적으로 복구되지 않았을 수 있습니다.\n" +
                    "관리자에게 문의하시기 바랍니다.",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ResetAllProgress()
        {
            // Windows Defender
            DefenderStatus.Text = "대기 중";
            DefenderProgress.Value = 0;
            DefenderTime.Text = "예상 시간: 2분";

            // Windows Firewall
            FirewallStatus.Text = "대기 중";
            FirewallProgress.Value = 0;
            FirewallTime.Text = "예상 시간: 30초";

            // Windows Security Center
            SecurityCenterStatus.Text = "대기 중";
            SecurityCenterProgress.Value = 0;
            SecurityCenterTime.Text = "예상 시간: 1분";

            // BitLocker
            BitLockerStatus.Text = "대기 중";
            BitLockerProgress.Value = 0;
            BitLockerTime.Text = "예상 시간: 2분";
        }

        private async Task RecoverDefender()
        {
            try
            {
                DefenderStatus.Text = "진행 중...";
                DefenderProgress.Value = 0;

                // PowerShell 스크립트 생성
                string scriptPath = Path.Combine(Path.GetTempPath(), "DefenderRecovery.ps1");
                string scriptContent = @"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Write-Host 'Windows Defender 복구를 시작합니다...' -ForegroundColor Cyan
Write-Host '----------------------------------------' -ForegroundColor Gray

Write-Host '`n[1/2] 실시간 보호 활성화 중...' -ForegroundColor Yellow
Set-MpPreference -RealtimeProtectionEnabled $true
Write-Host '실시간 보호 활성화 완료' -ForegroundColor Green
Start-Sleep -Seconds 2

Write-Host '`n[2/2] 빠른 검사 실행 중...' -ForegroundColor Yellow
Start-MpScan -ScanType QuickScan
Write-Host '빠른 검사 완료' -ForegroundColor Green
Start-Sleep -Seconds 2

Write-Host '`n----------------------------------------' -ForegroundColor Gray
Write-Host 'Windows Defender 복구가 완료되었습니다.' -ForegroundColor Cyan
Write-Host '아무 키나 누르면 창이 닫힙니다...' -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
";
                File.WriteAllText(scriptPath, scriptContent);

                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe", // powershell.exe 직접 사용
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Normal -Command \"chcp 65001; & {{Start-Process powershell -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"' -Verb RunAs -Wait}}\"", // chcp 65001 명령 포함
                    UseShellExecute = false,
                    CreateNoWindow = false
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        await Task.Run(() => process.WaitForExit());
                        if (process.ExitCode == 0)
                        {
                            await Dispatcher.InvokeAsync(() =>
                            {
                                DefenderProgress.Value = 100;
                                DefenderStatus.Text = "완료";
                                DefenderTime.Text = "완료됨";
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    DefenderStatus.Text = "오류 발생";
                    DefenderTime.Text = "실패";
                });
                throw new Exception($"Windows Defender 복구 중 오류: {ex.Message}");
            }
        }

        private async Task RecoverFirewall()
        {
            try
            {
                FirewallStatus.Text = "진행 중...";
                FirewallProgress.Value = 0;

                // PowerShell 스크립트 생성
                string scriptPath = Path.Combine(Path.GetTempPath(), "FirewallRecovery.ps1");
                string scriptContent = @"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Write-Host 'Windows Firewall 복구를 시작합니다...' -ForegroundColor Cyan
Write-Host '----------------------------------------' -ForegroundColor Gray

Write-Host '`n모든 프로필의 방화벽을 활성화합니다...' -ForegroundColor Yellow
Set-NetFirewallProfile -Profile Domain,Private,Public -Enabled True
Write-Host '방화벽 활성화 완료' -ForegroundColor Green
Start-Sleep -Seconds 2

Write-Host '`n----------------------------------------' -ForegroundColor Gray
Write-Host 'Windows Firewall 복구가 완료되었습니다.' -ForegroundColor Cyan
Write-Host '아무 키나 누르면 창이 닫힙니다...' -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
";
                File.WriteAllText(scriptPath, scriptContent);

                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe", // powershell.exe 직접 사용
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Normal -Command \"chcp 65001; & {{Start-Process powershell -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"' -Verb RunAs -Wait}}\"", // chcp 65001 명령 포함
                    UseShellExecute = false,
                    CreateNoWindow = false
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        await Task.Run(() => process.WaitForExit());
                        if (process.ExitCode == 0)
                        {
                            await Dispatcher.InvokeAsync(() =>
                            {
                                FirewallProgress.Value = 100;
                                FirewallStatus.Text = "완료";
                                FirewallTime.Text = "완료됨";
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    FirewallStatus.Text = "오류 발생";
                    FirewallTime.Text = "실패";
                });
                throw new Exception($"Windows Firewall 복구 중 오류: {ex.Message}");
            }
        }

        private async Task RecoverSecurityCenter()
        {
            try
            {
                SecurityCenterStatus.Text = "진행 중...";
                SecurityCenterProgress.Value = 0;

                // PowerShell 스크립트 생성
                string scriptPath = Path.Combine(Path.GetTempPath(), "SecurityCenterRecovery.ps1");
                string scriptContent = @"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Write-Host 'Windows Security Center 복구를 시작합니다...' -ForegroundColor Cyan
Write-Host '----------------------------------------' -ForegroundColor Gray

Write-Host '`n보안 센터 서비스를 재시작합니다...' -ForegroundColor Yellow
Restart-Service wscsvc -Force
Write-Host '서비스 재시작 완료' -ForegroundColor Green
Start-Sleep -Seconds 2

Write-Host '`n----------------------------------------' -ForegroundColor Gray
Write-Host 'Windows Security Center 복구가 완료되었습니다.' -ForegroundColor Cyan
Write-Host '아무 키나 누르면 창이 닫힙니다...' -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
";
                File.WriteAllText(scriptPath, scriptContent);

                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe", // powershell.exe 직접 사용
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Normal -Command \"chcp 65001; & {{Start-Process powershell -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"' -Verb RunAs -Wait}}\"", // chcp 65001 명령 포함
                    UseShellExecute = false,
                    CreateNoWindow = false
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        await Task.Run(() => process.WaitForExit());
                        if (process.ExitCode == 0)
                        {
                            await Dispatcher.InvokeAsync(() =>
                            {
                                SecurityCenterProgress.Value = 100;
                                SecurityCenterStatus.Text = "완료";
                                SecurityCenterTime.Text = "완료됨";
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    SecurityCenterStatus.Text = "오류 발생";
                    SecurityCenterTime.Text = "실패";
                });
                throw new Exception($"Windows Security Center 복구 중 오류: {ex.Message}");
            }
        }

        private async Task RecoverBitLocker()
        {
            try
            {
                BitLockerStatus.Text = "진행 중...";
                BitLockerProgress.Value = 0;

                // PowerShell 스크립트 생성
                string scriptPath = Path.Combine(Path.GetTempPath(), "BitLockerRecovery.ps1");
                string scriptContent = @"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Write-Host 'BitLocker 복구를 시작합니다...' -ForegroundColor Cyan
Write-Host '----------------------------------------' -ForegroundColor Gray

Write-Host '`n드라이브 암호화를 활성화합니다...' -ForegroundColor Yellow
Enable-BitLocker -MountPoint 'C:\' -RecoveryPasswordProtector
Write-Host '드라이브 암호화 활성화 완료' -ForegroundColor Green
Start-Sleep -Seconds 2

Write-Host '`n----------------------------------------' -ForegroundColor Gray
Write-Host 'BitLocker 복구가 완료되었습니다.' -ForegroundColor Cyan
Write-Host '아무 키나 누르면 창이 닫힙니다...' -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
";
                File.WriteAllText(scriptPath, scriptContent);

                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe", // powershell.exe 직접 사용
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Normal -Command \"chcp 65001; & {{Start-Process powershell -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"' -Verb RunAs -Wait}}\"", // chcp 65001 명령 포함
                    UseShellExecute = false,
                    CreateNoWindow = false
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        await Task.Run(() => process.WaitForExit());
                        if (process.ExitCode == 0)
                        {
                            await Dispatcher.InvokeAsync(() =>
                            {
                                BitLockerProgress.Value = 100;
                                BitLockerStatus.Text = "완료";
                                BitLockerTime.Text = "완료됨";
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    BitLockerStatus.Text = "오류 발생";
                    BitLockerTime.Text = "실패";
                });
                throw new Exception($"Windows BitLocker 복구 중 오류: {ex.Message}");
            }
        }

        private void UpdateResultReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("보안 프로그램 복구 결과:");
            report.AppendLine();

            // Windows Defender 결과
            report.AppendLine($"• Windows Defender: {DefenderStatus.Text}");
            if (DefenderStatus.Text == "완료")
            {
                report.AppendLine("  - 실시간 보호 활성화");
                report.AppendLine("  - 빠른 검사 완료");
            }

            // Windows Firewall 결과
            report.AppendLine($"• Windows Firewall: {FirewallStatus.Text}");
            if (FirewallStatus.Text == "완료")
                report.AppendLine("  - 모든 프로필 방화벽 활성화");

            // Windows Security Center 결과
            report.AppendLine($"• Windows Security Center: {SecurityCenterStatus.Text}");
            if (SecurityCenterStatus.Text == "완료")
                report.AppendLine("  - 보안 센터 서비스 재시작 완료");

            // BitLocker 결과
            report.AppendLine($"• BitLocker: {BitLockerStatus.Text}");
            if (BitLockerStatus.Text == "완료")
                report.AppendLine("  - 드라이브 암호화 활성화");

            ResultReport.Text = report.ToString();
        }

        // PowerShell 명령 실행 헬퍼 메서드
        private async Task<bool> RunPowerShellCommand(string command)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
                    UseShellExecute = true,  // PowerShell 창이 보이도록 변경
                    Verb = "runas"  // 관리자 권한으로 실행
                };

                using (var process = Process.Start(startInfo))
                {
                    process.WaitForExit();
                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PowerShell 명령 실행 오류: {ex.Message}");
                return false;
            }
        }

        // 사이드바 네비게이션 (임시)
        private void SidebarPrograms_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Page1());
        }

        private void SidebarModification_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Page2());
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

        private void NavigateToPage(Page page)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.NavigateToPage(page);
        }
    }

    public class SecurityStatusItem
    {
        public string Icon { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
    }
} 