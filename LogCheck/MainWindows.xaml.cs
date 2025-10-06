using System.ComponentModel;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LogCheck.Services;
// using System.Windows.Forms; // Tray handled by App

namespace LogCheck
{
    [SupportedOSPlatform("windows")]
    public partial class MainWindows : Window
    {
        private bool isExplicitClose = false;

        private int guideStep = 0;
        private List<FrameworkElement> guideElements = new();
        private List<string> guideDescriptions = new();
        private FrameworkElement currentTargetElement;

        // 보안 상태 업데이트를 위한 타이머
        private DispatcherTimer securityStatusTimer;
        private SecurityAnalyzer securityAnalyzer;

        [SupportedOSPlatform("windows")]
        public MainWindows()
        {
            InitializeComponent();
            InitializeGuide();
            InitializeSecurityStatus();
            InitializeToastNotifications();
            this.SizeChanged += OnWindowSizeChanged;
        }

        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (GuideOverlay.Visibility == Visibility.Visible && currentTargetElement != null)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    HighlightElement(currentTargetElement);          // 하이라이트 재계산
                    PositionGuideBubble(currentTargetElement);       // 위치 재계산
                }, DispatcherPriority.Loaded);
            }
        }

        private void InitializeGuide()
        {
            guideElements = new List<FrameworkElement>
            {
                securityStatusSection,
                SecurityDashboardButton,
                ModificationHistoryButton,
                InstalledProgramsButton,
                SecurityRecoveryButton
            };

            guideDescriptions = new List<string>
            {
                "시스템의 전반적인 보안 상태를\n확인할 수 있습니다.",
                "파일 해시 기반 악성 프로그램\n검사 결과를 확인할 수 있습니다.",
                "시스템에 설치된 모든 프로그램을\n확인하고 관리할 수 있습니다.",
                "실시간 네트워크 모니터링을 하고\n보안 위협을 탐지합니다.",
                "Windows Defender, 방화벽 등\n보안 프로그램을 정상화 합니다."
            };
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            guideStep = 0;
            GuideOverlay.Visibility = Visibility.Visible;
            ShowGuideStep();
        }

        private void ShowGuideStep()
        {
            if (guideStep >= guideElements.Count)
            {
                EndGuide();
                return;
            }

            currentTargetElement = guideElements[guideStep];
            GuideText.Text = guideDescriptions[guideStep];
            HighlightElement(currentTargetElement);
            ShowGuideAtTarget(currentTargetElement);

            GuidePrevButton.Visibility = guideStep == 0 ? Visibility.Collapsed : Visibility.Visible;
            GuideNextButton.Content = guideStep == guideElements.Count - 1 ? "닫기" : "다음";
            GuideSkipButton.Visibility = guideStep == guideElements.Count - 1 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ShowGuideAtTarget(FrameworkElement target)
        {
            GuideBubble.Visibility = Visibility.Visible;
            GuideTail.Visibility = Visibility.Visible;
            GuideSkipButton.Visibility = Visibility.Visible;

            Dispatcher.InvokeAsync(() =>
            {
                PositionGuideBubble(currentTargetElement);
            }, DispatcherPriority.Loaded);
        }

        private void PositionGuideBubble(FrameworkElement target)
        {
            var targetPos = target.TransformToAncestor(this).Transform(new System.Windows.Point(0, 0));
            var targetSize = new System.Windows.Size(target.ActualWidth, target.ActualHeight);

            double bubbleWidth = GuideBubble.ActualWidth > 0 ? GuideBubble.ActualWidth : 300;
            double bubbleHeight = GuideBubble.ActualHeight > 0 ? GuideBubble.ActualHeight : 120;

            double left = 0, top = 0;
            string placement = "right";

            // 말풍선 배치 우선순위: 오른쪽 → 왼쪽 → 아래 → 위
            if (targetPos.X + targetSize.Width + bubbleWidth + 10 < this.ActualWidth)
            {
                left = targetPos.X + targetSize.Width + 10;
                top = targetPos.Y + (targetSize.Height - bubbleHeight) / 2;
                placement = "right";
            }
            else if (targetPos.X - bubbleWidth - 10 > 0)
            {
                left = targetPos.X - bubbleWidth - 10;
                top = targetPos.Y + (targetSize.Height - bubbleHeight) / 2;
                placement = "left";
            }
            else if (targetPos.Y + targetSize.Height + bubbleHeight + 10 < this.ActualHeight)
            {
                left = targetPos.X + (targetSize.Width - bubbleWidth) / 2;
                top = targetPos.Y + targetSize.Height + 10;
                placement = "bottom";
            }
            else
            {
                left = targetPos.X + (targetSize.Width - bubbleWidth) / 2;
                top = targetPos.Y - bubbleHeight - 10;
                placement = "top";
            }

            // 경계 보정
            left = Math.Max(10, Math.Min(this.ActualWidth - bubbleWidth - 10, left));
            top = Math.Max(10, Math.Min(this.ActualHeight - bubbleHeight - 10, top));

            Canvas.SetLeft(GuideBubble, left);
            Canvas.SetTop(GuideBubble, top);

            // ▸ 꼬리 위치 및 회전
            double tailLeft = 0, tailTop = 0;
            double centerX = 0, centerY = 0;

            switch (placement)
            {
                case "right":
                    GuideTail.RenderTransform = new RotateTransform(90);
                    centerY = targetPos.Y + targetSize.Height / 2;
                    tailLeft = left - GuideTail.Width + 1;
                    tailTop = centerY - GuideTail.Height / 2;
                    break;

                case "left":
                    GuideTail.RenderTransform = new RotateTransform(-90);
                    centerY = targetPos.Y + targetSize.Height / 2;
                    tailLeft = left + bubbleWidth - 1;
                    tailTop = centerY - GuideTail.Height / 2;
                    break;

                case "bottom":
                    GuideTail.RenderTransform = new RotateTransform(180);
                    centerX = targetPos.X + targetSize.Width / 2;
                    tailLeft = centerX - GuideTail.Width / 2;
                    tailTop = top - GuideTail.Height + 1;
                    break;

                case "top":
                    GuideTail.RenderTransform = new RotateTransform(0);
                    centerX = targetPos.X + targetSize.Width / 2;
                    tailLeft = centerX - GuideTail.Width / 2;
                    tailTop = top + bubbleHeight - 1;
                    break;
            }

            Canvas.SetLeft(GuideTail, tailLeft);
            Canvas.SetTop(GuideTail, tailTop);

            // ▸ 건너뛰기 버튼 - 말풍선 오른쪽 위
            double skipLeft = left + bubbleWidth - GuideSkipButton.ActualWidth - 8;
            double skipTop = top - GuideSkipButton.ActualHeight - 8;

            Canvas.SetLeft(GuideSkipButton, skipLeft);
            Canvas.SetTop(GuideSkipButton, skipTop);
        }

        private void HighlightElement(FrameworkElement target)
        {
            MaskLayer.Children.Clear();

            var overlay = new System.Windows.Shapes.Rectangle
            {
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 0, 0, 0)),
                Width = this.ActualWidth,
                Height = this.ActualHeight
            };

            var fullRect = new RectangleGeometry(new Rect(0, 0, this.ActualWidth, this.ActualHeight));
            var bounds = VisualTreeHelper.GetDescendantBounds(target);
            var transform = target.TransformToAncestor(this);
            var topLeft = transform.Transform(bounds.TopLeft);

            Rect targetRect = new Rect(topLeft, bounds.Size);

            double radiusX = 0, radiusY = 0;
            if (target is Border border)
            {
                radiusX = radiusY = Math.Max(border.CornerRadius.TopLeft, 10);
            }
            else if (target is System.Windows.Controls.Button)
            {
                radiusX = radiusY = 15;
            }

            Geometry highlightGeometry = new RectangleGeometry(targetRect, radiusX, radiusY);
            var combined = new CombinedGeometry(GeometryCombineMode.Exclude, fullRect, highlightGeometry);

            var drawing = new GeometryDrawing
            {
                Geometry = combined,
                Brush = System.Windows.Media.Brushes.White
            };

            overlay.OpacityMask = new DrawingBrush(drawing);
            MaskLayer.Children.Add(overlay);
        }

        private void GuideNext_Click(object sender, RoutedEventArgs e)
        {
            if (guideStep == guideElements.Count - 1)
            {
                EndGuide();
                return;
            }
            guideStep++;
            ShowGuideStep();
        }

        private void GuidePrev_Click(object sender, RoutedEventArgs e)
        {
            if (guideStep > 0)
            {
                guideStep--;
                ShowGuideStep();
            }
        }

        private void GuideSkip_Click(object sender, RoutedEventArgs e)
        {
            EndGuide();
        }

        private void EndGuide()
        {
            GuideOverlay.Visibility = Visibility.Collapsed;
            GuideBubble.Visibility = Visibility.Collapsed;
            GuideTail.Visibility = Visibility.Collapsed;
            GuideSkipButton.Visibility = Visibility.Collapsed;
            MaskLayer.Children.Clear();
        }

        [SupportedOSPlatform("windows")]
        public void NavigateToPage(Page page)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            var mainGrid = FindName("mainGrid") as Grid;
            var mainButtonsGrid = FindName("mainButtonsGrid") as Border;
            var securityStatusSection = FindName("securityStatusSection") as Border;

            if (mainGrid == null || mainButtonsGrid == null || securityStatusSection == null)
            {
                throw new InvalidOperationException("필수 UI 요소를 찾을 수 없습니다.");
            }

            // 메인 버튼 그리드와 보안 상태 섹션 숨기기
            mainButtonsGrid.Visibility = Visibility.Collapsed;
            securityStatusSection.Visibility = Visibility.Collapsed;

            // 기존 Frame 제거
            UIElement? uiChildToRemove = null;
            foreach (UIElement child in mainGrid.Children)
            {
                if (child is Frame)
                {
                    uiChildToRemove = child;
                    break;
                }
            }

            if (uiChildToRemove != null)
            {
                mainGrid.Children.Remove(uiChildToRemove);
            }

            // 새로운 페이지 추가
            var frame = new Frame();
            frame.Content = page;
            mainGrid.Children.Add(frame);
            Grid.SetRow(frame, 2);  // 버튼 그리드와 같은 Row에 배치
            Grid.SetColumn(frame, 0);
        }

        [SupportedOSPlatform("windows")]
        private void SidebarPrograms_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogHelper.LogInfo("프로그램 목록 버튼 클릭됨");
                var installedProgramsPage = new ProgramsList();
                NavigateToPage(installedProgramsPage);
                LogHelper.LogInfo("프로그램 목록 페이지 네비게이션 완료");
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"프로그램 목록 페이지 로드 중 오류: {ex.Message}");
                LogHelper.LogError($"스택 트레이스: {ex.StackTrace}");

                System.Windows.MessageBox.Show($"프로그램 목록을 로드하는 중 오류가 발생했습니다:\n{ex.Message}",
                                              "오류",
                                              MessageBoxButton.OK,
                                              MessageBoxImage.Error);
            }
        }

        [SupportedOSPlatform("windows")]
        private void NetWorksNew_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new NetWorks_New()); // 수정됨
            HelpButton.Visibility = Visibility.Collapsed;
        }

        [SupportedOSPlatform("windows")]
        private void ProgramList_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new ProgramsList());
            HelpButton.Visibility = Visibility.Collapsed;
        }

        private void BtnHome_click(object sender, RoutedEventArgs e)
        {
            var mainGrid = FindName("mainGrid") as Grid;
            var mainButtonsGrid = FindName("mainButtonsGrid") as Border;
            var securityStatusSection = FindName("securityStatusSection") as Border;

            if (mainGrid == null || mainButtonsGrid == null || securityStatusSection == null)
            {
                throw new InvalidOperationException("필수 UI 요소를 찾을 수 없습니다.");
            }

            // 기존 Frame 제거
            UIElement? uiChildToRemove = null;
            foreach (UIElement child in mainGrid.Children)
            {
                if (child is Frame)
                {
                    uiChildToRemove = child;
                    break;
                }
            }

            if (uiChildToRemove != null)
            {
                mainGrid.Children.Remove(uiChildToRemove);
            }

            // 메인 버튼 그리드와 보안 상태 섹션 다시 보이기
            mainButtonsGrid.Visibility = Visibility.Visible;
            securityStatusSection.Visibility = Visibility.Visible;
            HelpButton.Visibility = Visibility.Visible;
        }

        [SupportedOSPlatform("windows")]
        private void BtnLog_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Logs());
            HelpButton.Visibility = Visibility.Collapsed;
        }

        [SupportedOSPlatform("windows")]
        private void BtnSetting_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Setting());
            HelpButton.Visibility = Visibility.Collapsed;
        }

        [SupportedOSPlatform("windows")]
        private void SecurityRecovery_Click(object sender, RoutedEventArgs e)
        {
            // Recovery 페이지로 네비게이션
            NavigateToPage(new Recoverys());
            HelpButton.Visibility = Visibility.Collapsed;
        }

        [SupportedOSPlatform("windows")]
        private void Vaccine_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Vaccine());
            HelpButton.Visibility = Visibility.Collapsed;
        }

        [SupportedOSPlatform("windows")]
        private void SecurityDashboard_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new SecurityDashboard());
            HelpButton.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// 보안 상태 초기화
        /// </summary>
        private async void InitializeSecurityStatus()
        {
            try
            {
                // SecurityAnalyzer 초기화
                securityAnalyzer = new SecurityAnalyzer();

                // 초기 보안 상태 업데이트
                await UpdateSecurityStatus();

                // 5분마다 보안 상태 업데이트하는 타이머 설정
                securityStatusTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMinutes(5)
                };
                securityStatusTimer.Tick += async (s, e) => await UpdateSecurityStatus();
                securityStatusTimer.Start();

                LogHelper.LogInfo("보안 상태 모니터링이 시작되었습니다.");
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"보안 상태 초기화 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 전체 보안 상태 업데이트
        /// </summary>
        [SupportedOSPlatform("windows")]
        private async Task UpdateSecurityStatus()
        {
            try
            {
                // 1. 핵심 Windows 보안 서비스 상태 업데이트
                await UpdateWindowsSecurityServices();

                // 2. 실시간 보안 모니터링 상태 업데이트
                await UpdateSecurityMonitoring();

                // 3. 시스템 위험 지표 업데이트
                await UpdateSystemRiskIndicators();

                LogHelper.LogInfo("보안 상태 업데이트 완료");
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"보안 상태 업데이트 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// Windows 보안 서비스 상태 업데이트
        /// </summary>
        [SupportedOSPlatform("windows")]
        private async Task UpdateWindowsSecurityServices()
        {
            try
            {
                // Windows Defender 상태
                bool defenderStatus = await WmiHelper.CheckDefenderStatusAsync();
                Dispatcher.Invoke(() =>
                {
                    DefenderStatusText.Text = defenderStatus ? "정상" : "비활성";
                    DefenderStatusText.Foreground = new SolidColorBrush(defenderStatus ? Colors.Green : Colors.Red);
                });

                // Windows Firewall 상태 (실제 방화벽 프로필 활성화 상태 확인)
                bool firewallStatus = WmiHelper.IsFirewallEnabled();
                Dispatcher.Invoke(() =>
                {
                    FirewallStatusText.Text = firewallStatus ? "정상" : "비활성";
                    FirewallStatusText.Foreground = new SolidColorBrush(firewallStatus ? Colors.Green : Colors.Red);
                });

                // Security Center 상태
                string securityCenterStatus = await WmiHelper.CheckSecurityCenterStatusAsync();
                Dispatcher.Invoke(() =>
                {
                    SecurityCenterStatusText.Text = securityCenterStatus;
                    SecurityCenterStatusText.Foreground = new SolidColorBrush(
                        securityCenterStatus == "활성" ? Colors.Green : Colors.Orange);
                });

                // BitLocker 상태
                string bitLockerStatus = await WmiHelper.CheckBitLockerStatusAsync();
                Dispatcher.Invoke(() =>
                {
                    BitLockerStatusText.Text = bitLockerStatus;
                    BitLockerStatusText.Foreground = new SolidColorBrush(
                        bitLockerStatus == "활성" ? Colors.Green : Colors.Orange);
                });
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"Windows 보안 서비스 상태 확인 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 실시간 보안 모니터링 상태 업데이트
        /// </summary>
        private async Task UpdateSecurityMonitoring()
        {
            try
            {
                // 네트워크 모니터링 상태 (임시로 활성으로 설정)
                Dispatcher.Invoke(() =>
                {
                    NetworkMonitoringStatusText.Text = "활성";
                    NetworkMonitoringStatusText.Foreground = new SolidColorBrush(Colors.Green);
                });

                // 보안 경고 통계
                var securityStats = securityAnalyzer.GetSecurityStatistics();
                Dispatcher.Invoke(() =>
                {
                    // 24시간 경고
                    Alerts24HText.Text = $"{securityStats.AlertsLast24Hours}건";
                    Alerts24HText.Foreground = new SolidColorBrush(
                        securityStats.AlertsLast24Hours == 0 ? Colors.Green :
                        securityStats.AlertsLast24Hours < 5 ? Colors.Orange : Colors.Red);

                    // 고위험 경고
                    HighRiskAlertsText.Text = $"{securityStats.HighSeverityAlerts}건";
                    HighRiskAlertsText.Foreground = new SolidColorBrush(
                        securityStats.HighSeverityAlerts == 0 ? Colors.Green : Colors.Red);

                    // 최근 검사 (임시로 현재 시간 기준으로 설정)
                    LastScanText.Text = "방금 전";
                    LastScanText.Foreground = new SolidColorBrush(Colors.Green);
                });
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"보안 모니터링 상태 업데이트 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 시스템 위험 지표 업데이트
        /// </summary>
        private async Task UpdateSystemRiskIndicators()
        {
            try
            {
                // 의심스러운 네트워크 활동
                var securityStats = securityAnalyzer.GetSecurityStatistics();
                Dispatcher.Invoke(() =>
                {
                    bool hasSuspiciousActivity = securityStats.TotalAlerts > 0;
                    SuspiciousNetworkText.Text = hasSuspiciousActivity ? "감지됨" : "정상";
                    SuspiciousNetworkText.Foreground = new SolidColorBrush(
                        hasSuspiciousActivity ? Colors.Orange : Colors.Green);

                    // 악성 IP 연결
                    bool hasMaliciousIP = securityStats.HighSeverityAlerts > 0;
                    MaliciousIPText.Text = hasMaliciousIP ? "감지됨" : "없음";
                    MaliciousIPText.Foreground = new SolidColorBrush(
                        hasMaliciousIP ? Colors.Red : Colors.Green);
                });

                // 설치된 프로그램 수 (비동기로 조회)
                await Task.Run(() =>
                {
                    try
                    {
                        var programs = WmiHelper.GetInstalledPrograms();
                        Dispatcher.Invoke(() =>
                        {
                            InstalledProgramsCountText.Text = $"{programs.Count}개";
                            InstalledProgramsCountText.Foreground = new SolidColorBrush(Colors.Blue);
                        });
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogError($"설치된 프로그램 수 조회 중 오류: {ex.Message}");
                        Dispatcher.Invoke(() =>
                        {
                            InstalledProgramsCountText.Text = "오류";
                            InstalledProgramsCountText.Foreground = new SolidColorBrush(Colors.Red);
                        });
                    }
                });

                // 마지막 악성코드 검사 (임시 값)
                Dispatcher.Invoke(() =>
                {
                    LastMalwareScanText.Text = "미실행";
                    LastMalwareScanText.Foreground = new SolidColorBrush(Colors.Orange);
                });
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"시스템 위험 지표 업데이트 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 보안 상태 새로 고침 버튼 클릭 이벤트
        /// </summary>
        [SupportedOSPlatform("windows")]
        private async void RefreshSecurityStatus_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 버튼 비활성화 (중복 클릭 방지)
                RefreshSecurityStatusButton.IsEnabled = false;

                LogHelper.LogInfo("사용자가 보안 상태 새로 고침을 요청했습니다.");

                // 보안 상태 업데이트
                await UpdateSecurityStatus();

                // 성공 메시지 표시 (선택적)
                LogHelper.LogInfo("보안 상태가 성공적으로 업데이트되었습니다.");
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"보안 상태 새로 고침 중 오류: {ex.Message}");
                System.Windows.MessageBox.Show("보안 상태를 업데이트하는 중 오류가 발생했습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                // 버튼 다시 활성화
                RefreshSecurityStatusButton.IsEnabled = true;
            }
        }

        [SupportedOSPlatform("windows")]

        protected override async void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            if (isExplicitClose) return;

            e.Cancel = true; // 기본 종료 취소

            var result = System.Windows.MessageBox.Show(
                "프로그램을 완전히 종료하시겠습니까?\n'아니오'를 선택하면 시스템 트레이로 이동합니다.",
                "종료 확인",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                isExplicitClose = true;
                try
                {
                    await LogCheck.Services.MonitoringHub.Instance.StopAsync();
                }
                catch { }

                System.Windows.Application.Current.Shutdown();
            }
            else if (result == MessageBoxResult.No)
            {
                Hide(); // 트레이로 이동
            }
            // Cancel이면 아무것도 하지 않음
        }

        /// <summary>
        /// Toast 알림 시스템 초기화
        /// </summary>
        private void InitializeToastNotifications()
        {
            // Toast 서비스 초기화 (컨테이너는 Loaded 이벤트에서 설정)
            var toastService = ToastNotificationService.Instance;


            Loaded += (s, e) =>
            {
                // UI 로드 후 ToastStack 컨테이너 설정
                if (FindName("ToastStack") is StackPanel toastStack)
                {
                    toastService.SetContainer(toastStack);
                }

                // 테스트용 환영 메시지 (선택사항)

                Dispatcher.InvokeAsync(async () =>
                {
                    await Task.Delay(1500); // UI 로드 완료 대기
                    await toastService.ShowInfoAsync("Windows Sentinel", "보안 모니터링이 시작되었습니다.");
                });
            };
        }
    }
}
