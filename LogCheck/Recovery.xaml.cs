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

namespace WindowsSentinel
{
    public partial class Recovery : Page
    {
        private readonly ObservableCollection<SecurityStatusItem> securityStatusItems = new();
        private readonly DispatcherTimer loadingTextTimer = new();
        private int dotCount = 0;
        private const int maxDots = 3;
        private string baseText = "처리 중";

        // 복구 작업 시간 측정을 위한 필드
        private TimeSpan defenderRecoveryDuration;
        private TimeSpan firewallRecoveryDuration;
        private TimeSpan securityCenterRecoveryDuration;
        private TimeSpan bitLockerRecoveryDuration;

        // 복구 작업 오류 메시지 저장을 위한 필드
        private string defenderRecoveryError = "";
        private string firewallRecoveryError = "";
        private string securityCenterRecoveryError = "";
        private string bitLockerRecoveryError = "";

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

            // 로딩 텍스트 타이머 설정
            loadingTextTimer.Interval = TimeSpan.FromMilliseconds(500);
            loadingTextTimer.Tick += LoadingTextTimer_Tick;

            // 고급 모드 토글 이벤트 핸들러
            AdvancedModeToggle.Checked += (s, e) => AdvancedOutputBorder.Visibility = Visibility.Visible;
            AdvancedModeToggle.Unchecked += (s, e) => AdvancedOutputBorder.Visibility = Visibility.Collapsed;

            // 초기 보안 상태 로드
            _ = LoadSecurityStatus();
        }

        private void LoadingTextTimer_Tick(object? sender, EventArgs e)
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
#if WINDOWS
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
#else
            return false;
#endif
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
                    Icon = "\uE72E", // 방패
                    Title = "Windows Defender",
                    Description = "바이러스 및 위협 방지",
                    Status = defenderStatus
                });
                // 현재 상태 UI 업데이트
                // await Dispatcher.InvokeAsync(() => CurrentDefenderStatus.Text = defenderStatus);

                // Windows Firewall 상태 확인
                var firewallStatus = await CheckFirewallStatus();
                securityStatusItems.Add(new SecurityStatusItem
                {
                    Icon = "\uE8FD", // 방화벽
                    Title = "Windows Firewall",
                    Description = "네트워크 보안",
                    Status = firewallStatus
                });
                // 현재 상태 UI 업데이트
                // await Dispatcher.InvokeAsync(() => CurrentFirewallStatus.Text = firewallStatus);

                // Windows Security Center 상태 확인
                var securityCenterStatus = await CheckSecurityCenterStatus();
                securityStatusItems.Add(new SecurityStatusItem
                {
                    Icon = "\uEA0B", // 체크 표시 원
                    Title = "Windows Security Center",
                    Description = "시스템 보안 상태",
                    Status = securityCenterStatus
                });
                // 현재 상태 UI 업데이트
                // await Dispatcher.InvokeAsync(() => CurrentSecurityCenterStatus.Text = securityCenterStatus);

                // BitLocker 상태 확인
                var bitLockerStatus = await CheckBitLockerStatus();
                securityStatusItems.Add(new SecurityStatusItem
                {
                    Icon = "\uEDE1", // 자물쇠
                    Title = "BitLocker",
                    Description = "드라이브 암호화 상태",
                    Status = bitLockerStatus
                });
                // 현재 상태 UI 업데이트
                // await Dispatcher.InvokeAsync(() => CurrentBitLockerStatus.Text = bitLockerStatus);

                // ItemsControl에 데이터 바인딩
                await Dispatcher.InvokeAsync(() =>
                {
                    SecurityStatusItemsAll.ItemsSource = securityStatusItems.ToList();
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

        // BitLocker 상태 확인 (Process.Start 사용)
        private async Task<string> CheckBitLockerStatus()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-Command \"Get-BitLockerVolume -MountPoint 'C:\\' | Select-Object -ExpandProperty VolumeStatus\"",
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
                        case "FullyEncrypted":
                            return "활성 (암호화 완료)";
                        case "EncryptionInProgress":
                            return "활성 (암호화 중)";
                        case "DecryptionInProgress":
                            return "비활성 (암호 해제 중)";
                        case "FullyDecrypted":
                            return "비활성 (암호 해제 완료)";
                        case "Paused":
                            return "일시 중지됨";
                        case "Partial":
                            return "부분 암호화됨";
                        default:
                            return "확인 불가";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"BitLocker 상태 확인 오류: {ex.Message}");
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
                await RunPowerShellCommand("Set-MpPreference -RealtimeProtectionEnabled $true", "Windows Defender 실시간 보호 활성화");

                // Windows Firewall 설정 최적화 (모든 프로필 활성화)
                await RunPowerShellCommand("Set-NetFirewallProfile -Profile Domain,Private,Public -Enabled True", "Windows 방화벽 활성화");

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

        // 정밀 보안 진단 마법사 시작 버튼 클릭 이벤트 수정
        private async void StartDiagnosticWizard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                ShowLoadingOverlay("정밀 보안 진단 및 복구 중...");
                PowerShellOutputBorder.Visibility = Visibility.Visible;
                PowerShellOutput.Text = "보안 진단 및 복구를 시작합니다...\n";
                ResultReport.Text = "복구 작업이 시작되었습니다. 각 항목별 진행 상태를 확인하세요."; // 결과 보고서 초기 메시지

                // 복구 상태 초기화
                ResetRecoveryState();
                
                // Windows Defender 복구 실행
                await RecoverDefender();

                // Windows Firewall 복구 실행
                await RecoverFirewall();

                // Windows Security Center 복구 실행
                await RecoverSecurityCenter();

                // BitLocker 복구 실행 (필요시)
                await RecoverBitLocker();

                // 모든 복구 작업 완료 후 최종 결과 보고서 업데이트는 각 Recover 메서드의 finally 블록에서 호출됩니다.
                // UpdateResultReport(); // 이미 finally 블록에서 호출됨

                MessageBox.Show("정밀 보안 진단 및 복구가 완료되었습니다.",
                              "완료",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"정밀 보안 진단 및 복구 중 오류가 발생했습니다: {ex.Message}",
                              "오류",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
            finally
            {
                HideLoadingOverlay();
                Mouse.OverrideCursor = null;
                _ = LoadSecurityStatus(); // 최종 보안 상태 새로고침
            }
        }

        private void ResetAllProgress()
        {
            // Windows Defender
            DefenderStatus.Text = "대기 중";
            DefenderProgress.Value = 0;

            // Windows Firewall
            FirewallStatus.Text = "대기 중";
            FirewallProgress.Value = 0;

            // Windows Security Center
            SecurityCenterStatus.Text = "대기 중";
            SecurityCenterProgress.Value = 0;

            // BitLocker
            BitLockerStatus.Text = "대기 중";
            BitLockerProgress.Value = 0;
        }

        // Windows Defender 복구
        private async Task RecoverDefender()
        {
            var stopwatch = Stopwatch.StartNew(); // 스톱워치 시작
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    DefenderStatus.Text = "진행 중...";
                    DefenderProgress.Value = 0;
                });

                // PowerShell 스크립트 내용
                string scriptContent = @"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Write-Host 'Windows Defender 복구를 시작합니다...' -ForegroundColor Cyan
Write-Host '----------------------------------------' -ForegroundColor Gray

Write-Host '`n[1/2] 실시간 보호 활성화 중...' -ForegroundColor Yellow
Set-MpPreference -RealtimeProtectionEnabled $true
Write-Host '실시간 보호 활성화 완료' -ForegroundColor Green
Start-Sleep -Seconds 1

Write-Host '`n[2/2] 빠른 검사 실행 중...' -ForegroundColor Yellow
Start-MpScan -ScanType QuickScan
Write-Host '빠른 검사 완료' -ForegroundColor Green
Start-Sleep -Seconds 2

Write-Host '`n----------------------------------------' -ForegroundColor Gray
Write-Host 'Windows Defender 복구가 완료되었습니다.' -ForegroundColor Cyan
Write-Host '아무 키나 누르면 창이 닫힙니다...' -ForegroundColor Gray
# $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown') # GUI 환경에서는 필요 없음
";

                // RunPowerShellCommand를 사용하여 스크립트 실행
                // RunPowerShellCommand는 자체적으로 예외 처리를 수행하고 결과를 반환합니다.
                bool success = await RunPowerShellCommand(scriptContent, "Windows Defender 복구", true);

                await Dispatcher.InvokeAsync(() =>
                {
                    if (success)
                    {
                        DefenderProgress.Value = 100;
                        DefenderStatus.Text = "완료";
                    }
                    else
                    {
                        DefenderStatus.Text = "오류 발생";
                        defenderRecoveryError = "스크립트 실행 실패"; // 오류 메시지 저장
                        AddUserFriendlyMessage($"❌ Windows Defender 복구 중 예기치 않은 오류 발생: {defenderRecoveryError}", MessageType.Error); // 사용자 친화적 오류 메시지 추가
                    }
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    // RunPowerShellCommand 자체에서 발생한 예외 (예: 프로세스 시작 실패)
                    DefenderStatus.Text = "오류 발생";
                    defenderRecoveryError = ex.Message; // 오류 메시지 저장
                    AddUserFriendlyMessage($"❌ Windows Defender 복구 중 예기치 않은 오류 발생: {ex.Message}", MessageType.Error); // 사용자 친화적 오류 메시지 추가
                });
            }
            finally
            {
                stopwatch.Stop(); // 스톱워치 중지
                defenderRecoveryDuration = stopwatch.Elapsed; // 소요 시간 저장
                await Dispatcher.InvokeAsync(() => UpdateResultReport()); // 최종 결과 보고서 업데이트
            }
        }

        // Windows Firewall 복구
        private async Task RecoverFirewall()
        {
            var stopwatch = Stopwatch.StartNew(); // 스톱워치 시작
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    FirewallStatus.Text = "진행 중...";
                    FirewallProgress.Value = 0;
                });

                // PowerShell 스크립트 내용
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
# $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown') # GUI 환경에서는 필요 없음
";

                // RunPowerShellCommand를 사용하여 스크립트 실행
                bool success = await RunPowerShellCommand(scriptContent, "Windows Firewall 복구", true);

                await Dispatcher.InvokeAsync(() =>
                {
                    if (success)
                    {
                        FirewallProgress.Value = 100;
                        FirewallStatus.Text = "완료";
                    }
                    else
                    {
                        FirewallStatus.Text = "오류 발생";
                        firewallRecoveryError = "스크립트 실행 실패"; // 오류 메시지 저장
                        AddUserFriendlyMessage($"❌ Windows Firewall 복구 중 예기치 않은 오류 발생: {firewallRecoveryError}", MessageType.Error); // 사용자 친화적 오류 메시지 추가
                    }
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    FirewallStatus.Text = "오류 발생";
                    firewallRecoveryError = ex.Message; // 오류 메시지 저장
                    AddUserFriendlyMessage($"❌ Windows Firewall 복구 중 예기치 않은 오류 발생: {ex.Message}", MessageType.Error); // 사용자 친화적 오류 메시지 추가
                });
            }
            finally
            {
                stopwatch.Stop(); // 스톱워치 중지
                firewallRecoveryDuration = stopwatch.Elapsed; // 소요 시간 저장
                await Dispatcher.InvokeAsync(() => UpdateResultReport()); // 최종 결과 보고서 업데이트
            }
        }

        // Windows Security Center 복구
        private async Task RecoverSecurityCenter()
        {
            var stopwatch = Stopwatch.StartNew(); // 스톱워치 시작
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    SecurityCenterStatus.Text = "진행 중...";
                    SecurityCenterProgress.Value = 0;
                });

                // PowerShell 스크립트 내용
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
# $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown') # GUI 환경에서는 필요 없음
";

                // RunPowerShellCommand를 사용하여 스크립트 실행
                bool success = await RunPowerShellCommand(scriptContent, "Windows Security Center 복구", true);

                await Dispatcher.InvokeAsync(() =>
                {
                    if (success)
                    {
                        SecurityCenterProgress.Value = 100;
                        SecurityCenterStatus.Text = "완료";
                    }
                    else
                    {
                        SecurityCenterStatus.Text = "오류 발생";
                        securityCenterRecoveryError = "스크립트 실행 실패"; // 오류 메시지 저장
                        AddUserFriendlyMessage($"❌ Windows Security Center 복구 중 예기치 않은 오류 발생: {securityCenterRecoveryError}", MessageType.Error); // 사용자 친화적 오류 메시지 추가
                    }
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    SecurityCenterStatus.Text = "오류 발생";
                    securityCenterRecoveryError = ex.Message; // 오류 메시지 저장
                    AddUserFriendlyMessage($"❌ Windows Security Center 복구 중 예기치 않은 오류 발생: {ex.Message}", MessageType.Error); // 사용자 친화적 오류 메시지 추가
                });
            }
            finally
            {
                stopwatch.Stop(); // 스톱워치 중지
                securityCenterRecoveryDuration = stopwatch.Elapsed; // 소요 시간 저장
                await Dispatcher.InvokeAsync(() => UpdateResultReport()); // 최종 결과 보고서 업데이트
            }
        }

        // BitLocker 복구
        private async Task RecoverBitLocker()
        {
            var stopwatch = Stopwatch.StartNew(); // 스톱워치 시작
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    BitLockerStatus.Text = "진행 중...";
                    BitLockerProgress.Value = 0;
                });

                // PowerShell 스크립트 내용
                string scriptContent = @"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Write-Host 'BitLocker 복구를 시작합니다...' -ForegroundColor Cyan
Write-Host '----------------------------------------' -ForegroundColor Gray

Write-Host '`n드라이브 암호화를 활성화합니다...' -ForegroundColor Yellow
# 드라이브 문자를 실제 환경에 맞게 수정해야 할 수 있습니다 (예: 'C:')
Enable-BitLocker -MountPoint 'C:\' -RecoveryPasswordProtector -SkipHardwareTest
Write-Host '드라이브 암호화 활성화 완료' -ForegroundColor Green
Start-Sleep -Seconds 2

Write-Host '`n----------------------------------------' -ForegroundColor Gray
Write-Host 'BitLocker 복구가 완료되었습니다.' -ForegroundColor Cyan
Write-Host '아무 키나 누르면 창이 닫힙니다...' -ForegroundColor Gray
# $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown') # GUI 환경에서는 필요 없음
";

                // RunPowerShellCommand를 사용하여 스크립트 실행
                bool success = await RunPowerShellCommand(scriptContent, "BitLocker 복구", true);

                await Dispatcher.InvokeAsync(() =>
                {
                    if (success)
                    {
                        BitLockerProgress.Value = 100;
                        BitLockerStatus.Text = "완료";
                    }
                    else
                    {
                        BitLockerStatus.Text = "오류 발생";
                        bitLockerRecoveryError = "스크립트 실행 실패"; // 오류 메시지 저장
                        AddUserFriendlyMessage($"❌ BitLocker 복구 중 예기치 않은 오류 발생: {bitLockerRecoveryError}", MessageType.Error); // 사용자 친화적 오류 메시지 추가
                    }
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    BitLockerStatus.Text = "오류 발생";
                    bitLockerRecoveryError = ex.Message; // 오류 메시지 저장
                    AddUserFriendlyMessage($"❌ BitLocker 복구 중 예기치 않은 오류 발생: {ex.Message}", MessageType.Error); // 사용자 친화적 오류 메시지 추가
                });
            }
            finally
            {
                stopwatch.Stop(); // 스톱워치 중지
                bitLockerRecoveryDuration = stopwatch.Elapsed; // 소요 시간 저장
                await Dispatcher.InvokeAsync(() => UpdateResultReport()); // 최종 결과 보고서 업데이트
            }
        }

        // 복구 상태 및 결과 보고서 관련 필드를 초기화하는 메서드 추가
        private void ResetRecoveryState()
        {
            defenderRecoveryDuration = TimeSpan.Zero;
            firewallRecoveryDuration = TimeSpan.Zero;
            securityCenterRecoveryDuration = TimeSpan.Zero;
            bitLockerRecoveryDuration = TimeSpan.Zero;

            defenderRecoveryError = "";
            firewallRecoveryError = "";
            securityCenterRecoveryError = "";
            bitLockerRecoveryError = "";

            ResultReport.Text = "아직 복구가 시작되지 않았습니다.";
            StatusText.Text = "준비됨";
            TimestampText.Text = DateTime.Now.ToString("HH:mm:ss");

            ResetAllProgress(); // 기존 진행 상태 초기화 메서드 호출
        }

        // 복구 결과 보고서 업데이트 메서드 수정
        private void UpdateResultReport()
        {
            var report = new System.Text.StringBuilder();

            // 전체 복구 작업 요약 추가
            bool overallSuccess = DefenderStatus.Text == "완료" &&
                                  FirewallStatus.Text == "완료" &&
                                  SecurityCenterStatus.Text == "완료" &&
                                  BitLockerStatus.Text == "완료";

            report.AppendLine("=== 전체 복구 결과 요약 ===");
            report.AppendLine(overallSuccess ? "모든 보안 프로그램 복구가 성공적으로 완료되었습니다." : "일부 보안 프로그램 복구 중 문제가 발생했습니다.");
            report.AppendLine("==========================");
            report.AppendLine();

            report.AppendLine("=== 상세 복구 결과 ===");
            report.AppendLine();

            // Windows Defender 결과 상세
            report.AppendLine($"• Windows Defender: {DefenderStatus.Text}");
            report.AppendLine($"  소요 시간: {defenderRecoveryDuration.TotalSeconds:F1}초");
            if (DefenderStatus.Text == "완료")
            {
                report.AppendLine("  - 실시간 보호 활성화");
                report.AppendLine("  - 빠른 검사 완료");
            }
            else if (!string.IsNullOrEmpty(defenderRecoveryError))
            {
                report.AppendLine($"  오류 상세: {defenderRecoveryError}");
            }
            report.AppendLine();

            // Windows Firewall 결과 상세
            report.AppendLine($"• Windows Firewall: {FirewallStatus.Text}");
            report.AppendLine($"  소요 시간: {firewallRecoveryDuration.TotalSeconds:F1}초");
            if (FirewallStatus.Text == "완료")
                report.AppendLine("  - 모든 프로필 방화벽 활성화");
            else if (!string.IsNullOrEmpty(firewallRecoveryError))
            {
                report.AppendLine($"  오류 상세: {firewallRecoveryError}");
            }
            report.AppendLine();

            // Windows Security Center 결과 상세
            report.AppendLine($"• Windows Security Center: {SecurityCenterStatus.Text}");
            report.AppendLine($"  소요 시간: {securityCenterRecoveryDuration.TotalSeconds:F1}초");
            if (SecurityCenterStatus.Text == "완료")
                report.AppendLine("  - 보안 센터 서비스 재시작 완료");
            else if (!string.IsNullOrEmpty(securityCenterRecoveryError))
            {
                report.AppendLine($"  오류 상세: {securityCenterRecoveryError}");
            }
            report.AppendLine();

            // BitLocker 결과 상세
            report.AppendLine($"• BitLocker: {BitLockerStatus.Text}");
            report.AppendLine($"  소요 시간: {bitLockerRecoveryDuration.TotalSeconds:F1}초");
            if (BitLockerStatus.Text == "완료")
                report.AppendLine("  - 드라이브 암호화 활성화");
            else if (!string.IsNullOrEmpty(bitLockerRecoveryError))
            {
                report.AppendLine($"  오류 상세: {bitLockerRecoveryError}");
            }
             report.AppendLine();
             report.AppendLine("==========================");

            ResultReport.Text = report.ToString();
        }

        // 사용자 친화적인 메시지 추가
        private void AddUserFriendlyMessage(string message, MessageType type = MessageType.Info)
        {
            var messageBlock = new TextBlock
            {
                Text = message,
                Foreground = GetMessageColor(type),
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            };

            UserFriendlyOutput.Children.Add(messageBlock);
            UserFriendlyOutputScroll.ScrollToBottom();
        }

        // 메시지 타입에 따른 색상 반환
        private System.Windows.Media.Brush GetMessageColor(MessageType type)
        {
            return type switch
            {
                MessageType.Success => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGreen),
                MessageType.Warning => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange),
                MessageType.Error => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red),
                MessageType.Info => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White)
            };
        }

        // 진행 상태 업데이트
        private void UpdateProgress(string operation, double progress)
        {
            CurrentOperation.Text = operation;
            OperationProgress.Value = progress;
            StatusText.Text = $"{progress:F0}% 완료";
            TimestampText.Text = DateTime.Now.ToString("HH:mm:ss");
        }

        // PowerShell 출력을 표시하는 메서드 개선
        private void ShowPowerShellOutput(string output, bool isUserFriendly = true)
        {
            PowerShellOutputBorder.Visibility = Visibility.Visible;
            
            if (isUserFriendly)
            {
                // 사용자 친화적인 메시지로 변환
                var message = ConvertToUserFriendlyMessage(output);
                AddUserFriendlyMessage(message);
            }
            else
            {
                // 고급 모드 출력
                PowerShellOutput.Text = output;
                PowerShellOutputScroll.ScrollToBottom();
            }
        }

        // PowerShell 출력을 사용자 친화적인 메시지로 변환
        private string ConvertToUserFriendlyMessage(string output)
        {
            // PowerShell 출력을 분석하여 사용자 친화적인 메시지로 변환
            if (output.Contains("Windows Defender 복구를 시작합니다"))
                return "Windows Defender 복구를 시작합니다...";
            if (output.Contains("실시간 보호 활성화 완료"))
                return "✅ Windows Defender 실시간 보호가 활성화되었습니다.";
            if (output.Contains("빠른 검사 완료"))
                return "✅ Windows Defender 빠른 검사가 완료되었습니다.";
            if (output.Contains("Windows Firewall 복구를 시작합니다"))
                return "Windows 방화벽 복구를 시작합니다...";
            if (output.Contains("방화벽 활성화 완료"))
                return "✅ Windows 방화벽이 활성화되었습니다.";
            if (output.Contains("Windows Security Center 복구를 시작합니다"))
                return "Windows 보안 센터 복구를 시작합니다...";
            if (output.Contains("서비스 재시작 완료"))
                return "✅ Windows 보안 센터 서비스가 재시작되었습니다.";
            if (output.Contains("BitLocker 복구를 시작합니다"))
                return "BitLocker 복구를 시작합니다...";
            if (output.Contains("드라이브 암호화 활성화 완료"))
                return "✅ BitLocker 드라이브 암호화가 활성화되었습니다.";

            return output;
        }

        // PowerShell 명령을 실행하고 출력을 표시하는 메서드 개선
        private async Task<bool> RunPowerShellCommand(string command, string userFriendlyOperation, bool showOutput = true)
        {
            try
            {
                UpdateProgress(userFriendlyOperation, 0);
                AddUserFriendlyMessage($"🔄 {userFriendlyOperation} 시작...", MessageType.Info);

                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using (var process = Process.Start(startInfo))
                {
                    var output = new StringBuilder();
                    var error = new StringBuilder();

                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            output.AppendLine(e.Data);
                            if (showOutput)
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    ShowPowerShellOutput(e.Data, true);
                                    ShowPowerShellOutput(e.Data, false);
                                });
                            }
                        }
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            error.AppendLine(e.Data);
                            if (showOutput)
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    AddUserFriendlyMessage($"⚠️ 오류: {e.Data}", MessageType.Error);
                                    ShowPowerShellOutput(e.Data, false);
                                });
                            }
                        }
                    };

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // 진행 상태 애니메이션
                    var progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                    double progress = 0;
                    progressTimer.Tick += (s, e) =>
                    {
                        if (progress < 90)
                        {
                            progress += 0.5;
                            UpdateProgress(userFriendlyOperation, progress);
                        }
                    };
                    progressTimer.Start();

                    await Task.Run(() => process.WaitForExit());
                    progressTimer.Stop();

                    UpdateProgress(userFriendlyOperation, 100);
                    AddUserFriendlyMessage($"✅ {userFriendlyOperation} 완료!", MessageType.Success);

                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                AddUserFriendlyMessage($"❌ 오류 발생: {ex.Message}", MessageType.Error);
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

        // 메시지 타입 열거형
        private enum MessageType
        {
            Info,
            Success,
            Warning,
            Error
        }
    }

    public class SecurityStatusItem
    {
        public required string Icon { get; set; }
        public required string Title { get; set; }
        public required string Description { get; set; }
        public required string Status { get; set; }
    }
} 