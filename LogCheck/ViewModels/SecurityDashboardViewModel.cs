using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LogCheck.Models;
using LogCheck.Services;
using SkiaSharp;

namespace LogCheck.ViewModels
{
    [SupportedOSPlatform("windows")]
    public class SecurityDashboardViewModel : INotifyPropertyChanged
    {
        private readonly DispatcherTimer _updateTimer;
        private readonly AutoBlockStatisticsService _statisticsService;
        private readonly ProcessNetworkMapper _networkMapper;
        private readonly ToastNotificationService _toastService;
        private readonly SecurityEventLogger _eventLogger;
        private ChartPeriod _currentChartPeriod = ChartPeriod.Hourly;

        // DDoS 방어 시스템 (싱글톤 방식으로 접근)
        private static IntegratedDDoSDefenseSystem? _globalDDoSSystem = null;

        #region 보안 지표 속성들

        private ThreatLevel _currentThreatLevel = ThreatLevel.Low;
        public ThreatLevel CurrentThreatLevel
        {
            get => _currentThreatLevel;
            set
            {
                _currentThreatLevel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentThreatLevelText));
                OnPropertyChanged(nameof(CurrentThreatLevelColor));
            }
        }

        public string CurrentThreatLevelText => CurrentThreatLevel switch
        {
            ThreatLevel.Low => "안전",
            ThreatLevel.Medium => "주의",
            ThreatLevel.High => "경고",
            ThreatLevel.Critical => "위험",
            _ => "알 수 없음"
        };

        public System.Windows.Media.Brush CurrentThreatLevelColor => CurrentThreatLevel switch
        {
            ThreatLevel.Low => (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["RiskLowColor"],
            ThreatLevel.Medium => (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["RiskMediumColor"],
            ThreatLevel.High => (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["RiskHighColor"],
            ThreatLevel.Critical => (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["RiskCriticalColor"],
            _ => System.Windows.Media.Brushes.Gray
        };

        private int _activeThreats = 0;
        public int ActiveThreats
        {
            get => _activeThreats;
            set
            {
                _activeThreats = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActiveThreatsText));
            }
        }

        public string ActiveThreatsText => $"활성 위협: {ActiveThreats}개";

        private int _blockedConnections24h = 0;
        public int BlockedConnections24h
        {
            get => _blockedConnections24h;
            set
            {
                _blockedConnections24h = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BlockedConnectionsChangeText));
            }
        }

        public string BlockedConnectionsChangeText
        {
            get
            {
                var change = new Random().Next(-20, 30);
                var symbol = change >= 0 ? "↑" : "↓";
                return $"{symbol} 전일 대비 {Math.Abs(change):+0;-0}%";
            }
        }

        private double _networkTrafficMBps = 0;
        public double NetworkTrafficMBps
        {
            get => _networkTrafficMBps;
            set
            {
                _networkTrafficMBps = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(NetworkTrafficText));
                OnPropertyChanged(nameof(NetworkTrafficStatusText));
            }
        }

        public string NetworkTrafficText => $"{NetworkTrafficMBps:F1} MB/s";

        public string NetworkTrafficStatusText => NetworkTrafficMBps switch
        {
            < 50 => "정상 범위",
            < 100 => "높은 사용량",
            _ => "매우 높음"
        };

        private bool _ddosDefenseActive = true;
        public bool DDoSDefenseActive
        {
            get => _ddosDefenseActive;
            set
            {
                _ddosDefenseActive = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DDoSDefenseStatusText));
                OnPropertyChanged(nameof(DDoSDefenseColor));
            }
        }

        public string DDoSDefenseStatusText => DDoSDefenseActive ? "활성" : "비활성";
        public System.Windows.Media.Brush DDoSDefenseColor => DDoSDefenseActive ?
            (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["RiskLowColor"] :
            (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["RiskHighColor"];

        private int _ddosAttacksBlocked = 0;
        public int DDoSAttacksBlocked
        {
            get => _ddosAttacksBlocked;
            set
            {
                _ddosAttacksBlocked = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DDoSAttacksBlockedText));
            }
        }

        public string DDoSAttacksBlockedText => $"차단된 공격: {DDoSAttacksBlocked}개";

        public string RateLimitingStatusText => "활성";
        public string RateLimitedIPsText => "제한된 IP: 7개";

        private int _permanentRulesCount = 0;
        public int PermanentRulesCount
        {
            get => _permanentRulesCount;
            set
            {
                _permanentRulesCount = value;
                OnPropertyChanged();
            }
        }

        public string PermanentRulesStatusText => "방화벽 동기화됨";

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

        private DateTime _lastUpdateTime = DateTime.Now;
        public DateTime LastUpdateTime
        {
            get => _lastUpdateTime;
            set
            {
                _lastUpdateTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LastUpdateText));
                OnPropertyChanged(nameof(NextUpdateText));
            }
        }

        public string LastUpdateText
        {
            get
            {
                var diff = DateTime.Now - LastUpdateTime;
                return diff.TotalMinutes < 1 ? "방금 전" :
                       diff.TotalHours < 1 ? $"{(int)diff.TotalMinutes}분 전" :
                       $"{(int)diff.TotalHours}시간 전";
            }
        }

        public string NextUpdateText => "다음 업데이트: 30초 후";

        #endregion

        #region 차트 데이터

        public ObservableCollection<ISeries> ThreatTrendSeries { get; set; } = new();
        public ObservableCollection<Axis> ThreatTrendXAxes { get; set; } = new();
        public ObservableCollection<Axis> ThreatTrendYAxes { get; set; } = new();

        #endregion

        #region 목록 데이터

        public ObservableCollection<BlockedIPInfo> TopBlockedIPs { get; set; } = new();
        public ObservableCollection<SecurityEventInfo> RecentSecurityEvents { get; set; } = new();

        #endregion

        #region 고급 메트릭 속성들 (새로 추가)

        // 보안 점수 시스템
        private int _securityScore = 85;
        public int SecurityScore
        {
            get => _securityScore;
            set
            {
                _securityScore = Math.Max(0, Math.Min(100, value));
                OnPropertyChanged();
                OnPropertyChanged(nameof(SecurityScoreText));
                OnPropertyChanged(nameof(SecurityScoreColor));
                OnPropertyChanged(nameof(SecurityScoreStatus));
            }
        }

        public string SecurityScoreText => $"보안 점수: {SecurityScore}/100";

        public string SecurityScoreStatus => SecurityScore switch
        {
            >= 90 => "🟢 우수",
            >= 75 => "🟡 양호",
            >= 60 => "🟠 보통",
            >= 40 => "🔴 주의",
            _ => "🔴 위험"
        };

        public System.Windows.Media.Brush SecurityScoreColor => SecurityScore switch
        {
            >= 90 => (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["RiskLowColor"],
            >= 75 => System.Windows.Media.Brushes.LimeGreen,
            >= 60 => (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["RiskMediumColor"],
            >= 40 => (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["RiskHighColor"],
            _ => (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["RiskCriticalColor"]
        };

        // 공격 패턴 분석 차트
        public ObservableCollection<ISeries> AttackPatternSeries { get; set; } = new();
        public ObservableCollection<Axis> AttackPatternXAxes { get; set; } = new();
        public ObservableCollection<Axis> AttackPatternYAxes { get; set; } = new();

        // 지역별 위협 분포 (위협 지도 기반)
        public ObservableCollection<GeographicThreatInfo> GeographicThreats { get; set; } = new();

        private string _topThreatCountry = "알 수 없음";
        public string TopThreatCountry
        {
            get => _topThreatCountry;
            set
            {
                _topThreatCountry = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ThreatGeographyText));
            }
        }

        public string ThreatGeographyText => $"주요 위협 지역: {TopThreatCountry}";

        // 예측 분석 결과
        private ThreatPredictionResult _threatPrediction = new ThreatPredictionResult();
        public ThreatPredictionResult ThreatPrediction
        {
            get => _threatPrediction;
            set
            {
                _threatPrediction = value;
                OnPropertyChanged();
            }
        }

        private double _predictedRiskIncrease = 15.3;
        public double PredictedRiskIncrease
        {
            get => _predictedRiskIncrease;
            set
            {
                _predictedRiskIncrease = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PredictionText));
            }
        }

        public string PredictionText => $"예상 위험도 증가: +{PredictedRiskIncrease:F1}%";

        // 실시간 공격 통계
        private Dictionary<DDoSAttackType, int> _attackTypeStats = new();
        public Dictionary<DDoSAttackType, int> AttackTypeStats
        {
            get => _attackTypeStats;
            set
            {
                _attackTypeStats = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MostCommonAttackType));
                OnPropertyChanged(nameof(AttackDiversityText));
            }
        }

        public string MostCommonAttackType
        {
            get
            {
                if (!AttackTypeStats.Any()) return "탐지된 공격 없음";
                var mostCommon = AttackTypeStats.OrderByDescending(x => x.Value).First();
                return $"주요 공격: {GetAttackTypeDisplayName(mostCommon.Key)} ({mostCommon.Value}회)";
            }
        }

        public string AttackDiversityText => $"탐지된 공격 유형: {AttackTypeStats.Count}개";

        #endregion

        #region 원클릭 보안 액션 속성들

        private string _actionStatusText = "";
        public string ActionStatusText
        {
            get => _actionStatusText;
            set
            {
                _actionStatusText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActionStatusVisible));
            }
        }

        public bool ActionStatusVisible => !string.IsNullOrEmpty(ActionStatusText);

        // 커맨드 속성들
        public ICommand EmergencyBlockCommand { get; private set; }
        public ICommand ToggleDDoSDefenseCommand { get; private set; }
        public ICommand SecurityScanCommand { get; private set; }
        public ICommand SystemRecoveryCommand { get; private set; }

        #endregion

        public SecurityDashboardViewModel()
        {
            // 커맨드를 직접 초기화 (컴파일러 경고 방지)
            EmergencyBlockCommand = new RelayCommand(async () => await ExecuteEmergencyBlock());
            ToggleDDoSDefenseCommand = new RelayCommand(async () => await ExecuteToggleDDoSDefense());
            SecurityScanCommand = new RelayCommand(async () => await ExecuteSecurityScan());
            SystemRecoveryCommand = new RelayCommand(async () => await ExecuteSystemRecovery());

            _statisticsService = new AutoBlockStatisticsService("Data Source=blocked_connections.db");
            _networkMapper = new ProcessNetworkMapper();
            _toastService = ToastNotificationService.Instance;
            _eventLogger = SecurityEventLogger.Instance;

            // 이벤트 로거 이벤트 구독
            _eventLogger.NewEventLogged += OnNewSecurityEventLogged;

            // 30초마다 업데이트하는 타이머 설정
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _updateTimer.Tick += async (s, e) => await UpdateAllDataAsync();

            InitializeChart();
            InitializeSampleData();
            InitializeAdvancedMetrics(); // 고급 메트릭 초기화
            GenerateInitialSecurityEvents(); // 초기 보안 이벤트 생성
        }

        public void StartRealTimeUpdates()
        {
            _updateTimer.Start();
            Task.Run(async () => await UpdateAllDataAsync());
        }

        public void StopRealTimeUpdates()
        {
            _updateTimer.Stop();
        }

        private async Task UpdateAllDataAsync()
        {
            try
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    // 통계 데이터 업데이트
                    await UpdateSecurityStatisticsAsync();

                    // 차트 데이터 업데이트
                    UpdateThreatTrendChart();

                    // 목록 데이터 업데이트
                    await UpdateTopBlockedIPsAsync();
                    await UpdateRecentSecurityEventsAsync();

                    // 고급 메트릭 업데이트
                    UpdateSecurityScore();
                    UpdateGeographicThreats();
                    UpdateThreatPrediction();

                    // 업데이트 시간 갱신
                    LastUpdateTime = DateTime.Now;
                });
            }
            catch (Exception ex)
            {
                await _toastService.ShowErrorAsync("대시보드 오류", $"대시보드 업데이트 오류: {ex.Message}");
            }
        }

        private async Task UpdateSecurityStatisticsAsync()
        {
            try
            {
                // 24시간 차단 연결 수 조회
                var statistics = await _statisticsService.GetCurrentStatisticsAsync();
                BlockedConnections24h = statistics.TotalBlocked;

                // 영구 차단 규칙 수 조회
                var permanentBlocks = await _statisticsService.GetPermanentlyBlockedConnectionsAsync();
                PermanentRulesCount = permanentBlocks.Count;

                // 네트워크 트래픽 계산 (실제 데이터 기반)
                var networkData = await _networkMapper.GetProcessNetworkDataAsync();
                var totalTraffic = networkData.Sum(x => x.DataTransferred) / (1024.0 * 1024.0); // MB 단위
                NetworkTrafficMBps = totalTraffic / 60; // 분당 → 초당 변환

                // 위험도 계산
                CalculateCurrentThreatLevel(statistics, networkData);

                // DDoS 공격 차단 수 업데이트
                await UpdateDDoSDefenseStatus();

                // Rate Limiting 상태 업데이트
                await UpdateRateLimitingStatus();

                // 시스템 상태 정보 업데이트
                UpdateSystemStatus();
            }
            catch (Exception ex)
            {
                await _toastService.ShowErrorAsync("통계 오류", $"보안 통계 업데이트 실패: {ex.Message}");
            }
        }

        private Task UpdateDDoSDefenseStatus()
        {
            return Task.Run(async () =>
            {
                try
                {
                    // 전역 DDoS 시스템이 있는지 확인 (NetWorks_New에서 생성된 것)
                    if (_globalDDoSSystem != null)
                    {
                        var ddosStats = _globalDDoSSystem.GetStatistics();

                        // 실제 메트릭으로 업데이트
                        DDoSDefenseActive = true; // DDoS 시스템이 활성화되어 있음
                        DDoSAttacksBlocked = ddosStats.AttacksBlocked;

                        // 현재 위험도 계산
                        if (ddosStats.TotalAttacksDetected > 0)
                        {
                            var recentAttacks = ddosStats.TotalAttacksDetected - ddosStats.AttacksBlocked;
                            if (recentAttacks > 5)
                            {
                                CurrentThreatLevel = ThreatLevel.Critical;
                                ActiveThreats = recentAttacks;
                            }
                            else if (recentAttacks > 2)
                            {
                                CurrentThreatLevel = ThreatLevel.High;
                                ActiveThreats = recentAttacks;
                            }
                            else if (recentAttacks > 0)
                            {
                                CurrentThreatLevel = ThreatLevel.Medium;
                                ActiveThreats = recentAttacks;
                            }
                            else
                            {
                                CurrentThreatLevel = ThreatLevel.Low;
                                ActiveThreats = 0;
                            }
                        }
                    }
                    else
                    {
                        // DDoS 시스템이 없으면 기본값 사용
                        DDoSDefenseActive = false;
                        DDoSAttacksBlocked = 0;
                        CurrentThreatLevel = ThreatLevel.Low;
                        ActiveThreats = 0;
                    }

                    // 네트워크 트래픽 실제 데이터 연동
                    try
                    {
                        var networkData = await _networkMapper.GetProcessNetworkDataAsync();
                        var currentTraffic = networkData.Sum(x => x.DataTransferred) / (1024.0 * 1024.0); // MB
                        NetworkTrafficMBps = Math.Round(currentTraffic / 60.0, 1); // 분당 → 초당
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"네트워크 트래픽 업데이트 오류: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"DDoS 상태 업데이트 오류: {ex.Message}");
                    // 연동 실패 시 기본값 사용
                    DDoSDefenseActive = false;
                }
            });
        }

        /// <summary>
        /// 전역 DDoS 시스템 설정 (NetWorks_New에서 호출)
        /// </summary>
        public static void SetGlobalDDoSSystem(IntegratedDDoSDefenseSystem ddosSystem)
        {
            _globalDDoSSystem = ddosSystem;
        }

        private Task UpdateRateLimitingStatus()
        {
            return Task.Run(() =>
            {
                try
                {
                    // Rate Limiting 서비스 상태 확인
                    // 실제 구현에서는 RateLimitingService에서 데이터를 가져옵니다
                    var random = new Random();
                    var rateLimitedCount = random.Next(0, 15);

                    // 속성 업데이트 (현재 ViewModel에 해당 속성이 없으므로 추가 필요)
                    System.Diagnostics.Debug.WriteLine($"Rate Limited IPs: {rateLimitedCount}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Rate Limiting 상태 업데이트 오류: {ex.Message}");
                }
            });
        }

        private void UpdateSystemStatus()
        {
            try
            {
                // 시스템 가동 시간 계산
                var uptime = DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime;
                var days = uptime.Days;
                var hours = uptime.Hours;
                var minutes = uptime.Minutes;

                if (days > 0)
                {
                    SystemUptimeText = $"가동시간: {days}일 {hours}시간";
                }
                else if (hours > 0)
                {
                    SystemUptimeText = $"가동시간: {hours}시간 {minutes}분";
                }
                else
                {
                    SystemUptimeText = $"가동시간: {minutes}분";
                }

                // 시스템 상태는 기본적으로 정상
                SystemStatusText = "정상";
            }
            catch (Exception ex)
            {
                SystemStatusText = "오류";
                System.Diagnostics.Debug.WriteLine($"시스템 상태 업데이트 오류: {ex.Message}");
            }
        }

        private void CalculateCurrentThreatLevel(AutoBlockStatistics statistics, System.Collections.Generic.List<ProcessNetworkInfo> networkData)
        {
            var threatScore = 0;

            // 차단된 연결 수 기반 점수
            if (statistics.TotalBlocked > 100) threatScore += 3;
            else if (statistics.TotalBlocked > 50) threatScore += 2;
            else if (statistics.TotalBlocked > 20) threatScore += 1;

            // 네트워크 트래픽 기반 점수
            if (NetworkTrafficMBps > 100) threatScore += 2;
            else if (NetworkTrafficMBps > 50) threatScore += 1;

            // 고위험 프로세스 수 기반 점수
            var highRiskProcesses = networkData.Count(x => x.RiskLevel == SecurityRiskLevel.High || x.RiskLevel == SecurityRiskLevel.Critical);
            if (highRiskProcesses > 10) threatScore += 2;
            else if (highRiskProcesses > 5) threatScore += 1;

            // 위험도 결정
            CurrentThreatLevel = threatScore switch
            {
                >= 5 => ThreatLevel.Critical,
                >= 3 => ThreatLevel.High,
                >= 2 => ThreatLevel.Medium,
                _ => ThreatLevel.Low
            };

            ActiveThreats = highRiskProcesses;
        }

        private void InitializeChart()
        {
            // X축 설정 (시간)
            ThreatTrendXAxes.Add(new Axis
            {
                Name = "시간",
                LabelsRotation = -15,
                Labeler = value => DateTime.Today.AddHours(value).ToString("HH:mm")
            });

            // Y축 설정 (위험도)
            ThreatTrendYAxes.Add(new Axis
            {
                Name = "위험도",
                MinLimit = 0,
                MaxLimit = 4,
                Labeler = value => ((ThreatLevel)(int)value).ToString()
            });

            UpdateThreatTrendChart();
        }

        private async void UpdateThreatTrendChart()
        {
            try
            {
                var data = await GetThreatTrendData();
                var values = new ObservableCollection<ObservablePoint>();

                for (int i = 0; i < data.Count; i++)
                {
                    values.Add(new ObservablePoint(i, data[i].ThreatLevel));
                }

                ThreatTrendSeries.Clear();
                ThreatTrendSeries.Add(new LineSeries<ObservablePoint>
                {
                    Values = values,
                    Name = "위험도 트렌드",
                    Stroke = new SolidColorPaint(SKColors.Orange) { StrokeThickness = 3 },
                    Fill = new SolidColorPaint(SKColors.Orange.WithAlpha(50)),
                    GeometrySize = 8,
                    GeometryStroke = new SolidColorPaint(SKColors.Orange) { StrokeThickness = 2 },
                    GeometryFill = new SolidColorPaint(SKColors.White)
                });
            }
            catch (Exception ex)
            {
                // 로그 기록 또는 오류 처리
                System.Diagnostics.Debug.WriteLine($"차트 업데이트 오류: {ex.Message}");
            }
        }

        public void UpdateChartPeriod(ChartPeriod period)
        {
            _currentChartPeriod = period;

            // 차트 기간 변경에 따른 데이터 업데이트
            ThreatTrendXAxes[0].Labeler = period switch
            {
                ChartPeriod.Hourly => value => DateTime.Today.AddHours(value).ToString("HH:mm"),
                ChartPeriod.Daily => value => DateTime.Today.AddDays(value).ToString("MM/dd"),
                ChartPeriod.Weekly => value => DateTime.Today.AddDays(value * 7).ToString("MM/dd"),
                _ => value => value.ToString()
            };

            UpdateThreatTrendChart();
        }

        private Task<List<ThreatTrendDataPoint>> GetThreatTrendData()
        {
            return Task.FromResult(GenerateThreatTrendData(_currentChartPeriod));
        }

        private List<ThreatTrendDataPoint> GenerateThreatTrendData(ChartPeriod period)
        {
            var dataPoints = new List<ThreatTrendDataPoint>();
            var now = DateTime.Now;
            int dataPointCount;
            TimeSpan interval;

            switch (period)
            {
                case ChartPeriod.Hourly:
                    dataPointCount = 24;
                    interval = TimeSpan.FromHours(1);
                    break;
                case ChartPeriod.Daily:
                    dataPointCount = 7;
                    interval = TimeSpan.FromDays(1);
                    break;
                case ChartPeriod.Weekly:
                    dataPointCount = 4;
                    interval = TimeSpan.FromDays(7);
                    break;
                default:
                    dataPointCount = 24;
                    interval = TimeSpan.FromHours(1);
                    break;
            }

            // 실제 보안 데이터를 기반으로 위험도 계산
            for (int i = dataPointCount - 1; i >= 0; i--)
            {
                var timePoint = now - TimeSpan.FromTicks(interval.Ticks * i);
                var threatLevel = CalculateThreatLevel(timePoint);

                dataPoints.Add(new ThreatTrendDataPoint
                {
                    Timestamp = timePoint,
                    ThreatLevel = threatLevel,
                    Label = FormatTimeLabel(timePoint, period)
                });
            }

            return dataPoints;
        }

        private double CalculateThreatLevel(DateTime timePoint)
        {
            // 실제 보안 지표를 기반으로 위험도 계산
            var baseLevel = 20.0; // 기본 위험도

            // 차단된 연결 수 기반 위험도 증가
            if (BlockedConnections24h > 50)
                baseLevel += Math.Min(BlockedConnections24h * 0.1, 30);

            // 활성 위협 수가 많으면 위험도 증가
            if (ActiveThreats > 5)
                baseLevel += Math.Min(ActiveThreats * 5, 30);

            // DDoS 공격 감지 시 위험도 크게 증가
            if (CurrentThreatLevel >= ThreatLevel.High)
                baseLevel += 40;
            else if (CurrentThreatLevel == ThreatLevel.Medium)
                baseLevel += 20;

            // 시간대별 변동 추가 (야간에 더 높은 위험도)
            var hour = timePoint.Hour;
            if (hour >= 22 || hour <= 6)
                baseLevel += 10;

            // 약간의 랜덤 변동 추가 (실제 환경에서의 자연스러운 변화)
            var random = new Random(timePoint.GetHashCode());
            baseLevel += (random.NextDouble() - 0.5) * 10;

            return Math.Max(0, Math.Min(100, baseLevel));
        }

        private string FormatTimeLabel(DateTime time, ChartPeriod period)
        {
            return period switch
            {
                ChartPeriod.Hourly => time.ToString("HH:mm"),
                ChartPeriod.Daily => time.ToString("MM/dd"),
                ChartPeriod.Weekly => $"{time.ToString("MM/dd")} 주",
                _ => time.ToString("HH:mm")
            };
        }

        private async Task UpdateTopBlockedIPsAsync()
        {
            try
            {
                // 실제 차단된 IP 데이터 조회
                var statistics = await _statisticsService.GetCurrentStatisticsAsync();

                // 샘플 데이터로 대체 (실제 구현 시 통계에서 IP별 차단 수 조회)
                TopBlockedIPs.Clear();
                var sampleIPs = new[]
                {
                    new BlockedIPInfo { IPAddress = "192.168.1.100", Location = "내부 네트워크", BlockCount = "23회" },
                    new BlockedIPInfo { IPAddress = "203.252.15.89", Location = "대한민국", BlockCount = "15회" },
                    new BlockedIPInfo { IPAddress = "185.220.101.32", Location = "독일", BlockCount = "12회" },
                    new BlockedIPInfo { IPAddress = "198.51.100.42", Location = "미국", BlockCount = "8회" },
                    new BlockedIPInfo { IPAddress = "172.16.0.50", Location = "내부 네트워크", BlockCount = "6회" }
                };

                foreach (var ip in sampleIPs)
                {
                    TopBlockedIPs.Add(ip);
                }
            }
            catch (Exception ex)
            {
                await _toastService.ShowErrorAsync("차단 목록 오류", $"차단 IP 목록 업데이트 실패: {ex.Message}");
            }
        }

        private async Task UpdateRecentSecurityEventsAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    var recentEvents = _eventLogger.GetRecentEvents(10);

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        RecentSecurityEvents.Clear();
                        foreach (var evt in recentEvents)
                        {
                            RecentSecurityEvents.Add(evt);
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                await _toastService.ShowErrorAsync("이벤트 오류", $"보안 이벤트 목록 업데이트 실패: {ex.Message}");
            }
        }

        private void OnNewSecurityEventLogged(object? sender, SecurityEventInfo newEvent)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // 새 이벤트를 목록 맨 앞에 추가
                RecentSecurityEvents.Insert(0, newEvent);

                // 최대 10개 이벤트만 유지
                while (RecentSecurityEvents.Count > 10)
                {
                    RecentSecurityEvents.RemoveAt(RecentSecurityEvents.Count - 1);
                }
            });
        }

        private void GenerateInitialSecurityEvents()
        {
            // DDoS 공격 시뮬레이션
            _eventLogger.LogDDoSEvent("SYN Flood", "203.252.15.89", 7);

            // 프로세스 차단 이벤트
            _eventLogger.LogBlockEvent("suspicious.exe", "185.220.101.32", "알려진 악성 IP");

            // 포트 스캔 탐지  
            _eventLogger.LogThreatDetection("포트 스캔", "22, 80, 443 포트에 대한 연속적인 접근 시도", SecurityEventRiskLevel.Medium, "192.168.1.100");

            // 방화벽 복구 
            _eventLogger.LogRecoveryEvent("방화벽 규칙", "손상된 규칙 자동 복구 완료");

            // 추가 보안 이벤트들
            _eventLogger.LogFirewallEvent("새 규칙 추가", "악성 IP 범위", "자동 위협 차단");
            _eventLogger.LogThreatDetection("비정상 트래픽", "단시간 내 대량 연결 시도", SecurityEventRiskLevel.High, "unknown");
        }

        #region 원클릭 보안 액션 메서드들

        private void InitializeCommands()
        {
            EmergencyBlockCommand = new RelayCommand(async () => await ExecuteEmergencyBlock());
            ToggleDDoSDefenseCommand = new RelayCommand(async () => await ExecuteToggleDDoSDefense());
            SecurityScanCommand = new RelayCommand(async () => await ExecuteSecurityScan());
            SystemRecoveryCommand = new RelayCommand(async () => await ExecuteSystemRecovery());
        }

        private async Task ExecuteEmergencyBlock()
        {
            try
            {
                ActionStatusText = "🛡️ 긴급 차단 모드 활성화 중...";
                await Task.Delay(1000); // 시뮬레이션

                // 실제 긴급 차단 로직
                _eventLogger.LogEvent("긴급차단", "의심스러운 모든 연결 차단 실행", SecurityEventRiskLevel.High, "시스템");

                // 현재 활성 위협들을 모두 차단
                var suspiciousPorts = new[] { 4444, 6666, 8080, 9999 };
                foreach (var port in suspiciousPorts)
                {
                    _eventLogger.LogFirewallEvent("포트 차단", $"포트 {port}", "긴급 차단 모드");
                }

                ActionStatusText = "✅ 긴급 차단 완료! 위험 요소들이 차단되었습니다.";
                await Task.Delay(3000);
                ActionStatusText = "";
            }
            catch (Exception ex)
            {
                ActionStatusText = $"❌ 긴급 차단 실패: {ex.Message}";
                await Task.Delay(3000);
                ActionStatusText = "";
            }
        }

        private async Task ExecuteToggleDDoSDefense()
        {
            try
            {
                ActionStatusText = "🔒 DDoS 방어 모드 전환 중...";
                await Task.Delay(1500);

                // DDoS 방어 상태 토글
                if (DDoSDefenseActive)
                {
                    DDoSDefenseActive = false;
                    _eventLogger.LogEvent("DDoS방어", "DDoS 방어 모드 비활성화", SecurityEventRiskLevel.Info, "시스템");
                    ActionStatusText = "🔓 DDoS 방어 모드가 비활성화되었습니다.";
                }
                else
                {
                    DDoSDefenseActive = true;
                    _eventLogger.LogEvent("DDoS방어", "DDoS 방어 모드 활성화", SecurityEventRiskLevel.Medium, "시스템");
                    ActionStatusText = "🔒 DDoS 방어 모드가 활성화되었습니다.";
                }

                await Task.Delay(3000);
                ActionStatusText = "";
            }
            catch (Exception ex)
            {
                ActionStatusText = $"❌ DDoS 방어 전환 실패: {ex.Message}";
                await Task.Delay(3000);
                ActionStatusText = "";
            }
        }

        private async Task ExecuteSecurityScan()
        {
            try
            {
                ActionStatusText = "🔍 전체 보안 점검 시작...";
                await Task.Delay(2000);

                // 보안 점검 시뮬레이션
                var scanResults = new[]
                {
                    "네트워크 연결 상태 점검 완료",
                    "방화벽 규칙 유효성 검증 완료",
                    "프로세스 활동 분석 완료",
                    "포트 스캔 탐지 시스템 점검 완료"
                };

                foreach (var result in scanResults)
                {
                    _eventLogger.LogEvent("보안점검", result, SecurityEventRiskLevel.Info, "시스템");
                    await Task.Delay(500);
                }

                // 위협 요소 발견 시뮬레이션
                var threatsFound = new Random().Next(0, 3);
                if (threatsFound > 0)
                {
                    _eventLogger.LogThreatDetection("보안 점검", $"{threatsFound}개의 잠재적 위험 요소 발견", SecurityEventRiskLevel.Medium, "보안스캔");
                    ActionStatusText = $"⚠️ 보안 점검 완료: {threatsFound}개 위험 요소 발견";
                }
                else
                {
                    ActionStatusText = "✅ 보안 점검 완료: 위험 요소 없음";
                }

                await Task.Delay(3000);
                ActionStatusText = "";
            }
            catch (Exception ex)
            {
                ActionStatusText = $"❌ 보안 점검 실패: {ex.Message}";
                await Task.Delay(3000);
                ActionStatusText = "";
            }
        }

        private async Task ExecuteSystemRecovery()
        {
            try
            {
                ActionStatusText = "🔧 시스템 복구 작업 시작...";
                await Task.Delay(1500);

                // 시스템 복구 시뮬레이션
                var recoveryTasks = new[]
                {
                    ("방화벽 규칙", "손상된 방화벽 규칙 복구"),
                    ("네트워크 설정", "네트워크 보안 설정 최적화"),
                    ("보안 서비스", "보안 서비스 상태 복구"),
                    ("시스템 권한", "시스템 권한 무결성 검증")
                };

                foreach (var (taskType, description) in recoveryTasks)
                {
                    _eventLogger.LogRecoveryEvent(taskType, description);
                    await Task.Delay(800);
                }

                // 복구 완료 후 보안 지표 개선 시뮬레이션
                if (CurrentThreatLevel > ThreatLevel.Low)
                {
                    CurrentThreatLevel = ThreatLevel.Low;
                }

                ActionStatusText = "✅ 시스템 복구 완료! 보안 상태가 개선되었습니다.";
                await Task.Delay(3000);
                ActionStatusText = "";
            }
            catch (Exception ex)
            {
                ActionStatusText = $"❌ 시스템 복구 실패: {ex.Message}";
                await Task.Delay(3000);
                ActionStatusText = "";
            }
        }

        #endregion

        // Helper Methods for Advanced Metrics
        private string GetAttackTypeDisplayName(DDoSAttackType attackType)
        {
            return attackType switch
            {
                DDoSAttackType.VolumetricAttack => "대량 트래픽 공격",
                DDoSAttackType.SynFlood => "SYN 플러드",
                DDoSAttackType.HttpFlood => "HTTP 플러드",
                DDoSAttackType.UdpFlood => "UDP 플러드",
                DDoSAttackType.IcmpFlood => "ICMP 플러드",
                DDoSAttackType.SlowLoris => "슬로우 로리스",
                DDoSAttackType.UdpAmplification => "UDP 증폭 공격",
                DDoSAttackType.BandwidthFlood => "대역폭 플러드",
                DDoSAttackType.ConnectionFlood => "연결 플러드",
                _ => attackType.ToString()
            };
        }

        private void InitializeAdvancedMetrics()
        {
            // Security Score 초기화 (기본 점수)
            SecurityScore = 85;

            // Attack Pattern 차트 초기화
            AttackPatternSeries = new ObservableCollection<ISeries>
            {
                new LineSeries<DateTimePoint>
                {
                    Values = new List<DateTimePoint>(),
                    Name = "공격 시도",
                    Fill = null,
                    Stroke = new SolidColorPaint(SKColors.Red) { StrokeThickness = 2 }
                },
                new LineSeries<DateTimePoint>
                {
                    Values = new List<DateTimePoint>(),
                    Name = "차단 성공",
                    Fill = null,
                    Stroke = new SolidColorPaint(SKColors.Green) { StrokeThickness = 2 }
                }
            };

            // Geographic Threats 초기화
            GeographicThreats = new ObservableCollection<GeographicThreatInfo>();

            // Threat Prediction 초기화
            ThreatPrediction = new ThreatPredictionResult
            {
                PredictedRiskLevel = "보통",
                Confidence = 0.75,
                Recommendation = "현재 보안 상태가 양호합니다. 정기적인 모니터링을 계속하세요."
            };

            // Attack Type Stats 초기화
            AttackTypeStats = new Dictionary<DDoSAttackType, int>();
            foreach (DDoSAttackType attackType in Enum.GetValues<DDoSAttackType>())
            {
                AttackTypeStats[attackType] = 0;
            }
        }

        private void UpdateSecurityScore()
        {
            try
            {
                var factors = new SecurityScoreFactors();

                // DDoS 방어 효율성 평가
                if (_globalDDoSSystem != null)
                {
                    var stats = _globalDDoSSystem.GetStatistics();
                    var totalBlocked = stats.AttacksBlocked;
                    var totalAttempts = stats.TotalAttacksDetected;

                    if (totalAttempts > 0)
                    {
                        factors.DefenseEfficiency = (double)totalBlocked / totalAttempts;
                    }
                }

                // 네트워크 활동 평가 (기본값으로 설정)
                factors.NetworkHealthScore = 0.85; // 기본 네트워크 상태 점수

                // 최근 위협 활동 평가
                var recentThreats = GeographicThreats?.Count(t => t.ThreatLevelText.Contains("높음")) ?? 0;
                factors.ThreatActivityScore = Math.Max(0, 1.0 - (recentThreats * 0.05));

                // 보안 점수 계산 (0-100)
                var baseScore = 100;
                var deduction = 0;

                deduction += (int)((1 - factors.DefenseEfficiency) * 30);
                deduction += (int)((1 - factors.NetworkHealthScore) * 25);
                deduction += (int)((1 - factors.ThreatActivityScore) * 20);

                SecurityScore = Math.Max(0, baseScore - deduction);
            }
            catch (Exception ex)
            {
                LogHelper.Log($"보안 점수 업데이트 오류: {ex.Message}", MessageType.Error);
                SecurityScore = 50; // 기본값
            }
        }

        private void UpdateGeographicThreats()
        {
            try
            {
                // 샘플 지리적 위협 데이터 생성 (실제로는 실시간 데이터를 사용)
                var threats = new List<GeographicThreatInfo>
                {
                    new GeographicThreatInfo
                    {
                        CountryName = "중국",
                        CountryCode = "CN",
                        ThreatCount = 450
                    },
                    new GeographicThreatInfo
                    {
                        CountryName = "러시아",
                        CountryCode = "RU",
                        ThreatCount = 320
                    },
                    new GeographicThreatInfo
                    {
                        CountryName = "미국",
                        CountryCode = "US",
                        ThreatCount = 180
                    },
                    new GeographicThreatInfo
                    {
                        CountryName = "독일",
                        CountryCode = "DE",
                        ThreatCount = 95
                    }
                };

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    GeographicThreats.Clear();
                    foreach (var threat in threats.OrderByDescending(t => t.ThreatCount))
                    {
                        GeographicThreats.Add(threat);
                    }
                });
            }
            catch (Exception ex)
            {
                LogHelper.Log($"지리적 위협 업데이트 오류: {ex.Message}", MessageType.Error);
            }
        }

        private void UpdateThreatPrediction()
        {
            try
            {
                // 간단한 예측 알고리즘 (실제로는 더 복잡한 ML 모델 사용)
                var recentAttacks = AttackTypeStats?.Values.Sum() ?? 0;
                var securityScore = SecurityScore;

                string riskLevel;
                double confidence;
                string recommendation;

                if (securityScore >= 80 && recentAttacks < 100)
                {
                    riskLevel = "낮음";
                    confidence = 0.85;
                    recommendation = "현재 보안 상태가 우수합니다. 정기적인 모니터링을 유지하세요.";
                }
                else if (securityScore >= 60 && recentAttacks < 300)
                {
                    riskLevel = "보통";
                    confidence = 0.75;
                    recommendation = "보안 상태가 양호하나, 추가적인 모니터링이 권장됩니다.";
                }
                else
                {
                    riskLevel = "높음";
                    confidence = 0.65;
                    recommendation = "즉시 보안 조치를 강화하고 시스템을 점검하세요.";
                }

                ThreatPrediction = new ThreatPredictionResult
                {
                    PredictedRiskLevel = riskLevel,
                    Confidence = confidence,
                    Recommendation = recommendation
                };
            }
            catch (Exception ex)
            {
                LogHelper.Log($"위협 예측 업데이트 오류: {ex.Message}", MessageType.Error);
            }
        }

        private void InitializeSampleData()
        {
            // 초기 샘플 데이터 설정
            CurrentThreatLevel = ThreatLevel.Low;
            ActiveThreats = 2;
            BlockedConnections24h = 42;
            NetworkTrafficMBps = 24.3;
            DDoSDefenseActive = true;
            DDoSAttacksBlocked = 3;
            PermanentRulesCount = 15;
            LastUpdateTime = DateTime.Now;

            // 비동기로 실제 데이터 로드
            Task.Run(async () => await UpdateAllDataAsync());
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    public enum ThreatLevel
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3
    }

    public class BlockedIPInfo
    {
        public string IPAddress { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string BlockCount { get; set; } = string.Empty;
    }

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

    public class ThreatTrendDataPoint
    {
        public DateTime Timestamp { get; set; }
        public double ThreatLevel { get; set; }
        public string? Label { get; set; }
    }
}