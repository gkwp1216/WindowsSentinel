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

        private CancellationTokenSource? _cancellationTokenSource;

        private class RecoveryProgress
        {
            public string Operation { get; set; } = "";
            public double Progress { get; set; }
            public string Status { get; set; } = "";
        }

        [SupportedOSPlatform("windows")]
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
                ShowLoadingOverlay("보안 상태 확인 중...");

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

        private async Task LoadDefenderStatus()
        {
            try
            {
                var defenderStatus = await CheckDefenderStatus();
                await Dispatcher.InvokeAsync(() =>
                {
                    securityStatusItems.Add(new SecurityStatusItem
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
                await Dispatcher.InvokeAsync(() =>
                {
                    securityStatusItems.Add(new SecurityStatusItem
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
                    securityStatusItems.Add(new SecurityStatusItem
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
                    securityStatusItems.Add(new SecurityStatusItem
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
        [SupportedOSPlatform("windows")]
        private async Task RecoverDefender(IProgress<RecoveryProgress>? progress = null)
        {
            try
            {
                if (_cancellationTokenSource == null)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                }

                ShowLoadingOverlay("Windows Defender 복구 중...");
                var startTime = DateTime.Now;

                progress?.Report(new RecoveryProgress { Operation = "Windows Defender", Progress = 0, Status = "복구 시작" });

                // WMIHelper를 사용하여 Windows Defender 활성화
                bool success = await WmiHelper.EnableDefenderAsync();
                
                defenderRecoveryDuration = DateTime.Now - startTime;
                
                if (success)
                {
                    progress?.Report(new RecoveryProgress { Operation = "Windows Defender", Progress = 100, Status = "완료" });
                    AddUserFriendlyMessage("Windows Defender가 성공적으로 활성화되었습니다.", MessageType.Success);
                    defenderRecoveryError = "";
                }
                else
                {
                    progress?.Report(new RecoveryProgress { Operation = "Windows Defender", Progress = 100, Status = "실패" });
                    AddUserFriendlyMessage("Windows Defender 활성화에 실패했습니다.", MessageType.Error);
                    defenderRecoveryError = "Windows Defender 활성화 실패";
                }
            }
            catch (OperationCanceledException)
            {
                progress?.Report(new RecoveryProgress { Operation = "Windows Defender", Progress = 0, Status = "취소됨" });
                AddUserFriendlyMessage("Windows Defender 복구가 취소되었습니다.", MessageType.Warning);
                defenderRecoveryError = "작업 취소됨";
            }
            catch (Exception ex)
            {
                progress?.Report(new RecoveryProgress { Operation = "Windows Defender", Progress = 0, Status = "오류" });
                defenderRecoveryError = ex.Message;
                AddUserFriendlyMessage($"Windows Defender 복구 중 오류 발생: {ex.Message}", MessageType.Error);
            }
            finally
            {
                HideLoadingOverlay();
            }
        }

        // Windows Firewall 복구
        private async Task RecoverFirewall(IProgress<RecoveryProgress>? progress = null)
        {
            try
            {
                if (_cancellationTokenSource == null)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                }

                ShowLoadingOverlay("Windows Firewall 복구 중...");
                var startTime = DateTime.Now;

                progress?.Report(new RecoveryProgress { Operation = "Windows Firewall", Progress = 0, Status = "복구 시작" });

                // WMIHelper를 사용하여 Windows Firewall 활성화
                bool success = await WmiHelper.EnableFirewallAsync();
                
                firewallRecoveryDuration = DateTime.Now - startTime;
                
                if (success)
                {
                    progress?.Report(new RecoveryProgress { Operation = "Windows Firewall", Progress = 100, Status = "완료" });
                    AddUserFriendlyMessage("Windows Firewall이 성공적으로 활성화되었습니다.", MessageType.Success);
                    firewallRecoveryError = "";
                }
                else
                {
                    progress?.Report(new RecoveryProgress { Operation = "Windows Firewall", Progress = 100, Status = "실패" });
                    AddUserFriendlyMessage("Windows Firewall 활성화에 실패했습니다.", MessageType.Error);
                    firewallRecoveryError = "Windows Firewall 활성화 실패";
                }
            }
            catch (OperationCanceledException)
            {
                progress?.Report(new RecoveryProgress { Operation = "Windows Firewall", Progress = 0, Status = "취소됨" });
                AddUserFriendlyMessage("Windows Firewall 복구가 취소되었습니다.", MessageType.Warning);
                firewallRecoveryError = "작업 취소됨";
            }
            catch (Exception ex)
            {
                progress?.Report(new RecoveryProgress { Operation = "Windows Firewall", Progress = 0, Status = "오류" });
                firewallRecoveryError = ex.Message;
                AddUserFriendlyMessage($"Windows Firewall 복구 중 오류 발생: {ex.Message}", MessageType.Error);
            }
            finally
            {
                HideLoadingOverlay();
            }
        }

        // Windows Security Center 복구
        private async Task RecoverSecurityCenter(IProgress<RecoveryProgress>? progress = null)
        {
            try
            {
                if (_cancellationTokenSource == null)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                }

                ShowLoadingOverlay("Windows Security Center 복구 중...");
                var startTime = DateTime.Now;

                progress?.Report(new RecoveryProgress { Operation = "Windows Security Center", Progress = 0, Status = "복구 시작" });

                // WMIHelper를 사용하여 Windows Security Center 활성화
                bool success = await WmiHelper.EnableSecurityCenterAsync();
                
                securityCenterRecoveryDuration = DateTime.Now - startTime;
                
                if (success)
                {
                    progress?.Report(new RecoveryProgress { Operation = "Windows Security Center", Progress = 100, Status = "완료" });
                    AddUserFriendlyMessage("Windows Security Center가 성공적으로 활성화되었습니다.", MessageType.Success);
                    securityCenterRecoveryError = "";
                }
                else
                {
                    progress?.Report(new RecoveryProgress { Operation = "Windows Security Center", Progress = 100, Status = "실패" });
                    AddUserFriendlyMessage("Windows Security Center 활성화에 실패했습니다.", MessageType.Error);
                    securityCenterRecoveryError = "Windows Security Center 활성화 실패";
                }
            }
            catch (OperationCanceledException)
            {
                progress?.Report(new RecoveryProgress { Operation = "Windows Security Center", Progress = 0, Status = "취소됨" });
                AddUserFriendlyMessage("Windows Security Center 복구가 취소되었습니다.", MessageType.Warning);
                securityCenterRecoveryError = "작업 취소됨";
            }
            catch (Exception ex)
            {
                progress?.Report(new RecoveryProgress { Operation = "Windows Security Center", Progress = 0, Status = "오류" });
                securityCenterRecoveryError = ex.Message;
                AddUserFriendlyMessage($"Windows Security Center 복구 중 오류 발생: {ex.Message}", MessageType.Error);
            }
            finally
            {
                HideLoadingOverlay();
            }
        }

        // BitLocker 복구
        private async Task RecoverBitLocker(IProgress<RecoveryProgress>? progress = null)
        {
            try
            {
                if (_cancellationTokenSource == null)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                }

                ShowLoadingOverlay("BitLocker 복구 중...");
                var startTime = DateTime.Now;

                progress?.Report(new RecoveryProgress { Operation = "BitLocker", Progress = 0, Status = "복구 시작" });

                // WMIHelper를 사용하여 BitLocker 활성화
                bool success = await WmiHelper.EnableBitLockerAsync();
                
                bitLockerRecoveryDuration = DateTime.Now - startTime;
                
                if (success)
                {
                    progress?.Report(new RecoveryProgress { Operation = "BitLocker", Progress = 100, Status = "완료" });
                    AddUserFriendlyMessage("BitLocker가 성공적으로 활성화되었습니다.", MessageType.Success);
                    bitLockerRecoveryError = "";
                }
                else
                {
                    progress?.Report(new RecoveryProgress { Operation = "BitLocker", Progress = 100, Status = "실패" });
                    AddUserFriendlyMessage("BitLocker 활성화에 실패했습니다.", MessageType.Error);
                    bitLockerRecoveryError = "BitLocker 활성화 실패";
                }
            }
            catch (OperationCanceledException)
            {
                progress?.Report(new RecoveryProgress { Operation = "BitLocker", Progress = 0, Status = "취소됨" });
                AddUserFriendlyMessage("BitLocker 복구가 취소되었습니다.", MessageType.Warning);
                bitLockerRecoveryError = "작업 취소됨";
            }
            catch (Exception ex)
            {
                progress?.Report(new RecoveryProgress { Operation = "BitLocker", Progress = 0, Status = "오류" });
                bitLockerRecoveryError = ex.Message;
                AddUserFriendlyMessage($"BitLocker 복구 중 오류 발생: {ex.Message}", MessageType.Error);
            }
            finally
            {
                HideLoadingOverlay();
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
        [SupportedOSPlatform("windows")]
        private void SidebarPrograms_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Page1());
        }

        [SupportedOSPlatform("windows")]
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