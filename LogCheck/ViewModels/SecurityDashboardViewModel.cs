using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Timers;
using System.Windows.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LogCheck.Models;
using LogCheck.Services;
using SkiaSharp;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace LogCheck.ViewModels
{
    [SupportedOSPlatform("windows")]
    public class SecurityDashboardViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly System.Timers.Timer _updateTimer;
        private readonly AutoBlockStatisticsService _statisticsService;
        private readonly ToastNotificationService _toastService;
        private readonly IntegratedDDoSDefenseSystem? _ddosDefenseSystem;

        // 위험도 및 상태

        private ThreatLevel _currentThreatLevel = ThreatLevel.Safe;
        public ThreatLevel CurrentThreatLevel
        {
            get => _currentThreatLevel;
            set
            {
                var previousLevel = _currentThreatLevel;
                _currentThreatLevel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ThreatLevelText));
                OnPropertyChanged(nameof(ThreatLevelColor));

                // 🔥 Toast 알림: 위험도 변경
                if (previousLevel != value && value != ThreatLevel.Safe)
                {
                    _ = System.Threading.Tasks.Task.Run(async () =>
                    {
                        await _toastService.ShowSecurityAsync(
                            "🚨 보안 위험도 변경",
                            $"시스템 위험도가 {GetThreatLevelDisplayName(previousLevel)}에서 {GetThreatLevelDisplayName(value)}로 변경되었습니다.");
                    });
                }
            }
        }

        // 실시간 메트릭
        private int _activeThreats = 0;
        public int ActiveThreats
        {
            get => _activeThreats;
            set
            {
                var previousThreats = _activeThreats;
                _activeThreats = value;
                OnPropertyChanged();

                // 🔥 Toast 알림: 새로운 위협 탐지
                if (value > previousThreats && value > 0)
                {
                    var newThreats = value - previousThreats;
                    _ = System.Threading.Tasks.Task.Run(async () =>
                    {
                        await _toastService.ShowWarningAsync(
                            "⚠️ 새로운 위협 탐지",
                            $"{newThreats}개의 새로운 보안 위협이 감지되었습니다. 총 활성 위협: {value}개");
                    });
                }
            }
        }

        private int _blockedConnections24h = 0;
        public int BlockedConnections24h
        {
            get => _blockedConnections24h;
            set
            {
                var previousBlocked = _blockedConnections24h;
                _blockedConnections24h = value;
                OnPropertyChanged();

                // 🔥 Toast 알림: 차단 작업 증가 (대량 차단 시에만)
                if (value > previousBlocked && (value - previousBlocked) >= 10)
                {
                    var newBlocks = value - previousBlocked;
                    _ = System.Threading.Tasks.Task.Run(async () =>
                    {
                        await _toastService.ShowSuccessAsync(
                            "🛡️ 대량 위협 차단",
                            $"{newBlocks}개의 악성 연결이 차단되었습니다. 24시간 내 총 차단: {value}개");
                    });
                }
            }
        }

        private double _networkTrafficMB = 0.0;
        public double NetworkTrafficMB
        {
            get => _networkTrafficMB;
            set
            {
                _networkTrafficMB = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(NetworkTrafficText));
            }
        }

        private bool _ddosDefenseActive = false;
        public bool DDoSDefenseActive
        {
            get => _ddosDefenseActive;
            set
            {
                _ddosDefenseActive = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DDoSDefenseText));
            }
        }

        // UI 표시용 텍스트 속성들
        public string ThreatLevelText => CurrentThreatLevel switch
        {
            ThreatLevel.Safe => "안전",
            ThreatLevel.Low => "낮음",
            ThreatLevel.Medium => "보통",
            ThreatLevel.High => "높음",
            ThreatLevel.Critical => "위험",
            _ => "알 수 없음"
        };

        public Brush ThreatLevelColor => CurrentThreatLevel switch
        {
            ThreatLevel.Safe => Brushes.Green,
            ThreatLevel.Low => Brushes.LightGreen,
            ThreatLevel.Medium => Brushes.Orange,
            ThreatLevel.High => Brushes.Red,
            ThreatLevel.Critical => Brushes.DarkRed,
            _ => Brushes.Gray
        };

        public string NetworkTrafficText => $"{NetworkTrafficMB:F2} MB/s";
        public string DDoSDefenseText => DDoSDefenseActive ? "활성" : "비활성";

        private string _systemStatusText = "정상";
        public string SystemStatusText
        {
            get => _systemStatusText;
            set
            {
                _systemStatusText = value;
                OnPropertyChanged();
            }
        }

        private string _systemUptimeText = "가동시간: 계산 중...";
        public string SystemUptimeText
        {
            get => _systemUptimeText;
            set
            {
                _systemUptimeText = value;
                OnPropertyChanged();
            }
        }

        private string _lastUpdateText = "방금 전";
        public string LastUpdateText
        {
            get => _lastUpdateText;
            set
            {
                _lastUpdateText = value;
                OnPropertyChanged();
            }
        }

        private string _nextUpdateText = "다음 업데이트: 30초 후";
        public string NextUpdateText
        {
            get => _nextUpdateText;
            set
            {
                _nextUpdateText = value;
                OnPropertyChanged();
            }
        }

        // 컬렉션들
        public ObservableCollection<SecurityEventInfo> RecentSecurityEvents { get; }
        public ObservableCollection<BlockedIPInfo> TopBlockedIPs { get; }

        // 차트 데이터 (LiveCharts)
        public ObservableCollection<ISeries> ThreatTrendSeries { get; set; } = new();
        public Axis[] ThreatTrendXAxes { get; set; } = Array.Empty<Axis>();
        public Axis[] ThreatTrendYAxes { get; set; } = Array.Empty<Axis>();

        // 명령들
        public ICommand EmergencyBlockCommand { get; }
        public ICommand ToggleDDoSDefenseCommand { get; }
        public ICommand SecurityScanCommand { get; }
        public ICommand SystemRecoveryCommand { get; }

        private string _actionStatusText = "";
        public string ActionStatusText
        {
            get => _actionStatusText;
            set
            {
                _actionStatusText = value;
                OnPropertyChanged();
            }
        }

        private bool _actionStatusVisible = false;
        public bool ActionStatusVisible
        {
            get => _actionStatusVisible;
            set
            {
                _actionStatusVisible = value;
                OnPropertyChanged();
            }
        }

        // 보안 스냅샷 히스토리
        public ObservableCollection<SecuritySnapshot> SecurityHistory { get; }

        public SecurityDashboardViewModel()
        {
            _statisticsService = new AutoBlockStatisticsService("Data Source=autoblock.db");
            _toastService = ToastNotificationService.Instance;

            // DDoS 방어 시스템 초기화 (싱글톤 패턴으로 가져오거나 의존성 주입)
            try
            {
                // 기존 시스템에서 사용 중인 DDoS 시스템 인스턴스 찾기
                _ddosDefenseSystem = App.Current?.Resources["IntegratedDDoSDefenseSystem"] as IntegratedDDoSDefenseSystem;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DDoS 시스템 연결 실패: {ex.Message}");
            }

            // 컬렉션 초기화
            RecentSecurityEvents = new ObservableCollection<SecurityEventInfo>();
            TopBlockedIPs = new ObservableCollection<BlockedIPInfo>();
            SecurityHistory = new ObservableCollection<SecuritySnapshot>();

            // 차트 초기화
            InitializeCharts();

            // 명령 초기화
            EmergencyBlockCommand = new RelayCommand(ExecuteEmergencyBlock);
            ToggleDDoSDefenseCommand = new RelayCommand(ExecuteToggleDDoSDefense);
            SecurityScanCommand = new RelayCommand(ExecuteSecurityScan);
            SystemRecoveryCommand = new RelayCommand(ExecuteSystemRecovery);

            // 업데이트 타이머 설정 (30초 간격)
            _updateTimer = new System.Timers.Timer(30000);
            _updateTimer.Elapsed += UpdateTimer_Elapsed;
            _updateTimer.AutoReset = true;
            _updateTimer.Start();

            // 초기 데이터 로드
            UpdateMetrics();
        }

        private void InitializeCharts()
        {
            var threatTrendValues = new ObservableCollection<ObservablePoint>();

            // 샘플 데이터로 초기화

            for (int i = 0; i < 24; i++)
            {
                threatTrendValues.Add(new ObservablePoint(i, Random.Shared.Next(0, 5)));
            }

            ThreatTrendSeries = new ObservableCollection<ISeries>
            {
                new LineSeries<ObservablePoint>
                {
                    Values = threatTrendValues,
                    Name = "위험도"
                }
            };

            ThreatTrendXAxes = new[]
            {
                new Axis
                {
                    Name = "시간",
                    NamePaint = new SolidColorPaint { Color = SKColors.Gray }
                }
            };

            ThreatTrendYAxes = new[]
            {
                new Axis
                {
                    Name = "위험도",
                    NamePaint = new SolidColorPaint { Color = SKColors.Gray },
                    MinLimit = 0,
                    MaxLimit = 5
                }
            };
        }

        private void UpdateTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            UpdateMetrics();
        }

        private void UpdateMetrics()
        {
            try
            {
                // 실제 DDoS 방어 시스템에서 통계 데이터 가져오기
                if (_ddosDefenseSystem != null)
                {
                    var ddosStats = _ddosDefenseSystem.GetStatistics();

                    // 실제 보안 데이터로 업데이트
                    ActiveThreats = ddosStats.TotalAttacksDetected;
                    BlockedConnections24h = ddosStats.AttacksBlocked;
                    NetworkTrafficMB = ddosStats.TotalTrafficBlocked; // MB 단위
                    DDoSDefenseActive = ddosStats.TotalAttacksDetected > 0;

                    // 위험도 계산 (공격 심각도 기반)
                    CurrentThreatLevel = CalculateThreatLevel(ddosStats);

                    // 차단된 IP 목록 업데이트
                    UpdateBlockedIPsList(ddosStats);
                }
                else
                {
                    // DDoS 시스템을 사용할 수 없는 경우 기본값
                    ActiveThreats = 0;
                    BlockedConnections24h = 0;
                    NetworkTrafficMB = 0.0;
                    DDoSDefenseActive = false;
                    CurrentThreatLevel = ThreatLevel.Safe;
                }

                // 시스템 가동시간 업데이트
                var uptime = DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime;
                SystemUptimeText = $"가동시간: {uptime.Days}일 {uptime.Hours}시간";

                // 업데이트 시간 표시
                LastUpdateText = DateTime.Now.ToString("HH:mm:ss");

                // 🔥 DISABLED: 테스트용 샘플 이벤트 생성 비활성화 (발표용)
                // AddSampleSecurityEvent(); // 실제 보안 이벤트만 표시

                // 보안 스냅샷 저장 (5분마다)

                SaveSecuritySnapshot();

                // UI 스레드에서 실행

                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    OnPropertyChanged(nameof(ThreatTrendSeries));
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"메트릭 업데이트 오류: {ex.Message}");
            }
        }

        private void AddSampleSecurityEvent()
        {
            // 최대 50개 이벤트만 유지
            if (RecentSecurityEvents.Count >= 50)
            {
                RecentSecurityEvents.RemoveAt(RecentSecurityEvents.Count - 1);
            }

            var eventTypes = new[] { "차단", "DDoS 탐지", "의심 연결", "방화벽 규칙" };
            var riskLevels = new[] { "낮음", "보통", "높음" };
            var sources = new[] { "192.168.1.100", "10.0.0.50", "172.16.0.25", "chrome.exe", "notepad.exe" };

            var eventType = eventTypes[Random.Shared.Next(eventTypes.Length)];
            var riskLevel = riskLevels[Random.Shared.Next(riskLevels.Length)];
            var source = sources[Random.Shared.Next(sources.Length)];

            var newEvent = new SecurityEventInfo
            {
                Timestamp = DateTime.Now,
                EventType = eventType,
                TypeColor = GetEventTypeColor(eventType),
                Description = $"{eventType} 이벤트가 {source}에서 발생했습니다.",
                RiskLevel = riskLevel,
                RiskColor = GetRiskLevelColor(riskLevel),
                Source = source
            };

            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                RecentSecurityEvents.Insert(0, newEvent);

                // 위험도가 높거나 중요한 이벤트의 경우 Toast 알림 표시

                if (riskLevel == "높음" || eventType == "DDoS 탐지")
                {
                    ShowSecurityToast(newEvent);
                }
            });
        }

        private async void ShowSecurityToast(SecurityEventInfo securityEvent)
        {
            var title = securityEvent.EventType switch
            {
                "DDoS 탐지" => "🛡️ DDoS 공격 탐지",
                "차단" => "🚫 연결 차단",
                "의심 연결" => "⚠️ 의심스러운 연결",
                "방화벽 규칙" => "🔒 방화벽 규칙 적용",
                _ => "🔍 보안 이벤트"
            };

            // 위험도에 따라 적절한 Toast 메서드 호출
            switch (securityEvent.RiskLevel)
            {
                case "높음":
                    await _toastService.ShowErrorAsync(title, securityEvent.Description);
                    break;
                case "보통":
                    await _toastService.ShowWarningAsync(title, securityEvent.Description);
                    break;
                default:
                    await _toastService.ShowInfoAsync(title, securityEvent.Description);
                    break;
            }
        }

        private DateTime _lastSnapshotTime = DateTime.MinValue;

        private void SaveSecuritySnapshot()
        {
            // 5분마다만 스냅샷 저장
            if (DateTime.Now - _lastSnapshotTime < TimeSpan.FromMinutes(5))
                return;

            _lastSnapshotTime = DateTime.Now;

            var snapshot = new SecuritySnapshot
            {
                Timestamp = DateTime.Now,
                ThreatLevel = CurrentThreatLevel,
                ActiveThreats = ActiveThreats,
                BlockedConnections = BlockedConnections24h,
                NetworkTrafficMB = NetworkTrafficMB,
                DDoSDefenseActive = DDoSDefenseActive,
                Summary = $"위험도: {ThreatLevelText}, 활성 위협: {ActiveThreats}개, 차단: {BlockedConnections24h}개"
            };

            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                // 최대 100개 스냅샷만 유지
                if (SecurityHistory.Count >= 100)
                {
                    SecurityHistory.RemoveAt(SecurityHistory.Count - 1);
                }


                SecurityHistory.Insert(0, snapshot);
            });
        }

        private Brush GetEventTypeColor(string eventType) => eventType switch
        {
            "차단" => Brushes.Red,
            "DDoS 탐지" => Brushes.Purple,
            "의심 연결" => Brushes.Orange,
            "방화벽 규칙" => Brushes.Blue,
            _ => Brushes.Gray
        };

        private Brush GetRiskLevelColor(string riskLevel) => riskLevel switch
        {
            "낮음" => Brushes.Green,
            "보통" => Brushes.Orange,
            "높음" => Brushes.Red,
            _ => Brushes.Gray
        };

        // 명령 실행 메서드들
        private async void ExecuteEmergencyBlock()
        {
            ActionStatusText = "긴급 차단 모드 활성화됨";
            ActionStatusVisible = true;
            await _toastService.ShowWarningAsync("🚨 긴급 차단", "긴급 차단 모드가 활성화되었습니다");
            // 실제 긴급 차단 로직 구현 필요
        }

        private async void ExecuteToggleDDoSDefense()
        {
            DDoSDefenseActive = !DDoSDefenseActive;
            ActionStatusText = DDoSDefenseActive ? "DDoS 방어 활성화됨" : "DDoS 방어 비활성화됨";
            ActionStatusVisible = true;


            if (DDoSDefenseActive)
            {
                await _toastService.ShowSuccessAsync("🛡️ DDoS 방어 활성화", "DDoS 방어 시스템이 활성화되었습니다");
            }
            else
            {
                await _toastService.ShowInfoAsync("🔓 DDoS 방어 비활성화", "DDoS 방어 시스템이 비활성화되었습니다");
            }
            // 실제 DDoS 방어 토글 로직 구현 필요
        }

        private async void ExecuteSecurityScan()
        {
            ActionStatusText = "보안 점검 시작됨";
            ActionStatusVisible = true;
            await _toastService.ShowInfoAsync("🔍 보안 점검", "시스템 보안 점검을 시작합니다");
            // 실제 보안 점검 로직 구현 필요
        }

        private async void ExecuteSystemRecovery()
        {
            ActionStatusText = "시스템 복구 시작됨";
            ActionStatusVisible = true;
            await _toastService.ShowInfoAsync("🔧 시스템 복구", "시스템 복구 작업을 시작합니다");
            // 실제 시스템 복구 로직 구현 필요
        }

        /// <summary>
        /// 실시간 업데이트 시작
        /// </summary>
        public void StartRealTimeUpdates()
        {
            _updateTimer?.Start();
        }

        /// <summary>
        /// 실시간 업데이트 중지
        /// </summary>
        public void StopRealTimeUpdates()
        {
            _updateTimer?.Stop();
        }

        /// <summary>
        /// 차트 기간 업데이트
        /// </summary>
        public void UpdateChartPeriod(object period)
        {
            // 기간 변경 처리 (향후 확장 가능)
            InitializeCharts();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// DDoS 통계를 기반으로 위험도 계산
        /// </summary>
        private ThreatLevel CalculateThreatLevel(DDoSDetectionStats ddosStats)
        {
            if (ddosStats.TotalAttacksDetected == 0)
                return ThreatLevel.Safe;

            // 심각도 기반 위험도 계산
            var criticalAttacks = ddosStats.AttacksBySeverity.GetValueOrDefault(DDoSSeverity.Critical, 0);
            var highAttacks = ddosStats.AttacksBySeverity.GetValueOrDefault(DDoSSeverity.High, 0);
            var mediumAttacks = ddosStats.AttacksBySeverity.GetValueOrDefault(DDoSSeverity.Medium, 0);

            if (criticalAttacks > 0)
                return ThreatLevel.Critical;
            else if (highAttacks > 0)
                return ThreatLevel.High;
            else if (mediumAttacks > 0)
                return ThreatLevel.Medium;
            else if (ddosStats.TotalAttacksDetected > 0)
                return ThreatLevel.Low;

            return ThreatLevel.Safe;
        }

        /// <summary>
        /// 차단된 IP 목록 업데이트
        /// </summary>
        private void UpdateBlockedIPsList(DDoSDetectionStats ddosStats)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                TopBlockedIPs.Clear();

                // 상위 차단된 IP들을 추가 (최대 10개)
                var topIPs = ddosStats.TopAttackerIPs
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(10);

                foreach (var ipInfo in topIPs)
                {
                    TopBlockedIPs.Add(new BlockedIPInfo
                    {
                        IPAddress = ipInfo.Key,
                        BlockCount = ipInfo.Value,
                        LastBlocked = DateTime.Now,
                        Location = "Unknown" // 기본값으로 설정
                    });
                }
            });
        }

        /// <summary>
        /// 위험도 레벨을 사용자 친화적인 이름으로 변환
        /// </summary>
        private static string GetThreatLevelDisplayName(ThreatLevel level)
        {
            return level switch
            {
                ThreatLevel.Safe => "안전",
                ThreatLevel.Low => "낮음",
                ThreatLevel.Medium => "보통",
                ThreatLevel.High => "높음",
                ThreatLevel.Critical => "심각",
                _ => "알 수 없음"
            };
        }

        public void Dispose()
        {
            _updateTimer?.Stop();
            _updateTimer?.Dispose();
        }
    }

    /// <summary>
    /// 보안 이벤트 정보
    /// </summary>
    public class SecurityEventInfo
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; } = string.Empty;
        public System.Windows.Media.Brush TypeColor { get; set; } = System.Windows.Media.Brushes.Gray;
        public string Description { get; set; } = string.Empty;
        public string RiskLevel { get; set; } = string.Empty;
        public System.Windows.Media.Brush RiskColor { get; set; } = System.Windows.Media.Brushes.Gray;
        public string Source { get; set; } = string.Empty;
    }

    /// <summary>
    /// 차단된 IP 정보
    /// </summary>
    public class BlockedIPInfo
    {
        public string IPAddress { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public int BlockCount { get; set; }
        public DateTime LastBlocked { get; set; }
    }

    /// <summary>
    /// 보안 스냅샷 정보
    /// </summary>
    public class SecuritySnapshot
    {
        public DateTime Timestamp { get; set; }
        public ThreatLevel ThreatLevel { get; set; }
        public int ActiveThreats { get; set; }
        public int BlockedConnections { get; set; }
        public double NetworkTrafficMB { get; set; }
        public bool DDoSDefenseActive { get; set; }
        public string Summary { get; set; } = string.Empty;
    }


}
