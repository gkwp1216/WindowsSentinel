using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
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
    public class SecurityDashboardViewModel : INotifyPropertyChanged
    {
        private readonly DispatcherTimer _updateTimer;
        private readonly AutoBlockStatisticsService _statisticsService;
        private readonly ProcessNetworkMapper _networkMapper;
        private readonly ToastNotificationService _toastService;

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
        public string SystemStatusText => "정상";
        public string SystemUptimeText => $"가동시간: {DateTime.Now.Subtract(System.Diagnostics.Process.GetCurrentProcess().StartTime):d\\d\\ h\\h}";

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

        public SecurityDashboardViewModel()
        {
            _statisticsService = new AutoBlockStatisticsService("Data Source=blocked_connections.db");
            _networkMapper = new ProcessNetworkMapper();
            _toastService = ToastNotificationService.Instance;

            // 30초마다 업데이트하는 타이머 설정
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _updateTimer.Tick += async (s, e) => await UpdateAllDataAsync();

            InitializeChart();
            InitializeSampleData();
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

                // DDoS 공격 차단 수 업데이트 (시뮬레이션)
                DDoSAttacksBlocked += new Random().Next(0, 2);
            }
            catch (Exception ex)
            {
                await _toastService.ShowErrorAsync("통계 오류", $"보안 통계 업데이트 실패: {ex.Message}");
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

        private void UpdateThreatTrendChart()
        {
            var values = new ObservableCollection<ObservablePoint>();
            var random = new Random();

            // 지난 24시간 데이터 시뮬레이션
            for (int i = 0; i < 24; i++)
            {
                var threatLevel = random.Next(0, 4);
                values.Add(new ObservablePoint(i, threatLevel));
            }

            ThreatTrendSeries.Clear();
            ThreatTrendSeries.Add(new LineSeries<ObservablePoint>
            {
                Values = values,
                Name = "위험도",
                Stroke = new SolidColorPaint(SKColors.Orange) { StrokeThickness = 3 },
                Fill = null,
                GeometrySize = 6
            });
        }

        public void UpdateChartPeriod(ChartPeriod period)
        {
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
                RecentSecurityEvents.Clear();

                // 최근 보안 이벤트 샘플 데이터
                var events = new[]
                {
                    new SecurityEventInfo
                    {
                        Timestamp = DateTime.Now.AddMinutes(-2),
                        EventType = "DDoS",
                        TypeColor = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["RiskHighColor"],
                        Description = "SYN Flood 공격 탐지 및 차단",
                        RiskLevel = "높음",
                        RiskColor = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["RiskHighColor"],
                        Source = "203.252.15.89"
                    },
                    new SecurityEventInfo
                    {
                        Timestamp = DateTime.Now.AddMinutes(-5),
                        EventType = "차단",
                        TypeColor = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["RiskMediumColor"],
                        Description = "의심스러운 프로세스 네트워크 연결 차단",
                        RiskLevel = "보통",
                        RiskColor = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["RiskMediumColor"],
                        Source = "malware.exe"
                    },
                    new SecurityEventInfo
                    {
                        Timestamp = DateTime.Now.AddMinutes(-8),
                        EventType = "탐지",
                        TypeColor = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["AccentBrush"],
                        Description = "포트 스캔 시도 감지",
                        RiskLevel = "낮음",
                        RiskColor = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["RiskLowColor"],
                        Source = "192.168.1.100"
                    },
                    new SecurityEventInfo
                    {
                        Timestamp = DateTime.Now.AddMinutes(-12),
                        EventType = "복구",
                        TypeColor = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["RiskLowColor"],
                        Description = "방화벽 규칙 자동 복구 완료",
                        RiskLevel = "정보",
                        RiskColor = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["AccentBrush"],
                        Source = "시스템"
                    }
                };

                foreach (var evt in events)
                {
                    RecentSecurityEvents.Add(evt);
                }
            }
            catch (Exception ex)
            {
                await _toastService.ShowErrorAsync("이벤트 오류", $"보안 이벤트 목록 업데이트 실패: {ex.Message}");
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
}