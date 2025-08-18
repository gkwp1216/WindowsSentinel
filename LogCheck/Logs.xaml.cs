using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using WpfMessageBox = System.Windows.MessageBox;

namespace LogCheck
{
    public partial class Logs : Page
    {
        private ToggleButton _selectedButton;

        public Logs()
        {
            InitializeComponent();

            SideLogsButton.IsChecked = true;
        }

        public class ChangeLogEntry
        {
            public int EventId { get; set; }
            public DateTime Date { get; set; }
            public required string ProgramName { get; set; }
            public string? Reason { get; set; }

            public static Dictionary<DateTime, int> Install_Date = new Dictionary<DateTime, int>();
        }

        [SupportedOSPlatform("windows")]
        private void BtnShowChangeLogs_Click(object sender, RoutedEventArgs e)
        {
            logsSection.Visibility = Visibility.Visible;
            logsDataGrid.ItemsSource = null;

            int countLimit = 30;

            var logEntries = new List<ChangeLogEntry>();
            var Counts = new Dictionary<int, int>();

            Counts[5007] = 0;
            Counts[2004] = 0;
            Counts[2006] = 0;
            Counts[2033] = 0;
            Counts[775] = 0;

            Counts[4624] = 0;
            Counts[4625] = 0;
            Counts[4672] = 0;

            var eventSources = new (int Id, string LogName, string ProgramName)[]
            {
                (5007, "Microsoft-Windows-Windows Defender/Operational", "Windows Defender"),
                (2004, "Microsoft-Windows-Windows Firewall With Advanced Security/Firewall", "Windows Firewall"),
                (2006, "Microsoft-Windows-Windows Firewall With Advanced Security/Firewall", "Windows Firewall"),
                (2033, "Microsoft-Windows-Windows Firewall With Advanced Security/Firewall", "Windows Firewall"),
                (775,  "Microsoft-Windows-BitLocker/Operational", "BitLocker")
            };

            var externalSources = new (int Id, string LogName, string ProgramName)[]
            {
                (4624, "Security", "Windows Security (로그인 성공)"),
                (4625, "Security", "Windows Security (로그인 실패)"),
                (4672, "Security", "Windows Security (특권 할당)")
            };

            DateTime oneYearAgo = DateTime.Now.AddYears(-1);

            foreach (var es in (isEventChecked ? eventSources : externalSources))
            {
                try
                {
                    long millisecondsInOneYear = 365L * 24 * 60 * 60 * 1000;
                    string query = $"*[System[(EventID={es.Id}) and TimeCreated[timediff(@SystemTime) <= {millisecondsInOneYear}]]]";
                    var eventQuery = new EventLogQuery(es.LogName, PathType.LogName, query)
                    {
                        ReverseDirection = true
                    };

                    using (var reader = new EventLogReader(eventQuery))
                    {
                        EventRecord record;
                        while ((record = reader.ReadEvent()) != null)
                        {
                            if (record.TimeCreated != null && record.TimeCreated.Value > oneYearAgo && Counts[record.Id] < countLimit)
                            {
                                string message = record.FormatDescription() ?? "(설명 없음)";

                                Counts[record.Id]++;

                                logEntries.Add(new ChangeLogEntry
                                {
                                    EventId = record.Id,
                                    Date = record.TimeCreated.Value,
                                    ProgramName = es.ProgramName,
                                    Reason = AnalyzeReason(message, record.Id)
                                });
                            }
                        }
                    }
                    if (Counts[es.Id] == 0)
                    {
                        logEntries.Add(new ChangeLogEntry
                        {
                            EventId = es.Id,
                            Date = DateTime.Now,
                            ProgramName = es.ProgramName,
                            Reason = "해당 이벤트 코드가 발생하지 않았습니다."
                        });
                    }
                }
                catch (Exception ex)
                {
                    logEntries.Add(new ChangeLogEntry
                    {
                        EventId = es.Id,
                        Date = DateTime.Now,
                        ProgramName = es.ProgramName,
                        Reason = $"로그 읽기 실패: {ex.Message}"
                    });
                }
            }
            logEntries = logEntries
                .GroupBy(entry => new { entry.EventId, entry.Date })  // EventId와 Date 기준으로 그룹화
                .Select(group => group.First())  // 그룹 내 첫 번째 항목만 선택
                .ToList();

            // DataGrid에 표시
            logsDataGrid.ItemsSource = logEntries;
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

        private bool isEventChecked = true;
        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (sender == chkExternalLog)
            {
                chkEventLog.IsChecked = false;
                isEventChecked = false;
            }
            else if (sender == chkEventLog)
            {
                chkExternalLog.IsChecked = false;
                isEventChecked = true;
            }
        }
        private string AnalyzeReason(string reason, int eventcode)
        {
            string r = reason.ToLower();
            if (eventcode == 5007)
            {
                // 1. 실시간 보호 비활성화
                if (r.Contains("real-time protection disabled") ||
                    (r.Contains("windows defender") && r.Contains("protection") && r.Contains("disabled")) ||
                    r.Contains("protection off") || r.Contains("실시간 보호 비활성화") || r.Contains("실시간 보호 해제"))
                    return "Windows Defender 실시간 보호 비활성화";

                // 2. 클라우드 보호 비활성화
                else if (r.Contains("cloud-delivered protection disabled") ||
                    (r.Contains("windows defender") && r.Contains("cloud") && r.Contains("disabled")) ||
                    r.Contains("cloud protection off") || r.Contains("클라우드 제공 보호 비활성화"))
                    return "Windows Defender 클라우드 제공 보호 비활성화";

                // 3. 샘플 제출 비활성화
                else if (r.Contains("automatic sample submission disabled") ||
                    (r.Contains("windows defender") && r.Contains("sample submission") && r.Contains("disabled")) ||
                    r.Contains("sample submission off") || r.Contains("자동 샘플 제출 비활성화"))
                    return "Windows Defender 샘플 제출 비활성화";

                // 4. 타사 보안 소프트웨어에 의한 기능 변경
                else if (r.Contains("third-party security software") ||
                    r.Contains("third-party antivirus") ||
                    (r.Contains("windows defender") && r.Contains("disabled") && r.Contains("third-party")) ||
                    (r.Contains("타사 보안 소프트웨어") && r.Contains("windows defender") && r.Contains("비활성화")))
                    return "타사 보안 소프트웨어에 의한 Windows Defender 기능 변경";

                // 5. 그룹 정책 변경
                else if ((r.Contains("group policy") && r.Contains("changed")) ||
                    (r.Contains("windows defender") && r.Contains("group policy")) ||
                    (r.Contains("group policy") && r.Contains("real-time protection") && r.Contains("disabled")) ||
                    (r.Contains("그룹 정책") && r.Contains("변경")))
                    return "그룹 정책을 통한 보안 설정 변경";

                // 6. 레지스트리 수정
                else if (r.Contains("registry modified") ||
                    (r.Contains("windows defender") && r.Contains("registry")) ||
                    (r.Contains("windows defender") && r.Contains("setting") && r.Contains("changed")) ||
                    (r.Contains("registry update") && r.Contains("windows defender")) ||
                    r.Contains("레지스트리 수정"))
                    return "레지스트리 수정";

                // 7. 악성코드 탐지 후 자동 설정 변경
                else if ((r.Contains("windows defender") && r.Contains("security settings") && r.Contains("changed")) ||
                    (r.Contains("windows defender") && r.Contains("automatic change")) ||
                    (r.Contains("windows defender") && r.Contains("settings updated")) ||
                    r.Contains("security change after threat detection") ||
                    (r.Contains("악성 코드 탐지 후") && r.Contains("보안 설정 변경")))
                    return "보안 설정이 자동으로 변경됨 (악성 코드 탐지 후)";

                else return "정의된 보안 이벤트 유형에 해당하지 않습니다.";
            }
            else if (eventcode == 2033)
            {
                // 1. 방화벽 규칙 추가
                if (r.Contains("firewall rule added") ||
                    (r.Contains("added rule") && r.Contains("firewall")) ||
                    (r.Contains("new rule") && r.Contains("firewall")) ||
                    (r.Contains("firewall") && r.Contains("rule added")) ||
                    r.Contains("방화벽 규칙 추가"))
                    return "방화벽 규칙 추가";

                // 2. 방화벽 규칙 삭제
                else if (r.Contains("firewall rule deleted") ||
                    (r.Contains("deleted rule") && r.Contains("firewall")) ||
                    (r.Contains("firewall") && r.Contains("rule deleted")) ||
                    (r.Contains("rule removed") && r.Contains("firewall")) ||
                    r.Contains("방화벽 규칙 삭제"))
                    return "방화벽 규칙 삭제";

                // 3. 방화벽 규칙 수정
                else if (r.Contains("firewall rule modified") ||
                    (r.Contains("modified rule") && r.Contains("firewall")) ||
                    (r.Contains("firewall") && r.Contains("rule modified")) ||
                    (r.Contains("updated rule") && r.Contains("firewall")) ||
                    r.Contains("방화벽 규칙 수정"))
                    return "방화벽 규칙 수정";

                // 4. 방화벽 규칙 초기화 또는 기본값 복원
                else if (r.Contains("firewall rule reset") ||
                    (r.Contains("firewall rules") && r.Contains("reset to default")) ||
                    (r.Contains("firewall") && r.Contains("reset")) ||
                    (r.Contains("restore default rules") && r.Contains("firewall")) ||
                    r.Contains("방화벽 규칙 초기화"))
                    return "방화벽 규칙 초기화 또는 기본값 복원";

                // 5. 방화벽 규칙 그룹 정책에 의한 변경
                else if ((r.Contains("group policy") && r.Contains("firewall rule")) ||
                    r.Contains("firewall rule modified by group policy") ||
                    (r.Contains("group policy") && r.Contains("applied")) ||
                    (r.Contains("firewall") && r.Contains("group policy change")) ||
                    (r.Contains("그룹 정책") && r.Contains("방화벽 규칙")))
                    return "방화벽 규칙 그룹 정책에 의한 변경";

                // 6. 타사 보안 소프트웨어에 의한 방화벽 규칙 변경
                else if (r.Contains("third-party firewall rule modification") ||
                    (r.Contains("firewall rule") && r.Contains("third-party software")) ||
                    (r.Contains("external security software") && r.Contains("firewall")) ||
                    (r.Contains("타사 보안 소프트웨어") && r.Contains("방화벽 규칙 변경")))
                    return "타사 보안 소프트웨어에 의한 방화벽 규칙 변경";

                // 7. 방화벽 규칙 적용 실패
                else if (r.Contains("firewall rule application failed") ||
                    (r.Contains("failed to apply rule") && r.Contains("firewall")) ||
                    (r.Contains("firewall rule") && r.Contains("application error")) ||
                    r.Contains("방화벽 규칙 적용 실패"))
                    return "방화벽 규칙 적용 실패";

                else
                    return "정의된 방화벽 이벤트 유형에 해당하지 않습니다.";
            }
            else if (eventcode == 2006)
            {
                // 1. 언어 팩 설치 실패
                if (r.Contains("language pack installation failed") ||
                    (r.Contains("language pack") && r.Contains("could not be installed")) ||
                    (r.Contains("installation error") && r.Contains("language pack")) ||
                    r.Contains("failed to install language pack") ||
                    r.Contains("언어 팩 설치 실패"))
                    return "언어 팩 설치 실패";

                // 2. 언어 팩 제거 실패
                else if (r.Contains("language pack removal failed") ||
                    (r.Contains("language pack") && r.Contains("could not be removed")) ||
                    (r.Contains("removal error") && r.Contains("language pack")) ||
                    r.Contains("failed to remove language pack") ||
                    r.Contains("언어 팩 제거 실패"))
                    return "언어 팩 제거 실패";

                // 3. 언어 팩 업데이트 실패
                else if (r.Contains("language pack update failed") ||
                    (r.Contains("language pack") && r.Contains("update failed")) ||
                    r.Contains("failed to update language pack") ||
                    r.Contains("언어 팩 업데이트 실패"))
                    return "언어 팩 업데이트 실패";

                // 4. 시스템 언어 변경 실패
                else if (r.Contains("system language change failed") ||
                    r.Contains("failed to change system language") ||
                    (r.Contains("language change") && r.Contains("failed")) ||
                    r.Contains("시스템 언어 변경 실패"))
                    return "시스템 언어 변경 실패";

                // 5. 언어 팩 파일 손상
                else if (r.Contains("language pack file corrupted") ||
                    r.Contains("corrupted language pack") ||
                    (r.Contains("language pack") && r.Contains("corrupted")) ||
                    r.Contains("failed to install corrupted language pack") ||
                    r.Contains("언어 팩 파일 손상"))
                    return "언어 팩 파일 손상";

                // 6. 호환성 문제로 인한 언어 팩 설치 실패
                else if (r.Contains("language pack compatibility issue") ||
                    (r.Contains("language pack") && r.Contains("incompatible")) ||
                    r.Contains("failed to install incompatible language pack") ||
                    r.Contains("언어 팩 호환성 문제"))
                    return "호환성 문제로 인한 언어 팩 설치 실패";

                else
                    return "정의된 언어 팩 이벤트 유형에 해당하지 않습니다.";
            }
            else if (eventcode == 2004)
            {
                // 1. 가상 메모리 부족 (페이지 파일 부족)
                if (r.Contains("virtual memory exhaustion") ||
                    (r.Contains("virtual memory") && r.Contains("low")) ||
                    (r.Contains("virtual memory") && r.Contains("exhausted")) ||
                    (r.Contains("pagefile") && r.Contains("insufficient")) ||
                    r.Contains("가상 메모리 부족"))
                    return "가상 메모리 부족 (페이지 파일 부족)";

                // 2. 메모리 누수 (특정 프로세스에서 과도한 메모리 사용)
                else if (r.Contains("memory leak detected") ||
                    (r.Contains("process") && r.Contains("memory usage")) ||
                    (r.Contains("memory usage") && r.Contains("exceeded")) ||
                    (r.Contains("memory leak") && r.Contains("critical")) ||
                    r.Contains("메모리 누수"))
                    return "메모리 누수 (특정 프로세스에서 과도한 메모리 사용)";

                // 3. 디스크 공간 부족 (가상 메모리를 위한 공간 부족)
                else if (r.Contains("disk space low") ||
                    (r.Contains("pagefile") && r.Contains("not enough disk space")) ||
                    (r.Contains("virtual memory") && r.Contains("disk space")) ||
                    r.Contains("디스크 공간 부족"))
                    return "디스크 공간 부족 (가상 메모리를 위한 공간 부족)";

                // 4. 메모리 관리 최적화 실패 (가상 메모리 크기 자동 조정 실패)
                else if (r.Contains("memory optimization failed") ||
                    (r.Contains("pagefile") && r.Contains("auto adjustment failed")) ||
                    (r.Contains("virtual memory") && r.Contains("optimization")) ||
                    r.Contains("메모리 최적화 실패"))
                    return "메모리 관리 최적화 실패 (가상 메모리 크기 자동 조정 실패)";

                // 5. 시스템 설정 오류 (가상 메모리 설정 오류)
                else if (r.Contains("virtual memory settings error") ||
                    (r.Contains("pagefile size") && r.Contains("error")) ||
                    (r.Contains("virtual memory") && r.Contains("configuration error")) ||
                    r.Contains("가상 메모리 설정 오류"))
                    return "시스템 설정 오류 (가상 메모리 설정 오류)";

                else
                    return "정의된 가상 메모리 이벤트 유형에 해당하지 않습니다.";
            }
            else if (eventcode == 775)
            {
                // 1. Windows Defender가 악성 소프트웨어 차단
                if (r.Contains("windows defender blocked") ||
                    (r.Contains("windows defender") && r.Contains("action blocked")) ||
                    (r.Contains("blocked behavior") && r.Contains("malicious")) ||
                    (r.Contains("behavior blocked") && r.Contains("malware")) ||
                    (r.Contains("windows defender") && r.Contains("blocked suspicious activity")))
                    return "Windows Defender가 악성 소프트웨어 차단";

                // 2. 타사 보안 소프트웨어에 의한 행동 차단
                else if (r.Contains("third-party security software blocked") ||
                    r.Contains("third-party antivirus blocked") ||
                    (r.Contains("third-party security") && r.Contains("blocked action")) ||
                    (r.Contains("security software") && r.Contains("blocked malicious behavior")) ||
                    r.Contains("behavior blocked by third-party"))
                    return "타사 보안 소프트웨어에 의한 행동 차단";

                // 3. Windows Defender 실시간 보호 기능이 특정 행위 차단
                else if (r.Contains("real-time protection blocked") ||
                    r.Contains("windows defender real-time blocked") ||
                    (r.Contains("real-time protection") && r.Contains("blocked")) ||
                    (r.Contains("windows defender") && r.Contains("real-time protection") && r.Contains("malicious behavior")))
                    return "Windows Defender 실시간 보호 기능이 특정 행위 차단";

                // 4. 레지스트리 변경 시 악성 행위 차단
                else if (r.Contains("registry modification blocked") ||
                    r.Contains("blocked registry modification") ||
                    r.Contains("malicious registry change blocked") ||
                    (r.Contains("windows defender") && r.Contains("blocked registry modification")) ||
                    r.Contains("레지스트리 변경 차단"))
                    return "레지스트리 변경 시 악성 행위 차단";

                // 5. 파일 실행 차단 (악성 파일 실행 차단)
                else if (r.Contains("file execution blocked") ||
                    r.Contains("blocked executable") ||
                    r.Contains("malicious executable blocked") ||
                    r.Contains("windows defender blocked file execution") ||
                    r.Contains("파일 실행 차단"))
                    return "파일 실행 차단 (악성 파일 실행 차단)";

                // 6. 스크립트 또는 프로세스 실행 차단
                else if (r.Contains("script execution blocked") ||
                    r.Contains("blocked process execution") ||
                    r.Contains("suspicious script blocked") ||
                    r.Contains("windows defender blocked suspicious script") ||
                    r.Contains("프로세스 실행 차단"))
                    return "스크립트 또는 프로세스 실행 차단";

                // 7. 네트워크 연결 차단 (악성 네트워크 활동 차단)
                else if (r.Contains("network connection blocked") ||
                    r.Contains("blocked network connection") ||
                    r.Contains("windows defender blocked network traffic") ||
                    r.Contains("malicious network activity blocked") ||
                    r.Contains("네트워크 연결 차단"))
                    return "네트워크 연결 차단 (악성 네트워크 활동 차단)";

                else
                    return "정의된 차단 이벤트 유형에 해당하지 않습니다.";
            }
            else if (eventcode == 4624)
            {
                // 1. 직접 로그인 (콘솔 로그온)
                if (r.Contains("logon type:  2") || r.Contains("로그온 유형:  2"))
                    return "직접 로그인 (콘솔 로그온)";

                // 2. 원격 로그인 (Remote Desktop)
                else if (r.Contains("logon type:  10") || r.Contains("로그온 유형:  10"))
                    return "원격 로그인 (Remote Desktop)";

                // 3. 네트워크 로그인 (공유 폴더 접근 등)
                else if (r.Contains("logon type:  3") || r.Contains("로그온 유형:  3"))
                    return "네트워크 로그인 (공유 폴더 접근 등)";

                // 4. 서비스 계정 로그인
                else if (r.Contains("logon type:  5") || r.Contains("로그온 유형:  5"))
                    return "서비스 계정 로그인";

                // 5. 배치 작업 또는 예약된 작업에 의한 로그인
                else if (r.Contains("logon type:  4") || r.Contains("로그온 유형:  4"))
                    return "배치 작업 또는 예약된 작업에 의한 로그인";

                else
                    return "기타 로그인 유형 (확인 필요)";
            }

            else if (eventcode == 4625)
            {
                // 1. 원격 데스크톱 로그인 실패 (RDP)
                if (r.Contains("logon type:  10") || r.Contains("로그온 유형:  10"))
                    return "원격 데스크톱 로그인 실패 (RDP)";

                // 2. 네트워크 로그인 실패 (공유 접근)
                else if (r.Contains("logon type:  3") || r.Contains("로그온 유형:  3"))
                    return "네트워크 로그인 실패 (공유 접근)";

                // 3. 인증 실패 (계정 정보 오류)
                else if (r.Contains("failure reason: unknown user name or bad password") ||
                         r.Contains("잘못된 사용자 이름 또는 암호"))
                    return "인증 실패 (계정 정보 오류)";

                // 4. 계정 비활성화로 인한 로그인 실패
                else if (r.Contains("account currently disabled") || r.Contains("사용자 계정이 사용 중지됨"))
                    return "계정 비활성화로 인한 로그인 실패";

                // 5. 계정 잠김 (로그인 시도 초과)
                else if (r.Contains("account locked out") || r.Contains("계정 잠김"))
                    return "계정 잠김 (로그인 시도 초과)";

                else
                    return "기타 로그인 실패 (추가 분석 필요)";
            }
            else if (eventcode == 4672)
            {
                // 1. 디버깅 권한 포함 (SeDebugPrivilege) – 시스템 제어 가능
                if (r.Contains("sedebugPpivilege"))
                    return "디버깅 권한 포함 (SeDebugPrivilege) – 시스템 제어 가능";

                // 2.운영 체제 수준 권한 포함 (SeTcbPrivilege)
                else if (r.Contains("setcbprivilege"))
                    return "운영 체제 수준 권한 포함 (setcbprivilege)";

                // 3. 백업/복원 권한 포함 – 데이터 접근 가능
                else if (r.Contains("sebackupprivilege") || r.Contains("serestoreprivilege"))
                    return "백업/복원 권한 포함 – 데이터 접근 가능";

                // 4. 관리자 계정 로그인 (특권 포함)
                else if (r.Contains("administrator") || r.Contains("관리자"))
                    return "관리자 계정 로그인 (특권 포함)";

                else
                    return "특권 계정 로그인 (상세 권한 분석 필요)";
            }
            else
                return "알 수 없는 이유";
        }
    }
}
