/*
 * [버전 변경 사항 요약] 
 * 1. 관리자 권한 확인 로직 추가 - 레지스트리 접근 전 필수 검증
 * 2. Windows Defender 예외 검사 제거 - 불필요한 시스템 접근 최소화
 * 3. 설치 날짜 파싱 로직 강화 - 다양한 형식(yyyyMMdd, Unix time 등) 지원
 * 4. 성능 최적화 - HashSet을 이용한 중복 프로그램 검사 방지
 */

using LogCheck;
using LogCheck.Services; // MalwareBazaarClient
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http; // MalwareBazaar 호출
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using System.Security.Cryptography; // SHA256
using System.Security.Cryptography.X509Certificates;
using System.Security.Policy;
using System.Security.Principal;
using System.Text; // 해시 계산
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using static LogCheck.NetWorks;
using WpfApplication = System.Windows.Application;
using WpfCursors = System.Windows.Input.Cursors;
// Windows Forms와의 충돌을 방지하기 위한 alias 설정
using WpfMessageBox = System.Windows.MessageBox;

namespace LogCheck
{
    /// <summary>
    /// Page1.xaml에 대한 상호 작용 논리
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class ProgramsList : Page
    {
        // 프로그램 목록 관리를 위한 변수
        private List<ProgramInfo> programList = new();
        private CollectionViewSource viewSource = new();

        // 중복 프로그램 검사 방지용 집합
        private HashSet<string> processedPrograms = new();

        // 보안 프로그램(Defender/Firewall/BitLocker) 최신 동작 날짜
        public static SecurityDate[] SD = new SecurityDate[3];

        private ToggleButton _selectedButton;
        private int dotCount = 0;
        private const int maxDots = 3;
        private string baseText = "검사 중";
        private DispatcherTimer? loadingTextTimer;

        public ProgramsList()
        {
            try
            {
                // 관리자 권한 확인
                if (!IsRunningAsAdmin())
                {
                    WpfMessageBox.Show("이 프로그램은 관리자 권한으로 실행해야 합니다.",
                                  "권한 필요",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Warning);
                    WpfApplication.Current.Shutdown();
                    return;
                }

                LogHelper.LogInfo("InstalledPrograms 초기화 시작");

                InitializeComponent();

                SideProgramsListButton.IsChecked = true;

                LogHelper.LogInfo("InitializeComponent 완료");

                loadingTextTimer = new DispatcherTimer();

                // DataGrid 초기화
                var dataGrid = this.FindName("programDataGrid") as DataGrid;
                if (dataGrid != null)
                {
                    viewSource.Source = programList;
                    dataGrid.ItemsSource = viewSource.View;
                    LogHelper.LogInfo("DataGrid 초기화 완료");
                }

                // 보안 로그 분석 시작
                CheckLogs();
                SetupLoadingTextAnimation(); // 로딩 텍스트 애니메이션 설정

                // UI 컨트롤 안전 확인
                if (SpinnerItems != null)
                {
                    SpinnerItems.ItemsSource = CreateSpinnerPoints(40, 50, 50);
                    StartRotation();
                    LogHelper.LogInfo("스피너 초기화 완료");
                }

                LogHelper.LogInfo("InstalledPrograms 초기화 완료");
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"InstalledPrograms 초기화 중 오류 발생: {ex.Message}");
                LogHelper.LogError($"스택 트레이스: {ex.StackTrace}");

                // 기본 초기화만 수행
                try
                {
                    InitializeComponent();
                    loadingTextTimer = new DispatcherTimer();
                    LogHelper.LogInfo("기본 초기화 완료");
                }
                catch (Exception initEx)
                {
                    LogHelper.LogError($"기본 초기화도 실패: {initEx.Message}");
                    WpfMessageBox.Show($"프로그램 초기화 중 오류가 발생했습니다: {initEx.Message}",
                                      "초기화 오류",
                                      MessageBoxButton.OK,
                                      MessageBoxImage.Error);
                }
            }
        }

        private List<System.Windows.Point> CreateSpinnerPoints(double radius, double centerX, double centerY)
        {
            var points = new List<System.Windows.Point>();
            for (int i = 0; i < 8; i++)
            {
                double angle = i * 360.0 / 8 * Math.PI / 180.0;
                double x = centerX + radius * Math.Cos(angle) - 5; // -5는 중심 보정
                double y = centerY + radius * Math.Sin(angle) - 5;
                points.Add(new System.Windows.Point(x, y));
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

        private void LoadingTextTimer_Tick(object? sender, EventArgs e)
        {
            dotCount = (dotCount + 1) % (maxDots + 1);
            LoadingText.Text = baseText + new string('.', dotCount);
        }

        private void ShowLoadingOverlay()
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            loadingTextTimer?.Start();
        }

        private void HideLoadingOverlay()
        {
            loadingTextTimer?.Stop();
            LoadingOverlay.Visibility = Visibility.Collapsed;
            LoadingText.Text = baseText; // 텍스트 초기화
        }

        /// <summary>
        /// 현재 프로세스가 관리자 권한으로 실행 중인지 확인
        /// [신규 추가] 보안 강화를 위해 추가됨
        /// </summary>
        /// <returns>관리자 권한 여부</returns>
        [SupportedOSPlatform("windows")]
        private bool IsRunningAsAdmin()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        /// <summary>
        /// Windows 보안 로그 분석 (Defender/Firewall/BitLocker)
        /// </summary>
        [SupportedOSPlatform("windows")]
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
        [SupportedOSPlatform("windows")]
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
                    .Where(e => eventIds.Contains((int)e.InstanceId) && e.TimeGenerated > oneYearAgo)
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
        /// 레지스트리에서 설치된 프로그램 정보 수집
        /// [변경] Defender 예외 검사 제거로 성능 개선
        /// </summary>
        /// 
        [SupportedOSPlatform("windows")]
        private void CollectInstalledPrograms()
        {

            var Security_Check = new (int Id, int score)[]
            {
                (11707, 0),
                (7045, -10),
                (6, -15),
                (5156, -10),
                (5158, -10),
                (4688, -5),
                (3, 1) //로그확인 필요
            };

            DateTime oneYearAgo = DateTime.Now.AddYears(-1);

            foreach (var sc in Security_Check)
            {
                try
                {
                    long millisecondsInOneYear = 365L * 24 * 60 * 60 * 1000;
                    string query = $"*[System[(EventID={sc.Id}) and TimeCreated[timediff(@SystemTime) <= {millisecondsInOneYear}]]]";
                    using (var reader = new EventLogReader(query))
                    {
                        EventRecord record;
                        while ((record = reader.ReadEvent()) != null)
                        {
                            if (record.TimeCreated != null && record.TimeCreated.Value > oneYearAgo)
                            {
                                if (sc.score > 0)
                                {
                                    var r = (record.FormatDescription()).ToLower();
                                    //DCE 접속 흔적 시간대
                                    if (r.Contains("destination port: 135") || r.Contains("destination port: 445"))
                                        ChangeLogEntry.Install_Date[record.TimeCreated.Value] = -25;

                                    //비표준 포트 외부접속
                                    Match port = Regex.Match(r, @"destinationport: (\d+)");
                                    if (int.Parse(port.Groups[1].Value) >= 49576)
                                        ChangeLogEntry.Install_Date[record.TimeCreated.Value] = -10;
                                }
                                else
                                {
                                    if (ChangeLogEntry.Install_Date.ContainsKey(record.TimeCreated.Value))
                                        ChangeLogEntry.Install_Date[record.TimeCreated.Value] += sc.score;

                                    else ChangeLogEntry.Install_Date[record.TimeCreated.Value] = sc.score;
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            programList = new List<ProgramInfo>();
            processedPrograms = new HashSet<string>();

            DateTime today = DateTime.Now;

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
                            if (subkey == null) continue;

                            string? displayName = subkey.GetValue("DisplayName")?.ToString();
                            if (string.IsNullOrEmpty(displayName) || processedPrograms.Contains(displayName))
                                continue;

                            // 이름에 "update"가 포함된 프로그램 필터링
                            if (displayName.ToLower().Contains("update"))
                                continue;

                            // 설치 경로 가져오기
                            string installLocation = GetInstallLocation(subkey);

                            DateTime installDate = DateTime.MinValue;

                            // 1. 레지스트리의 InstallDate 키 값 확인
                            string? installDateStr = subkey.GetValue("InstallDate")?.ToString();
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
                                SecurityDetails = "",
                                MalwareVerdict = "Unknown"
                            };


                            // 보안 정보 분석
                            int score = 100 + SecurityCheck(installDate) + InstallPath_Check(installLocation)
                                        + (program.Publisher == "" ? -5 : 0);
                            for (int i = 0; i < ProgramSecurityManager.Name.Count; i++)
                            {
                                if (ProgramSecurityManager.Name[i] == displayName)
                                    score += ProgramSecurityManager.Scores[i];
                            }

                            var securityInfo = GetSecurityInfo(program.InstallPath, score);
                            program.SecurityLevel = securityInfo.SecurityLevel;
                            program.SecurityDetails = securityInfo.Details;

                            // MalwareBazaar 검사
                            try
                            {
                                string? exePath = GetRepresentativeExecutable(program.InstallPath, program.Name);
                                if (!string.IsNullOrEmpty(exePath))
                                {
                                    string sha256 = ComputeSha256(exePath);
                                    string verdict = MalwareBazaarClient.GetVerdictAsync(sha256).GetAwaiter().GetResult();
                                    program.MalwareVerdict = verdict;
                                }
                            }
                            catch { /* 무시 */ }

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
        [SupportedOSPlatform("windows")]
        private string GetInstallLocation(RegistryKey subkey)
        {
            if (subkey == null)
            {
                return string.Empty;
            }

            // 모든 가능한 경로 수집
            List<string> paths = new List<string>();

            // 1. InstallLocation (우선순위 1)
            string? installLocation = subkey.GetValue("InstallLocation")?.ToString();
            if (!string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation))
                paths.Add(installLocation);

            // 2. InstallPath (우선순위 2)
            string? installPath = subkey.GetValue("InstallPath")?.ToString();
            if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
                paths.Add(installPath);

            // 3. InstallDir (우선순위 3)
            string? installDir = subkey.GetValue("InstallDir")?.ToString();
            if (!string.IsNullOrEmpty(installDir) && Directory.Exists(installDir))
                paths.Add(installDir);

            // 4. UninstallString에서 경로 추출 (우선순위 4)
            string? uninstallString = subkey.GetValue("UninstallString")?.ToString();
            if (!string.IsNullOrEmpty(uninstallString))
            {
                string path = ExtractPathFromUninstallString(uninstallString);
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    paths.Add(path);
            }

            // 5. DisplayIcon에서 경로 추출 (우선순위 5)
            string? displayIcon = subkey.GetValue("DisplayIcon")?.ToString();
            if (!string.IsNullOrEmpty(displayIcon))
            {
                string path = ExtractPathFromDisplayIcon(displayIcon);
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    paths.Add(path);
            }

            // 중복 제거 및 우선순위에 따라 정렬
            paths = paths.Distinct().ToList();

            // 우선순위에 따라 첫 번째 경로 반환
            return paths.FirstOrDefault() ?? string.Empty;
        }

        /// <summary>
        /// UninstallString에서 설치 경로를 추출합니다.
        /// </summary>
        [SupportedOSPlatform("windows")]
        private string ExtractPathFromUninstallString(string uninstallString)
        {
            if (string.IsNullOrEmpty(uninstallString))
            {
                return string.Empty;
            }

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
                        return Path.GetDirectoryName(path) ?? string.Empty;
                    }
                }

                // 공백으로 구분된 첫 번째 경로 추출
                string[] parts = uninstallString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    string path = parts[0];
                    if (File.Exists(path) || Directory.Exists(path))
                        return Path.GetDirectoryName(path) ?? string.Empty;
                }
            }
            catch (Exception)
            {
                // 경로 추출 중 오류 발생
            }

            return string.Empty;
        }

        /// <summary>
        /// DisplayIcon에서 설치 경로를 추출합니다.
        /// </summary>
        [SupportedOSPlatform("windows")]
        private string ExtractPathFromDisplayIcon(string displayIcon)
        {
            if (string.IsNullOrEmpty(displayIcon))
            {
                return string.Empty;
            }

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
                        return Path.GetDirectoryName(path) ?? string.Empty;
                    }
                }

                // 쉼표로 구분된 첫 번째 경로 추출
                string[] parts = displayIcon.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    string path = parts[0].Trim();
                    if (File.Exists(path) || Directory.Exists(path))
                        return Path.GetDirectoryName(path) ?? string.Empty;
                }
            }
            catch (Exception)
            {
                // 경로 추출 중 오류 발생
            }

            return string.Empty;
        }

        /// <summary>
        /// 프로그램 보안 정보 분석
        /// [변경] Defender 예외 검사 제거된 버전
        /// </summary>
        [SupportedOSPlatform("windows")]
        private SecurityInfo GetSecurityInfo(string installLocation, int score)
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
                            var cert2 = new X509Certificate2(cert);

                            bool isValid = cert2.Verify();

                            if (isValid)
                                info.Details += $"{Path.GetFileName(exe)}: 서명 있음 (유효)\n";
                            else
                                info.Details += $"{Path.GetFileName(exe)}: 서명 있음 (⚠ 유효하지 않음)\n";
                        }
                        catch
                        {
                            info.Details += $"{Path.GetFileName(exe)}: 서명 없음\n";
                        }
                    }

                }

                score += info.Details.Contains("서명 있음") ? 0 : -20;
                score += info.Details.Contains("유효하지 않음") ? -30 : 0;
                if (score >= 80) info.SecurityLevel = "정상";
                else if (score >= 50) info.SecurityLevel = "주의";
                else info.SecurityLevel = "위험";
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
            if (SD == null || SD.Length < 3 || programList == null) return;

            int check_id = 0;
            DateTime Check_Date = SD[0].Date;

            // 가장 최근 날짜 찾기
            for (int i = 1; i < SD.Length; i++)
            {
                if (SD[i].Date > Check_Date)
                {
                    Check_Date = SD[i].Date;
                    check_id = i;
                }
            }

            var filteredPrograms = programList
                .OrderByDescending(p => p.InstallDate)
                .ToList();

            viewSource.Source = filteredPrograms;

            // DataGrid 업데이트
            var dataGrid = this.FindName("programDataGrid") as DataGrid;
            if (dataGrid != null)
            {
                dataGrid.ItemsSource = viewSource.View;
                dataGrid.Items.Refresh();
            }

            Title = $"Windows Sentinel - {SD[check_id].Program_name} 설치된 프로그램 ({filteredPrograms.Count}개)";
        }

        /// <summary>
        /// 프로그램 정보 저장용 클래스
        /// </summary>
        public class ProgramInfo
        {
            public string Name { get; set; }
            public DateTime? InstallDate { get; set; }
            public string InstallPath { get; set; }
            public string Version { get; set; }
            public string Publisher { get; set; }
            public string SecurityLevel { get; set; }
            public string SecurityDetails { get; set; }
            // MalwareBazaar 판정 결과 ("Malicious" / "Unknown")
            public string MalwareVerdict { get; set; }

            public ProgramInfo()
            {
                Name = "알 수 없음";
                InstallDate = null;
                InstallPath = "";
                Version = "";
                Publisher = "";
                SecurityLevel = "";
                SecurityDetails = "";
                MalwareVerdict = "Unknown";
            }
        }

        /// <summary>
        /// 보안 정보 저장용 클래스
        /// </summary>
        public class SecurityInfo
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

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor = WpfCursors.Wait;
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
                WpfMessageBox.Show($"프로그램 검사 중 오류가 발생했습니다: {ex.Message}",
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

        private void NavigateToPage(Page page)
        {
            var mainWindow = Window.GetWindow(this) as MainWindows;
            mainWindow?.NavigateToPage(page);
        }
        private int SecurityCheck(DateTime time)
        {
            int secuScore = 0;
            foreach (var insD in ChangeLogEntry.Install_Date)
            {
                if (insD.Key >= time && insD.Key <= time.AddMinutes(5))
                    secuScore += insD.Value; //Log.xaml.cs에서 설정한 보안점수
                else secuScore += (-15); //로그는 발생하지 않았으나 설치된 경우
            }
            if (time.Date == new DateTime(0001, 01, 01)) secuScore -= 100;
            return secuScore;
        }
        private int InstallPath_Check(string InstallLocation)
        {
            string lnsLo = InstallLocation.ToLower();
            if (lnsLo.Contains("programfiles") || string.IsNullOrEmpty(lnsLo)) return 0;
            else return -10;
        }

        /// <summary>
        /// 설치 경로에서 대표 실행 파일 추정 (디렉터리 내 가장 큰 exe 또는 이름이 프로그램명과 유사한 파일)
        /// </summary>
        private string? GetRepresentativeExecutable(string installPath, string programName)
        {
            if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
                return null;

            var exeFiles = Directory.GetFiles(installPath, "*.exe", SearchOption.TopDirectoryOnly);
            if (exeFiles.Length == 0) return null;

            // 프로그램 이름과 유사한 exe 우선
            var match = exeFiles.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(programName, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(match)) return match;

            // 가장 큰 파일 선택
            return exeFiles.OrderByDescending(f => new FileInfo(f).Length).First();
        }

        /// <summary>
        /// SHA-256 해시 계산
        /// </summary>
        private static string ComputeSha256(string filePath)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha.ComputeHash(stream);
            var sb = new StringBuilder();
            foreach (byte b in hash)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}