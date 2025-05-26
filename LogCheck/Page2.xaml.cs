/*
 * [버전 변경 사항 요약] 
 * 1. 관리자 권한 확인 로직 추가 - 레지스트리 접근 전 필수 검증
 * 2. Windows Defender 예외 검사 제거 - 불필요한 시스템 접근 최소화
 * 3. 설치 날짜 파싱 로직 강화 - 다양한 형식(yyyyMMdd, Unix time 등) 지원
 * 4. 성능 최적화 - HashSet을 이용한 중복 프로그램 검사 방지
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Diagnostics;
using System.Linq;
using Microsoft.Win32;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Security.Principal;
using System.Windows.Controls;
using WindowsSentinel;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Animation;
using System.Windows.Input;

namespace WindowsSentinel
{
    /// <summary>
    /// Page2.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class Page2 : Page
    {
        // 분석된 프로그램 목록 캐시
        private List<ProgramInfo> programList;

        // 중복 프로그램 검사 방지용 집합
        private HashSet<string> processedPrograms;

        // 보안 프로그램(Defender/Firewall/BitLocker) 최신 동작 날짜
        public static SecurityDate[] SD = new SecurityDate[3];

        private DispatcherTimer loadingTextTimer;
        private int dotCount = 0;
        private const int maxDots = 3;
        private string baseText = "검사 중";

        public Page2()
        {
            // [변경] 레지스트리 접근 전 관리자 권한 확인
            if (!IsRunningAsAdmin())
            {
                MessageBox.Show("이 프로그램은 관리자 권한으로 실행해야 합니다.",
                              "권한 필요",
                              MessageBoxButton.OK,
                              MessageBoxImage.Warning);
                Application.Current.Shutdown();
                return;
            }

            InitializeComponent();  // UI 컴포넌트 초기화
            CheckLogs(); // 보안 로그 분석 시작
            SetupLoadingTextAnimation(); // 로딩 텍스트 애니메이션 설정
            SpinnerItems.ItemsSource = CreateSpinnerPoints(40, 50, 50);
            StartRotation();
        }

        private List<Point> CreateSpinnerPoints(double radius, double centerX, double centerY)
        {
            var points = new List<Point>();
            for (int i = 0; i < 8; i++)
            {
                double angle = i * 360.0 / 8 * Math.PI / 180.0;
                double x = centerX + radius * Math.Cos(angle) - 5; // -5는 중심 보정
                double y = centerY + radius * Math.Sin(angle) - 5;
                points.Add(new Point(x, y));
            }
            return points;
        }
        private void StartRotation()
        {
            var rotateAnimation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = new Duration(TimeSpan.FromSeconds(2.0)),
                RepeatBehavior = RepeatBehavior.Forever
            };
            SpinnerRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, rotateAnimation);
        }

        private void SetupLoadingTextAnimation()
        {
            loadingTextTimer = new DispatcherTimer();
            loadingTextTimer.Interval = TimeSpan.FromMilliseconds(500);
            loadingTextTimer.Tick += LoadingTextTimer_Tick;
        }

        private void LoadingTextTimer_Tick(object sender, EventArgs e)
        {
            dotCount = (dotCount + 1) % (maxDots + 1);
            LoadingText.Text = baseText + new string('.', dotCount);
        }

        private void ShowLoadingOverlay()
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            loadingTextTimer.Start();
        }

        private void HideLoadingOverlay()
        {
            loadingTextTimer.Stop();
            LoadingOverlay.Visibility = Visibility.Collapsed;
            LoadingText.Text = baseText; // 텍스트 초기화
        }

        /// <summary>
        /// 현재 프로세스가 관리자 권한으로 실행 중인지 확인
        /// [신규 추가] 보안 강화를 위해 추가됨
        /// </summary>
        /// <returns>관리자 권한 여부</returns>
        private bool IsRunningAsAdmin()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        /// <summary>
        /// Windows 보안 로그 분석 (Defender/Firewall/BitLocker)
        /// </summary>
        public void CheckLogs()
        {
            DateTime oneYearAgo = DateTime.Now.AddYears(-1);
            // 로그 업데이트 및 각 프로그램에 적합한 메시지 설정
            SD[0] = new SecurityDate(GetLatestLogDate("Microsoft-Windows-Windows Defender/Operational", new int[] { 5007 }, oneYearAgo, "Windows Defender"), "Defender");
            SD[1] = new SecurityDate(GetLatestLogDate("Microsoft-Windows-Windows Firewall With Advanced Security/Firewall", new int[] { 2004, 2006, 2033 }, oneYearAgo, "Windows Firewall"), "Firewall");
            SD[2] = new SecurityDate(GetLatestLogDate("Microsoft-Windows-BitLocker/Operational", new int[] { 775 }, oneYearAgo, "Windows BitLocker"), "BitLocker");

            Array.Sort(SD, (a, b) => a.Date.CompareTo(b.Date));
            UpdateSecurityStatus();
        }

        /// <summary>
        /// 보안 상태 업데이트
        /// </summary>
        private void UpdateSecurityStatus()
        {
            // 여기에 보안 상태 UI 업데이트 로직 추가
        }

        /// <summary>
        /// 특정 이벤트 로그에서 최신 기록 날짜 조회
        /// </summary>
        private static DateTime GetLatestLogDate(string logName, int[] eventIds, DateTime oneYearAgo, String Program_name)
        {
            try
            {
                EventLog eventLog = new EventLog(logName);
                if (eventLog?.Entries == null || eventIds == null || !eventIds.Any())
                {
                    return oneYearAgo;
                }

                var recentLog = eventLog.Entries
                    .Cast<EventLogEntry>()
                    .Where(e => eventIds.Contains(e.EventID) && e.TimeGenerated > oneYearAgo)
                    .OrderByDescending(e => e.TimeGenerated)
                    .FirstOrDefault();

                if (recentLog != null)
                {
                    return recentLog.TimeGenerated;
                }
            }
            catch (Exception)
            {
                return oneYearAgo;
            }

            return oneYearAgo;
        }

        /// <summary>
        /// 프로그램 검사 버튼 클릭 핸들러
        /// </summary>
        private async void BtnCheck_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                ShowLoadingOverlay();

                // 강제로 UI 렌더링을 실행 (로딩 스피너 표시)
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);

                await Task.Run(() =>
                {
                    CollectInstalledPrograms();
                    System.Threading.Thread.Sleep(5000);
                });

                DisplayFilteredPrograms();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"프로그램 검사 중 오류가 발생했습니다: {ex.Message}",
                              "오류",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                HideLoadingOverlay();
            }
        }

        /// <summary>
        /// 레지스트리에서 설치된 프로그램 정보 수집
        /// [변경] Defender 예외 검사 제거로 성능 개선
        /// </summary>
        private void CollectInstalledPrograms()
        {
            programList = new List<ProgramInfo>();
            processedPrograms = new HashSet<string>();

            DateTime today = DateTime.Now;
            DateTime oneYearAgo = today.AddYears(-1);

            string[] registryPaths = new string[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (string regPath in registryPaths)
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(regPath))
                {
                    if (key == null) continue;

                    foreach (string subkeyName in key.GetSubKeyNames())
                    {
                        using (RegistryKey subkey = key.OpenSubKey(subkeyName))
                        {
                            string displayName = subkey?.GetValue("DisplayName")?.ToString();
                            if (string.IsNullOrEmpty(displayName) || processedPrograms.Contains(displayName))
                                continue;

                            // 이름에 "update"가 포함된 프로그램 필터링
                            if (displayName.ToLower().Contains("update"))
                                continue;

                            // 설치 경로 가져오기
                            string installLocation = GetInstallLocation(subkey);

                            DateTime installDate = DateTime.MinValue;

                            // 1. 레지스트리의 InstallDate 키 값 확인
                            string installDateStr = subkey.GetValue("InstallDate")?.ToString();
                            if (!string.IsNullOrEmpty(installDateStr))
                            {
                                installDate = ParseInstallDate(installDateStr) ?? DateTime.MinValue;
                            }

                            // 2. InstallDate가 없는 경우 InstallLocation의 파일 생성 시간 확인
                            if (installDate == DateTime.MinValue && !string.IsNullOrEmpty(installLocation))
                            {
                                try
                                {
                                    if (Directory.Exists(installLocation))
                                    {
                                        var directoryInfo = new DirectoryInfo(installLocation);
                                        installDate = directoryInfo.CreationTime;
                                    }
                                }
                                catch (Exception)
                                {
                                    // 파일 접근 권한 문제 등으로 인한 예외 처리
                                }
                            }

                            var program = new ProgramInfo
                            {
                                Name = displayName,
                                InstallDate = installDate,
                                InstallPath = installLocation,
                                Version = subkey.GetValue("DisplayVersion")?.ToString() ?? "",
                                Publisher = subkey.GetValue("Publisher")?.ToString() ?? "",
                                SecurityLevel = "",
                                SecurityDetails = ""
                            };

                            // 보안 정보 분석
                            var securityInfo = GetSecurityInfo(program.InstallPath);
                            program.SecurityLevel = securityInfo.SecurityLevel;
                            program.SecurityDetails = securityInfo.Details;

                            programList.Add(program);
                            processedPrograms.Add(displayName);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 프로그램의 설치 경로를 가져옵니다.
        /// </summary>
        private string GetInstallLocation(RegistryKey subkey)
        {
            // 모든 가능한 경로 수집
            List<string> paths = new List<string>();

            // 1. InstallLocation (우선순위 1)
            string installLocation = subkey.GetValue("InstallLocation")?.ToString() ?? "";
            if (!string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation))
                paths.Add(installLocation);

            // 2. InstallPath (우선순위 2)
            string installPath = subkey.GetValue("InstallPath")?.ToString() ?? "";
            if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
                paths.Add(installPath);

            // 3. InstallDir (우선순위 3)
            string installDir = subkey.GetValue("InstallDir")?.ToString() ?? "";
            if (!string.IsNullOrEmpty(installDir) && Directory.Exists(installDir))
                paths.Add(installDir);

            // 4. UninstallString에서 경로 추출 (우선순위 4)
            string uninstallString = subkey.GetValue("UninstallString")?.ToString() ?? "";
            if (!string.IsNullOrEmpty(uninstallString))
            {
                string path = ExtractPathFromUninstallString(uninstallString);
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    paths.Add(path);
            }

            // 5. DisplayIcon에서 경로 추출 (우선순위 5)
            string displayIcon = subkey.GetValue("DisplayIcon")?.ToString() ?? "";
            if (!string.IsNullOrEmpty(displayIcon))
            {
                string path = ExtractPathFromDisplayIcon(displayIcon);
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    paths.Add(path);
            }

            // 중복 제거 및 우선순위에 따라 정렬
            paths = paths.Distinct().ToList();

            // 우선순위에 따라 첫 번째 경로 반환
            return paths.FirstOrDefault() ?? "";
        }

        /// <summary>
        /// UninstallString에서 설치 경로를 추출합니다.
        /// </summary>
        private string ExtractPathFromUninstallString(string uninstallString)
        {
            try
            {
                // 따옴표로 묶인 경로 추출
                if (uninstallString.Contains("\""))
                {
                    int start = uninstallString.IndexOf('"') + 1;
                    int end = uninstallString.IndexOf('"', start);
                    if (end > start)
                    {
                        string path = uninstallString.Substring(start, end - start);
                        return Path.GetDirectoryName(path);
                    }
                }

                // 공백으로 구분된 첫 번째 경로 추출
                string[] parts = uninstallString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    string path = parts[0];
                    if (File.Exists(path) || Directory.Exists(path))
                        return Path.GetDirectoryName(path);
                }
            }
            catch (Exception)
            {
                // 경로 추출 중 오류 발생
            }

            return "";
        }

        /// <summary>
        /// DisplayIcon에서 설치 경로를 추출합니다.
        /// </summary>
        private string ExtractPathFromDisplayIcon(string displayIcon)
        {
            try
            {
                // 따옴표로 묶인 경로 추출
                if (displayIcon.Contains("\""))
                {
                    int start = displayIcon.IndexOf('"') + 1;
                    int end = displayIcon.IndexOf('"', start);
                    if (end > start)
                    {
                        string path = displayIcon.Substring(start, end - start);
                        return Path.GetDirectoryName(path);
                    }
                }

                // 쉼표로 구분된 첫 번째 경로 추출
                string[] parts = displayIcon.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    string path = parts[0].Trim();
                    if (File.Exists(path) || Directory.Exists(path))
                        return Path.GetDirectoryName(path);
                }
            }
            catch (Exception)
            {
                // 경로 추출 중 오류 발생
            }

            return "";
        }

        /// <summary>
        /// 프로그램 보안 정보 분석
        /// [변경] Defender 예외 검사 제거된 버전
        /// </summary>
        private SecurityInfo GetSecurityInfo(string installLocation)
        {
            var info = new SecurityInfo();
            try
            {
                if (!string.IsNullOrEmpty(installLocation))
                {
                    // 디지털 서명 확인
                    var exeFiles = Directory.GetFiles(installLocation, "*.exe", SearchOption.TopDirectoryOnly);
                    foreach (var exe in exeFiles)
                    {
                        try
                        {
                            var cert = X509Certificate.CreateFromSignedFile(exe);
                            info.Details += $"{Path.GetFileName(exe)}: 서명 있음\n";
                        }
                        catch
                        {
                            info.Details += $"{Path.GetFileName(exe)}: 서명 없음\n";
                        }
                    }
                }

                info.SecurityLevel = info.Details.Contains("서명 있음") ? "중간" : "낮음";
            }
            catch (Exception ex)
            {
                info.Details = $"보안 검사 오류: {ex.Message}";
                info.SecurityLevel = "오류";
            }
            return info;
        }

        /// <summary>
        /// 설치 날짜 문자열 파싱 (다양한 형식 지원)
        /// [변경] yyyyMMdd, Unix time 등 추가 지원
        /// </summary>
        private DateTime? ParseInstallDate(string rawDate)
        {
            if (rawDate == null) return null;

            // yyyyMMdd 형식
            if (rawDate.Length == 8 && int.TryParse(rawDate, out _))
            {
                if (DateTime.TryParseExact(rawDate, "yyyyMMdd", null, DateTimeStyles.None, out var date))
                    return date;
            }

            // Unix time 형식
            if (long.TryParse(rawDate, out var unixTime) && unixTime > 0)
            {
                return DateTimeOffset.FromUnixTimeSeconds(unixTime).DateTime;
            }

            // 일반 DateTime 형식
            if (DateTime.TryParse(rawDate, out var fallbackDate))
                return fallbackDate;

            return null;
        }

        /// <summary>
        /// 필터링된 프로그램 목록 표시
        /// </summary>
        private void DisplayFilteredPrograms()
        {
            int check_id;
            DateTime Check_Date;
            if (SD[0].Date > SD[1].Date && SD[0].Date > SD[2].Date) { Check_Date = SD[0].Date; check_id = 0; }
            else if (SD[1].Date > SD[2].Date) { Check_Date = SD[1].Date; check_id = 1; }
            else { Check_Date = SD[2].Date; check_id = 2; }

            var filteredPrograms = programList
                .OrderByDescending(p => p.InstallDate)
                .ToList();

            programDataGrid.ItemsSource = filteredPrograms;
            Title = $"Windows Sentinel - {SD[check_id].Program_name} 설치된 프로그램 ({filteredPrograms.Count}개)";
        }

        /// <summary>
        /// 프로그램 정보 저장용 클래스
        /// </summary>
        private class ProgramInfo
        {
            public string Name { get; set; } = "알 수 없음";
            public DateTime? InstallDate { get; set; } = DateTime.MinValue;
            public string InstallPath { get; set; } = "";
            public string Version { get; set; } = "";
            public string Publisher { get; set; } = "";
            public string SecurityLevel { get; set; } = "";
            public string SecurityDetails { get; set; } = "";
        }

        /// <summary>
        /// 보안 정보 저장용 클래스
        /// </summary>
        private class SecurityInfo
        {
            public string Details { get; set; } = "";
            public string SecurityLevel { get; set; } = "낮음";
            public bool HasSecurityChanges { get; set; } = false;
        }

        /// <summary>
        /// 보안 프로그램 날짜 정보 구조체
        /// </summary>
        public struct SecurityDate
        {
            public DateTime Date;
            public String Program_name;

            public SecurityDate(DateTime date, string name)
            {
                Date = date;
                Program_name = name;
            }
        }

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
            NavigateToPage(new Recovery());
        }

        private void NavigateToPage(Page page)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.NavigateToPage(page);
        }
    }
}