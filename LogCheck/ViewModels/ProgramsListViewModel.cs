using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Threading;
using LogCheck.Models;
using LogCheck.Services;
using Microsoft.Win32;

namespace LogCheck.ViewModels
{
    /// <summary>
    /// 프로그램 목록 페이지용 ViewModel
    /// BasePageViewModel을 상속받아 공통 기능 활용
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class ProgramsListViewModel : BasePageViewModel
    {
        #region Fields
        private readonly ObservableCollection<ProgramInfo> _programList;
        private readonly CollectionViewSource _viewSource;
        private readonly HashSet<string> _processedPrograms;
        private readonly DispatcherTimer _loadingTextTimer;

        private int _dotCount = 0;
        private const int MaxDots = 3;
        private const string BaseText = "검사 중";
        private bool _isLoadingOverlayVisible = false;
        private string _loadingText = BaseText;
        #endregion

        #region Properties
        /// <summary>
        /// 프로그램 목록 컬렉션
        /// </summary>
        public ObservableCollection<ProgramInfo> ProgramList => _programList;

        /// <summary>
        /// DataGrid용 뷰 소스
        /// </summary>
        public CollectionViewSource ViewSource => _viewSource;

        /// <summary>
        /// 로딩 오버레이 표시 여부
        /// </summary>
        public bool IsLoadingOverlayVisible
        {
            get => _isLoadingOverlayVisible;
            set { _isLoadingOverlayVisible = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 로딩 텍스트
        /// </summary>
        public string LoadingText
        {
            get => _loadingText;
            set { _loadingText = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 보안 프로그램 날짜 정보
        /// </summary>
        public static SecurityDate[] SecurityDates { get; set; } = new SecurityDate[3];
        #endregion

        #region Constructor
        /// <summary>
        /// 생성자
        /// </summary>
        public ProgramsListViewModel()
        {
            _programList = new ObservableCollection<ProgramInfo>();
            _viewSource = new CollectionViewSource();
            _processedPrograms = new HashSet<string>();
            _loadingTextTimer = new DispatcherTimer();

            // ViewSource 설정
            _viewSource.Source = _programList;

            // 로딩 타이머 설정
            SetupLoadingTextAnimation();
        }
        #endregion

        #region BasePageViewModel Implementation
        /// <summary>
        /// 페이지 초기화 (동기)
        /// </summary>
        public override void Initialize()
        {
            LogService.AddLogMessage("🔄 ProgramsList 동기 초기화");
            // 동기 초기화 작업 (필요시)
        }

        /// <summary>
        /// 페이지 비동기 초기화
        /// </summary>
        public override async Task InitializeAsync()
        {
            try
            {
                LogService.AddLogMessage("🔄 ProgramsList 비동기 초기화 시작");

                // 관리자 권한 확인
                if (!IsRunningAsAdmin())
                {
                    LogService.LogError("관리자 권한이 필요합니다.");
                    return;
                }

                // 보안 로그 분석 시작
                await Task.Run(() => CheckLogs());

                // 설치된 프로그램 정보 수집
                ShowLoadingOverlay();
                await Task.Run(() => CollectInstalledPrograms());
                HideLoadingOverlay();

                LogService.AddLogMessage("✅ ProgramsList 초기화 완료");
            }
            catch (Exception ex)
            {
                LogService.LogError($"ProgramsList 초기화 중 오류: {ex.Message}");
                HideLoadingOverlay();
            }
        }

        /// <summary>
        /// 데이터 새로고침
        /// </summary>
        public async Task RefreshDataAsync()
        {
            try
            {
                LogService.AddLogMessage("🔄 프로그램 목록 새로고침 시작");

                ShowLoadingOverlay();

                // 기존 데이터 정리
                _programList.Clear();
                _processedPrograms.Clear();

                // 새로 수집
                await Task.Run(() => CollectInstalledPrograms());

                HideLoadingOverlay();
                LogService.AddLogMessage("✅ 프로그램 목록 새로고침 완료");
            }
            catch (Exception ex)
            {
                LogService.LogError($"프로그램 목록 새로고침 중 오류: {ex.Message}");
                HideLoadingOverlay();
            }
        }

        /// <summary>
        /// 정리 작업
        /// </summary>
        public override void Cleanup()
        {
            try
            {
                _loadingTextTimer?.Stop();
                _programList.Clear();
                _processedPrograms.Clear();
                LogService.AddLogMessage("🧹 ProgramsList 정리 완료");
            }
            catch (Exception ex)
            {
                LogService.LogError($"ProgramsList 정리 중 오류: {ex.Message}");
            }
        }
        #endregion

        #region Loading UI Methods
        /// <summary>
        /// 로딩 텍스트 애니메이션 설정
        /// </summary>
        private void SetupLoadingTextAnimation()
        {
            _loadingTextTimer.Interval = TimeSpan.FromMilliseconds(500);
            _loadingTextTimer.Tick += (s, e) =>
            {
                _dotCount = (_dotCount + 1) % (MaxDots + 1);
                LoadingText = BaseText + new string('.', _dotCount);
            };
        }

        /// <summary>
        /// 로딩 오버레이 표시
        /// </summary>
        public void ShowLoadingOverlay()
        {
            IsLoadingOverlayVisible = true;
            _loadingTextTimer?.Start();
        }

        /// <summary>
        /// 로딩 오버레이 숨김
        /// </summary>
        public void HideLoadingOverlay()
        {
            _loadingTextTimer?.Stop();
            IsLoadingOverlayVisible = false;
            LoadingText = BaseText; // 텍스트 초기화
        }
        #endregion

        #region Security Log Analysis
        /// <summary>
        /// 보안 로그 분석
        /// </summary>
        [SupportedOSPlatform("windows")]
        private void CheckLogs()
        {
            try
            {
                LogService.AddLogMessage("🔍 보안 로그 분석 시작");

                DateTime oneYearAgo = DateTime.Now.AddYears(-1);

                // Windows Defender 로그 확인 (이벤트 ID: 1116, 1117)
                SecurityDates[0] = new SecurityDate(
                    GetLatestLogDate("Microsoft-Windows-Windows Defender/Operational", new[] { 1116, 1117 }, oneYearAgo, "Windows Defender"),
                    "Windows Defender"
                );

                // Windows Firewall 로그 확인 (이벤트 ID: 2004, 2006)
                SecurityDates[1] = new SecurityDate(
                    GetLatestLogDate("Microsoft-Windows-Windows Firewall With Advanced Security/Firewall", new[] { 2004, 2006 }, oneYearAgo, "Windows Firewall"),
                    "Windows Firewall"
                );

                // BitLocker 로그 확인 (이벤트 ID: 845, 846)
                SecurityDates[2] = new SecurityDate(
                    GetLatestLogDate("Microsoft-Windows-BitLocker/BitLocker Management", new[] { 845, 846 }, oneYearAgo, "BitLocker"),
                    "BitLocker"
                );

                LogService.AddLogMessage("✅ 보안 로그 분석 완료");
            }
            catch (Exception ex)
            {
                LogService.LogError($"보안 로그 분석 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 특정 이벤트 로그에서 최신 기록 날짜 조회
        /// </summary>
        [SupportedOSPlatform("windows")]
        private static DateTime GetLatestLogDate(string logName, int[] eventIds, DateTime oneYearAgo, string programName)
        {
            try
            {
                using (var eventLog = new EventLogReader(logName))
                {
                    EventRecord? eventRecord;
                    while ((eventRecord = eventLog.ReadEvent()) != null)
                    {
                        using (eventRecord)
                        {
                            if (eventIds.Contains(eventRecord.Id) && eventRecord.TimeCreated.HasValue)
                            {
                                var eventTime = eventRecord.TimeCreated.Value;
                                if (eventTime > oneYearAgo)
                                {
                                    return eventTime;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{programName} 로그 조회 중 오류: {ex.Message}");
            }

            return oneYearAgo;
        }
        #endregion

        #region Program Collection
        /// <summary>
        /// 레지스트리에서 설치된 프로그램 정보 수집
        /// </summary>
        [SupportedOSPlatform("windows")]
        private void CollectInstalledPrograms()
        {
            try
            {
                LogService.AddLogMessage("📋 설치된 프로그램 정보 수집 시작");

                string[] registryKeys = {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };

                foreach (string keyPath in registryKeys)
                {
                    ProcessRegistryKey(Registry.LocalMachine, keyPath);
                }

                LogService.AddLogMessage($"📊 총 {_programList.Count}개 프로그램 수집 완료");
            }
            catch (Exception ex)
            {
                LogService.LogError($"프로그램 정보 수집 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 레지스트리 키 처리
        /// </summary>
        [SupportedOSPlatform("windows")]
        private void ProcessRegistryKey(RegistryKey baseKey, string keyPath)
        {
            try
            {
                using (var key = baseKey.OpenSubKey(keyPath))
                {
                    if (key == null) return;

                    foreach (string subkeyName in key.GetSubKeyNames())
                    {
                        using (var subkey = key.OpenSubKey(subkeyName))
                        {
                            if (subkey == null) continue;

                            var programInfo = ExtractProgramInfo(subkey);
                            if (programInfo != null && !_processedPrograms.Contains(programInfo.Name))
                            {
                                _processedPrograms.Add(programInfo.Name);

                                // UI 스레드에서 컬렉션 업데이트
                                App.Current.Dispatcher.Invoke(() =>
                                {
                                    _programList.Add(programInfo);
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogError($"레지스트리 키 처리 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 레지스트리 서브키에서 프로그램 정보 추출
        /// </summary>
        [SupportedOSPlatform("windows")]
        private ProgramInfo? ExtractProgramInfo(RegistryKey subkey)
        {
            try
            {
                string? displayName = subkey.GetValue("DisplayName")?.ToString();

                // 표시 이름이 없거나 시스템 업데이트인 경우 제외
                if (string.IsNullOrEmpty(displayName) ||
                    displayName.Contains("KB") ||
                    displayName.Contains("Update") ||
                    displayName.Contains("Hotfix"))
                {
                    return null;
                }

                var programInfo = new ProgramInfo
                {
                    Name = displayName,
                    Version = subkey.GetValue("DisplayVersion")?.ToString() ?? "",
                    Publisher = subkey.GetValue("Publisher")?.ToString() ?? "",
                    InstallPath = subkey.GetValue("InstallLocation")?.ToString() ?? ""
                };

                // 설치 날짜 파싱
                programInfo.InstallDate = ParseInstallDate(subkey.GetValue("InstallDate")?.ToString());

                // 보안 레벨 계산
                CalculateSecurityLevel(programInfo);

                return programInfo;
            }
            catch (Exception ex)
            {
                LogService.LogError($"프로그램 정보 추출 중 오류: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 설치 날짜 파싱
        /// </summary>
        private DateTime? ParseInstallDate(string? installDateString)
        {
            if (string.IsNullOrEmpty(installDateString))
                return null;

            try
            {
                // yyyyMMdd 형식 시도
                if (DateTime.TryParseExact(installDateString, "yyyyMMdd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
                {
                    return result;
                }

                // Unix timestamp 시도
                if (long.TryParse(installDateString, out long unixTime))
                {
                    return DateTimeOffset.FromUnixTimeSeconds(unixTime).DateTime;
                }

                // 일반적인 날짜 파싱 시도
                if (DateTime.TryParse(installDateString, out DateTime generalResult))
                {
                    return generalResult;
                }
            }
            catch (Exception ex)
            {
                LogService.LogError($"설치 날짜 파싱 중 오류: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 보안 레벨 계산
        /// </summary>
        private void CalculateSecurityLevel(ProgramInfo programInfo)
        {
            try
            {
                int securityScore = 0;
                var securityDetails = new List<string>();

                // 설치 경로 검사
                int pathScore = CheckInstallPath(programInfo.InstallPath);
                securityScore += pathScore;
                if (pathScore < 0)
                {
                    securityDetails.Add("비정상적인 설치 경로");
                }

                // 발행자 검사
                if (string.IsNullOrEmpty(programInfo.Publisher))
                {
                    securityScore -= 5;
                    securityDetails.Add("발행자 정보 없음");
                }
                else if (IsTrustedPublisher(programInfo.Publisher))
                {
                    securityScore += 10;
                    securityDetails.Add("신뢰할 수 있는 발행자");
                }

                // 보안 레벨 결정
                if (securityScore >= 10)
                {
                    programInfo.SecurityLevel = "높음";
                }
                else if (securityScore >= 0)
                {
                    programInfo.SecurityLevel = "보통";
                }
                else
                {
                    programInfo.SecurityLevel = "낮음";
                }

                programInfo.SecurityDetails = string.Join(", ", securityDetails);
            }
            catch (Exception ex)
            {
                LogService.LogError($"보안 레벨 계산 중 오류: {ex.Message}");
                programInfo.SecurityLevel = "알 수 없음";
            }
        }

        /// <summary>
        /// 설치 경로 검사
        /// </summary>
        private int CheckInstallPath(string installLocation)
        {
            if (string.IsNullOrEmpty(installLocation))
                return 0;

            string lowerPath = installLocation.ToLower();

            // Program Files에 설치된 경우 신뢰도 높음
            if (lowerPath.Contains("program files"))
                return 10;

            // 시스템 폴더나 임시 폴더에 설치된 경우 위험
            if (lowerPath.Contains("temp") || lowerPath.Contains("appdata"))
                return -10;

            return 0;
        }

        /// <summary>
        /// 신뢰할 수 있는 발행자 검사
        /// </summary>
        private bool IsTrustedPublisher(string publisher)
        {
            var trustedPublishers = new[]
            {
                "Microsoft", "Google", "Apple", "Adobe", "Oracle",
                "Mozilla", "Valve", "NVIDIA", "Intel", "AMD"
            };

            return trustedPublishers.Any(trusted =>
                publisher.Contains(trusted, StringComparison.OrdinalIgnoreCase));
        }
        #endregion

        #region Utility Methods
        /// <summary>
        /// 관리자 권한 확인
        /// </summary>
        [SupportedOSPlatform("windows")]
        public static bool IsRunningAsAdmin()
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
        #endregion
    }

    #region Data Models
    /// <summary>
    /// 프로그램 정보 저장용 클래스
    /// </summary>
    public class ProgramInfo
    {
        public string Name { get; set; } = "알 수 없음";
        public DateTime? InstallDate { get; set; }
        public string InstallPath { get; set; } = "";
        public string Version { get; set; } = "";
        public string Publisher { get; set; } = "";
        public string SecurityLevel { get; set; } = "알 수 없음";
        public string SecurityDetails { get; set; } = "";
        public string MalwareVerdict { get; set; } = "Unknown";
    }

    /// <summary>
    /// 보안 프로그램 날짜 정보 구조체
    /// </summary>
    public struct SecurityDate
    {
        public DateTime Date;
        public string Program_name;

        public SecurityDate(DateTime date, string name)
        {
            Date = date;
            Program_name = name;
        }
    }
    #endregion
}