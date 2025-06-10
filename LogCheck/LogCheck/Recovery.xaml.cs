    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media.Animation;
    using System.Windows.Threading;
    using System.Security.Principal;
    using System.Diagnostics;
    using System.Text; // StringBuilder 사용을 위해 추가
    using System.Windows.Media; // VisualTreeHelper를 사용하기 위해 추가

    namespace LogCheck
    {
    // WmiHelper 클래스 정의
    internal static class WmiHelper
    {
        public static async Task<bool> EnableDefenderAsync()
        {
            // WMI를 통한 Windows Defender 활성화 로직 구현
            await Task.Delay(1500); // 실제 구현에서는 제거
            return true;
        }

        public static async Task<bool> EnableFirewallAsync()
        {
            // WMI를 통한 Windows Firewall 활성화 로직 구현
            await Task.Delay(1500); // 실제 구현에서는 제거
            return true;
        }

        public static async Task<bool> EnableSecurityCenterAsync()
        {
            // WMI를 통한 Security Center 활성화 로직 구현
            await Task.Delay(1500); // 실제 구현에서는 제거
            return true;
        }

        public static async Task<bool> EnableBitLockerAsync()
        {
            // WMI를 통한 BitLocker 활성화 로직 구현
            await Task.Delay(1500); // 실제 구현에서는 제거
            return true;
        }
    }

    public partial class Recovery : Window
    {
        // XAML 컨트롤 필드 정의
        private TextBlock DefenderStatus;
        private ProgressBar DefenderProgress;
        private TextBlock FirewallStatus;
        private ProgressBar FirewallProgress;
        private TextBlock SecurityCenterStatus;
        private ProgressBar SecurityCenterProgress;
        private TextBlock BitLockerStatus;
        private ProgressBar BitLockerProgress;
        private ListBox MessagesList;

        // 취소 토큰 소스
        private CancellationTokenSource _cancellationTokenSource;

        // 복구 작업 소요 시간 측정을 위한 필드
        private TimeSpan defenderRecoveryDuration;
        private TimeSpan firewallRecoveryDuration;
        private TimeSpan securityCenterRecoveryDuration;
        private TimeSpan bitLockerRecoveryDuration;

        // 복구 작업 오류 메시지 저장을 위한 필드
        private string defenderRecoveryError = "";
        private string firewallRecoveryError = "";
        private string securityCenterRecoveryError = "";
        private string bitLockerRecoveryError = "";

        // 복구 성공 여부 추적을 위한 필드
        private bool defenderRecovered = false;
        private bool firewallRecovered = false;
        private bool securityCenterRecovered = false;
        private bool bitlockerRecovered = false;

        // 복구 시도 횟수 추적
        private int defenderAttempts = 0;
        private int firewallAttempts = 0;
        private int securityCenterAttempts = 0;
        private int bitlockerAttempts = 0;

        // 상태 업데이트 타이머
        private DispatcherTimer statusUpdateTimer;

        // 복구 상태 기록을 위한 컬렉션
        private ObservableCollection<string> userFriendlyMessages = new ObservableCollection<string>();

        public Recovery()
        {
            InitializeComponent();

            // UI 컨트롤 초기화 - 실제 XAML 파일이 있으면 필요 없음
            InitializeControls();

            // 타이머 초기화
            statusUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            statusUpdateTimer.Tick += StatusUpdateTimer_Tick;

            // 메시지 리스트 초기화
            MessagesList.ItemsSource = userFriendlyMessages;
        }

        // XAML 파일이 없는 경우를 위한 컨트롤 초기화 메서드
        private void InitializeComponent()
        {
            // 실제 XAML 파일이 있으면 자동 생성됨
            // 여기서는 빈 메서드로 두어 컴파일 오류 방지
        }

        private void InitializeControls()
        {
            // 런타임에 UI 컨트롤 생성
            DefenderStatus = new TextBlock();
            DefenderProgress = new ProgressBar();
            FirewallStatus = new TextBlock();
            FirewallProgress = new ProgressBar();
            SecurityCenterStatus = new TextBlock();
            SecurityCenterProgress = new ProgressBar();
            BitLockerStatus = new TextBlock();
            BitLockerProgress = new ProgressBar();
            MessagesList = new ListBox();

            // 결과 보고서 컨테이너 생성
            StackPanel resultReportContainer = new StackPanel
            {
                Name = "ResultReportContainer",
                Margin = new Thickness(10)
            };

            // 결과 보고서 헤더 추가
            TextBlock reportHeader = new TextBlock
            {
                Text = "복구 결과 보고서",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 15),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            resultReportContainer.Children.Add(reportHeader);

            // 초기 메시지
            TextBlock initialMessage = new TextBlock
            {
                Text = "복구 작업을 시작하면 여기에 결과가 표시됩니다.",
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 20, 0, 20),
                FontStyle = FontStyles.Italic
            };
            resultReportContainer.Children.Add(initialMessage);

            // 결과 보고서 영역을 담을 Border 생성
            Border resultBorder = new Border
            {
                Name = "ResultReportBorder",
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(10),
                Padding = new Thickness(10),
                Child = resultReportContainer
            };

            // 이 컨트롤을 윈도우에 등록 (실제 XAML에서는 이미 정의되어 있음)
            // 실제 구현에서는 이 부분이 필요 없음
            // this.RegisterName("ResultReport", resultReport);
        }

        private void StatusUpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdateOperationStatusSummary();
        }

        private void UpdateOperationStatusSummary()
        {
            // 진행 중인 복구 작업 상태 요약 업데이트
        }

        private void UpdateResultReport()
        {
            try
            {
                // 복구 결과 보고서 업데이트
                Debug.WriteLine("복구 결과 보고서 업데이트");

                // 각 보안 기능의 복구 상태 요약
                string defenderResult = defenderRecovered ? "성공" : "실패";
                string firewallResult = firewallRecovered ? "성공" : "실패";
                string securityCenterResult = securityCenterRecovered ? "성공" : "실패";
                string bitLockerResult = bitlockerRecovered ? "성공" : "실패";

                // 기준 시간 설정
                DateTime startTime = DateTime.Now.Subtract(new TimeSpan(
                    defenderRecoveryDuration.Ticks + 
                    firewallRecoveryDuration.Ticks + 
                    securityCenterRecoveryDuration.Ticks + 
                    bitLockerRecoveryDuration.Ticks));

                // 복구 시도 횟수 합계
                int totalAttempts = defenderAttempts + firewallAttempts + securityCenterAttempts + bitlockerAttempts;

                // 복구 결과 메시지 생성
                StringBuilder report = new StringBuilder();
                
                // 복구 성공 항목 수 계산
                int successCount = CountSuccessfulRecoveries();

                // 시스템 정보 섹션
                report.AppendLine("===== 시스템 정보 =====");
                report.AppendLine($"OS 버전: {Environment.OSVersion}");
                report.AppendLine($"컴퓨터 이름: {Environment.MachineName}");
                report.AppendLine($"사용자 이름: {Environment.UserName}");
                report.AppendLine($"사용자 권한: {(IsAdministrator() ? "관리자" : "일반 사용자")}");
                report.AppendLine();

                // 복구 실행 정보
                report.AppendLine("===== 복구 실행 정보 =====");
                report.AppendLine($"복구 시작 시간: {startTime:yyyy-MM-dd HH:mm:ss}");
                report.AppendLine($"복구 완료 시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                TimeSpan totalDuration = new TimeSpan(
                    defenderRecoveryDuration.Ticks + 
                    firewallRecoveryDuration.Ticks + 
                    securityCenterRecoveryDuration.Ticks + 
                    bitLockerRecoveryDuration.Ticks);
                report.AppendLine($"전체 소요 시간: {totalDuration.TotalSeconds:F1}초");
                report.AppendLine($"전체 시도 횟수: {totalAttempts}회");
                report.AppendLine();

                // 복구 상태 요약
                report.AppendLine("===== 복구 상태 요약 =====");
                report.AppendLine($"복구 대상 항목 수: 4개");
                report.AppendLine($"성공한 복구 작업: {successCount}개");
                report.AppendLine($"실패한 복구 작업: {4 - successCount}개");
                report.AppendLine($"전체 성공률: {(successCount / 4.0 * 100):F1}%");
                report.AppendLine();

                // 세부 복구 결과
                report.AppendLine("===== 세부 복구 결과 =====");

                // Windows Defender 결과 상세
                report.AppendLine("[Windows Defender]");
                report.AppendLine($"상태: {defenderResult}");
                report.AppendLine($"소요 시간: {defenderRecoveryDuration.TotalSeconds:F1}초");
                report.AppendLine($"시도 횟수: {defenderAttempts}회");
                if (!string.IsNullOrEmpty(defenderRecoveryError))
                    report.AppendLine($"오류 내용: {defenderRecoveryError}");
                if (defenderRecovered)
                    report.AppendLine("조치 내용: 실시간 보호 활성화, 서비스 시작, 자동 실행 설정");
                report.AppendLine();

                // Windows Firewall 결과 상세
                report.AppendLine("[Windows Firewall]");
                report.AppendLine($"상태: {firewallResult}");
                report.AppendLine($"소요 시간: {firewallRecoveryDuration.TotalSeconds:F1}초");
                report.AppendLine($"시도 횟수: {firewallAttempts}회");
                if (!string.IsNullOrEmpty(firewallRecoveryError))
                    report.AppendLine($"오류 내용: {firewallRecoveryError}");
                if (firewallRecovered)
                    report.AppendLine("조치 내용: 방화벽 서비스 시작, 도메인/개인/공용 프로필 활성화");
                report.AppendLine();

                // Windows Security Center 결과 상세
                report.AppendLine("[Windows Security Center]");
                report.AppendLine($"상태: {securityCenterResult}");
                report.AppendLine($"소요 시간: {securityCenterRecoveryDuration.TotalSeconds:F1}초");
                report.AppendLine($"시도 횟수: {securityCenterAttempts}회");
                if (!string.IsNullOrEmpty(securityCenterRecoveryError))
                    report.AppendLine($"오류 내용: {securityCenterRecoveryError}");
                if (securityCenterRecovered)
                    report.AppendLine("조치 내용: 보안 센터 서비스 재시작, 보안 알림 활성화");
                report.AppendLine();

                // BitLocker 결과 상세
                report.AppendLine("[BitLocker]");
                report.AppendLine($"상태: {bitLockerResult}");
                report.AppendLine($"소요 시간: {bitLockerRecoveryDuration.TotalSeconds:F1}초");
                report.AppendLine($"시도 횟수: {bitlockerAttempts}회");
                if (!string.IsNullOrEmpty(bitLockerRecoveryError))
                    report.AppendLine($"오류 내용: {bitLockerRecoveryError}");
                if (bitlockerRecovered)
                    report.AppendLine("조치 내용: BitLocker 서비스 활성화, 시스템 드라이브 암호화 설정");
                report.AppendLine();

                // 오류 발생 기록
                if (HasAnyErrors())
                {
                    report.AppendLine("===== 오류 발생 기록 =====");
                    if (!string.IsNullOrEmpty(defenderRecoveryError))
                        report.AppendLine($"• Windows Defender: {defenderRecoveryError}");
                    if (!string.IsNullOrEmpty(firewallRecoveryError))
                        report.AppendLine($"• Windows Firewall: {firewallRecoveryError}");
                    if (!string.IsNullOrEmpty(securityCenterRecoveryError))
                        report.AppendLine($"• Windows Security Center: {securityCenterRecoveryError}");
                    if (!string.IsNullOrEmpty(bitLockerRecoveryError))
                        report.AppendLine($"• BitLocker: {bitLockerRecoveryError}");
                    report.AppendLine();
                }

                // 권장 조치사항
                report.AppendLine("===== 권장 조치사항 =====");
                bool hasRecommendations = false;
                successCount = CountSuccessfulRecoveries();

                if (!defenderRecovered)
                {
                    hasRecommendations = true;
                    report.AppendLine("• Windows Defender 수동 점검 필요:");
                    report.AppendLine("  - Windows 보안 앱을 열고 바이러스 및 위협 방지 설정 확인");
                    report.AppendLine("  - 서비스(services.msc)에서 'Windows Defender' 서비스 상태 확인");
                    report.AppendLine("  - 바이러스 정의 업데이트 확인");
                }

                if (!firewallRecovered)
                {
                    hasRecommendations = true;
                    report.AppendLine("• Windows Firewall 수동 점검 필요:");
                    report.AppendLine("  - 제어판 > Windows Defender 방화벽에서 상태 확인");
                    report.AppendLine("  - 서비스(services.msc)에서 'Windows Defender Firewall' 서비스 재시작");
                    report.AppendLine("  - 'netsh advfirewall set allprofiles state on' 명령 수동 실행");
                }

                if (!securityCenterRecovered)
                {
                    hasRecommendations = true;
                    report.AppendLine("• Windows Security Center 수동 점검 필요:");
                    report.AppendLine("  - 서비스(services.msc)에서 'Security Center' 서비스 재시작");
                    report.AppendLine("  - 레지스트리 편집기에서 관련 설정 확인");
                    report.AppendLine("  - 보안 센터 재구성 명령 실행");
                }

                if (!bitlockerRecovered)
                {
                    hasRecommendations = true;
                    report.AppendLine("• BitLocker 수동 점검 필요:");
                    report.AppendLine("  - 제어판 > BitLocker 드라이브 암호화에서 상태 확인");
                    report.AppendLine("  - TPM(신뢰할 수 있는 플랫폼 모듈) 상태 확인");
                    report.AppendLine("  - 'manage-bde -status' 명령으로 상태 확인");
                }

                if (!hasRecommendations)
                {
                    report.AppendLine("• 모든 보안 기능이 정상적으로 복구되었습니다.");
                    report.AppendLine("• 정기적인 보안 점검을 위해 다음 작업을 권장합니다:");
                    report.AppendLine("  - Windows 업데이트 정기 확인");
                    report.AppendLine("  - Windows Defender 정기 검사 수행");
                    report.AppendLine("  - 중요 데이터 백업 유지");
                }

                // 결과 보고서 텍스트 적용
                string finalReport = report.ToString();
                Debug.WriteLine(finalReport);

                // UI에 결과 보고서 표시 (Dispatcher 사용해 UI 스레드에서 실행)
                Dispatcher.Invoke(() => {
                    // 결과 보고서 컨테이너 찾기
                    Panel reportContainer = FindReportContainer();
                    if (reportContainer != null)
                    {
                        // 컨테이너 초기화
                        reportContainer.Children.Clear();

                        // 상태 요약 테이블 추가
                        reportContainer.Children.Add(new TextBlock
                        {
                            Text = "복구 상태 요약",
                            FontSize = 16,
                            FontWeight = FontWeights.Bold,
                            Margin = new Thickness(0, 0, 0, 10)
                        });

                        // 상태 그리드 추가
                        Grid statusGrid = CreateRecoveryStatusGrid();
                        reportContainer.Children.Add(statusGrid);

                        // 상세 보고서 헤더 추가
                        reportContainer.Children.Add(new TextBlock
                        {
                            Text = "상세 복구 결과 보고서",
                            FontSize = 16,
                            FontWeight = FontWeights.Bold,
                            Margin = new Thickness(0, 20, 0, 10)
                        });

                        // 상세 보고서 텍스트 추가
                        TextBox detailReport = new TextBox
                        {
                            Text = finalReport,
                            IsReadOnly = true,
                            TextWrapping = TextWrapping.Wrap,
                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                            MaxHeight = 400,
                            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                            FontSize = 12,
                            Padding = new Thickness(10)
                        };

                        reportContainer.Children.Add(detailReport);
                    }
                    else
                    {
                        // 컨테이너가 없는 경우 기존 방식으로 표시
                        if (FindResultReportControl() is TextBox textBox)
                        {
                            textBox.Text = finalReport;
                        }
                        else if (FindResultReportControl() is RichTextBox richTextBox)
                        {
                            richTextBox.Document.Blocks.Clear();
                            richTextBox.AppendText(finalReport);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"결과 보고서 업데이트 중 오류 발생: {ex.Message}");
            }
        }

        // 결과 보고서 컨트롤 찾기
        private Control FindResultReportControl()
        {
            // 여기서는 간단히 구현 - 실제로는 컨트롤 이름이나 태그 등으로 찾아야 함
            TextBox resultReportTextBox = new TextBox
            { 
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12
            };

            // 실제 XAML에 정의된 컨트롤을 찾아서 반환
            // 예: 이름이 ResultReport인 TextBox 찾기
            if (this.FindName("ResultReport") is TextBox existingTextBox)
            {
                return existingTextBox;
            }

            // 찾지 못한 경우 새로 생성한 컨트롤 반환 (실제 구현에서는 필요 없음)
            return resultReportTextBox;
        }

        // 결과 보고서 컨테이너 찾기
        private Panel FindReportContainer()
        {
            // XAML에 정의된 결과 보고서 컨테이너 찾기
            // 예: 이름이 ResultReportContainer인 StackPanel 또는 Grid 찾기
            if (this.FindName("ResultReportContainer") is Panel panel)
            {
                return panel;
            }

            // 또는 'ReportPanel'이라는 이름의 패널 찾기
            if (this.FindName("ReportPanel") is Panel reportPanel)
            {
                return reportPanel;
            }

            // 특정 이름이나 태그로 찾지 못한 경우 UI에서 적절한 컨테이너 찾기 시도
            // 이 부분은 실제 UI 구조에 맞게 수정 필요
            DependencyObject rootElement = this;
            Panel resultPanel = FindVisualChild<StackPanel>(rootElement, "ResultReportPanel");
            if (resultPanel != null)
            {
                return resultPanel;
            }

            return null;
        }

        // 시각적 트리에서 특정 타입 및 이름의 자식 요소 찾기
        private T FindVisualChild<T>(DependencyObject parent, string childName) where T : DependencyObject
        {
            // 이름이 일치하는 자식이 있는지 확인
            if (parent is FrameworkElement element && element.Name == childName && parent is T match)
            {
                return match;
            }

            // 자식 요소 탐색
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                T result = FindVisualChild<T>(child, childName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        // 성공적으로 복구된 항목 수 계산
        private int CountSuccessfulRecoveries()
        {
            int count = 0;
            if (defenderRecovered) count++;
            if (firewallRecovered) count++;
            if (securityCenterRecovered) count++;
            if (bitlockerRecovered) count++;
            return count;
        }

        // 오류가 있는지 확인
        private bool HasAnyErrors()
        {
            return !string.IsNullOrEmpty(defenderRecoveryError) ||
                   !string.IsNullOrEmpty(firewallRecoveryError) ||
                   !string.IsNullOrEmpty(securityCenterRecoveryError) ||
                   !string.IsNullOrEmpty(bitLockerRecoveryError);
        }

        // 오류 기록 추가
        private void AppendErrorHistory(StringBuilder report)
        {
            if (!string.IsNullOrEmpty(defenderRecoveryError))
                report.AppendLine($"• Windows Defender: {defenderRecoveryError}");
            if (!string.IsNullOrEmpty(firewallRecoveryError))
                report.AppendLine($"• Windows Firewall: {firewallRecoveryError}");
            if (!string.IsNullOrEmpty(securityCenterRecoveryError))
                report.AppendLine($"• Windows Security Center: {securityCenterRecoveryError}");
            if (!string.IsNullOrEmpty(bitLockerRecoveryError))
                report.AppendLine($"• BitLocker: {bitLockerRecoveryError}");
        }

        // 관리자 권한 확인 메서드
        private bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        // 복구 상태 요약 표시용 테이블 생성
        private Grid CreateRecoveryStatusGrid()
        {
            Grid grid = new Grid();

            // 열 정의
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });

            // 행 정의
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 헤더 행
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Defender
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Firewall
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Security Center
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // BitLocker

            // 테두리 스타일
            var borderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGray);
            var headerBackground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightSteelBlue);

            // 헤더 추가
            AddGridCell(grid, "보안 기능", 0, 0, headerBackground, true);
            AddGridCell(grid, "상태", 0, 1, headerBackground, true);
            AddGridCell(grid, "소요 시간", 0, 2, headerBackground, true);
            AddGridCell(grid, "시도 횟수", 0, 3, headerBackground, true);

            // Defender 행
            AddGridCell(grid, "Windows Defender", 1, 0, null, false);
            AddGridCell(grid, defenderRecovered ? "성공" : "실패", 1, 1, GetStatusBackground(defenderRecovered), false);
            AddGridCell(grid, $"{defenderRecoveryDuration.TotalSeconds:F1}초", 1, 2, null, false);
            AddGridCell(grid, $"{defenderAttempts}회", 1, 3, null, false);

            // Firewall 행
            AddGridCell(grid, "Windows Firewall", 2, 0, null, false);
            AddGridCell(grid, firewallRecovered ? "성공" : "실패", 2, 1, GetStatusBackground(firewallRecovered), false);
            AddGridCell(grid, $"{firewallRecoveryDuration.TotalSeconds:F1}초", 2, 2, null, false);
            AddGridCell(grid, $"{firewallAttempts}회", 2, 3, null, false);

            // Security Center 행
            AddGridCell(grid, "Windows Security Center", 3, 0, null, false);
            AddGridCell(grid, securityCenterRecovered ? "성공" : "실패", 3, 1, GetStatusBackground(securityCenterRecovered), false);
            AddGridCell(grid, $"{securityCenterRecoveryDuration.TotalSeconds:F1}초", 3, 2, null, false);
            AddGridCell(grid, $"{securityCenterAttempts}회", 3, 3, null, false);

            // BitLocker 행
            AddGridCell(grid, "BitLocker", 4, 0, null, false);
            AddGridCell(grid, bitlockerRecovered ? "성공" : "실패", 4, 1, GetStatusBackground(bitlockerRecovered), false);
            AddGridCell(grid, $"{bitLockerRecoveryDuration.TotalSeconds:F1}초", 4, 2, null, false);
            AddGridCell(grid, $"{bitlockerAttempts}회", 4, 3, null, false);

            // 테두리 설정
            grid.ShowGridLines = true;

            return grid;
        }

        // 그리드 셀 추가 헬퍼 메서드
        private void AddGridCell(Grid grid, string text, int row, int column, System.Windows.Media.Brush background, bool isHeader)
        {
            Border border = new Border
            {
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGray),
                BorderThickness = new Thickness(1),
                Background = background,
                Padding = new Thickness(5)
            };

            TextBlock textBlock = new TextBlock
            {
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = isHeader ? FontWeights.Bold : FontWeights.Normal
            };

            border.Child = textBlock;

            Grid.SetRow(border, row);
            Grid.SetColumn(border, column);

            grid.Children.Add(border);
        }

        // 상태에 따른 배경색 가져오기
        private System.Windows.Media.Brush GetStatusBackground(bool isSuccess)
        {
            return isSuccess 
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGreen)
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightPink);
        }

        private void ResetRecoveryState()
        {
            // 복구 상태 초기화
            defenderRecovered = false;
            firewallRecovered = false;
            securityCenterRecovered = false;
            bitlockerRecovered = false;

            defenderRecoveryError = "";
            firewallRecoveryError = "";
            securityCenterRecoveryError = "";
            bitLockerRecoveryError = "";

            // 복구 시도 횟수 초기화
            defenderAttempts = 0;
            firewallAttempts = 0;
            securityCenterAttempts = 0;
            bitlockerAttempts = 0;

            // 복구 시간 초기화
            defenderRecoveryDuration = TimeSpan.Zero;
            firewallRecoveryDuration = TimeSpan.Zero;
            securityCenterRecoveryDuration = TimeSpan.Zero;
            bitLockerRecoveryDuration = TimeSpan.Zero;
        }

        private void ShowLoadingOverlay(string message)
        {
            // 로딩 오버레이 표시
            Debug.WriteLine($"로딩 오버레이 표시: {message}");
            // 실제 구현에서는 UI에 로딩 오버레이 표시
        }

        private void HideLoadingOverlay()
        {
            // 로딩 오버레이 숨기기
            Debug.WriteLine("로딩 오버레이 숨김");
            // 실제 구현에서는 UI에서 로딩 오버레이 제거
        }

        private void AddUserFriendlyMessage(string message, MessageType type)
        {
            // 사용자 친화적 메시지 추가
            string formattedMessage = $"[{DateTime.Now:HH:mm:ss}] [{type}] {message}";
            userFriendlyMessages.Add(formattedMessage);
        }

        private async Task<bool> RecoverDefender(IProgress<RecoveryProgress>? progress = null)
        {
            try
            {
                defenderAttempts++;

                if (_cancellationTokenSource == null)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                }

                ShowLoadingOverlay("Windows Defender 복구 중...");
                var startTime = DateTime.Now;

                progress?.Report(new RecoveryProgress { Operation = "Windows Defender", Progress = 0, Status = "복구 시작" });
                // UI 상태 업데이트
                DefenderStatus.Text = "복구 중...";
                DefenderProgress.Value = 10;

                // WMIHelper를 사용하여 Windows Defender 활성화
                bool success = await WmiHelper.EnableDefenderAsync();

                defenderRecoveryDuration = DateTime.Now - startTime;

                if (success)
                {
                    progress?.Report(new RecoveryProgress { Operation = "Windows Defender", Progress = 100, Status = "완료" });
                    AddUserFriendlyMessage("Windows Defender가 성공적으로 활성화되었습니다.", MessageType.Success);
                    defenderRecoveryError = "";
                    defenderRecovered = true;
                    // UI 상태 업데이트
                    DefenderStatus.Text = "완료";
                    DefenderProgress.Value = 100;
                    return true;
                }
                else
                {
                    progress?.Report(new RecoveryProgress { Operation = "Windows Defender", Progress = 100, Status = "실패" });
                    AddUserFriendlyMessage("Windows Defender 활성화에 실패했습니다.", MessageType.Error);
                    defenderRecoveryError = "Windows Defender 활성화 실패";
                    // UI 상태 업데이트
                    DefenderStatus.Text = "실패";
                    DefenderProgress.Value = 100;
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                progress?.Report(new RecoveryProgress { Operation = "Windows Defender", Progress = 0, Status = "취소됨" });
                AddUserFriendlyMessage("Windows Defender 복구가 취소되었습니다.", MessageType.Warning);
                defenderRecoveryError = "작업 취소됨";
                // UI 상태 업데이트
                DefenderStatus.Text = "취소됨";
                DefenderProgress.Value = 0;
                return false;
            }
            catch (Exception ex)
            {
                progress?.Report(new RecoveryProgress { Operation = "Windows Defender", Progress = 0, Status = "오류" });
                defenderRecoveryError = ex.Message;
                AddUserFriendlyMessage($"Windows Defender 복구 중 오류 발생: {ex.Message}", MessageType.Error);
                // UI 상태 업데이트
                DefenderStatus.Text = "오류";
                DefenderProgress.Value = 0;
                return false;
            }
            finally
            {
                HideLoadingOverlay();
                UpdateResultReport();
            }
        }

        private async Task<bool> RecoverFirewall(IProgress<RecoveryProgress>? progress = null)
        {
            try
            {
                firewallAttempts++;

                if (_cancellationTokenSource == null)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                }

                ShowLoadingOverlay("Windows Firewall 복구 중...");
                var startTime = DateTime.Now;

                progress?.Report(new RecoveryProgress { Operation = "Windows Firewall", Progress = 0, Status = "복구 시작" });
                // UI 상태 업데이트
                FirewallStatus.Text = "복구 중...";
                FirewallProgress.Value = 10;

                // WMIHelper를 사용하여 Windows Firewall 활성화
                bool success = await WmiHelper.EnableFirewallAsync();

                firewallRecoveryDuration = DateTime.Now - startTime;

                if (success)
                {
                    progress?.Report(new RecoveryProgress { Operation = "Windows Firewall", Progress = 100, Status = "완료" });
                    AddUserFriendlyMessage("Windows Firewall이 성공적으로 활성화되었습니다.", MessageType.Success);
                    firewallRecoveryError = "";
                    firewallRecovered = true;
                    // UI 상태 업데이트
                    FirewallStatus.Text = "완료";
                    FirewallProgress.Value = 100;
                    return true;
                }
                else
                {
                    progress?.Report(new RecoveryProgress { Operation = "Windows Firewall", Progress = 100, Status = "실패" });
                    AddUserFriendlyMessage("Windows Firewall 활성화에 실패했습니다.", MessageType.Error);
                    firewallRecoveryError = "Windows Firewall 활성화 실패";
                    // UI 상태 업데이트
                    FirewallStatus.Text = "실패";
                    FirewallProgress.Value = 100;
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                progress?.Report(new RecoveryProgress { Operation = "Windows Firewall", Progress = 0, Status = "취소됨" });
                AddUserFriendlyMessage("Windows Firewall 복구가 취소되었습니다.", MessageType.Warning);
                firewallRecoveryError = "작업 취소됨";
                // UI 상태 업데이트
                FirewallStatus.Text = "취소됨";
                FirewallProgress.Value = 0;
                return false;
            }
            catch (Exception ex)
            {
                progress?.Report(new RecoveryProgress { Operation = "Windows Firewall", Progress = 0, Status = "오류" });
                firewallRecoveryError = ex.Message;
                AddUserFriendlyMessage($"Windows Firewall 복구 중 오류 발생: {ex.Message}", MessageType.Error);
                // UI 상태 업데이트
                FirewallStatus.Text = "오류";
                FirewallProgress.Value = 0;
                return false;
            }
            finally
            {
                HideLoadingOverlay();
                UpdateResultReport();
            }
        }

        private async Task<bool> RecoverSecurityCenter(IProgress<RecoveryProgress>? progress = null)
        {
            try
            {
                securityCenterAttempts++;

                if (_cancellationTokenSource == null)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                }

                ShowLoadingOverlay("Windows Security Center 복구 중...");
                var startTime = DateTime.Now;

                progress?.Report(new RecoveryProgress { Operation = "Windows Security Center", Progress = 0, Status = "복구 시작" });
                // UI 상태 업데이트
                SecurityCenterStatus.Text = "복구 중...";
                SecurityCenterProgress.Value = 10;

                // 재시도 설정
                const int maxRetries = 2;
                int attempt = 0;
                bool success = false;
                Exception lastError = null;

                while (attempt <= maxRetries)
                {
                    try
                    {
                        attempt++;

                        // 재시도 시 메시지 표시
                        if (attempt > 1)
                        {
                            var retryMessage = $"Windows Security Center 복구 재시도 중... (시도 {attempt}/{maxRetries + 1})";
                            progress?.Report(new RecoveryProgress { Operation = "Windows Security Center", Progress = 33 * attempt, Status = retryMessage });
                            AddUserFriendlyMessage(retryMessage, MessageType.Warning);

                            // UI 상태 업데이트
                            SecurityCenterStatus.Text = $"재시도 중... {attempt}/{maxRetries + 1}";
                            SecurityCenterProgress.Value = 30 * attempt;

                            // 재시도 간 지연 (1초, 2초, ...)
                            await Task.Delay(1000 * attempt, _cancellationTokenSource?.Token ?? default);
                        }

                        // WMIHelper를 사용하여 Windows Security Center 활성화 시도
                        success = await WmiHelper.EnableSecurityCenterAsync();

                        if (success)
                        {
                            securityCenterRecoveryDuration = DateTime.Now - startTime;
                            progress?.Report(new RecoveryProgress { Operation = "Windows Security Center", Progress = 100, Status = "완료" });
                            AddUserFriendlyMessage("Windows Security Center가 성공적으로 활성화되었습니다.", MessageType.Success);
                            securityCenterRecoveryError = "";
                            securityCenterRecovered = true;
                            // UI 상태 업데이트
                            SecurityCenterStatus.Text = "완료";
                            SecurityCenterProgress.Value = 100;
                            return true;
                        }

                        // 활성화 실패 시 예외 발생시켜 재시도 로직 유도
                        throw new InvalidOperationException("Windows Security Center 활성화에 실패했습니다.");
                    }
                    catch (OperationCanceledException)
                    {
                        throw; // 취소 예외는 상위에서 처리
                    }
                    catch (Exception ex) when (attempt <= maxRetries)
                    {
                            lastError = ex;
                            // 재시도 전에 잠시 대기
                            await Task.Delay(1000, _cancellationTokenSource?.Token ?? default);
                    }
                }

                // 모든 재시도 실패 시
                securityCenterRecoveryDuration = DateTime.Now - startTime;
                progress?.Report(new RecoveryProgress { Operation = "Windows Security Center", Progress = 100, Status = "실패" });

                var errorMessage = $"Windows Security Center 활성화에 {maxRetries + 1}번 시도했으나 실패했습니다.";
                if (lastError != null)
                {
                    errorMessage += $"\n마지막 오류: {lastError.Message}";
                }

                AddUserFriendlyMessage(errorMessage, MessageType.Error);
                securityCenterRecoveryError = errorMessage;
                // UI 상태 업데이트
                SecurityCenterStatus.Text = "실패";
                SecurityCenterProgress.Value = 100;
                return false;
            }
            catch (OperationCanceledException)
            {
                progress?.Report(new RecoveryProgress { Operation = "Windows Security Center", Progress = 0, Status = "취소됨" });
                AddUserFriendlyMessage("Windows Security Center 복구가 취소되었습니다.", MessageType.Warning);
                securityCenterRecoveryError = "작업 취소됨";
                // UI 상태 업데이트
                SecurityCenterStatus.Text = "취소됨";
                SecurityCenterProgress.Value = 0;
                return false;
            }
            catch (Exception ex)
            {
                progress?.Report(new RecoveryProgress { Operation = "Windows Security Center", Progress = 0, Status = "오류" });
                securityCenterRecoveryError = ex.Message;
                AddUserFriendlyMessage($"Windows Security Center 복구 중 오류 발생: {ex.Message}", MessageType.Error);
                // UI 상태 업데이트
                SecurityCenterStatus.Text = "오류";
                SecurityCenterProgress.Value = 0;
                return false;
            }
            finally
            {
                HideLoadingOverlay();
                UpdateResultReport();
            }
        }

        private async Task<bool> RecoverBitLocker(IProgress<RecoveryProgress>? progress = null)
        {
            try
            {
                bitlockerAttempts++;

                if (_cancellationTokenSource == null)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                }

                ShowLoadingOverlay("BitLocker 복구 중...");
                var startTime = DateTime.Now;

                progress?.Report(new RecoveryProgress { Operation = "BitLocker", Progress = 0, Status = "복구 시작" });
                // UI 상태 업데이트
                BitLockerStatus.Text = "복구 중...";
                BitLockerProgress.Value = 10;

                // WMIHelper를 사용하여 BitLocker 활성화
                bool success = await WmiHelper.EnableBitLockerAsync();

                bitLockerRecoveryDuration = DateTime.Now - startTime;

                if (success)
                {
                    progress?.Report(new RecoveryProgress { Operation = "BitLocker", Progress = 100, Status = "완료" });
                    AddUserFriendlyMessage("BitLocker가 성공적으로 활성화되었습니다.", MessageType.Success);
                    bitLockerRecoveryError = "";
                    bitlockerRecovered = true;
                    // UI 상태 업데이트
                    BitLockerStatus.Text = "완료";
                    BitLockerProgress.Value = 100;
                    return true;
                }
                else
                {
                    progress?.Report(new RecoveryProgress { Operation = "BitLocker", Progress = 100, Status = "실패" });
                    AddUserFriendlyMessage("BitLocker 활성화에 실패했습니다.", MessageType.Error);
                    bitLockerRecoveryError = "BitLocker 활성화 실패";
                    // UI 상태 업데이트
                    BitLockerStatus.Text = "실패";
                    BitLockerProgress.Value = 100;
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                progress?.Report(new RecoveryProgress { Operation = "BitLocker", Progress = 0, Status = "취소됨" });
                AddUserFriendlyMessage("BitLocker 복구가 취소되었습니다.", MessageType.Warning);
                bitLockerRecoveryError = "작업 취소됨";
                // UI 상태 업데이트
                BitLockerStatus.Text = "취소됨";
                BitLockerProgress.Value = 0;
                return false;
            }
            catch (Exception ex)
            {
                progress?.Report(new RecoveryProgress { Operation = "BitLocker", Progress = 0, Status = "오류" });
                bitLockerRecoveryError = ex.Message;
                AddUserFriendlyMessage($"BitLocker 복구 중 오류 발생: {ex.Message}", MessageType.Error);
                // UI 상태 업데이트
                BitLockerStatus.Text = "오류";
                BitLockerProgress.Value = 0;
                return false;
            }
            finally
            {
                HideLoadingOverlay();
                UpdateResultReport();
            }
        }

        // RecoveryProgress 클래스 정의
        public class RecoveryProgress
        {
            public string Operation { get; set; }
            public int Progress { get; set; }
            public string Status { get; set; }
        }

        // 메시지 타입 열거형
        public enum MessageType
        {
            Information,
            Success,
            Warning,
            Error
        }
    }
    }
