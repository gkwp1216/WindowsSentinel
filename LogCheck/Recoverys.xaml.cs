using LogCheck;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Threading;
using WindowsSentinel;
// Windows Forms와의 충돌을 방지하기 위한 alias 설정
using WpfControl = System.Windows.Controls.Control;

namespace LogCheck
{
    public partial class Recoverys : System.Windows.Controls.Page
    {
        private ToggleButton _selectedButton;
        private readonly ObservableCollection<RecoverySecurityStatusItem> securityStatusItems = new();
        private readonly System.Windows.Threading.DispatcherTimer loadingTextTimer = new();
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

        private CancellationTokenSource? _cancellationTokenSource;

        private class RecoveryProgress
        {
            public string Operation { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public int Progress { get; set; }
        }

        [SupportedOSPlatform("windows")]
        public Recoverys()
        {
            InitializeComponent();

            SideRecoveryButton.IsChecked = true;

            // 관리자 권한 확인
            if (!IsRunningAsAdmin())
            {
                System.Windows.MessageBox.Show("이 프로그램은 관리자 권한으로 실행해야 합니다.",
                              "권한 필요",
                              System.Windows.MessageBoxButton.OK,
                              System.Windows.MessageBoxImage.Warning);
                System.Windows.Application.Current.Shutdown();
                return;
            }

            // 로딩 텍스트 타이머 설정
            loadingTextTimer.Interval = TimeSpan.FromMilliseconds(500);
            loadingTextTimer.Tick += LoadingTextTimer_Tick;

            // 고급 모드 토글 이벤트 핸들러
            AdvancedModeToggle.Checked += (s, e) => AdvancedOutputBorder.Visibility = System.Windows.Visibility.Visible;
            AdvancedModeToggle.Unchecked += (s, e) => AdvancedOutputBorder.Visibility = System.Windows.Visibility.Collapsed;

            // 취소 토큰 소스 초기화
            _cancellationTokenSource = new CancellationTokenSource();

            // 초기 보안 상태 로드
            _ = LoadSecurityStatus();

            // 페이지 언로드 이벤트 핸들러 등록
            this.Unloaded += (s, e) =>
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            };
        }

        private void LoadingTextTimer_Tick(object sender, EventArgs e)
        {
            dotCount = (dotCount + 1) % (maxDots + 1);
            string dots = new string('.', dotCount);
            CurrentOperation.Text = $"{baseText}{dots}";
        }

        private void ShowLoadingOverlay()
        {
            PowerShellOutputBorder.Visibility = System.Windows.Visibility.Visible;
            loadingTextTimer.Start();
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        }

        private void HideLoadingOverlay()
        {
            PowerShellOutputBorder.Visibility = System.Windows.Visibility.Collapsed;
            loadingTextTimer.Stop();
            Mouse.OverrideCursor = null;
        }

        // 관리자 권한 확인 메서드
        [SupportedOSPlatform("windows")]
        private bool IsRunningAsAdmin()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        // 보안 상태 로드 메서드
        private async Task LoadSecurityStatus(IProgress<RecoveryProgress>? progress = null)
        {
            try
            {
                if (_cancellationTokenSource == null)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                }

                securityStatusItems.Clear();
                ShowLoadingOverlay();

                progress?.Report(new RecoveryProgress { Operation = "Windows Defender", Progress = 0, Status = "확인 중..." });
                await LoadDefenderStatus();
                progress?.Report(new RecoveryProgress { Operation = "Windows Defender", Progress = 25, Status = "완료" });

                progress?.Report(new RecoveryProgress { Operation = "Windows Firewall", Progress = 25, Status = "확인 중..." });
                await LoadFirewallStatus();
                progress?.Report(new RecoveryProgress { Operation = "Windows Firewall", Progress = 50, Status = "완료" });

                progress?.Report(new RecoveryProgress { Operation = "Windows Security Center", Progress = 50, Status = "확인 중..." });
                await LoadSecurityCenterStatus();
                progress?.Report(new RecoveryProgress { Operation = "Windows Security Center", Progress = 75, Status = "완료" });

                progress?.Report(new RecoveryProgress { Operation = "BitLocker", Progress = 75, Status = "확인 중..." });
                await LoadBitLockerStatus();
                progress?.Report(new RecoveryProgress { Operation = "BitLocker", Progress = 100, Status = "완료" });
            }
            catch (OperationCanceledException)
            {
                AddUserFriendlyMessage("작업이 취소되었습니다.", MessageType.Warning);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"보안 상태 확인 중 오류가 발생했습니다: {ex.Message}",
                              "오류",
                              System.Windows.MessageBoxButton.OK,
                              System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                HideLoadingOverlay();
            }
        }

        private async Task LoadDefenderStatus()
        {
            try
            {
                var defenderStatus = await CheckDefenderStatus();
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    securityStatusItems.Add(new RecoverySecurityStatusItem
                    {
                        Icon = "\uE72E",
                        Title = "Windows Defender",
                        Description = "바이러스 및 위협 방지",
                        Status = defenderStatus
                    });
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Windows Defender 상태 로드 오류: {ex.Message}");
            }
        }

        private async Task LoadFirewallStatus()
        {
            try
            {
                var firewallStatus = await CheckFirewallStatus();
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    securityStatusItems.Add(new RecoverySecurityStatusItem
                    {
                        Icon = "\uE8FD",
                        Title = "Windows Firewall",
                        Description = "네트워크 보안",
                        Status = firewallStatus
                    });
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Windows Firewall 상태 로드 오류: {ex.Message}");
            }
        }

        private async Task LoadSecurityCenterStatus()
        {
            try
            {
                var securityCenterStatus = await CheckSecurityCenterStatus();
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    securityStatusItems.Add(new RecoverySecurityStatusItem
                    {
                        Icon = "\uEA0B",
                        Title = "Windows Security Center",
                        Description = "시스템 보안 상태",
                        Status = securityCenterStatus
                    });
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Windows Security Center 상태 로드 오류: {ex.Message}");
            }
        }

        private async Task LoadBitLockerStatus()
        {
            try
            {
                var bitLockerStatus = await CheckBitLockerStatus();
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    securityStatusItems.Add(new RecoverySecurityStatusItem
                    {
                        Icon = "\uEDE1",
                        Title = "BitLocker",
                        Description = "드라이브 암호화 상태",
                        Status = bitLockerStatus
                    });
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"BitLocker 상태 로드 오류: {ex.Message}");
            }
        }

        // Windows Defender 상태 확인 (WMIHelper 사용)
        private async Task<string> CheckDefenderStatus()
        {
            try
            {
                bool isEnabled = await WmiHelper.CheckDefenderStatusAsync();
                return isEnabled ? "활성" : "비활성";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Windows Defender 상태 확인 오류: {ex.Message}");
                return "확인 불가";
            }
        }

        // Windows Firewall 상태 확인 (WMIHelper 사용)
        private async Task<string> CheckFirewallStatus()
        {
            try
            {
                bool isEnabled = await WmiHelper.CheckFirewallStatusAsync();
                return isEnabled ? "활성" : "비활성";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Windows Firewall 상태 확인 오류: {ex.Message}");
                return "확인 불가";
            }
        }

        // Windows Security Center 상태 확인 (WMIHelper 사용)
        private async Task<string> CheckSecurityCenterStatus()
        {
            try
            {
                string status = await WmiHelper.CheckSecurityCenterStatusAsync();
                return status;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Windows Security Center 상태 확인 오류: {ex.Message}");
                return "확인 불가";
            }
        }

        // BitLocker 상태 확인 (WMIHelper 사용)
        private async Task<string> CheckBitLockerStatus()
        {
            try
            {
                string status = await WmiHelper.CheckBitLockerStatusAsync();
                return status;
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
                ShowLoadingOverlay();

                // Windows Defender 설정 최적화 (실시간 보호 활성화 등)
                await RunPowerShellCommand("Set-MpPreference -RealtimeProtectionEnabled $true", "Windows Defender 실시간 보호 활성화");

                // Windows Firewall 설정 최적화 (모든 프로필 활성화)
                await RunPowerShellCommand("Set-NetFirewallProfile -Profile Domain,Private,Public -Enabled True", "Windows 방화벽 활성화");

                System.Windows.MessageBox.Show("보안 설정 최적화가 완료되었습니다.",
                    "알림",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"보안 설정 최적화 중 오류가 발생했습니다: {ex.Message}",
                              "오류",
                              System.Windows.MessageBoxButton.OK,
                              System.Windows.MessageBoxImage.Error);
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
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                ShowLoadingOverlay();
                PowerShellOutputBorder.Visibility = System.Windows.Visibility.Visible;
                PowerShellOutput.Text = "보안 진단 및 복구를 시작합니다...\n";
                ResultReport.Text = "복구 작업이 시작되었습니다. 각 항목별 진행 상태를 확인하세요.";

                // 복구 상태 초기화
                ResetRecoveryState();

                // 진행 상태를 추적하기 위한 Progress 객체 생성
                var progress = new Progress<RecoveryProgress>(p =>
                {
                    switch (p.Operation)
                    {
                        case "Windows Defender":
                            if (DefenderStatus is System.Windows.Controls.TextBlock defenderStatus)
                            {
                                defenderStatus.Text = p.Status;
                            }
                            if (DefenderProgress is System.Windows.Controls.ProgressBar defenderProgress)
                            {
                                defenderProgress.Value = p.Progress;
                            }
                            break;
                        case "Windows Firewall":
                            if (FirewallStatus is System.Windows.Controls.TextBlock firewallStatus)
                            {
                                firewallStatus.Text = p.Status;
                            }
                            if (FirewallProgress is System.Windows.Controls.ProgressBar firewallProgress)
                            {
                                firewallProgress.Value = p.Progress;
                            }
                            break;
                        case "Windows Security Center":
                            if (SecurityCenterStatus is System.Windows.Controls.TextBlock securityCenterStatus)
                            {
                                securityCenterStatus.Text = p.Status;
                            }
                            if (SecurityCenterProgress is System.Windows.Controls.ProgressBar securityCenterProgress)
                            {
                                securityCenterProgress.Value = p.Progress;
                            }
                            break;
                        case "BitLocker":
                            if (BitLockerStatus is System.Windows.Controls.TextBlock bitLockerStatus)
                            {
                                bitLockerStatus.Text = p.Status;
                            }
                            if (BitLockerProgress is System.Windows.Controls.ProgressBar bitLockerProgress)
                            {
                                bitLockerProgress.Value = p.Progress;
                            }
                            break;
                    }
                    UpdateResultReport();
                });

                // Windows Defender 복구 실행
                await RecoverDefender(progress);

                // Windows Firewall 복구 실행
                await RecoverFirewall(progress);

                // Windows Security Center 복구 실행
                await RecoverSecurityCenter(progress);

                // BitLocker 복구 실행 (필요시)
                await RecoverBitLocker(progress);

                System.Windows.MessageBox.Show("정밀 보안 진단 및 복구가 완료되었습니다.",
                              "완료",
                              System.Windows.MessageBoxButton.OK,
                              System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"정밀 보안 진단 및 복구 중 오류가 발생했습니다: {ex.Message}",
                              "오류",
                              System.Windows.MessageBoxButton.OK,
                              System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                HideLoadingOverlay();
                Mouse.OverrideCursor = null;
                _ = LoadSecurityStatus();
            }
        }

        private void ResetAllProgress()
        {
            // Windows Defender
            if (DefenderStatus is System.Windows.Controls.TextBlock defenderStatus)
            {
                defenderStatus.Text = "대기 중";
            }
            if (DefenderProgress is System.Windows.Controls.ProgressBar defenderProgress)
            {
                defenderProgress.Value = 0;
            }

            // Windows Firewall
            if (FirewallStatus is System.Windows.Controls.TextBlock firewallStatus)
            {
                firewallStatus.Text = "대기 중";
            }
            if (FirewallProgress is System.Windows.Controls.ProgressBar firewallProgress)
            {
                firewallProgress.Value = 0;
            }

            // Windows Security Center
            if (SecurityCenterStatus is System.Windows.Controls.TextBlock securityCenterStatus)
            {
                securityCenterStatus.Text = "대기 중";
            }
            if (SecurityCenterProgress is System.Windows.Controls.ProgressBar securityCenterProgress)
            {
                securityCenterProgress.Value = 0;
            }

            // BitLocker
            if (BitLockerStatus is System.Windows.Controls.TextBlock bitLockerStatus)
            {
                bitLockerStatus.Text = "대기 중";
            }
            if (BitLockerProgress is System.Windows.Controls.ProgressBar bitLockerProgress)
            {
                bitLockerProgress.Value = 0;
            }
        }

        // Windows Defender 복구
        [SupportedOSPlatform("windows")]
        private async Task RecoverDefender(IProgress<RecoveryProgress> progress = null)
        {
            try
            {
                progress?.Report(new RecoveryProgress { Operation = "Windows Defender", Status = "진단 중...", Progress = 0 });

                // Windows Defender 상태 확인
                var status = await CheckDefenderStatus();
                progress?.Report(new RecoveryProgress { Operation = "Windows Defender", Status = "상태 확인 완료", Progress = 20 });

                // Defender 서비스 상태 확인 및 시작
                progress?.Report(new RecoveryProgress { Operation = "Windows Defender", Status = "서비스 확인 중...", Progress = 30 });
                await RunPowerShellCommand(
                    "Get-Service -Name WinDefend | Select-Object -ExpandProperty Status",
                    "Defender 서비스 상태 확인"
                );

                // Defender 서비스 시작
                progress?.Report(new RecoveryProgress { Operation = "Windows Defender", Status = "서비스 시작 중...", Progress = 40 });
                await RunPowerShellCommand(
                    "Start-Service -Name WinDefend; Set-Service -Name WinDefend -StartupType Automatic",
                    "Defender 서비스 시작"
                );

                // Defender 정책 복구
                progress?.Report(new RecoveryProgress { Operation = "Windows Defender", Status = "정책 복구 중...", Progress = 60 });

                // 실시간 보호 활성화
                await RunPowerShellCommand(
                    "Set-MpPreference -DisableRealtimeMonitoring $false",
                    "실시간 보호 활성화"
                );
                progress?.Report(new RecoveryProgress { Operation = "Windows Defender", Status = "실시간 보호 활성화 완료", Progress = 70 });

                // IOAV 보호 활성화
                await RunPowerShellCommand(
                    "Set-MpPreference -DisableIOAVProtection $false",
                    "IOAV 보호 활성화"
                );
                progress?.Report(new RecoveryProgress { Operation = "Windows Defender", Status = "IOAV 보호 활성화 완료", Progress = 80 });

                // 행동 모니터링 활성화
                await RunPowerShellCommand(
                    "Set-MpPreference -DisableBehaviorMonitoring $false",
                    "행동 모니터링 활성화"
                );
                progress?.Report(new RecoveryProgress { Operation = "Windows Defender", Status = "행동 모니터링 활성화 완료", Progress = 90 });

                // 최종 상태 확인
                progress?.Report(new RecoveryProgress { Operation = "Windows Defender", Status = "최종 확인 중...", Progress = 95 });
                var finalStatus = await CheckDefenderStatus();

                if (finalStatus == "활성")
                {
                    progress?.Report(new RecoveryProgress { Operation = "Windows Defender", Status = "복구 완료", Progress = 100 });
                    if (DefenderStatus is System.Windows.Controls.TextBlock statusBlock)
                    {
                        statusBlock.Text = "정상";
                        statusBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
                    }
                    if (DefenderProgress is System.Windows.Controls.ProgressBar progressBar)
                    {
                        progressBar.Value = 100;
                    }
                    AddUserFriendlyMessage("Windows Defender가 성공적으로 활성화되었습니다.", MessageType.Success);
                }
                else
                {
                    throw new Exception("Windows Defender 활성화 실패");
                }
            }
            catch (Exception ex)
            {
                if (DefenderStatus is System.Windows.Controls.TextBlock statusBlock)
                {
                    statusBlock.Text = "오류";
                    statusBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                }
                if (DefenderProgress is System.Windows.Controls.ProgressBar progressBar)
                {
                    progressBar.Value = 0;
                }
                defenderRecoveryError = $"오류 코드: 0x{ex.HResult:X8}, 원인: {ex.Message}";
                AddUserFriendlyMessage($"Windows Defender 복구 중 오류 발생: {ex.Message}", MessageType.Error);
            }
            finally
            {
                UpdateResultReport();
            }
        }

        // Windows Firewall 복구
        private async Task RecoverFirewall(IProgress<RecoveryProgress> progress = null)
        {
            try
            {
                progress?.Report(new RecoveryProgress { Operation = "Windows Firewall", Status = "진단 중...", Progress = 0 });

                // Windows Firewall 상태 확인
                var status = await CheckFirewallStatus();
                progress?.Report(new RecoveryProgress { Operation = "Windows Firewall", Status = "상태 확인 완료", Progress = 20 });

                // Firewall 서비스 상태 확인 및 시작
                progress?.Report(new RecoveryProgress { Operation = "Windows Firewall", Status = "서비스 확인 중...", Progress = 30 });
                await RunPowerShellCommand(
                    "Get-Service -Name MpsSvc | Select-Object -ExpandProperty Status",
                    "Firewall 서비스 상태 확인"
                );

                // Firewall 서비스 시작
                progress?.Report(new RecoveryProgress { Operation = "Windows Firewall", Status = "서비스 시작 중...", Progress = 40 });
                await RunPowerShellCommand(
                    "Start-Service -Name MpsSvc; Set-Service -Name MpsSvc -StartupType Automatic",
                    "Firewall 서비스 시작"
                );

                // Firewall 정책 복구
                progress?.Report(new RecoveryProgress { Operation = "Windows Firewall", Status = "정책 복구 중...", Progress = 60 });

                // 각 프로필별로 개별적으로 활성화
                await RunPowerShellCommand(
                    "Set-NetFirewallProfile -Profile Domain -Enabled True",
                    "도메인 프로필 방화벽 활성화"
                );
                progress?.Report(new RecoveryProgress { Operation = "Windows Firewall", Status = "도메인 프로필 활성화 완료", Progress = 70 });

                await RunPowerShellCommand(
                    "Set-NetFirewallProfile -Profile Private -Enabled True",
                    "개인 프로필 방화벽 활성화"
                );
                progress?.Report(new RecoveryProgress { Operation = "Windows Firewall", Status = "개인 프로필 활성화 완료", Progress = 80 });

                await RunPowerShellCommand(
                    "Set-NetFirewallProfile -Profile Public -Enabled True",
                    "공용 프로필 방화벽 활성화"
                );
                progress?.Report(new RecoveryProgress { Operation = "Windows Firewall", Status = "공용 프로필 활성화 완료", Progress = 90 });

                // 최종 상태 확인
                progress?.Report(new RecoveryProgress { Operation = "Windows Firewall", Status = "최종 확인 중...", Progress = 95 });
                var finalStatus = await CheckFirewallStatus();

                if (finalStatus == "활성")
                {
                    progress?.Report(new RecoveryProgress { Operation = "Windows Firewall", Status = "복구 완료", Progress = 100 });
                    if (FirewallStatus is System.Windows.Controls.TextBlock statusBlock)
                    {
                        statusBlock.Text = "정상";
                        statusBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
                    }
                    if (FirewallProgress is System.Windows.Controls.ProgressBar progressBar)
                    {
                        progressBar.Value = 100;
                    }
                    AddUserFriendlyMessage("Windows 방화벽이 성공적으로 활성화되었습니다.", MessageType.Success);
                }
                else
                {
                    throw new Exception("Windows 방화벽 활성화 실패");
                }
            }
            catch (Exception ex)
            {
                if (FirewallStatus is System.Windows.Controls.TextBlock statusBlock)
                {
                    statusBlock.Text = "오류";
                    statusBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                }
                if (FirewallProgress is System.Windows.Controls.ProgressBar progressBar)
                {
                    progressBar.Value = 0;
                }
                firewallRecoveryError = $"오류 코드: 0x{ex.HResult:X8}, 원인: {ex.Message}";
                AddUserFriendlyMessage($"Windows 방화벽 복구 중 오류 발생: {ex.Message}", MessageType.Error);
            }
            finally
            {
                UpdateResultReport();
            }
        }

        // Windows Security Center 복구
        private async Task RecoverSecurityCenter(IProgress<RecoveryProgress> progress = null)
        {
            try
            {
                progress?.Report(new RecoveryProgress { Operation = "Windows Security Center", Status = "진단 중...", Progress = 0 });

                // Windows Security Center 상태 확인
                var status = await CheckSecurityCenterStatus();
                progress?.Report(new RecoveryProgress { Operation = "Windows Security Center", Status = "상태 확인 완료", Progress = 20 });

                // Security Center 서비스 상태 확인 및 시작
                progress?.Report(new RecoveryProgress { Operation = "Windows Security Center", Status = "서비스 확인 중...", Progress = 30 });
                await RunPowerShellCommand(
                    "Get-Service -Name SecurityHealthService | Select-Object -ExpandProperty Status",
                    "Security Center 서비스 상태 확인"
                );

                // Security Center 서비스 시작
                progress?.Report(new RecoveryProgress { Operation = "Windows Security Center", Status = "서비스 시작 중...", Progress = 40 });
                await RunPowerShellCommand(
                    "Start-Service -Name SecurityHealthService; Set-Service -Name SecurityHealthService -StartupType Automatic",
                    "Security Center 서비스 시작"
                );

                // Security Center 정책 복구
                progress?.Report(new RecoveryProgress { Operation = "Windows Security Center", Status = "정책 복구 중...", Progress = 60 });

                // 보안 센터 서비스 재시작
                await RunPowerShellCommand(
                    "Restart-Service -Name SecurityHealthService -Force",
                    "보안 센터 서비스 재시작"
                );
                progress?.Report(new RecoveryProgress { Operation = "Windows Security Center", Status = "서비스 재시작 완료", Progress = 70 });

                // 보안 센터 정책 설정
                await RunPowerShellCommand(
                    "Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System' -Name 'EnableLUA' -Value 1",
                    "보안 센터 정책 설정"
                );
                progress?.Report(new RecoveryProgress { Operation = "Windows Security Center", Status = "정책 설정 완료", Progress = 80 });

                // 보안 센터 알림 설정
                await RunPowerShellCommand(
                    "Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System' -Name 'EnableVirtualization' -Value 1",
                    "보안 센터 알림 설정"
                );
                progress?.Report(new RecoveryProgress { Operation = "Windows Security Center", Status = "알림 설정 완료", Progress = 90 });

                // 최종 상태 확인
                progress?.Report(new RecoveryProgress { Operation = "Windows Security Center", Status = "최종 확인 중...", Progress = 95 });
                var finalStatus = await CheckSecurityCenterStatus();

                if (finalStatus == "활성")
                {
                    progress?.Report(new RecoveryProgress { Operation = "Windows Security Center", Status = "복구 완료", Progress = 100 });
                    if (SecurityCenterStatus is System.Windows.Controls.TextBlock statusBlock)
                    {
                        statusBlock.Text = "정상";
                        statusBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
                    }
                    if (SecurityCenterProgress is System.Windows.Controls.ProgressBar progressBar)
                    {
                        progressBar.Value = 100;
                    }
                    AddUserFriendlyMessage("Windows 보안 센터가 성공적으로 활성화되었습니다.", MessageType.Success);
                }
                else
                {
                    throw new Exception("Windows 보안 센터 활성화 실패");
                }
            }
            catch (Exception ex)
            {
                if (SecurityCenterStatus is System.Windows.Controls.TextBlock statusBlock)
                {
                    statusBlock.Text = "오류";
                    statusBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                }
                if (SecurityCenterProgress is System.Windows.Controls.ProgressBar progressBar)
                {
                    progressBar.Value = 0;
                }
                securityCenterRecoveryError = $"오류 코드: 0x{ex.HResult:X8}, 원인: {ex.Message}";
                AddUserFriendlyMessage($"Windows 보안 센터 복구 중 오류 발생: {ex.Message}", MessageType.Error);
            }
            finally
            {
                UpdateResultReport();
            }
        }

        // BitLocker 복구
        private async Task RecoverBitLocker(IProgress<RecoveryProgress> progress = null)
        {
            try
            {
                // Windows Security Center 상태 확인
                var status = await CheckBitLockerStatus();
                progress?.Report(new RecoveryProgress { Operation = "BitLocker", Status = "상태 확인 완료", Progress = 20 });

                // BitLocker 서비스 상태 확인 및 시작
                progress?.Report(new RecoveryProgress { Operation = "BitLocker", Status = "서비스 확인 중...", Progress = 30 });
                await RunPowerShellCommand(
                    "Get-Service -Name BDESVC | Select-Object -ExpandProperty Status",
                    "BitLocker 서비스 상태 확인"
                );

                // BitLocker 서비스 시작
                progress?.Report(new RecoveryProgress { Operation = "BitLocker", Status = "서비스 시작 중...", Progress = 40 });
                await RunPowerShellCommand(
                    "Start-Service -Name BDESVC; Set-Service -Name BDESVC -StartupType Automatic",
                    "BitLocker 서비스 시작"
                );

                // BitLocker 정책 복구
                progress?.Report(new RecoveryProgress { Operation = "BitLocker", Status = "정책 복구 중...", Progress = 60 });

                // TPM 확인
                await RunPowerShellCommand(
                    "Get-Tpm | Select-Object -ExpandProperty TpmPresent",
                    "TPM 상태 확인"
                );
                progress?.Report(new RecoveryProgress { Operation = "BitLocker", Status = "TPM 확인 완료", Progress = 70 });

                // BitLocker 정책 설정
                await RunPowerShellCommand(
                    "Enable-BitLocker -MountPoint C: -EncryptionMethod Aes256 -UsedSpaceOnly -SkipHardwareTest",
                    "BitLocker 정책 설정"
                );
                progress?.Report(new RecoveryProgress { Operation = "BitLocker", Status = "정책 설정 완료", Progress = 80 });

                // BitLocker 보호기 추가
                await RunPowerShellCommand(
                    "Add-BitLockerKeyProtector -MountPoint C: -RecoveryPasswordProtector",
                    "BitLocker 보호기 추가"
                );
                progress?.Report(new RecoveryProgress { Operation = "BitLocker", Status = "보호기 추가 완료", Progress = 90 });

                // 최종 상태 확인
                progress?.Report(new RecoveryProgress { Operation = "BitLocker", Status = "최종 확인 중...", Progress = 95 });
                var finalStatus = await CheckBitLockerStatus();

                if (finalStatus == "활성")
                {
                    progress?.Report(new RecoveryProgress { Operation = "BitLocker", Status = "복구 완료", Progress = 100 });
                    if (BitLockerStatus is System.Windows.Controls.TextBlock statusBlock)
                    {
                        statusBlock.Text = "정상";
                        statusBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
                    }
                    if (BitLockerProgress is System.Windows.Controls.ProgressBar progressBar)
                    {
                        progressBar.Value = 100;
                    }
                    AddUserFriendlyMessage("BitLocker가 성공적으로 활성화되었습니다.", MessageType.Success);
                }
                else
                {
                    throw new Exception("BitLocker 활성화 실패");
                }
            }
            catch (Exception ex)
            {
                if (BitLockerStatus is System.Windows.Controls.TextBlock statusBlock)
                {
                    statusBlock.Text = "오류";
                    statusBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                }
                if (BitLockerProgress is System.Windows.Controls.ProgressBar progressBar)
                {
                    progressBar.Value = 0;
                }
                bitLockerRecoveryError = $"오류 코드: 0x{ex.HResult:X8}, 원인: {ex.Message}";
                AddUserFriendlyMessage($"BitLocker 복구 중 오류 발생: {ex.Message}", MessageType.Error);
            }
            finally
            {
                UpdateResultReport();
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

            // UI 스레드에서 ResultReport 업데이트
            Dispatcher.Invoke(() => {
                if (ResultReport != null)
                {
                    ResultReport.Text = report.ToString();
                }
            });
        }

        // 사용자 친화적인 메시지 추가
        private void AddUserFriendlyMessage(string message, MessageType type = MessageType.Info)
        {
            var messageBlock = new System.Windows.Controls.TextBlock
            {
                Text = message,
                Foreground = GetMessageColor(type),
                FontSize = 14,
                Margin = new System.Windows.Thickness(0, 0, 0, 10),
                TextWrapping = System.Windows.TextWrapping.Wrap
            };

            if (UserFriendlyOutput is System.Windows.Controls.Panel panel)
            {
                panel.Children.Add(messageBlock);
            }
            if (UserFriendlyOutputScroll is System.Windows.Controls.ScrollViewer scrollViewer)
            {
                scrollViewer.ScrollToBottom();
            }
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
            if (OperationProgress is System.Windows.Controls.ProgressBar progressBar)
            {
                progressBar.Value = progress;
            }
            StatusText.Text = $"{progress:F0}% 완료";
            TimestampText.Text = DateTime.Now.ToString("HH:mm:ss");
        }

        // PowerShell 출력을 표시하는 메서드 개선
        private void ShowPowerShellOutput(string output, bool isUserFriendly = true)
        {
            PowerShellOutputBorder.Visibility = System.Windows.Visibility.Visible;

            if (isUserFriendly)
            {
                // 사용자 친화적인 메시지로 변환
                var message = ConvertToUserFriendlyMessage(output);
                AddUserFriendlyMessage(message);
            }
            else
            {
                // 고급 모드 출력
                if (PowerShellOutput is System.Windows.Controls.TextBlock textBlock)
                {
                    textBlock.Text = output;
                }
                if (PowerShellOutputScroll is System.Windows.Controls.ScrollViewer scrollViewer)
                {
                    scrollViewer.ScrollToBottom();
                }
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
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"chcp 65001; [Console]::OutputEncoding = [System.Text.Encoding]::UTF8; {command}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        AddUserFriendlyMessage("프로세스를 시작할 수 없습니다.", MessageType.Error);
                        return false;
                    }

                    var output = new StringBuilder();
                    var error = new StringBuilder();

                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            output.AppendLine(e.Data);
                            if (showOutput)
                            {
                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
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
                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
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
                    var progressTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
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

        #region Sidebar Navigation
        

        [SupportedOSPlatform("windows")]
        private void SidebarButton_Click(object sender, RoutedEventArgs e)
        {
            var clicked = sender as ToggleButton;
            if (clicked == null) return;

            // 이전 선택 해제
            if (_selectedButton != null && _selectedButton != clicked)
                _selectedButton.IsChecked = false;

            // 선택 상태 유지
            clicked.IsChecked = true;
            _selectedButton = clicked;

            switch (clicked.CommandParameter?.ToString())
            {
                case "Vaccine":
                    NavigateToPage(new Vaccine());
                    break;
                case "NetWorks":
                    NavigateToPage(new NetWorks());
                    break;
                case "ProgramsList":
                    NavigateToPage(new ProgramsList());
                    break;
                case "Recoverys":
                    NavigateToPage(new Recoverys());
                    break;
                case "Logs":
                    NavigateToPage(new Logs());
                    break;
            }
        }

        [SupportedOSPlatform("windows")]
        private void NavigateToPage(Page page)
        {
            var mainWindow = Window.GetWindow(this) as MainWindows;
            mainWindow?.NavigateToPage(page);
        }
        #endregion

        // 메시지 타입 열거형
        private enum MessageType
        {
            Info,
            Success,
            Warning,
            Error
        }

        public class RecoverySecurityStatusItem
        {
            public required string Icon { get; set; }
            public required string Title { get; set; }
            public required string Description { get; set; }
            public required string Status { get; set; }
        }
    }
}