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

namespace WindowsSentinel
{
    /// <summary>
    /// 시스템 설치 프로그램 분석 및 보안 검사 메인 윈도우
    /// 주요 기능:
    /// - 설치된 프로그램 목록 수집
    /// - 프로그램 보안 수준 분석
    /// - Windows 보안 로그 분석
    /// </summary>
    public partial class MainWindow : Window
    {
        // 분석된 프로그램 목록 캐시
        private List<ProgramInfo> programList;
        
        // 중복 프로그램 검사 방지용 집합
        private HashSet<string> processedPrograms;
        
        // 보안 프로그램(Defender/Firewall/BitLocker) 최신 동작 날짜
        public static SecurityDate[] SD = new SecurityDate[3]; 

        /// <summary>
        /// 생성자 - 초기화 및 권한 검증
        /// </summary>
        public MainWindow()
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
            InitializeRadioButtons(); // 라디오 버튼 설정
            CheckLogs(); // 보안 로그 분석 시작
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
        /// 라디오 버튼 초기화 및 이벤트 핸들러 연결
        /// </summary>
        private void InitializeRadioButtons()
        {
            first.Checked += RadioButton_Checked;    // 첫번째 필터
            seconds.Checked += RadioButton_Checked;  // 두번째 필터
            thirds.Checked += RadioButton_Checked;   // 세번째 필터
        }

        /// <summary>
        /// 라디오 버튼 선택 이벤트 핸들러
        /// </summary>
        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (programList != null && programList.Any())
            {
                DisplayFilteredPrograms(); // 선택된 필터 기준 프로그램 표시
            }
        }

        /// <summary>
        /// Windows 보안 로그 분석 (Defender/Firewall/BitLocker)
        /// </summary>
        public void CheckLogs()
        {
            DateTime oneYearAgo = DateTime.Now.AddYears(-1);
            // 로그 업데이트 및 각 프로그램에 적합한 메시지 설정
            SD[0] = new SecurityDate(GetLatestLogDate("Microsoft-Windows-Windows Defender/Operational", new int[] { 5007 }, oneYearAgo, "Windows Defender", firstLogMessage), "Defender");
            SD[1] = new SecurityDate(GetLatestLogDate("Microsoft-Windows-Windows Firewall With Advanced Security/Firewall", new int[] { 2004, 2006, 2033 }, oneYearAgo, "Windows Firewall", secondsLogMessage), "Firewall");
            SD[2] = new SecurityDate(GetLatestLogDate("Microsoft-Windows-BitLocker/Operational", new int[] { 775 }, oneYearAgo, "Windows BitLocker", thirdsLogMessage), "BitLocker");


            Array.Sort(SD, (a, b) => a.Date.CompareTo(b.Date));
            first.Content = SD[0].Program_name;
            seconds.Content = SD[1].Program_name;
            thirds.Content = SD[2].Program_name;
        }

        /// <summary>
        /// 특정 이벤트 로그에서 최신 기록 날짜 조회
        /// </summary>
        private static DateTime GetLatestLogDate(string logName, int[] eventIds, DateTime oneYearAgo, String Program_name, TextBlock logMessage)
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
                // TextBlock에 메시지 설정
                logMessage.Text = $"{Program_name}의 수정 로그를 찾을 수 없습니다.";
                return oneYearAgo;
            }

            return oneYearAgo;
        }

        /// <summary>
        /// [UI 이벤트] 설치 프로그램 검사 버튼 클릭 핸들러
        /// </summary>
        private void btnCollectPrograms_Click(object sender, RoutedEventArgs e)
        {
            CollectInstalledPrograms();  // 프로그램 정보 수집
            DisplayFilteredPrograms();   // 수집된 정보 표시
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

                            var program = new ProgramInfo
                            {
                                Name = displayName,
                                InstallDate = DateTime.MinValue,
                                InstallPath = subkey.GetValue("InstallLocation")?.ToString() ?? "",
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
            if (first.IsChecked == true) { Check_Date = SD[0].Date; check_id = 0; }
            else if (seconds.IsChecked == true) { Check_Date = SD[1].Date; check_id = 1; }
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
            public DateTime InstallDate { get; set; } = DateTime.MinValue;
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
    }
}
