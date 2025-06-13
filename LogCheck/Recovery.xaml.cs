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

namespace LogCheck
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
<<<<<<< HEAD
        private Button _startRecoveryButton;
=======
>>>>>>> origin/main

        // 복구 상태 필드
        private TimeSpan _defenderRecoveryTime;
        private TimeSpan _firewallRecoveryTime;
        private TimeSpan _securityCenterRecoveryTime;
        private TimeSpan _bitLockerRecoveryTime;
        private string _defenderRecoveryErrorMessage;
        private string _firewallRecoveryErrorMessage;
        private string _securityCenterRecoveryErrorMessage;
        private string _bitLockerRecoveryErrorMessage;
<<<<<<< HEAD

=======
        private CancellationTokenSource _recoveryCancellationTokenSource;

        // 보안 상태 항목
        private SecurityStatusItem _defenderStatus;
        private SecurityStatusItem _firewallStatus;
        private SecurityStatusItem _securityCenterStatus;
        private SecurityStatusItem _bitLockerStatus;

        private enum MessageType
        {
            Info,
            Success,
            Warning,
            Error
        }

        private class SecurityStatusItem
        {
            public string Name { get; set; }
            public string Status { get; set; }
            public string ErrorMessage { get; set; }
            public TimeSpan RecoveryTime { get; set; }
            public bool IsRecovered { get; set; }
        }

        private class RecoveryProgress
        {
            public string Operation { get; set; }
            public string Status { get; set; }
            public int Progress { get; set; }
        }

        [SupportedOSPlatform("windows")]
>>>>>>> origin/main
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

<<<<<<< HEAD
            if (_startRecoveryButton != null)
            {
                _startRecoveryButton.IsEnabled = false;
            }
=======
            // 로딩 텍스트 타이머 설정
            loadingTextTimer.Interval = TimeSpan.FromMilliseconds(500);
            loadingTextTimer.Tick += LoadingTextTimer_Tick;

            // 고급 모드 토글 이벤트 핸들러
            AdvancedModeToggle.Checked += (s, e) => AdvancedOutputBorder.Visibility = Visibility.Visible;
            AdvancedModeToggle.Unchecked += (s, e) => AdvancedOutputBorder.Visibility = Visibility.Collapsed;

            // 취소 토큰 소스 초기화
            _recoveryCancellationTokenSource = new CancellationTokenSource();

            // 초기 보안 상태 로드
            _ = LoadSecurityStatus();

            // 페이지 언로드 이벤트 핸들러 등록
            this.Unloaded += (s, e) =>
            {
                _recoveryCancellationTokenSource?.Cancel();
                _recoveryCancellationTokenSource?.Dispose();
                _recoveryCancellationTokenSource = null;
            };

            // UI 요소 참조 초기화
            _defenderStatusText = DefenderStatusText;
            _firewallStatusText = FirewallStatusText;
            _securityCenterStatusText = SecurityCenterStatusText;
            _bitLockerStatusText = BitLockerStatusText;
            _defenderProgressBar = DefenderProgressBar;
            _firewallProgressBar = FirewallProgressBar;
            _securityCenterProgressBar = SecurityCenterProgressBar;
            _bitLockerProgressBar = BitLockerProgressBar;

            // 상태 초기화
            _defenderStatus = new SecurityStatusItem { Name = "Windows Defender", Status = "대기 중" };
            _firewallStatus = new SecurityStatusItem { Name = "Windows Firewall", Status = "대기 중" };
            _securityCenterStatus = new SecurityStatusItem { Name = "Windows Security Center", Status = "대기 중" };
            _bitLockerStatus = new SecurityStatusItem { Name = "BitLocker", Status = "대기 중" };
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
>>>>>>> origin/main
            loadingTextTimer.Start();

            try
            {
<<<<<<< HEAD
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
=======
                if (_recoveryCancellationTokenSource == null)
                {
                    _recoveryCancellationTokenSource = new CancellationTokenSource();
>>>>>>> origin/main
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
<<<<<<< HEAD
                        FileName = "powershell.exe",
                        Arguments = "-Command \"Set-MpPreference -DisableRealtimeMonitoring $false; Start-MpScan -ScanType QuickScan\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                        Verb = "runas"
=======
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
                await Dispatcher.InvokeAsync(() =>
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
                await Dispatcher.InvokeAsync(() =>
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
                await Dispatcher.InvokeAsync(() =>
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
                ResultReport.Text = "복구 작업이 시작되었습니다. 각 항목별 진행 상태를 확인하세요.";

                // 복구 상태 초기화
                ResetStatus();
                
                // 진행 상태를 추적하기 위한 Progress 객체 생성
                var progress = new Progress<RecoveryProgress>(p =>
                {
                    switch (p.Operation)
                    {
                        case "Windows Defender":
                            _defenderStatusText.Text = p.Status;
                            _defenderProgressBar.Value = p.Progress;
                            break;
                        case "Windows Firewall":
                            _firewallStatusText.Text = p.Status;
                            _firewallProgressBar.Value = p.Progress;
                            break;
                        case "Windows Security Center":
                            _securityCenterStatusText.Text = p.Status;
                            _securityCenterProgressBar.Value = p.Progress;
                            break;
                        case "BitLocker":
                            _bitLockerStatusText.Text = p.Status;
                            _bitLockerProgressBar.Value = p.Progress;
                            break;
>>>>>>> origin/main
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

<<<<<<< HEAD
                if (process.ExitCode == 0)
                {
                    UpdateStatusText(_defenderStatusText, "완료");
                    UpdateProgressBar(_defenderProgressBar, 100);
=======
                // Windows Firewall 복구 실행
                await RecoverFirewall(progress);

                // Windows Security Center 복구 실행
                await RecoverSecurityCenter(progress);

                // BitLocker 복구 실행 (필요시)
                await RecoverBitLocker(progress);

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
                _ = LoadSecurityStatus();
            }
        }

        private void ResetStatus()
        {
            // Windows Defender
            _defenderStatusText.Text = "대기 중";
            _defenderProgressBar.Value = 0;
            _defenderRecoveryTime = TimeSpan.Zero;
            _defenderRecoveryErrorMessage = null;

            // Windows Firewall
            _firewallStatusText.Text = "대기 중";
            _firewallProgressBar.Value = 0;
            _firewallRecoveryTime = TimeSpan.Zero;
            _firewallRecoveryErrorMessage = null;

            // Windows Security Center
            _securityCenterStatusText.Text = "대기 중";
            _securityCenterProgressBar.Value = 0;
            _securityCenterRecoveryTime = TimeSpan.Zero;
            _securityCenterRecoveryErrorMessage = null;

            // BitLocker
            _bitLockerStatusText.Text = "대기 중";
            _bitLockerProgressBar.Value = 0;
            _bitLockerRecoveryTime = TimeSpan.Zero;
            _bitLockerRecoveryErrorMessage = null;
        }

        // Windows Defender 복구
        [SupportedOSPlatform("windows")]
        private async Task RecoverDefender(IProgress<RecoveryProgress> progress)
        {
            try
            {
                _defenderStatusText.Text = "복구 중...";
                _defenderProgressBar.Value = 0;
                _defenderStatusText.Foreground = new SolidColorBrush(Colors.Orange);
                AddUserFriendlyMessage("Windows Defender 복구를 시작합니다...", MessageType.Info);

                // Windows Defender 상태 확인
                var defenderStatus = await CheckDefenderStatus();
                progress.Report(new RecoveryProgress { Operation = "Windows Defender", Status = "상태 확인 완료", Progress = 20 });

                // Defender 서비스 상태 확인 및 시작
                progress.Report(new RecoveryProgress { Operation = "Windows Defender", Status = "서비스 확인 중...", Progress = 30 });
                await RunPowerShellCommand(
                    "Get-Service -Name WinDefend | Select-Object -ExpandProperty Status",
                    "Defender 서비스 상태 확인"
                );

                // Defender 서비스 시작
                progress.Report(new RecoveryProgress { Operation = "Windows Defender", Status = "서비스 시작 중...", Progress = 40 });
                await RunPowerShellCommand(
                    "Start-Service -Name WinDefend; Set-Service -Name WinDefend -StartupType Automatic",
                    "Defender 서비스 시작"
                );

                // Defender 정책 복구
                progress.Report(new RecoveryProgress { Operation = "Windows Defender", Status = "정책 복구 중...", Progress = 60 });
                
                // 실시간 보호 활성화
                await RunPowerShellCommand(
                    "Set-MpPreference -DisableRealtimeMonitoring $false",
                    "실시간 보호 활성화"
                );
                progress.Report(new RecoveryProgress { Operation = "Windows Defender", Status = "실시간 보호 활성화 완료", Progress = 70 });

                // IOAV 보호 활성화
                await RunPowerShellCommand(
                    "Set-MpPreference -DisableIOAVProtection $false",
                    "IOAV 보호 활성화"
                );
                progress.Report(new RecoveryProgress { Operation = "Windows Defender", Status = "IOAV 보호 활성화 완료", Progress = 80 });

                // 행동 모니터링 활성화
                await RunPowerShellCommand(
                    "Set-MpPreference -DisableBehaviorMonitoring $false",
                    "행동 모니터링 활성화"
                );
                progress.Report(new RecoveryProgress { Operation = "Windows Defender", Status = "행동 모니터링 활성화 완료", Progress = 90 });

                // 최종 상태 확인
                progress.Report(new RecoveryProgress { Operation = "Windows Defender", Status = "최종 확인 중...", Progress = 95 });
                var finalStatus = await CheckDefenderStatus();
                
                if (finalStatus == "활성")
                {
                    progress.Report(new RecoveryProgress { Operation = "Windows Defender", Status = "복구 완료", Progress = 100 });
                    _defenderStatusText.Text = "정상";
                    _defenderProgressBar.Value = 100;
                    _defenderStatusText.Foreground = new SolidColorBrush(Colors.Green);
                    _defenderRecoveryTime = TimeSpan.FromSeconds(5); // 실제 복구 시간으로 대체
                    AddUserFriendlyMessage("Windows Defender가 성공적으로 복구되었습니다.", MessageType.Success);
>>>>>>> origin/main
                }
                else
                {
                    throw new Exception($"Windows Defender 복구 실패 (종료 코드: {process.ExitCode})");
                }
            }
            catch (Exception ex)
            {
<<<<<<< HEAD
                _defenderRecoveryErrorMessage = ex.Message;
                UpdateStatusText(_defenderStatusText, "실패");
                UpdateProgressBar(_defenderProgressBar, 0);
=======
                _defenderStatusText.Text = "오류";
                _defenderProgressBar.Value = 0;
                _defenderStatusText.Foreground = new SolidColorBrush(Colors.Red);
                _defenderRecoveryErrorMessage = $"오류 코드: 0x{ex.HResult:X8}, 원인: {ex.Message}";
                AddUserFriendlyMessage($"Windows Defender 복구 중 오류가 발생했습니다: {ex.Message}", MessageType.Error);
>>>>>>> origin/main
            }
            finally
            {
                _defenderRecoveryTime = DateTime.Now - startTime;
            }
        }

<<<<<<< HEAD
        private async Task RecoverWindowsFirewall()
=======
        // Windows Firewall 복구
        private async Task RecoverFirewall(IProgress<RecoveryProgress> progress)
>>>>>>> origin/main
        {
            var startTime = DateTime.Now;
            try
            {
<<<<<<< HEAD
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
=======
                _firewallStatusText.Text = "복구 중...";
                _firewallProgressBar.Value = 0;
                _firewallStatusText.Foreground = new SolidColorBrush(Colors.Orange);
                AddUserFriendlyMessage("Windows 방화벽 복구를 시작합니다...", MessageType.Info);

                // Windows Firewall 상태 확인
                var firewallStatus = await CheckFirewallStatus();
                progress.Report(new RecoveryProgress { Operation = "Windows Firewall", Status = "상태 확인 완료", Progress = 20 });

                // Firewall 서비스 상태 확인 및 시작
                progress.Report(new RecoveryProgress { Operation = "Windows Firewall", Status = "서비스 확인 중...", Progress = 30 });
                await RunPowerShellCommand(
                    "Get-Service -Name MpsSvc | Select-Object -ExpandProperty Status",
                    "Firewall 서비스 상태 확인"
                );

                // Firewall 서비스 시작
                progress.Report(new RecoveryProgress { Operation = "Windows Firewall", Status = "서비스 시작 중...", Progress = 40 });
                await RunPowerShellCommand(
                    "Start-Service -Name MpsSvc; Set-Service -Name MpsSvc -StartupType Automatic",
                    "Firewall 서비스 시작"
                );

                // Firewall 정책 복구
                progress.Report(new RecoveryProgress { Operation = "Windows Firewall", Status = "정책 복구 중...", Progress = 60 });
                
                // 각 프로필별로 개별적으로 활성화
                await RunPowerShellCommand(
                    "Set-NetFirewallProfile -Profile Domain -Enabled True",
                    "도메인 프로필 방화벽 활성화"
                );
                progress.Report(new RecoveryProgress { Operation = "Windows Firewall", Status = "도메인 프로필 활성화 완료", Progress = 70 });

                await RunPowerShellCommand(
                    "Set-NetFirewallProfile -Profile Private -Enabled True",
                    "개인 프로필 방화벽 활성화"
                );
                progress.Report(new RecoveryProgress { Operation = "Windows Firewall", Status = "개인 프로필 활성화 완료", Progress = 80 });

                await RunPowerShellCommand(
                    "Set-NetFirewallProfile -Profile Public -Enabled True",
                    "공용 프로필 방화벽 활성화"
                );
                progress.Report(new RecoveryProgress { Operation = "Windows Firewall", Status = "공용 프로필 활성화 완료", Progress = 90 });

                // 최종 상태 확인
                progress.Report(new RecoveryProgress { Operation = "Windows Firewall", Status = "최종 확인 중...", Progress = 95 });
                var finalStatus = await CheckFirewallStatus();
                
                if (finalStatus == "활성")
                {
                    progress.Report(new RecoveryProgress { Operation = "Windows Firewall", Status = "복구 완료", Progress = 100 });
                    _firewallStatusText.Text = "정상";
                    _firewallProgressBar.Value = 100;
                    _firewallStatusText.Foreground = new SolidColorBrush(Colors.Green);
                    _firewallRecoveryTime = TimeSpan.FromSeconds(5); // 실제 복구 시간으로 대체
                    AddUserFriendlyMessage("Windows 방화벽이 성공적으로 복구되었습니다.", MessageType.Success);
>>>>>>> origin/main
                }
                else
                {
                    throw new Exception($"Windows Firewall 복구 실패 (종료 코드: {process.ExitCode})");
                }
            }
            catch (Exception ex)
            {
<<<<<<< HEAD
                _firewallRecoveryErrorMessage = ex.Message;
                UpdateStatusText(_firewallStatusText, "실패");
                UpdateProgressBar(_firewallProgressBar, 0);
=======
                _firewallStatusText.Text = "오류";
                _firewallProgressBar.Value = 0;
                _firewallStatusText.Foreground = new SolidColorBrush(Colors.Red);
                _firewallRecoveryErrorMessage = $"오류 코드: 0x{ex.HResult:X8}, 원인: {ex.Message}";
                AddUserFriendlyMessage($"Windows 방화벽 복구 중 오류가 발생했습니다: {ex.Message}", MessageType.Error);
>>>>>>> origin/main
            }
            finally
            {
                _firewallRecoveryTime = DateTime.Now - startTime;
            }
        }

<<<<<<< HEAD
        private async Task RecoverSecurityCenter()
=======
        // Windows Security Center 복구
        private async Task RecoverSecurityCenter(IProgress<RecoveryProgress> progress)
>>>>>>> origin/main
        {
            var startTime = DateTime.Now;
            try
            {
<<<<<<< HEAD
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
=======
                _securityCenterStatusText.Text = "복구 중...";
                _securityCenterProgressBar.Value = 0;
                _securityCenterStatusText.Foreground = new SolidColorBrush(Colors.Orange);
                AddUserFriendlyMessage("Windows 보안 센터 복구를 시작합니다...", MessageType.Info);

                // Windows Security Center 상태 확인
                var securityCenterStatus = await CheckSecurityCenterStatus();
                progress.Report(new RecoveryProgress { Operation = "Windows Security Center", Status = "상태 확인 완료", Progress = 20 });

                // Security Center 서비스 상태 확인 및 시작
                progress.Report(new RecoveryProgress { Operation = "Windows Security Center", Status = "서비스 확인 중...", Progress = 30 });
                await RunPowerShellCommand(
                    "Get-Service -Name SecurityHealthService | Select-Object -ExpandProperty Status",
                    "Security Center 서비스 상태 확인"
                );

                // Security Center 서비스 시작
                progress.Report(new RecoveryProgress { Operation = "Windows Security Center", Status = "서비스 시작 중...", Progress = 40 });
                await RunPowerShellCommand(
                    "Start-Service -Name SecurityHealthService; Set-Service -Name SecurityHealthService -StartupType Automatic",
                    "Security Center 서비스 시작"
                );

                // Security Center 정책 복구
                progress.Report(new RecoveryProgress { Operation = "Windows Security Center", Status = "정책 복구 중...", Progress = 60 });
                
                // 보안 센터 서비스 재시작
                await RunPowerShellCommand(
                    "Restart-Service -Name SecurityHealthService -Force",
                    "보안 센터 서비스 재시작"
                );
                progress.Report(new RecoveryProgress { Operation = "Windows Security Center", Status = "서비스 재시작 완료", Progress = 70 });

                // 보안 센터 정책 설정
                await RunPowerShellCommand(
                    "Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System' -Name 'EnableLUA' -Value 1",
                    "보안 센터 정책 설정"
                );
                progress.Report(new RecoveryProgress { Operation = "Windows Security Center", Status = "정책 설정 완료", Progress = 80 });

                // 보안 센터 알림 설정
                await RunPowerShellCommand(
                    "Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System' -Name 'EnableVirtualization' -Value 1",
                    "보안 센터 알림 설정"
                );
                progress.Report(new RecoveryProgress { Operation = "Windows Security Center", Status = "알림 설정 완료", Progress = 90 });

                // 최종 상태 확인
                progress.Report(new RecoveryProgress { Operation = "Windows Security Center", Status = "최종 확인 중...", Progress = 95 });
                var finalStatus = await CheckSecurityCenterStatus();
                
                if (finalStatus == "활성")
                {
                    progress.Report(new RecoveryProgress { Operation = "Windows Security Center", Status = "복구 완료", Progress = 100 });
                    _securityCenterStatusText.Text = "정상";
                    _securityCenterProgressBar.Value = 100;
                    _securityCenterStatusText.Foreground = new SolidColorBrush(Colors.Green);
                    _securityCenterRecoveryTime = TimeSpan.FromSeconds(5); // 실제 복구 시간으로 대체
                    AddUserFriendlyMessage("Windows 보안 센터가 성공적으로 복구되었습니다.", MessageType.Success);
>>>>>>> origin/main
                }
                else
                {
                    throw new Exception($"Security Center 복구 실패 (종료 코드: {process.ExitCode})");
                }
            }
            catch (Exception ex)
            {
<<<<<<< HEAD
                _securityCenterRecoveryErrorMessage = ex.Message;
                UpdateStatusText(_securityCenterStatusText, "실패");
                UpdateProgressBar(_securityCenterProgressBar, 0);
=======
                _securityCenterStatusText.Text = "오류";
                _securityCenterProgressBar.Value = 0;
                _securityCenterStatusText.Foreground = new SolidColorBrush(Colors.Red);
                _securityCenterRecoveryErrorMessage = $"오류 코드: 0x{ex.HResult:X8}, 원인: {ex.Message}";
                AddUserFriendlyMessage($"Windows 보안 센터 복구 중 오류가 발생했습니다: {ex.Message}", MessageType.Error);
>>>>>>> origin/main
            }
            finally
            {
                _securityCenterRecoveryTime = DateTime.Now - startTime;
            }
        }

<<<<<<< HEAD
        private async Task RecoverBitLocker()
=======
        // BitLocker 복구
        private async Task RecoverBitLocker(IProgress<RecoveryProgress> progress)
>>>>>>> origin/main
        {
            var startTime = DateTime.Now;
            try
            {
<<<<<<< HEAD
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
=======
                _bitLockerStatusText.Text = "복구 중...";
                _bitLockerProgressBar.Value = 0;
                _bitLockerStatusText.Foreground = new SolidColorBrush(Colors.Orange);
                AddUserFriendlyMessage("BitLocker 복구를 시작합니다...", MessageType.Info);

                // BitLocker 상태 확인
                var bitLockerStatus = await CheckBitLockerStatus();
                progress.Report(new RecoveryProgress { Operation = "BitLocker", Status = "상태 확인 완료", Progress = 20 });

                // BitLocker 서비스 상태 확인 및 시작
                progress.Report(new RecoveryProgress { Operation = "BitLocker", Status = "서비스 확인 중...", Progress = 30 });
                await RunPowerShellCommand(
                    "Get-Service -Name BDESVC | Select-Object -ExpandProperty Status",
                    "BitLocker 서비스 상태 확인"
                );

                // BitLocker 서비스 시작
                progress.Report(new RecoveryProgress { Operation = "BitLocker", Status = "서비스 시작 중...", Progress = 40 });
                await RunPowerShellCommand(
                    "Start-Service -Name BDESVC; Set-Service -Name BDESVC -StartupType Automatic",
                    "BitLocker 서비스 시작"
                );

                // BitLocker 정책 복구
                progress.Report(new RecoveryProgress { Operation = "BitLocker", Status = "정책 복구 중...", Progress = 60 });
                
                // TPM 확인
                await RunPowerShellCommand(
                    "Get-Tpm | Select-Object -ExpandProperty TpmPresent",
                    "TPM 상태 확인"
                );
                progress.Report(new RecoveryProgress { Operation = "BitLocker", Status = "TPM 확인 완료", Progress = 70 });

                // BitLocker 정책 설정
                await RunPowerShellCommand(
                    "Enable-BitLocker -MountPoint C: -EncryptionMethod Aes256 -UsedSpaceOnly -SkipHardwareTest",
                    "BitLocker 정책 설정"
                );
                progress.Report(new RecoveryProgress { Operation = "BitLocker", Status = "정책 설정 완료", Progress = 80 });

                // BitLocker 보호기 추가
                await RunPowerShellCommand(
                    "Add-BitLockerKeyProtector -MountPoint C: -RecoveryPasswordProtector",
                    "BitLocker 보호기 추가"
                );
                progress.Report(new RecoveryProgress { Operation = "BitLocker", Status = "보호기 추가 완료", Progress = 90 });

                // 최종 상태 확인
                progress.Report(new RecoveryProgress { Operation = "BitLocker", Status = "최종 확인 중...", Progress = 95 });
                var finalStatus = await CheckBitLockerStatus();
                
                if (finalStatus == "활성")
                {
                    progress.Report(new RecoveryProgress { Operation = "BitLocker", Status = "복구 완료", Progress = 100 });
                    _bitLockerStatusText.Text = "정상";
                    _bitLockerProgressBar.Value = 100;
                    _bitLockerStatusText.Foreground = new SolidColorBrush(Colors.Green);
                    _bitLockerRecoveryTime = TimeSpan.FromSeconds(5); // 실제 복구 시간으로 대체
                    AddUserFriendlyMessage("BitLocker가 성공적으로 복구되었습니다.", MessageType.Success);
>>>>>>> origin/main
                }
                else
                {
                    throw new Exception($"BitLocker 복구 실패 (종료 코드: {process.ExitCode})");
                }
            }
            catch (Exception ex)
            {
<<<<<<< HEAD
                _bitLockerRecoveryErrorMessage = ex.Message;
                UpdateStatusText(_bitLockerStatusText, "실패");
                UpdateProgressBar(_bitLockerProgressBar, 0);
=======
                _bitLockerStatusText.Text = "오류";
                _bitLockerProgressBar.Value = 0;
                _bitLockerStatusText.Foreground = new SolidColorBrush(Colors.Red);
                _bitLockerRecoveryErrorMessage = $"오류 코드: 0x{ex.HResult:X8}, 원인: {ex.Message}";
                AddUserFriendlyMessage($"BitLocker 복구 중 오류가 발생했습니다: {ex.Message}", MessageType.Error);
>>>>>>> origin/main
            }
            finally
            {
                _bitLockerRecoveryTime = DateTime.Now - startTime;
            }
        }

        private void GenerateReport_Click(object sender, RoutedEventArgs e)
        {
<<<<<<< HEAD
            var report = new StringBuilder();
            report.AppendLine("=== Windows 보안 복구 보고서 ===");
            report.AppendLine($"생성 시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
=======
            _defenderRecoveryTime = TimeSpan.Zero;
            _firewallRecoveryTime = TimeSpan.Zero;
            _securityCenterRecoveryTime = TimeSpan.Zero;
            _bitLockerRecoveryTime = TimeSpan.Zero;

            _defenderRecoveryErrorMessage = null;
            _firewallRecoveryErrorMessage = null;
            _securityCenterRecoveryErrorMessage = null;
            _bitLockerRecoveryErrorMessage = null;

            ResultReport.Text = "아직 복구가 시작되지 않았습니다.";
            StatusText.Text = "준비됨";
            TimestampText.Text = DateTime.Now.ToString("HH:mm:ss");

            ResetStatus(); // 기존 진행 상태 초기화 메서드 호출
        }

        // 복구 결과 보고서 업데이트 메서드 수정
        private void UpdateResultReport()
        {
            var report = new System.Text.StringBuilder();

            // 전체 복구 작업 요약 추가
            bool overallSuccess = _defenderStatusText.Text == "완료" &&
                                  _firewallStatusText.Text == "완료" &&
                                  _securityCenterStatusText.Text == "완료" &&
                                  _bitLockerStatusText.Text == "완료";

            report.AppendLine("=== 전체 복구 결과 요약 ===");
            report.AppendLine(overallSuccess ? "모든 보안 프로그램 복구가 성공적으로 완료되었습니다." : "일부 보안 프로그램 복구 중 문제가 발생했습니다.");
            report.AppendLine("==========================");
>>>>>>> origin/main
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
<<<<<<< HEAD
                report.AppendLine("  - BitLocker 서비스 재개 완료");
=======
                report.AppendLine("  - 드라이브 암호화 활성화");
>>>>>>> origin/main
            else if (!string.IsNullOrEmpty(_bitLockerRecoveryErrorMessage))
            {
                report.AppendLine($"  오류 상세: {_bitLockerRecoveryErrorMessage}");
            }

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
<<<<<<< HEAD
                Filter = "텍스트 파일 (*.txt)|*.txt",
                FileName = $"Windows 보안 복구 보고서_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                File.WriteAllText(saveFileDialog.FileName, report.ToString());
                MessageBox.Show("보고서가 성공적으로 저장되었습니다.", "저장 완료", MessageBoxButton.OK, MessageBoxImage.Information);
=======
                Text = message,
                Foreground = GetMessageColor(type),
                Margin = new Thickness(0, 5, 0, 5),
                TextWrapping = TextWrapping.Wrap
            };

            AdvancedOutputPanel.Children.Add(messageBlock);
            AdvancedOutputScrollViewer.ScrollToBottom();
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
            Dispatcher.Invoke(() =>
            {
                switch (operation)
                {
                    case "Windows Defender":
                        _defenderProgressBar.Value = progress;
                        break;
                    case "Windows Firewall":
                        _firewallProgressBar.Value = progress;
                        break;
                    case "Windows Security Center":
                        _securityCenterProgressBar.Value = progress;
                        break;
                    case "BitLocker":
                        _bitLockerProgressBar.Value = progress;
                        break;
                }
            });
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
>>>>>>> origin/main
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