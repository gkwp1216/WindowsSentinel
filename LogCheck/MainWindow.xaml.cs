using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Linq;
using Microsoft.Win32;
using System.Security.Cryptography.X509Certificates;
using System.IO;

/// <summary>
/// Windows Sentinel - 시스템에 설치된 프로그램 모니터링 및 보안 분석 도구
/// 
/// 주요 기능:
/// 1. 설치된 프로그램 검색 및 분석
/// 2. 설치 날짜 기반 필터링 (1일/7일/30일)
/// 3. 프로그램별 보안 상태 분석
/// 4. 방화벽 규칙 및 Windows Defender 예외 확인
/// </summary>
namespace WindowsSentinel
{
    /// <summary>
    /// MainWindow 클래스 - 시스템에 설치된 프로그램을 모니터링하고 보안 상태를 분석하는 메인 윈도우
    /// </summary>
    public partial class MainWindow : Window
    {
        // 검색된 프로그램 정보를 저장하는 컬렉션
        private List<ProgramInfo> programList;
        // 중복 프로그램 체크를 위한 해시셋
        private HashSet<string> processedPrograms;

        /// <summary>
        /// MainWindow 생성자
        /// UI 초기화 및 이벤트 핸들러 설정
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            InitializeRadioButtons();
        }

        /// <summary>
        /// 기간 필터 라디오 버튼들의 이벤트 핸들러 초기화
        /// - rb1Day: 1일 이내 설치된 프로그램 필터
        /// - rb7Days: 7일 이내 설치된 프로그램 필터
        /// - rb30Days: 30일 이내 설치된 프로그램 필터
        /// - rb365Days: 1년 이내 설치된 프로그램 필터
        /// </summary>
        private void InitializeRadioButtons()
        {
            rb1Day.Checked += RadioButton_Checked;    // 1일 필터 이벤트 연결
            rb7Days.Checked += RadioButton_Checked;   // 7일 필터 이벤트 연결
            rb30Days.Checked += RadioButton_Checked;  // 30일 필터 이벤트 연결
            rb365Days.Checked += RadioButton_Checked;  // 1년 필터 이벤트 연결
        }

        /// <summary>
        /// 라디오 버튼 선택 변경 시 호출되는 이벤트 핸들러
        /// 프로그램 목록이 있을 경우 선택된 기간에 따라 필터링하여 표시
        /// </summary>
        /// <param name="sender">이벤트를 발생시킨 라디오 버튼</param>
        /// <param name="e">이벤트 인자</param>
        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            // 프로그램 목록이 존재하고 비어있지 않은 경우에만 필터링 수행
            if (programList != null && programList.Any())
            {
                DisplayFilteredPrograms();
            }
        }

        /// <summary>
        /// '설치된 프로그램 검사' 버튼 클릭 이벤트 핸들러
        /// 시스템에 설치된 프로그램을 검색하고 결과를 표시
        /// </summary>
        private void btnCollectPrograms_Click(object sender, RoutedEventArgs e)
        {
            CollectInstalledPrograms();  // 프로그램 정보 수집
            DisplayFilteredPrograms();    // 수집된 정보 표시
        }

        /// <summary>
        /// 시스템에 설치된 프로그램 정보를 수집하는 메서드
        /// 1. 레지스트리에서 프로그램 정보 검색
        /// 2. 설치 날짜 확인
        /// 3. 보안 상태 분석
        /// 4. 중복 제거 후 목록에 추가
        /// </summary>
        private void CollectInstalledPrograms()
        {
            // 날짜 기준점 설정: 프로그램 설치 날짜를 기준으로 최근 설치된 프로그램을 식별하기 위해 날짜 기준점을 설정합니다.
            // - today: 현재 날짜
            // - day1: 1일 전 (어제)
            // - day7: 7일 전 (일주일 전)
            // - day30: 30일 전 (한달 전)
            // - day365: 1년 전
            DateTime today = DateTime.Today;
            DateTime day1 = today.AddDays(-1);
            DateTime day7 = today.AddDays(-7);
            DateTime day30 = today.AddDays(-30);
            DateTime day365 = today.AddDays(-365);

            // 프로그램 정보를 저장할 컬렉션 초기화: 수집된 프로그램 정보를 저장하고 중복 프로그램을 체크하기 위해 컬렉션을 초기화합니다.
            // - programList: 수집된 프로그램 정보 저장
            // - processedPrograms: 중복 프로그램 체크를 위한 해시셋
            programList = new List<ProgramInfo>();
            processedPrograms = new HashSet<string>();

            // 검색할 레지스트리 경로 설정: 32비트와 64비트 프로그램을 모두 수집하기 위해 두 가지 레지스트리 경로를 설정합니다.
            // - Uninstall: 32비트 프로그램용 레지스트리 경로
            // - WOW6432Node: 64비트 시스템에서 실행되는 32비트 프로그램용 레지스트리 경로
            string[] registryPaths = new string[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            // 검사한 레지스트리 키의 총 개수를 저장
            int totalChecked = 0;
            
            // 각 레지스트리 경로에서 프로그램 정보 검색
            foreach (string regPath in registryPaths)
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(regPath))
                {
                    if (key == null) continue;  // 레지스트리 키가 없는 경우 스킵

                    // 각 프로그램의 레지스트리 키 검사
                    foreach (string subKeyName in key.GetSubKeyNames())
                    {
                        totalChecked++;  // 검사한 레지스트리 키 수 증가
                        using (RegistryKey subKey = key.OpenSubKey(subKeyName))
                        {
                            // 프로그램 정보 추출: 각 프로그램의 세부 정보를 추출하여 프로그램 정보 객체를 생성합니다.
                            // - DisplayName: 프로그램 이름
                            // - InstallDate: 설치 날짜 (yyyyMMdd 형식)
                            // - DisplayVersion: 프로그램 버전
                            // - Publisher: 제작사
                            // - InstallLocation: 설치 위치
                            //   - DisplayName: 프로그램의 표시 이름을 추출합니다.
                            //   - InstallDate: 프로그램의 설치 날짜를 추출하여 설치 날짜를 확인합니다.
                            //   - DisplayVersion: 프로그램의 버전을 추출하여 버전 정보를 확인합니다.
                            //   - Publisher: 프로그램의 제작사를 추출하여 프로그램의 출처를 확인합니다.
                            //   - InstallLocation: 프로그램의 설치 위치를 추출하여 프로그램이 설치된 경로를 확인합니다.
                            string name = subKey?.GetValue("DisplayName") as string;
                            string installDateRaw = subKey?.GetValue("InstallDate") as string;
                            string version = subKey?.GetValue("DisplayVersion") as string;
                            string publisher = subKey?.GetValue("Publisher") as string;
                            string installLocation = subKey?.GetValue("InstallLocation") as string;

                            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(installDateRaw))
                            {
                                string programKey = $"{name}_{version}";
                                if (processedPrograms.Contains(programKey))
                                {
                                    continue;
                                }
                                processedPrograms.Add(programKey);

                                if (DateTime.TryParseExact(installDateRaw, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime installDate))
                                {
                                    string period = "";
                                    if (installDate >= day1) period = "1일 이내";
                                    else if (installDate >= day7) period = "7일 이내";
                                    else if (installDate >= day30) period = "30일 이내";
                                    else if (installDate >= day365) period = "1년 이내";
                                    else continue;

                                    var securityInfo = GetSecurityInfo(installLocation);
                                    programList.Add(new ProgramInfo
                                    {
                                        Name = name,
                                        InstallDate = installDate.ToString("yyyy-MM-dd"),
                                        Period = period,
                                        Version = version ?? "알 수 없음",
                                        Publisher = publisher ?? "알 수 없음",
                                        InstallLocation = installLocation ?? "알 수 없음",
                                        SecurityLevel = securityInfo.SecurityLevel,
                                        SecurityDetails = securityInfo.Details,
                                        HasSecurityChanges = securityInfo.HasSecurityChanges
                                    });
                                }
                            }
                        }
                    }
                }
            }
            /*
            MessageBox.Show($"총 {totalChecked}개의 레지스트리 키를 검사했습니다.\n" +
                          $"중복 제거된 프로그램 수: {duplicateCount}개\n" +
                          $"총 {programList.Count}개의 고유한 프로그램이 발견되었습니다.",
                          "검사 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            */
        }

        /// <summary>
        /// 프로그램 설치 위치의 보안 정보를 수집하는 메서드
        /// 1. 방화벽 규칙 확인
        /// 2. Windows Defender 예외 확인
        /// 3. 디지털 서명 확인
        /// </summary>
        /// <param name="installLocation">프로그램 설치 위치</param>
        /// <returns>보안 정보</returns>
        private SecurityInfo GetSecurityInfo(string installLocation)
        {
            var securityInfo = new SecurityInfo();
            
            if (string.IsNullOrEmpty(installLocation) || installLocation == "알 수 없음")
            {
                securityInfo.SecurityLevel = "알 수 없음";
                securityInfo.Details = "설치 위치를 찾을 수 없음";
                return securityInfo;
            }

            try
            {
        // 1. 방화벽 규칙 확인
        // Windows 방화벽에서 해당 프로그램 경로에 대한 규칙이 있는지 검사
                using (RegistryKey firewallKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallRules"))
                {
                    if (firewallKey != null)
                    {
                        var rules = firewallKey.GetValueNames()
                            .Where(name => name.Contains(installLocation))
                            .ToList();
                        
                        if (rules.Any())
                        {
                            securityInfo.HasSecurityChanges = true;
                            securityInfo.Details += "방화벽 규칙이 추가됨\n";
                        }
                    }
                }

        // 2. Windows Defender 예외 확인
        // Windows Defender의 검사 제외 목록에 프로그램이 포함되어 있는지 검사
                using (RegistryKey defenderKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Defender\Exclusions\Paths"))
                {
                    if (defenderKey != null)
                    {
                        var exclusions = defenderKey.GetValueNames()
                            .Where(name => name.Contains(installLocation))
                            .ToList();
                        
                        if (exclusions.Any())
                        {
                            securityInfo.HasSecurityChanges = true;
                            securityInfo.Details += "Windows Defender 예외로 등록됨\n";
                        }
                    }
                }

                // 실행 파일의 디지털 서명 확인
                var exeFiles = Directory.GetFiles(installLocation, "*.exe", SearchOption.AllDirectories);
                foreach (var exeFile in exeFiles)
                {
                    try
                    {
                // 디지털 서명 확인
                        var cert = X509Certificate.CreateFromSignedFile(exeFile);
                        if (cert != null)
                        {
                            securityInfo.Details += $"디지털 서명 확인됨: {Path.GetFileName(exeFile)}\n";
                        }
                    }
                    catch
                    {
                        securityInfo.Details += $"디지털 서명 없음: {Path.GetFileName(exeFile)}\n";
                    }
                }

                // 보안 수준 결정
        // 수집된 보안 정보를 기반으로 프로그램의 전반적인 보안 수준을 결정
                if (securityInfo.HasSecurityChanges)
                {
            // 보안 변경 사항이 있는 경우 (방화벽 규칙 추가, Defender 예외 등록 등)
            // 보안 수준을 '높음'으로 설정
                    securityInfo.SecurityLevel = "높음";
                }
                else if (securityInfo.Details.Contains("디지털 서명"))
                {
                    securityInfo.SecurityLevel = "중간";
                }
                else
                {
                    securityInfo.SecurityLevel = "낮음";
                }
            }
            catch (Exception ex)
            {
        // 보안 정보 수집 중 오류 발생 시 처리
        // - 파일 접근 권한 부족
        // - 레지스트리 접근 실패
        // - 디지털 서명 확인 실패 등
        // 보안 수준을 '오류'로 설정
                securityInfo.SecurityLevel = "오류";
                securityInfo.Details = $"보안 정보 수집 중 오류 발생: {ex.Message}";
            }

            return securityInfo;
        }

        /// <summary>
        /// 선택된 기간에 따라 프로그램 목록을 필터링하여 표시하는 메서드
        /// </summary>
        private void DisplayFilteredPrograms()
        {
            string selectedPeriod = "";
            if (rb1Day.IsChecked == true) selectedPeriod = "1일 이내";
            else if (rb7Days.IsChecked == true) selectedPeriod = "7일 이내";
            else if (rb30Days.IsChecked == true) selectedPeriod = "30일 이내";
            else if (rb365Days.IsChecked == true) selectedPeriod = "1년 이내";

            var filteredPrograms = programList.Where(p => 
            {
                if (selectedPeriod == "1년 이내") return true;
                else if (selectedPeriod == "30일 이내") return p.Period == "1일 이내" || p.Period == "7일 이내" || p.Period == "30일 이내";
                else if (selectedPeriod == "7일 이내") return p.Period == "1일 이내" || p.Period == "7일 이내";
                else if (selectedPeriod == "1일 이내") return p.Period == "1일 이내";
                return false;
            })
            .OrderBy(p => p.InstallDate)
            .ToList();

            programDataGrid.ItemsSource = filteredPrograms;
            Title = $"Windows Sentinel - {selectedPeriod} 설치된 프로그램 ({filteredPrograms.Count}개)";
        }

        /// <summary>
        /// Windows Defender Antivirus, Windows Defender Firewall, Windows Defender SmartScreen 상태 확인 및 버튼 색상 업데이트
        /// </summary>
        private void CheckSecurityStatus()
        {
            // Windows Defender Antivirus 상태 확인
            bool isAntivirusEnabled = IsDefenderAntivirusEnabled();
            btnDefenderAntivirus.Background = isAntivirusEnabled ? Brushes.LightGreen : Brushes.LightCoral;

            // Windows Defender Firewall 상태 확인
            bool isFirewallEnabled = IsDefenderFirewallEnabled();
            btnDefenderFirewall.Background = isFirewallEnabled ? Brushes.LightGreen : Brushes.LightCoral;

            // Windows Defender SmartScreen 상태 확인
            bool isSmartScreenEnabled = IsDefenderSmartScreenEnabled();
            btnDefenderSmartScreen.Background = isSmartScreenEnabled ? Brushes.LightGreen : Brushes.LightCoral;
        }

        /// <summary>
        /// Windows Defender 버튼 Click 이벤트 핸들러
        /// </summary>
        private void btnDefenderAntivirus_Click(object sender, RoutedEventArgs e)
        {
            CheckSecurityStatus();
        }

        private void btnDefenderFirewall_Click(object sender, RoutedEventArgs e)
        {
            CheckSecurityStatus();
        }

        private void btnDefenderSmartScreen_Click(object sender, RoutedEventArgs e)
        {
            CheckSecurityStatus();
        }

        /// <summary>
        /// Windows Defender Antivirus 활성화 여부 확인
        /// </summary>
        /// <returns>활성화 여부</returns>
        private bool IsDefenderAntivirusEnabled()
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection"))
            {
                return key?.GetValue("DisableRealtimeMonitoring")?.ToString() == "0";
            }
        }

        /// <summary>
        /// Windows Defender Firewall 활성화 여부 확인
        /// </summary>
        /// <returns>활성화 여부</returns>
        private bool IsDefenderFirewallEnabled()
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile"))
            {
                return key?.GetValue("EnableFirewall")?.ToString() == "1";
            }
        }

        /// <summary>
        /// Windows Defender SmartScreen 활성화 여부 확인
        /// </summary>
        /// <returns>활성화 여부</returns>
        private bool IsDefenderSmartScreenEnabled()
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer"))
            {
                return key?.GetValue("SmartScreenEnabled")?.ToString() == "On";
            }
        }

        /// <summary>
        /// 프로그램 정보를 저장하는 클래스
        /// </summary>
        private class ProgramInfo
        {
            /// <summary>
            /// 프로그램 이름
            /// </summary>
            public string Name { get; set; }
            /// <summary>
            /// 설치 날짜
            /// </summary>
            public string InstallDate { get; set; }
            /// <summary>
            /// 설치 기간
            /// </summary>
            public string Period { get; set; }
            /// <summary>
            /// 프로그램 버전
            /// </summary>
            public string Version { get; set; }
            /// <summary>
            /// 프로그램 출판사
            /// </summary>
            public string Publisher { get; set; }
            /// <summary>
            /// 프로그램 설치 위치
            /// </summary>
            public string InstallLocation { get; set; }
            /// <summary>
            /// 프로그램 보안 수준
            /// </summary>
            public string SecurityLevel { get; set; }
            /// <summary>
            /// 프로그램 보안 상세 정보
            /// </summary>
            public string SecurityDetails { get; set; }
            /// <summary>
            /// 프로그램 보안 변경 여부
            /// </summary>
            public bool HasSecurityChanges { get; set; }
        }

        /// <summary>
        /// 보안 정보를 저장하는 클래스
        /// </summary>
        private class SecurityInfo
        {
            /// <summary>
            /// 보안 수준
            /// </summary>
            public string SecurityLevel { get; set; }
            /// <summary>
            /// 보안 상세 정보
            /// </summary>
            public string Details { get; set; }
            /// <summary>
            /// 보안 변경 여부
            /// </summary>
            public bool HasSecurityChanges { get; set; }
        }
    }
}
