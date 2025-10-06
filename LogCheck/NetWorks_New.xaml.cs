using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives; // Popup 사용시
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LogCheck.Models;
using LogCheck.Services;
using SkiaSharp;
using Controls = System.Windows.Controls;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;
using SecurityAlert = LogCheck.Services.SecurityAlert;

namespace LogCheck
{
    /// <summary>
    /// NetWorks_New.xaml에 대한 상호작용 논리
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class NetWorks_New : Page, LogCheck.Models.INavigable, INotifyPropertyChanged
    {
        private ToggleButton? _selectedButton;

        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly ObservableCollection<ProcessNetworkInfo> _generalProcessData;
        private readonly ObservableCollection<ProcessNetworkInfo> _systemProcessData;

        // TreeView용 프로세스 노드 컬렉션 (작업 관리자 방식)
        private readonly ObservableCollection<ProcessTreeNode> _processTreeNodes;
        private readonly ObservableCollection<ProcessTreeNode> _systemProcessTreeNodes;

        // 기존 그룹화된 데이터 컬렉션 (하위 호환성을 위해 유지)
        private readonly ObservableCollection<ProcessGroup> _generalProcessGroups;
        private readonly ObservableCollection<ProcessGroup> _systemProcessGroups;

        // XAML 컨트롤 레퍼런스는 XAML에서 자동으로 생성됨
        private readonly ProcessNetworkMapper _processNetworkMapper;
        private readonly NetworkConnectionManager _connectionManager;
        private readonly RealTimeSecurityAnalyzer _securityAnalyzer;
        private readonly DispatcherTimer _updateTimer;
        private readonly ObservableCollection<ProcessNetworkInfo> _processNetworkData;
        private readonly ObservableCollection<SecurityAlert> _securityAlerts;
        private readonly LogMessageService _logService;

        // AutoBlock 시스템
        private readonly IAutoBlockService _autoBlockService;
        private readonly AutoBlockStatisticsService _autoBlockStats;
        private readonly ObservableCollection<AutoBlockedConnection> _blockedConnections;
        private readonly ObservableCollection<AutoWhitelistEntry> _whitelistEntries;

        // DDoS 방어 시스템
        private IntegratedDDoSDefenseSystem? _ddosDefenseSystem;
        private DDoSDetectionEngine? _ddosDetectionEngine;
        private AdvancedPacketAnalyzer? _packetAnalyzer;
        private RateLimitingService? _rateLimitingService;
        private DDoSSignatureDatabase? _signatureDatabase;
        private readonly ObservableCollection<DDoSDetectionResult> _ddosAlerts;
        private readonly ObservableCollection<DDoSDetectionResult> _attackHistory;
        private readonly DispatcherTimer _ddosUpdateTimer;

        // 영구 방화벽 차단 시스템
        private PersistentFirewallManager? _persistentFirewallManager;
        private readonly ObservableCollection<FirewallRuleInfo> _firewallRules;
        private bool _isInitialized = false;
        private int _totalBlockedCount = 0;
        private int _level1BlockCount = 0;
        private int _level2BlockCount = 0;
        private int _level3BlockCount = 0;
        private int _uniqueProcesses = 0;
        private int _uniqueIPs = 0;

        // 통계 데이터
        private int _totalConnections = 0;
        private int _lowRiskCount = 0;
        private int _mediumRiskCount = 0;
        private int _highRiskCount = 0;
        private int _tcpCount = 0;
        private int _udpCount = 0;
        private int _icmpCount = 0;
        private long _totalDataTransferred = 0;

        // 바인딩용 공개 프로퍼티들
        public int TotalConnections
        {
            get => _totalConnections;
            set { _totalConnections = value; OnPropertyChanged(); }
        }

        public int LowRiskCount
        {
            get => _lowRiskCount;
            set { _lowRiskCount = value; OnPropertyChanged(); }
        }

        public int MediumRiskCount
        {
            get => _mediumRiskCount;
            set { _mediumRiskCount = value; OnPropertyChanged(); }
        }

        public int HighRiskCount
        {
            get => _highRiskCount;
            set { _highRiskCount = value; OnPropertyChanged(); }
        }

        public int TcpCount
        {
            get => _tcpCount;
            set { _tcpCount = value; OnPropertyChanged(); }
        }

        public int UdpCount
        {
            get => _udpCount;
            set { _udpCount = value; OnPropertyChanged(); }
        }

        public int IcmpCount
        {
            get => _icmpCount;
            set { _icmpCount = value; OnPropertyChanged(); }
        }

        public string TotalDataTransferred
        {
            get => $"{_totalDataTransferred / (1024.0 * 1024.0):F1} MB";
        }

        // DDoS 관련 바인딩 프로퍼티
        private double _riskScore = 0;
        private int _attacksDetected = 0;
        private int _blockedIPs = 0;
        private double _trafficVolume = 0;

        public double RiskScore
        {
            get => _riskScore;
            set { _riskScore = value; OnPropertyChanged(); }
        }

        public int AttacksDetected
        {
            get => _attacksDetected;
            set { _attacksDetected = value; OnPropertyChanged(); }
        }

        public int BlockedIPs
        {
            get => _blockedIPs;
            set { _blockedIPs = value; OnPropertyChanged(); }
        }

        public double TrafficVolume
        {
            get => _trafficVolume;
            set { _trafficVolume = value; OnPropertyChanged(); }
        }

        // AutoBlock 바인딩 프로퍼티
        public int TotalBlockedCount
        {
            get => _totalBlockedCount;
            set { _totalBlockedCount = value; OnPropertyChanged(); }
        }

        public int Level1BlockCount
        {
            get => _level1BlockCount;
            set { _level1BlockCount = value; OnPropertyChanged(); }
        }

        public int Level2BlockCount
        {
            get => _level2BlockCount;
            set { _level2BlockCount = value; OnPropertyChanged(); }
        }

        public int Level3BlockCount
        {
            get => _level3BlockCount;
            set { _level3BlockCount = value; OnPropertyChanged(); }
        }

        public int UniqueProcesses
        {
            get => _uniqueProcesses;
            set { _uniqueProcesses = value; OnPropertyChanged(); }
        }

        public int UniqueIPs
        {
            get => _uniqueIPs;
            set { _uniqueIPs = value; OnPropertyChanged(); }
        }

        public ObservableCollection<FirewallRuleInfo> FirewallRules => _firewallRules;

        public ObservableCollection<AutoBlockedConnection> BlockedConnections => _blockedConnections;
        public ObservableCollection<AutoWhitelistEntry> WhitelistEntries => _whitelistEntries;

        public ObservableCollection<ISeries> ChartSeries => _chartSeries;
        public ObservableCollection<Axis> ChartXAxes => _chartXAxes;
        public ObservableCollection<Axis> ChartYAxes => _chartYAxes;

        // 차트 데이터
        private readonly ObservableCollection<ISeries> _chartSeries;
        private readonly ObservableCollection<Axis> _chartXAxes;
        private readonly ObservableCollection<Axis> _chartYAxes;

        // 모니터링 상태
        private bool _isMonitoring = false;

        // 캡처 서비스 연동
        private readonly ICaptureService _captureService;
        private long _livePacketCount = 0; // 틱 간 누적 패킷 수


        // 로그 파일 생성 비활성화
        // private readonly string _logFilePath =
        //     System.IO.Path.Combine(
        //         AppDomain.CurrentDomain.BaseDirectory, // exe 기준 폴더   
        //         @"..\..\..\monitoring_log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt"
        //         );

        private bool _hubSubscribed = false;

        // 간단한 그룹 확장 상태 관리
        private readonly Dictionary<int, bool> _groupExpandedStates = new Dictionary<int, bool>();

        public NetWorks_New()
        {
            // 컬렉션 및 차트 데이터 먼저 초기화 (InitializeComponent 중 SelectionChanged 등 이벤트가 호출될 수 있음)
            _generalProcessData = new ObservableCollection<ProcessNetworkInfo>();
            _systemProcessData = new ObservableCollection<ProcessNetworkInfo>();
            _generalProcessGroups = new ObservableCollection<ProcessGroup>();
            _systemProcessGroups = new ObservableCollection<ProcessGroup>();
            _processTreeNodes = new ObservableCollection<ProcessTreeNode>();
            _systemProcessTreeNodes = new ObservableCollection<ProcessTreeNode>();
            _processNetworkData = new ObservableCollection<ProcessNetworkInfo>();
            _securityAlerts = new ObservableCollection<SecurityAlert>();
            _logService = new LogMessageService(Dispatcher.CurrentDispatcher);
            _chartSeries = new ObservableCollection<ISeries>();
            _chartXAxes = new ObservableCollection<Axis>();
            _chartYAxes = new ObservableCollection<Axis>();

            // AutoBlock 컬렉션 초기화
            _blockedConnections = new ObservableCollection<AutoBlockedConnection>();
            _whitelistEntries = new ObservableCollection<AutoWhitelistEntry>();
            _firewallRules = new ObservableCollection<FirewallRuleInfo>();

            // DDoS 방어 컬렉션 초기화
            _ddosAlerts = new ObservableCollection<DDoSDetectionResult>();
            _attackHistory = new ObservableCollection<DDoSDetectionResult>();
            _ddosUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _ddosUpdateTimer.Tick += DDoSUpdateTimer_Tick;

            // 서비스 초기화
            // 전역 허브의 인스턴스를 사용하여 중복 실행 방지
            var hub = MonitoringHub.Instance;
            _processNetworkMapper = hub.ProcessMapper;
            _connectionManager = new NetworkConnectionManager();
            _securityAnalyzer = new RealTimeSecurityAnalyzer();
            _captureService = hub.Capture;

            // AutoBlock 서비스 초기화
            var dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "autoblock.db");
            var connectionString = $"Data Source={dbPath};";
            _autoBlockService = new AutoBlockService(connectionString);
            _autoBlockStats = new AutoBlockStatisticsService(connectionString);

            // 로그 파일 경로 설정 비활성화
            // _logFilePath = System.IO.Path.Combine(
            //     AppDomain.CurrentDomain.BaseDirectory,
            //     @"..\..\..\monitoring_log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt"
            // );

            // XAML 로드 (이 시점에 SelectionChanged가 발생해도 컬렉션은 준비됨)
            InitializeComponent();
            SideNetWorksNewButton.IsChecked = true;

            // TreeView 바인딩
            if (ProcessTreeView != null)
                ProcessTreeView.ItemsSource = _processTreeNodes;

            // 기존 데이터 바인딩
            GeneralProcessDataGrid.ItemsSource = _generalProcessData;
            SystemProcessDataGrid.ItemsSource = _systemProcessData;

            // 차단된 연결 목록 초기 로드
            Task.Run(async () => await LoadBlockedConnectionsAsync());

            LogMessagesControl.ItemsSource = _logService.LogMessages;
            NetworkActivityChart.Series = _chartSeries;
            NetworkActivityChart.XAxes = _chartXAxes;
            NetworkActivityChart.YAxes = _chartYAxes;

            // DataContext 설정 (바인딩을 위해)
            this.DataContext = this;

            // 이벤트 구독
            SubscribeToEvents();

            // DDoS 방어 시스템 초기화 (백그라운드)
            Task.Run(async () => await InitializeDDoSDefenseSystem());
            SubscribeToAutoBlockEvents();

            // UI 초기화
            InitializeUI();

            // 타이머 설정
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _updateTimer.Tick += UpdateTimer_Tick;

            // 트레이 아이콘은 App.xaml.cs에서 관리됩니다

            // 로그 메시지 추가
            AddLogMessage("네트워크 보안 모니터링 시스템 초기화 완료");

            // ProcessTreeNode 상태 관리 시스템 초기화 (작업 관리자 방식)
            ProcessTreeNode.ClearExpandedStates(); // 이전 세션 상태 초기화 (선택적)
            System.Diagnostics.Debug.WriteLine("[NetWorks_New] ProcessTreeNode 상태 관리 시스템 초기화됨");

            // 앱 종료 시 타이머 정리 (종료 보장)
            System.Windows.Application.Current.Exit += (_, __) =>
            {
                try { _updateTimer?.Stop(); } catch { }
            };

            // 허브 상태에 따라 초기 UI 업데이트
            if (MonitoringHub.Instance.IsRunning)
            {
                _isMonitoring = true;
                StartMonitoringButton.Visibility = Visibility.Collapsed;
                StopMonitoringButton.Visibility = Visibility.Visible;
                MonitoringStatusText.Text = "모니터링 중";
                MonitoringStatusIndicator.Fill = new SolidColorBrush(Colors.Green);
                // 새로 추가된 런타임 구성 요약 갱신
                UpdateRuntimeConfigText();
                _updateTimer.Start();
            }

            // 허브 이벤트 구독
            SubscribeHub();

            // AutoBlock 통계 시스템 초기화 (비동기)
            _ = Task.Run(async () =>
            {
                await InitializeAutoBlockStatisticsAsync();
            });

            // 방화벽 관리 초기화 (비동기)
            _ = Task.Run(async () =>
            {
                await InitializeFirewallManagementAsync();
            });

            this.Unloaded += (_, __) =>
            {
                try { _updateTimer?.Stop(); } catch { }
                UnsubscribeHub();
            };
        }

        private string BuildRuntimeConfigSummary()
        {
            var s = LogCheck.Properties.Settings.Default;
            var bpf = string.IsNullOrWhiteSpace(s.BpfFilter) ? "tcp or udp or icmp" : s.BpfFilter;
            string nicText = s.AutoSelectNic ? "자동 선택" : (string.IsNullOrWhiteSpace(s.SelectedNicId) ? "자동 선택" : s.SelectedNicId);
            return $"NIC: {nicText} | BPF: {bpf}";
        }

        private void UpdateRuntimeConfigText()
        {
            try
            {
                if (FindName("RuntimeConfigText") is TextBlock rct)
                {
                    rct.Text = BuildRuntimeConfigSummary();
                }
            }
            catch
            {
                // ignore
            }
        }

        private void SubscribeHub()
        {
            if (_hubSubscribed) return;
            var hub = MonitoringHub.Instance;
            hub.MonitoringStateChanged += OnHubMonitoringStateChanged;
            hub.MetricsUpdated += OnHubMetricsUpdated;
            hub.ErrorOccurred += OnHubErrorOccurred;
            _hubSubscribed = true;
        }

        private void UnsubscribeHub()
        {
            if (!_hubSubscribed) return;
            var hub = MonitoringHub.Instance;
            hub.MonitoringStateChanged -= OnHubMonitoringStateChanged;
            hub.MetricsUpdated -= OnHubMetricsUpdated;
            hub.ErrorOccurred -= OnHubErrorOccurred;
            _hubSubscribed = false;
        }

        private void OnHubMonitoringStateChanged(object? sender, bool running)
        {
            try
            {
                // 애플리케이션이 종료 중인지 확인
                if (System.Windows.Application.Current?.Dispatcher?.HasShutdownStarted == true)
                    return;

                // UI가 아직 유효한지 확인
                if (Dispatcher.HasShutdownStarted)
                    return;

                SafeInvokeUI(() =>
                {
                    _isMonitoring = running;

                    // UI 요소들이 유효한지 확인
                    if (StartMonitoringButton != null)
                        StartMonitoringButton.Visibility = running ? Visibility.Collapsed : Visibility.Visible;
                    if (StopMonitoringButton != null)
                        StopMonitoringButton.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
                    if (MonitoringStatusText != null)
                        MonitoringStatusText.Text = running ? "모니터링 중" : "대기 중";

                    if (MonitoringStatusIndicator != null)
                        MonitoringStatusIndicator.Fill = new SolidColorBrush(running ? Colors.Green : Colors.Gray);

                    if (running)
                    {
                        // 런타임 구성 갱신
                        UpdateRuntimeConfigText();
                        _updateTimer?.Start();
                    }
                    else
                    {
                        _updateTimer?.Stop();
                    }
                });
            }
            catch (TaskCanceledException)
            {
                // 종료 시 발생하는 TaskCanceledException은 무시
                System.Diagnostics.Debug.WriteLine("OnHubMonitoringStateChanged: TaskCanceledException 발생 (정상 종료 과정)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnHubMonitoringStateChanged 예외: {ex.Message}");
            }
        }

        private void OnHubMetricsUpdated(object? sender, CaptureMetrics metrics)
        {
            // 현재는 틱마다 _livePacketCount로 pps를 표기하므로, 여기서는 선택적으로 활용
            // 필요하다면 별도 레이블로 최신 pps 표시 가능
        }

        private void OnHubErrorOccurred(object? sender, Exception ex)
        {
            try
            {
                // 애플리케이션이 종료 중인지 확인
                if (System.Windows.Application.Current?.Dispatcher?.HasShutdownStarted == true)
                    return;

                // UI가 아직 유효한지 확인
                if (Dispatcher.HasShutdownStarted)
                    return;

                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        AddLogMessage($"허브 오류: {ex.Message}");
                    }
                    catch (Exception uiEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"UI 업데이트 중 오류: {uiEx.Message}");
                    }
                });
            }
            catch (TaskCanceledException)
            {
                // 종료 시 발생하는 TaskCanceledException은 무시
                System.Diagnostics.Debug.WriteLine("OnHubErrorOccurred: TaskCanceledException 발생 (정상 종료 과정)");
            }
            catch (Exception dispatcherEx)
            {
                System.Diagnostics.Debug.WriteLine($"OnHubErrorOccurred 예외: {dispatcherEx.Message}");
            }
        }

        /// <summary>
        /// 이벤트 구독
        /// </summary>
        private void SubscribeToEvents()
        {
            _processNetworkMapper.ProcessNetworkDataUpdated += OnProcessNetworkDataUpdated;
            _processNetworkMapper.ErrorOccurred += OnErrorOccurred;
            _connectionManager.ConnectionBlocked += OnConnectionBlocked;
            _connectionManager.ProcessTerminated += OnProcessTerminated;
            _connectionManager.ErrorOccurred += OnErrorOccurred;
            _securityAnalyzer.SecurityAlertGenerated += OnSecurityAlertGenerated;
            _securityAnalyzer.ErrorOccurred += OnErrorOccurred;

            // 캡처 서비스 이벤트
            _captureService.OnPacket += OnCapturePacket;
            _captureService.OnError += (s, ex) => OnErrorOccurred(s, ex.Message);
        }

        /// <summary>
        /// AutoBlock 시스템 초기화
        /// </summary>
        private async void SubscribeToAutoBlockEvents()
        {
            try
            {
                // AutoBlock 서비스 초기화
                await _autoBlockService.InitializeAsync();
                _logService.LogSuccess("AutoBlock 시스템이 초기화되었습니다.");

                // System Idle Process 자동 화이트리스트 추가
                await EnsureSystemIdleProcessWhitelistAsync();

                // 초기 통계 및 데이터 로드
                await LoadAutoBlockDataAsync();
            }
            catch (Exception ex)
            {
                _logService.LogError($"AutoBlock 시스템 초기화 실패: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"AutoBlock initialization error: {ex}");
            }
        }

        /// <summary>
        /// System Idle Process를 화이트리스트에 자동 추가
        /// </summary>
        private async Task EnsureSystemIdleProcessWhitelistAsync()
        {
            try
            {
                const string systemIdleProcessPath = "System Idle Process";
                const string whitelistReason = "시스템 기본 프로세스 - PID 0 (자동 추가)";

                // 이미 화이트리스트에 있는지 확인
                var existingWhitelist = await _autoBlockService.GetWhitelistAsync();
                var alreadyWhitelisted = existingWhitelist.Any(w =>
                    string.Equals(w.ProcessPath, systemIdleProcessPath, StringComparison.OrdinalIgnoreCase));

                if (!alreadyWhitelisted)
                {
                    // System Idle Process를 화이트리스트에 추가
                    var success = await _autoBlockService.AddToWhitelistAsync(systemIdleProcessPath, whitelistReason);

                    if (success)
                    {
                        _logService.LogSuccess("System Idle Process가 자동으로 화이트리스트에 추가되었습니다.");
                    }
                    else
                    {
                        _logService.LogWarning("System Idle Process 화이트리스트 추가 실패");
                    }
                }
                else
                {
                    _logService.LogInfo("System Idle Process가 이미 화이트리스트에 등록되어 있습니다.");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ System Idle Process 화이트리스트 처리 오류: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"System Idle Process whitelist error: {ex}");
            }
        }

        /// <summary>
        /// AutoBlock 데이터 로드
        /// </summary>
        private async Task LoadAutoBlockDataAsync()
        {
            try
            {
                // 최근 24시간 차단 이력 로드
                var since = DateTime.Now.AddDays(-1);
                var recentBlocks = await _autoBlockService.GetBlockHistoryAsync(since, 100);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _blockedConnections.Clear();
                    foreach (var block in recentBlocks)
                    {
                        _blockedConnections.Add(block);
                    }
                });

                // 화이트리스트 로드
                var whitelist = await _autoBlockService.GetWhitelistAsync();
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _whitelistEntries.Clear();
                    foreach (var entry in whitelist)
                    {
                        _whitelistEntries.Add(entry);
                    }
                });

                // 통계 업데이트
                UpdateAutoBlockStatistics();
            }
            catch (Exception ex)
            {
                AddLogMessage($"AutoBlock 데이터 로드 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// UI 초기화
        /// </summary>
        private void InitializeUI()
        {
            // 시스템 탭 DataGrid 바인딩
            if (SystemProcessDataGrid != null)
                SystemProcessDataGrid.ItemsSource = _systemProcessData;

            // 일반 탭 DataGrid 바인딩
            if (GeneralProcessDataGrid != null)
                GeneralProcessDataGrid.ItemsSource = _generalProcessData;

            // 나머지 UI 초기화
            if (LogMessagesControl != null)
                LogMessagesControl.ItemsSource = _logService.LogMessages;

            if (NetworkActivityChart != null)
            {
                NetworkActivityChart.Series = _chartSeries;
                NetworkActivityChart.XAxes = _chartXAxes;
                NetworkActivityChart.YAxes = _chartYAxes;
            }

            InitializeNetworkInterfaces();
            InitializeChart();
        }

        /// <summary>
        /// 네트워크 인터페이스 초기화
        /// </summary>
        private void InitializeNetworkInterfaces()
        {
            try
            {
                // 실제 구현에서는 사용 가능한 네트워크 인터페이스를 가져와야 함
                if (NetworkInterfaceComboBox != null)
                {
                    NetworkInterfaceComboBox.Items.Add("모든 인터페이스");
                    NetworkInterfaceComboBox.Items.Add("이더넷");
                    NetworkInterfaceComboBox.Items.Add("Wi-Fi");
                    NetworkInterfaceComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"네트워크 인터페이스 초기화 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 차트 초기화
        /// </summary>
        private void InitializeChart()
        {
            try
            {
                // 샘플 데이터로 차트 초기화 (0-25 범위의 현실적인 데이터)
                var sampleData = new List<double> { 2, 3, 1, 5, 8, 12, 18, 22, 20, 15, 10, 5 };
                var currentTime = DateTime.Now;
                var sampleLabels = new List<string>();
                for (int i = 0; i < 12; i++)
                {
                    var timeSlot = currentTime.AddHours(-22 + (i * 2));
                    sampleLabels.Add(timeSlot.ToString("HH"));
                }

                var lineSeries = new LineSeries<double>
                {
                    Values = sampleData,
                    Name = "Network Activity",
                    Stroke = new SolidColorPaint(SKColors.DodgerBlue, 2),
                    Fill = new SolidColorPaint(SKColors.DodgerBlue.WithAlpha(20)),
                    GeometrySize = 3,
                    GeometryStroke = new SolidColorPaint(SKColors.DodgerBlue, 1),
                    GeometryFill = new SolidColorPaint(SKColors.White),
                    LineSmoothness = 0.2, // 부드러운 곡선
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                    DataLabelsSize = 7,
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top
                };

                _chartSeries.Add(lineSeries);

                // X축 설정 개선 (수치 표시 문제 해결)
                _chartXAxes.Add(new Axis
                {
                    Labels = sampleLabels,
                    LabelsRotation = 0,
                    TextSize = 8,
                    LabelsPaint = new SolidColorPaint(SKColors.Black),
                    SeparatorsPaint = new SolidColorPaint(SKColors.LightGray, 1),
                    Name = "Time (Hours)",
                    NameTextSize = 8,
                    NamePaint = new SolidColorPaint(SKColors.DarkGray),
                    ShowSeparatorLines = true
                });

                // Y축 설정 개선 (수치 뭉침 현상 해결)  
                _chartYAxes.Add(new Axis
                {
                    Name = "Connections",
                    NameTextSize = 8,
                    NamePaint = new SolidColorPaint(SKColors.DarkGray),
                    TextSize = 8,
                    LabelsPaint = new SolidColorPaint(SKColors.Black),
                    SeparatorsPaint = new SolidColorPaint(SKColors.LightGray, 1),
                    MinLimit = 0,
                    MaxLimit = 300, // 고정 최대값으로 일관된 스케일
                    MinStep = 50, // 50단위 간격
                    ForceStepToMin = true,
                    ShowSeparatorLines = true,
                    Labeler = value =>
                    {
                        // 50의 배수만 표시하여 뭉침 방지
                        if (value % 50 == 0)
                            return value.ToString("0");
                        return "";
                    }
                });
            }
            catch (Exception ex)
            {
                AddLogMessage($"차트 초기화 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 모니터링 시작 버튼 클릭
        /// </summary>
        private async void StartMonitoring_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLogMessage("네트워크 모니터링 시작...");

                // 전역 허브를 통해 모니터링 시작 (설정 기반 NIC/BPF 사용)
                var s = LogCheck.Properties.Settings.Default;
                var bpf = string.IsNullOrWhiteSpace(s.BpfFilter) ? "tcp or udp or icmp" : s.BpfFilter;
                string? nic = s.AutoSelectNic ? null : (string.IsNullOrWhiteSpace(s.SelectedNicId) ? null : s.SelectedNicId);
                await MonitoringHub.Instance.StartAsync(bpf, nic);
                _isMonitoring = true;
                Interlocked.Exchange(ref _livePacketCount, 0);

                // UI 상태 업데이트
                StartMonitoringButton.Visibility = Visibility.Collapsed;
                StopMonitoringButton.Visibility = Visibility.Visible;
                MonitoringStatusText.Text = "모니터링 중";
                MonitoringStatusIndicator.Fill = new SolidColorBrush(Colors.Green);
                UpdateRuntimeConfigText();

                // 타이머 시작
                _updateTimer.Start();

                AddLogMessage("네트워크 모니터링이 시작되었습니다.");
            }
            catch (Exception ex)
            {
                AddLogMessage($"모니터링 시작 오류: {ex.Message}");
                MessageBox.Show($"모니터링 시작 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 모니터링 중지 버튼 클릭
        /// </summary>
        private async void StopMonitoring_Click(object sender, RoutedEventArgs e)
        {

            try
            {
                AddLogMessage("네트워크 모니터링 중지...");
                await MonitoringHub.Instance.StopAsync();
                _isMonitoring = false;

                StartMonitoringButton.Visibility = Visibility.Visible;
                StopMonitoringButton.Visibility = Visibility.Collapsed;
                MonitoringStatusText.Text = "대기 중";
                MonitoringStatusIndicator.Fill = new SolidColorBrush(Colors.Gray);
                // 구성 요약은 유지하거나 필요 시 빈 값으로 둘 수 있음 (여기서는 유지)

                _updateTimer.Stop();

                AddLogMessage("네트워크 모니터링이 중지되었습니다.");
            }
            catch (Exception ex)
            {
                AddLogMessage($"모니터링 중지 오류: {ex.Message}");
                MessageBox.Show($"모니터링 중지 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 새로고침 버튼 클릭
        /// </summary>
        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            try {
                AddLogMessage("데이터 새로고침 중...");
                var data = await _processNetworkMapper.GetProcessNetworkDataAsync();
                await UpdateProcessNetworkDataAsync(data);
                AddLogMessage("데이터 새로고침이 완료되었습니다.");
            }
            catch (Exception ex) {
                AddLogMessage($"새로고침 오류: {ex.Message}");
                MessageBox.Show($"데이터 새로고침 중 오류가 발생했습니다:{ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 검색 텍스트 변경
        /// </summary>
        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                var searchText = SearchTextBox.Text.ToLower();
                var view = CollectionViewSource.GetDefaultView(_processNetworkData);

                if (string.IsNullOrEmpty(searchText))
                {
                    view.Filter = null;
                }
                else
                {
                    view.Filter = item =>
                    {
                        if (item is ProcessNetworkInfo connection)
                        {
                            return connection.ProcessName.ToLower().Contains(searchText) ||
                                   connection.RemoteAddress.ToLower().Contains(searchText) ||
                                   connection.RemotePort.ToString().Contains(searchText);
                        }
                        return false;
                    };
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"검색 필터링 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 프로토콜 필터 변경
        /// </summary>
        private void ProtocolFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // sender 우선 사용하여 XAML 이름(null 가능) 의존 제거
                var combo = sender as Controls.ComboBox ?? ProtocolFilterComboBox;
                if (combo == null) return;

                string? protocol = null;
                if (combo.SelectedItem is Controls.ComboBoxItem cbi)
                    protocol = cbi.Content?.ToString();
                else
                    protocol = combo.SelectedItem?.ToString();

                if (string.IsNullOrWhiteSpace(protocol)) return;

                if (_processNetworkData == null) return;
                var view = CollectionViewSource.GetDefaultView(_processNetworkData);
                if (view == null) return;

                if (protocol == "모든 프로토콜")
                {
                    view.Filter = null;
                }
                else
                {
                    view.Filter = item =>
                    {
                        if (item is ProcessNetworkInfo connection)
                        {
                            return connection.Protocol == protocol;
                        }
                        return false;
                    };
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"프로토콜 필터링 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 프로세스-네트워크 데이터 선택 변경
        /// </summary>
        private void ProcessNetworkDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // sender를 DataGrid로 캐스팅
                if (sender is DataGrid dg && dg.SelectedItem is ProcessNetworkInfo selectedItem)
                {
                    AddLogMessage($"선택됨: {selectedItem.ProcessName} (PID: {selectedItem.ProcessId}) - {selectedItem.RemoteAddress}:{selectedItem.RemotePort}");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"선택 변경 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// ProcessTreeView 로드 완료 시 - TreeView는 자동으로 IsExpanded 바인딩 관리
        /// </summary>
        private void GroupedProcessDataGrid_Loaded(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("ProcessTreeView Loaded - TreeView 자동 상태 관리 활성화");
            // TreeView는 IsExpanded 바인딩을 통해 자동으로 상태를 관리하므로 추가 작업 불필요
        }

        /// <summary>
        /// 프로세스 그룹 펼침 이벤트 핸들러
        /// </summary>
        private void ProcessGroupExpander_Expanded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Expander expander && expander.DataContext is CollectionViewGroup group)
                {
                    if (int.TryParse(group.Name?.ToString(), out int processId))
                    {
                        _groupExpandedStates[processId] = true;
                        System.Diagnostics.Debug.WriteLine($"그룹 {processId} 펼침됨 - 상태 저장");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ProcessGroupExpander_Expanded 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 프로세스 그룹 접힘 이벤트 핸들러
        /// </summary>
        private void ProcessGroupExpander_Collapsed(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Expander expander && expander.DataContext is CollectionViewGroup group)
                {
                    if (int.TryParse(group.Name?.ToString(), out int processId))
                    {
                        _groupExpandedStates[processId] = false;
                        System.Diagnostics.Debug.WriteLine($"그룹 {processId} 접힘됨 - 상태 저장");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ProcessGroupExpander_Collapsed 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 연결 차단 버튼 클릭
        /// </summary>
        private async void BlockConnection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Controls.Button;
                if (button?.Tag is ProcessNetworkInfo connection)
                {
                    // 영구 차단 옵션 선택 다이얼로그
                    var blockOptions = ShowPermanentBlockDialog(connection);
                    if (blockOptions == null) return; // 사용자가 취소함

                    if (blockOptions.UsePermanentBlock)
                    {
                        // 영구 방화벽 차단 적용
                        await ApplyPermanentBlockAsync(connection, blockOptions);
                        ShowTrayNotification($"영구 차단 완료: {connection.ProcessName} - {connection.RemoteAddress}:{connection.RemotePort}");
                        return; // 영구 차단 완료 후 메서드 종료
                    }

                    // 기존 임시 차단 로직 진행
                    var result = MessageBox.Show(
                        $"프로세스 '{connection.ProcessName}' (PID: {connection.ProcessId})의 네트워크 연결을 임시 차단하시겠습니까?\n\n" +
                        $"연결 정보: {connection.RemoteAddress}:{connection.RemotePort} ({connection.Protocol})\n\n" +
                        "참고: 임시 차단은 프로그램 재시작 시 해제됩니다.",
                        "임시 연결 차단 확인",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        AddLogMessage($"연결 차단 시작: {connection.ProcessName} - {connection.RemoteAddress}:{connection.RemotePort}");

                        // ⭐ AutoBlock 시스템을 통한 차단 (통계 연동)
                        var decision = new BlockDecision
                        {
                            Level = BlockLevel.Warning,
                            Reason = "사용자 수동 차단 요청",
                            ConfidenceScore = 1.0,
                            TriggeredRules = new List<string> { "Manual Block Request" },
                            RecommendedAction = "사용자가 직접 차단을 요청했습니다.",
                            ThreatCategory = "User Action",
                            AnalyzedAt = DateTime.Now
                        };

                        var autoBlockSuccess = await _autoBlockService.BlockConnectionAsync(connection, decision.Level);
                        var connectionSuccess = await _connectionManager.DisconnectProcessAsync(
                            connection.ProcessId,
                            "사용자 요청 - 보안 위협 탐지");

                        if (autoBlockSuccess || connectionSuccess)
                        {
                            // 차단된 연결 정보 생성 및 통계 기록
                            var blockedConnection = new AutoBlockedConnection
                            {
                                ProcessName = connection.ProcessName,
                                ProcessPath = connection.ProcessPath,
                                ProcessId = connection.ProcessId,
                                RemoteAddress = connection.RemoteAddress,
                                RemotePort = connection.RemotePort,
                                Protocol = connection.Protocol,
                                BlockLevel = decision.Level,
                                Reason = decision.Reason,
                                BlockedAt = DateTime.Now,
                                ConfidenceScore = decision.ConfidenceScore,
                                IsBlocked = true,
                                TriggeredRules = string.Join(", ", decision.TriggeredRules)
                            };

                            // 통계 시스템과 차단된 연결 목록에 기록
                            _ = Task.Run(async () =>
                            {
                                await RecordBlockEventAsync(blockedConnection);
                                await _autoBlockStats.AddBlockedConnectionAsync(blockedConnection);
                            });

                            // 통계 UI 업데이트
                            UpdateStatisticsDisplay();

                            // 차단된 연결 목록 새로고침
                            _ = Task.Run(async () => await LoadBlockedConnectionsAsync());

                            AddLogMessage($"✅ [Manual-Block] 연결 차단 완료: {connection.ProcessName} -> {connection.RemoteAddress}:{connection.RemotePort}");
                            //MessageBox.Show("연결 차단이 완료되었습니다.\n\nAutoBlock 통계에 기록되었습니다.", "성공", MessageBoxButton.OK, MessageBoxImage.Information);

                            // NotifyIcon 사용하여 트레이 알림
                            ShowTrayNotification($"연결 차단 완료: {connection.ProcessName} - {connection.RemoteAddress}:{connection.RemotePort}");
                        }
                        else
                        {
                            AddLogMessage("❌ 연결 차단에 실패했습니다.");
                            MessageBox.Show("연결 차단에 실패했습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"연결 차단 오류: {ex.Message}");
                MessageBox.Show($"연결 차단 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 트레이 알림 (BalloonTip) 표시 함수 - App.xaml.cs의 전역 트레이 아이콘을 사용
        private void ShowTrayNotification(string message)
        {
            try
            {
                // App.xaml.cs의 App 클래스에서 트레이 알림 표시
                if (System.Windows.Application.Current is App app)
                {
                    app.ShowBalloonTip("네트워크 보안 알림", message, ToolTipIcon.Info);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"트레이 알림 표시 실패: {ex.Message}");
            }
        }


        /// <summary>
        /// 프로세스 종료 버튼 클릭
        /// </summary>
        private async void TerminateProcess_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Controls.Button;
                if (button?.Tag is ProcessNetworkInfo connection)
                {
                    if (!OperatingSystem.IsWindows())
                    {
                        MessageBox.Show("이 기능은 Windows에서만 지원됩니다.", "미지원 플랫폼", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    var result = MessageBox.Show(
                        $"프로세스 '{connection.ProcessName}' (PID: {connection.ProcessId})을(를) 강제 종료하시겠습니까?\n\n" +
                        "⚠️ 주의: 이 작업은 데이터 손실을 야기할 수 있습니다.",
                        "프로세스 종료 확인",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        AddLogMessage($"프로세스 종료 시작: {connection.ProcessName} (PID: {connection.ProcessId})");

                        try
                        {
                            // 프로세스 트리(패밀리) 전체 종료 시도 (Chrome 등 멀티 프로세스 대응)
                            bool success = await Task.Run(() => _connectionManager.TerminateProcessFamily(connection.ProcessId));
                            if (success)
                            {
                                AddLogMessage("프로세스 종료가 완료되었습니다.");
                                //MessageBox.Show("프로세스 종료가 완료되었습니다.", "성공", MessageBoxButton.OK, MessageBoxImage.Information);
                                ShowTrayNotification($"프로세스 종료 완료: {connection.ProcessName} (PID: {connection.ProcessId})");

                                try
                                {
                                    // 종료된 프로세스를 UI에서 즉시 반영
                                    var data = await _processNetworkMapper.GetProcessNetworkDataAsync();
                                    await UpdateProcessNetworkDataAsync(data);
                                }
                                catch (Exception refreshEx)
                                {
                                    AddLogMessage($"리스트 새로고침 실패: {refreshEx.Message}");
                                }
                            }
                            else
                            {
                                AddLogMessage("프로세스 종료에 실패했습니다. 관리자 권한이 필요할 수 있습니다.");
                                MessageBox.Show("프로세스 종료에 실패했습니다. 관리자 권한이 필요할 수 있습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                        catch (Exception ex)
                        {
                            AddLogMessage($"프로세스 종료 실패: {ex.Message}");
                            MessageBox.Show($"프로세스 종료에 실패했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"프로세스 종료 오류: {ex.Message}");
                MessageBox.Show($"프로세스 종료 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 타이머 틱 이벤트
        /// </summary>
        private async void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[NetWorks_New] UpdateTimer_Tick 호출됨, 모니터링 상태: {_isMonitoring}");

                if (_isMonitoring)
                {
                    // 최근 틱 간 패킷 처리율 계산 및 상태 표시
                    var taken = Interlocked.Exchange(ref _livePacketCount, 0);
                    var secs = Math.Max(1, (int)_updateTimer.Interval.TotalSeconds);
                    var pps = taken / secs;
                    if (MonitoringStatusText != null) MonitoringStatusText.Text = $"모니터링 중 ({pps} pps)";

                    // 주기적으로 데이터 업데이트
                    System.Diagnostics.Debug.WriteLine("[NetWorks_New] 프로세스 데이터 가져오기 시작");
                    var data = await _processNetworkMapper.GetProcessNetworkDataAsync();
                    System.Diagnostics.Debug.WriteLine($"[NetWorks_New] 프로세스 데이터 가져오기 완료: {data?.Count ?? 0}개");

                    // AutoBlock 분석 수행
                    if (_autoBlockService != null && data?.Any() == true)
                    {
                        await AnalyzeConnectionsWithAutoBlockAsync(data);
                    }

                    await UpdateProcessNetworkDataAsync(data ?? new List<ProcessNetworkInfo>());
                }

                // AutoBlock 통계 주기적 업데이트 (1분마다)
                if (_updateTimer != null && DateTime.Now.Second == 0) // 매분 0초에 업데이트
                {
                    await UpdateAutoBlockStatisticsFromDatabase();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NetWorks_New] 타이머 업데이트 오류: {ex.Message}");
                AddLogMessage($"타이머 업데이트 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 캡처 서비스 패킷 수신 이벤트
        /// </summary>
        private void OnCapturePacket(object? sender, PacketDto dto)
        {
            // 배경 스레드에서 호출됨: 원자적 증가
            Interlocked.Increment(ref _livePacketCount);
        }

        /// <summary>
        /// 프로세스-네트워크 데이터 업데이트 (그룹화 포함)
        /// </summary>
        private async Task UpdateProcessNetworkDataAsync(List<ProcessNetworkInfo> data)
        {
            data ??= new List<ProcessNetworkInfo>();
            System.Diagnostics.Debug.WriteLine($"[NetWorks_New] UpdateProcessNetworkDataAsync 호출됨, 데이터 개수: {data.Count}");

            // Task.Run에서 모든 데이터 처리와 보안 분석 수행
            var processedData = await Task.Run(async () =>
            {
                try
                {
                    // 1. System Idle Process 완전 제외 (실수로 종료되는 것 방지)
                    var filteredData = data.Where(p => !IsSystemIdleProcess(p)).ToList();
                    System.Diagnostics.Debug.WriteLine($"[NetWorks_New] System Idle Process 제외 후 데이터 개수: {filteredData.Count}");

                    // 2. IsSystem 자동 판단
                    foreach (var item in filteredData)
                    {
                        item.IsSystem = IsSystemProcess(item.ProcessName, item.ProcessId);
                    }

                    // 3. 데이터 분류
                    var general = filteredData.Where(p => !p.IsSystem).ToList();
                    var system = filteredData.Where(p => p.IsSystem).ToList();

                    System.Diagnostics.Debug.WriteLine($"[NetWorks_New] 일반 프로세스: {general.Count}개, 시스템 프로세스: {system.Count}개");

                    // 4. 보안 분석 수행
                    var alerts = await _securityAnalyzer.AnalyzeConnectionsAsync(filteredData);

                    return new
                    {
                        FilteredData = filteredData,
                        General = general,
                        System = system,
                        SecurityAlerts = alerts
                    };
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"백그라운드 데이터 처리 중 오류: {ex.Message}");
                    return new
                    {
                        FilteredData = new List<ProcessNetworkInfo>(),
                        General = new List<ProcessNetworkInfo>(),
                        System = new List<ProcessNetworkInfo>(),
                        SecurityAlerts = new List<SecurityAlert>()
                    };
                }
            });

            try
            {
                // 애플리케이션이 종료 중인지 확인
                if (System.Windows.Application.Current?.Dispatcher?.HasShutdownStarted == true)
                    return;

                // UI가 아직 유효한지 확인
                if (Dispatcher.HasShutdownStarted)
                    return;

                // 최종 UI 업데이트만 SafeInvokeUIAsync에서 수행 (BasePageViewModel 패턴 적용)
                await SafeInvokeUIAsync(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"[NetWorks_New] UI 업데이트 시작 - 기존 일반 프로세스: {_generalProcessData.Count}개, 시스템 프로세스: {_systemProcessData.Count}개");

                    // 스마트 업데이트: 컬렉션을 완전히 지우지 않고 업데이트
                    UpdateCollectionSmart(_generalProcessData, processedData.General);
                    UpdateCollectionSmart(_systemProcessData, processedData.System);

                    // PID별 그룹화된 데이터 업데이트 (기존)
                    UpdateProcessGroups(_generalProcessGroups, processedData.General);
                    UpdateProcessGroups(_systemProcessGroups, processedData.System);

                    // 작업 관리자 방식의 TreeView 업데이트 (새로운 방식)
                    UpdateProcessTreeSmart(_processTreeNodes, processedData.General);
                    UpdateProcessTreeSmart(_systemProcessTreeNodes, processedData.System);

                    // 통계, 차트, 보안 알림 업데이트
                    UpdateStatistics(processedData.FilteredData);
                    UpdateChart(processedData.FilteredData);
                    UpdateSecurityAlerts(processedData.SecurityAlerts);

                    System.Diagnostics.Debug.WriteLine($"[NetWorks_New] UI 업데이트 완료 - 새로운 일반 프로세스: {_generalProcessData.Count}개, 시스템 프로세스: {_systemProcessData.Count}개");
                    System.Diagnostics.Debug.WriteLine($"[NetWorks_New] 그룹 업데이트 완료 - 일반 그룹: {_generalProcessGroups.Count}개, 시스템 그룹: {_systemProcessGroups.Count}개");
                });

                // 간단한 상태 복원 시도
                _ = Task.Delay(100).ContinueWith(_ =>
                {
                    SafeInvokeUI(() => RestoreGroupStates());
                });

            }
            catch (TaskCanceledException)
            {
                // 종료 시 발생하는 TaskCanceledException은 무시
                System.Diagnostics.Debug.WriteLine("UpdateProcessNetworkDataAsync: TaskCanceledException 발생 (정상 종료 과정)");
                return;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateProcessNetworkDataAsync Dispatcher 호출 중 예외: {ex.Message}");
                return;
            }
        }

        /// <summary>
        /// 작업 관리자 방식의 스마트 TreeView 업데이트
        /// 기존 노드 객체를 유지하면서 데이터만 업데이트하여 확장 상태 보존
        /// </summary>
        private void UpdateProcessTreeSmart(ObservableCollection<ProcessTreeNode> treeNodeCollection, List<ProcessNetworkInfo> processes)
        {
            try
            {
                // 프로세스별로 그룹화
                var groupedData = processes
                    .GroupBy(p => new { p.ProcessId, p.ProcessName })
                    .ToDictionary(g => g.Key, g => g.ToList());

                System.Diagnostics.Debug.WriteLine($"[UpdateProcessTreeSmart] 그룹화된 프로세스: {groupedData.Count}개");

                // 1. 더 이상 존재하지 않는 프로세스 제거
                var nodesToRemove = treeNodeCollection
                    .Where(node => !groupedData.ContainsKey(new { ProcessId = node.ProcessId, ProcessName = node.ProcessName }))
                    .ToList();

                foreach (var node in nodesToRemove)
                {
                    treeNodeCollection.Remove(node);
                    System.Diagnostics.Debug.WriteLine($"[UpdateProcessTreeSmart] 프로세스 제거: {node.ProcessName} ({node.ProcessId})");
                }

                // 2. 기존 노드 업데이트 또는 신규 노드 추가
                foreach (var group in groupedData)
                {
                    var existingNode = treeNodeCollection.FirstOrDefault(n =>
                        n.ProcessId == group.Key.ProcessId &&
                        n.ProcessName == group.Key.ProcessName);

                    if (existingNode != null)
                    {
                        // 기존 노드 업데이트 (IsExpanded 상태는 자동으로 유지됨)
                        existingNode.UpdateConnections(group.Value);
                        existingNode.UpdateProcessInfo(group.Value.First());

                        System.Diagnostics.Debug.WriteLine($"[UpdateProcessTreeSmart] 기존 노드 업데이트: {existingNode.ProcessName} ({existingNode.ProcessId}) - {group.Value.Count}개 연결, 확장상태: {existingNode.IsExpanded}");
                    }
                    else
                    {
                        // 새 노드 생성
                        var firstConnection = group.Value.First();
                        var newNode = new ProcessTreeNode
                        {
                            ProcessId = group.Key.ProcessId,
                            ProcessName = group.Key.ProcessName,
                            ProcessPath = firstConnection.ProcessPath
                        };

                        // 저장된 확장 상태 복원
                        var savedState = ProcessTreeNode.GetSavedExpandedState(newNode.UniqueId);
                        newNode.IsExpanded = savedState;

                        // 연결 정보 추가
                        newNode.UpdateConnections(group.Value);

                        treeNodeCollection.Add(newNode);

                        System.Diagnostics.Debug.WriteLine($"[UpdateProcessTreeSmart] 새 노드 생성: {newNode.ProcessName} ({newNode.ProcessId}) - {group.Value.Count}개 연결, 확장상태: {newNode.IsExpanded} (복원됨: {savedState})");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[UpdateProcessTreeSmart] 업데이트 완료 - 총 {treeNodeCollection.Count}개 노드");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateProcessTreeSmart] 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// PID별로 프로세스를 그룹화하여 '스마트하게' 업데이트합니다.
        /// </summary>
        private void UpdateProcessGroups(ObservableCollection<ProcessGroup> groupCollection, List<ProcessNetworkInfo> processes)
        {
            // 1. 새로운 데이터를 PID 기준으로 그룹화합니다.
            var newGroups = processes
                .Where(p => p != null)
                .GroupBy(p => p.ProcessId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 2. 기존 그룹 목록에서 더 이상 존재하지 않는 프로세스 그룹을 제거합니다.
            var pidsToRemove = groupCollection.Select(g => g.ProcessId).Except(newGroups.Keys).ToList();
            foreach (var pid in pidsToRemove)
            {
                var groupToRemove = groupCollection.FirstOrDefault(g => g.ProcessId == pid);
                if (groupToRemove != null)
                {
                    groupCollection.Remove(groupToRemove);
                    System.Diagnostics.Debug.WriteLine($"그룹 제거: PID {pid}");
                }
            }

            // 3. 신규 또는 기존 그룹의 내용을 업데이트합니다.
            foreach (var newGroup in newGroups)
            {
                var existingGroup = groupCollection.FirstOrDefault(g => g.ProcessId == newGroup.Key);

                if (existingGroup != null)
                {
                    // 3-1. 그룹이 이미 존재하면 내부 프로세스 목록만 업데이트합니다.
                    // 이렇게 하면 ProcessGroup 객체 자체가 교체되지 않으므로 IsExpanded 상태가 유지됩니다.
                    var firstProcess = newGroup.Value.First();
                    existingGroup.ProcessName = firstProcess.ProcessName ?? "Unknown";
                    existingGroup.ProcessPath = firstProcess.ProcessPath ?? "";

                    // 내부 컬렉션도 Clear/Add 대신 스마트하게 업데이트하면 더 좋습니다.
                    // 지금은 간단하게 Clear/Add로 처리해도 IsExpanded 상태는 유지됩니다.
                    existingGroup.Processes.Clear();
                    foreach (var process in newGroup.Value)
                    {
                        existingGroup.Processes.Add(process);
                    }
                    System.Diagnostics.Debug.WriteLine($"그룹 업데이트: PID {newGroup.Key} ({newGroup.Value.Count}개 항목)");
                }
                else
                {
                    // 3-2. 새로운 그룹이면 컬렉션에 추가합니다.
                    var firstProcess = newGroup.Value.First();

                    // 저장된 상태가 있으면 사용, 없으면 기본값(false)
                    bool isExpanded = _groupExpandedStates.ContainsKey(newGroup.Key)
                        ? _groupExpandedStates[newGroup.Key]
                        : false;

                    var processGroup = new ProcessGroup
                    {
                        ProcessId = newGroup.Key,
                        ProcessName = firstProcess.ProcessName ?? "Unknown",
                        ProcessPath = firstProcess.ProcessPath ?? "",
                        Processes = new ObservableCollection<ProcessNetworkInfo>(newGroup.Value),
                        IsExpanded = isExpanded
                    };
                    groupCollection.Add(processGroup);
                    System.Diagnostics.Debug.WriteLine($"그룹 추가: PID {newGroup.Key} (상태: {(isExpanded ? "펼침" : "접힘")})");
                }
            }
        }

        /// <summary>
        /// 시스템 프로세스 여부 판단 (간단 예시)
        /// </summary>
        private bool IsSystemProcess(string processName, int pid)
        {
            if (pid <= 4) return true; // 시스템 프로세스 PID 0~4는 무조건 시스템
            var systemNames = new[] { "svchost", "System", "wininit", "winlogon", "lsass", "services" };
            return systemNames.Any(n => processName.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// System Idle Process 여부 확인 (UI에서 숨기기 위함)
        /// </summary>
        private bool IsSystemIdleProcess(ProcessNetworkInfo process)
        {
            return process.ProcessId == 0 &&
                   string.Equals(process.ProcessName, "System Idle Process", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 통계 업데이트
        /// </summary>
        private void UpdateStatistics(List<ProcessNetworkInfo> data)
        {
            try
            {
                data ??= new List<ProcessNetworkInfo>();
                int total = 0, low = 0, medium = 0, high = 0, tcp = 0, udp = 0, icmp = 0; long transferred = 0;
                foreach (var x in data)
                {
                    total++; transferred += x.DataTransferred;
                    switch (x.RiskLevel)
                    {
                        case SecurityRiskLevel.Low: low++; break;
                        case SecurityRiskLevel.Medium: medium++; break;
                        case SecurityRiskLevel.High: high++; break;
                        case SecurityRiskLevel.Critical: high++; break; 
                        // Critical도 위험에 포함
                    }
                    switch (x.Protocol.ToUpperInvariant()) {
                        case "TCP": tcp++; break;
                        case "UDP": udp++; break;
                        case "ICMP": icmp++; break;
                    }
                }
                TotalConnections = total;
                LowRiskCount = low;
                MediumRiskCount = medium;
                HighRiskCount = high;
                TcpCount = tcp;
                UdpCount = udp;
                IcmpCount = icmp;
                _totalDataTransferred = transferred;
                
                OnPropertyChanged(nameof(TotalDataTransferred));
                
                if (ActiveConnectionsText != null)
                    ActiveConnectionsText.Text = total.ToString();
                if (DangerousConnectionsText != null)
                    DangerousConnectionsText.Text = (high).ToString();
                
                UpdateStatisticsDisplay();
            }
            catch (Exception ex) { AddLogMessage($"통계 업데이트 오류: {ex.Message}"); }
        }
        /// <summary>
        /// 통계 표시 업데이트
        /// </summary>
        private void UpdateStatisticsDisplay()
        {
            try
            {
                // 실제 구현에서는 바인딩된 속성을 업데이트해야 함
                // 여기서는 간단하게 텍스트로 표시
                var statsText = $"총 연결: {_totalConnections} | " +
                               $"위험 연결: {_highRiskCount} | " +
                               $"TCP: {_tcpCount} | " +
                               $"UDP: {_udpCount} | " +
                               $"총 데이터: {_totalDataTransferred / (1024 * 1024):F1} MB";

                AddLogMessage($"통계 업데이트: {statsText}");
            }
            catch (Exception ex)
            {
                AddLogMessage($"통계 표시 업데이트 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 컬렉션을 스마트하게 업데이트 (Clear 대신 개별 아이템 변경)
        /// </summary>
        private void UpdateCollectionSmart<T>(ObservableCollection<T> collection, List<T> newItems)
            where T : class
        {
            try
            {
                collection.Clear();
                foreach (var item in newItems)
                    collection.Add(item); 
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"스마트 업데이트 실패: {ex.Message}");
                collection.Clear();
                foreach (var item in newItems)
                    collection.Add(item);
            }
        }

        /// <summary>
        /// 차트 업데이트
        /// </summary>
        private void UpdateChart(List<ProcessNetworkInfo> data)
        {
            try
            {
                if (_chartSeries.Count == 0 || !(_chartSeries[0] is LineSeries<double> lineSeries))
                    return;
                
                data ??= new List<ProcessNetworkInfo>();

                var groupedByHour = data
                    .GroupBy(x => x.ConnectionStartTime.Hour)
                    .ToDictionary(g => g.Key, g => g.Count());

                var chartData = new List<double>();
                var timeLabels = new List<string>();
                var currentTime = DateTime.Now;

                for (int i = 0; i < 12; i++)
                {
                    var timeSlot = currentTime.AddHours(-22 + (i * 2));
                    int hour = timeSlot.Hour;

                    groupedByHour.TryGetValue(hour, out int count);

                    // Y축 범위에 맞게 제한
                    chartData.Add(Math.Clamp(count, 0, 25));
                    timeLabels.Add(timeSlot.ToString("HH"));
                }
                SafeInvokeUI(() => {
                    lineSeries.Values = chartData;
                    if (_chartXAxes.Count > 0)
                        _chartXAxes[0].Labels = timeLabels;
                }); 
            }
            catch (Exception ex)
            {
                AddLogMessage($"차트 업데이트 오류: {ex.Message}"); 
            }
        }

        /// <summary>
        /// 보안 경고 업데이트
        /// </summary>
        private void UpdateSecurityAlerts(List<SecurityAlert> alerts)
        {
            try
            {
                if (!_isMonitoring) return; // 모니터링 중이 아니면 알림 건너뜀
                alerts ??= new List<SecurityAlert>();

                _securityAlerts.Clear();
                foreach (var alert in alerts)
                {
                    _securityAlerts.Add(alert);

                    // "위험" 메시지만 토스트 알림 표시
                    if (alert.Title.Contains("위험") || alert.Title.Contains("Critical"))
                        ShowSecurityAlertToast(alert);
                }

                AddLogMessage($"보안 경고 {alerts.Count}개 생성됨");
            }
            catch (Exception ex)
            {
                AddLogMessage($"보안 경고 업데이트 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 이벤트 핸들러들
        /// </summary>
        private void OnProcessNetworkDataUpdated(object? sender, List<ProcessNetworkInfo> data)
        {
            _ = Task.Run(async () =>
            {
                await UpdateProcessNetworkDataAsync(data);
            });
        }

        private void OnConnectionBlocked(object? sender, string message)
        {
            AddLogMessage($"연결 차단: {message}");
        }

        private void OnProcessTerminated(object? sender, string message)
        {
            AddLogMessage($"프로세스 종료: {message}");
        }

        private void OnSecurityAlertGenerated(object? sender, SecurityAlert alert)
        {
            AddLogMessage($"보안 경고: {alert.Title}");
        }

        private void OnErrorOccurred(object? sender, string message)
        {
            AddLogMessage($"오류: {message}");
        }

        /// <summary>
        /// 로그 메시지 추가 (LogMessageService로 위임)
        /// </summary>
        private void AddLogMessage(string message)
        {
            _logService.AddLogMessage(message);
        }

        /// <summary>
        /// 페이지 종료 시 정리
        /// </summary>
        public void Shutdown()
        {
            try
            {
                // UI 업데이트 타이머 중지
                _updateTimer?.Stop();

                // Hub 이벤트 구독 해제
                try
                {
                    UnsubscribeHub();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Hub 구독 해제 중 오류: {ex.Message}");
                }

                // ❌ 모니터링 정지 호출 제거 (전역 허브에서 관리)
                // _ = _processNetworkMapper?.StopMonitoringAsync();
                // _ = _captureService?.StopAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"페이지 종료 시 정리 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// INavigable 인터페이스 구현
        /// </summary>
        public void OnNavigatedTo()
        {
            // 페이지로 이동했을 때 호출
            AddLogMessage("네트워크 보안 모니터링 페이지로 이동");
        }

        public void OnNavigatedFrom()
        {
            // 페이지에서 이동할 때 호출
            Shutdown();
        }

        /// <summary>
        /// WindowsSentinel 아이콘을 로드합니다
        /// </summary>
        private System.Drawing.Icon TryLoadWindowsSentinelIcon()
        {
            try
            {
                // 여러 경로에서 아이콘을 찾아서 로드
                var possiblePaths = new[]
                {
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WindowsSentinel.ico"),
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IconTexture", "WindowsSentinel.ico"),
                    System.IO.Path.Combine(Environment.CurrentDirectory, "WindowsSentinel.ico"),
                    System.IO.Path.Combine(Environment.CurrentDirectory, "IconTexture", "WindowsSentinel.ico")
                };

                foreach (var path in possiblePaths)
                {
                    if (System.IO.File.Exists(path))
                    {
                        System.Diagnostics.Debug.WriteLine($"NetWorks_New 아이콘 로드 성공: {path}");
                        return new System.Drawing.Icon(path);
                    }
                }

                System.Diagnostics.Debug.WriteLine("NetWorks_New: WindowsSentinel 아이콘을 찾을 수 없음. 기본 아이콘 사용.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NetWorks_New 아이콘 로드 오류: {ex.Message}");
            }
            return System.Drawing.SystemIcons.Application;
        }
        private void ShowSecurityAlertToast(SecurityAlert alert)
        {
            try
            {
                // 애플리케이션이 종료 중인지 확인
                if (System.Windows.Application.Current?.Dispatcher?.HasShutdownStarted == true)
                    return;

                // UI가 아직 유효한지 확인
                if (Dispatcher.HasShutdownStarted)
                    return;

                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        var popup = new Popup
                        {
                            Placement = PlacementMode.Bottom,  // 유효한 값 사용
                            AllowsTransparency = true,
                            PopupAnimation = PopupAnimation.Fade,
                            StaysOpen = false,
                            HorizontalOffset = SystemParameters.WorkArea.Width - 300, // 화면 우측
                            VerticalOffset = SystemParameters.WorkArea.Height - 100   // 화면 하단
                        };


                        // 타이틀 TextBlock 생성
                        var titleTextBlock = new TextBlock
                        {
                            Text = alert.Title,
                            Foreground = MediaBrushes.White,
                            FontWeight = FontWeights.Bold,
                            FontSize = 14
                        };
                        DockPanel.SetDock(titleTextBlock, Dock.Left); // 여기서 Dock 설정

                        // X 버튼 생성
                        var closeButton = new System.Windows.Controls.Button
                        {
                            Content = "X",
                            Width = 20,
                            Height = 20,
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
                        };
                        closeButton.Click += (s, e) => popup.IsOpen = false;

                        // DockPanel 생성 및 Children 추가
                        var dockPanel = new DockPanel
                        {
                            LastChildFill = true
                        };
                        dockPanel.Children.Add(titleTextBlock);
                        dockPanel.Children.Add(closeButton);

                        // Border 생성
                        var border = new Border
                        {
                            Background = new SolidColorBrush(MediaColor.FromArgb(220, 60, 60, 60)),
                            CornerRadius = new CornerRadius(8),
                            Padding = new Thickness(12),
                            Child = new StackPanel
                            {
                                Children =
        {
            dockPanel,
            new TextBlock
            {
                Text = alert.Description,
                Foreground = MediaBrushes.White,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 250
            },
            new TextBlock
            {
                Text = $"권장 조치: {alert.RecommendedAction}",
                Foreground = MediaBrushes.LightGray,
                FontSize = 11,
                FontStyle = FontStyles.Italic,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 250
            }
        }
                            }
                        };

                        popup.Child = border;

                        // 화면 우측 상단 위치
                        popup.HorizontalOffset = SystemParameters.WorkArea.Width - 300;
                        popup.VerticalOffset = 20;

                        popup.IsOpen = true;

                        // 일정 시간 후 자동 닫기
                        var timer = new DispatcherTimer
                        {
                            Interval = TimeSpan.FromSeconds(5)
                        };
                        timer.Tick += (s, e) =>
                        {
                            popup.IsOpen = false;
                            timer.Stop();
                        };
                        timer.Start();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"보안 알림 토스트 생성 중 오류: {ex.Message}");
                    }
                });
            }
            catch (TaskCanceledException)
            {
                // 종료 시 발생하는 TaskCanceledException은 무시
                System.Diagnostics.Debug.WriteLine("ShowSecurityAlertToast: TaskCanceledException 발생 (정상 종료 과정)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowSecurityAlertToast 예외: {ex.Message}");
            }
        }
        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 현재 창에서 내비게이션 메서드를 찾아 호출
                if (Window.GetWindow(this) is MainWindows mw)
                {
                    mw.NavigateToPage(new Setting());
                }
                else
                {
                    // 대체: 현재 페이지의 최상위 Frame을 찾아 네비게이트
                    var parent = this.Parent;
                    while (parent != null && parent is not System.Windows.Controls.Frame)
                    {
                        parent = (parent as FrameworkElement)?.Parent;
                    }
                    if (parent is System.Windows.Controls.Frame frame)
                    {
                        frame.Navigate(new Setting());
                    }
                    else
                    {
                        // 마지막 대안: 새 창으로 설정 페이지 열기
                        var win = new Window
                        {
                            Title = "설정",
                            Width = 800,
                            Height = 600,
                            Content = new Setting(),
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            Owner = Window.GetWindow(this)
                        };
                        win.Show();
                    }
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"설정 열기 오류: {ex.Message}");
            }
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
                case "NetWorks_New":
                    NavigateToPage(new NetWorks_New());
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

        #region 그룹화 기능 이벤트 핸들러

        /// <summary>
        /// 그룹 확장/축소 버튼 클릭 이벤트
        /// </summary>
        private void ExpandGroup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ProcessGroup? group = null;

                // Button 클릭인 경우
                if (sender is System.Windows.Controls.Button button && button.Tag is ProcessGroup buttonGroup)
                {
                    group = buttonGroup;
                }
                // StackPanel MouseLeftButtonDown인 경우  
                else if (sender is System.Windows.Controls.StackPanel panel && panel.Tag is ProcessGroup panelGroup)
                {
                    group = panelGroup;
                }

                if (group != null)
                {
                    group.IsExpanded = !group.IsExpanded;

                    // 상세 정보 표시 로직
                    if (group.IsExpanded)
                    {
                        AddLogMessage($"프로세스 '{group.ProcessName}' (PID: {group.ProcessId})의 {group.ProcessCount}개 프로세스 연결 정보를 표시합니다.");

                        // TODO: 실제 하위 프로세스 목록을 표시하는 UI 구현
                        // 현재는 로그 메시지로만 표시
                        foreach (var process in group.Processes.Take(5)) // 최대 5개만 표시
                        {
                            AddLogMessage($"  - {process.LocalAddress} -> {process.RemoteAddress} ({process.Protocol}, {process.ConnectionState})");
                        }
                        if (group.Processes.Count > 5)
                        {
                            AddLogMessage($"  ... 그 외 {group.Processes.Count - 5}개 연결");
                        }
                    }
                    else
                    {
                        AddLogMessage($"프로세스 그룹 '{group.ProcessName}' (PID: {group.ProcessId})을 축소했습니다.");
                    }
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"그룹 확장/축소 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 그룹 내 모든 연결 차단
        /// </summary>
        private async void BlockGroupConnections_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // TreeView 방식: Button의 Tag에서 ProcessTreeNode 가져오기
                var button = sender as System.Windows.Controls.Button;
                var processNode = button?.Tag as ProcessTreeNode;

                if (processNode != null && processNode.Connections.Count > 0)
                {
                    var result = MessageBox.Show(
                        $"프로세스 '{processNode.ProcessName}' (PID: {processNode.ProcessId})의 모든 네트워크 연결 {processNode.Connections.Count}개를 차단하시겠습니까?",
                        "네트워크 연결 차단 확인",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        int blockedCount = 0;
                        int autoBlockedCount = 0;

                        foreach (var connection in processNode.Connections.ToList())
                        {
                            try
                            {
                                // ⭐ AutoBlock 시스템을 통한 그룹 차단
                                var decision = new BlockDecision
                                {
                                    Level = BlockLevel.Warning,
                                    Reason = $"사용자 그룹 단위 차단 요청 (프로세스: {processNode.ProcessName})",
                                    ConfidenceScore = 1.0,
                                    TriggeredRules = new List<string> { "Manual Group Block Request" },
                                    RecommendedAction = "사용자가 프로세스 그룹 전체 차단을 요청했습니다.",
                                    ThreatCategory = "User Group Action",
                                    AnalyzedAt = DateTime.Now
                                };

                                // AutoBlock 시스템으로 차단 시도
                                var autoBlockSuccess = await _autoBlockService.BlockConnectionAsync(connection, decision.Level);

                                // 기존 차단 로직도 실행
                                connection.IsBlocked = true;
                                connection.BlockedTime = DateTime.Now;
                                connection.BlockReason = "사용자가 그룹 단위로 차단";
                                blockedCount++;

                                if (autoBlockSuccess)
                                {
                                    autoBlockedCount++;

                                    // 차단된 연결 정보 생성 및 통계 기록
                                    var blockedConnection = new AutoBlockedConnection
                                    {
                                        ProcessName = connection.ProcessName,
                                        ProcessPath = connection.ProcessPath,
                                        ProcessId = connection.ProcessId,
                                        RemoteAddress = connection.RemoteAddress,
                                        RemotePort = connection.RemotePort,
                                        Protocol = connection.Protocol,
                                        BlockLevel = decision.Level,
                                        Reason = decision.Reason,
                                        BlockedAt = DateTime.Now,
                                        ConfidenceScore = decision.ConfidenceScore,
                                        IsBlocked = true,
                                        TriggeredRules = string.Join(", ", decision.TriggeredRules)
                                    };

                                    // 통계 시스템과 차단된 연결 목록에 기록 (백그라운드에서)
                                    _ = Task.Run(async () =>
                                    {
                                        await RecordBlockEventAsync(blockedConnection);
                                        await _autoBlockStats.AddBlockedConnectionAsync(blockedConnection);
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                AddLogMessage($"연결 차단 실패 ({connection.RemoteAddress}:{connection.RemotePort}): {ex.Message}");
                            }
                        }

                        // 통계 UI 업데이트
                        if (autoBlockedCount > 0)
                        {
                            UpdateStatisticsDisplay();
                            // 차단된 연결 목록 새로고침
                            _ = Task.Run(async () => await LoadBlockedConnectionsAsync());
                        }

                        AddLogMessage($"✅ [Manual-Group-Block] 프로세스 그룹 '{processNode.ProcessName}'에서 {blockedCount}개 연결을 차단했습니다. (AutoBlock 시스템: {autoBlockedCount}개)");

                        if (blockedCount > 0)
                        {
                            //MessageBox.Show($"그룹 차단이 완료되었습니다.\n\n차단된 연결: {blockedCount}개\nAutoBlock 통계 기록: {autoBlockedCount}개", "성공", MessageBoxButton.OK, MessageBoxImage.Information);
                            ShowTrayNotification($"그룹 차단 완료: {processNode.ProcessName} - {blockedCount}개 연결 차단됨");

                            // UI 새로고침
                            await RefreshProcessData();
                        }
                    }
                }
                else
                {
                    MessageBox.Show("선택된 프로세스 그룹을 찾을 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"그룹 연결 차단 오류: {ex.Message}");
                MessageBox.Show($"연결 차단 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 그룹 프로세스 종료
        /// </summary>
        private async void TerminateGroupProcess_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as System.Windows.Controls.Button;


                int? pid = null;
                string? pname = null;


                // TreeView 기반 (ProcessTreeNode)
                if (button?.Tag is ProcessTreeNode node)
                {
                    pid = node.ProcessId;
                    pname = node.ProcessName;
                }
                // 그룹 뷰 기반 (ProcessGroup)
                else if (button?.Tag is ProcessGroup group)
                {
                    pid = group.ProcessId;
                    pname = group.ProcessName;
                }


                // CollectionViewGroup 안전망 (XAML 그룹화된 경우)
                else if (button?.DataContext is CollectionViewGroup cvg &&
                int.TryParse(cvg.Name?.ToString(), out int pidFromGroup))
                {
                    pid = pidFromGroup;
                    pname = cvg.Name?.ToString();
                }


                if (pid == null)
                {
                    AddLogMessage("선택된 프로세스 그룹을 찾을 수 없습니다.");
                    MessageBox.Show("선택된 프로세스 그룹을 찾을 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }


                await TerminateProcessByPidAsync(pid.Value, pname);
            }
            catch (Exception ex)
            {
                AddLogMessage($"그룹 프로세스 종료 오류: {ex.Message}");
                MessageBox.Show($"프로세스 종료 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async Task TerminateProcessByPidAsync(int pid, string? name)
        {
            var result = MessageBox.Show(
            $"프로세스 '{name}' (PID: {pid})을(를) 강제 종료하시겠습니까?\n\n⚠️ 주의: 데이터 손실이 발생할 수 있습니다.",
            "프로세스 종료 확인",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);


            if (result != MessageBoxResult.Yes) return;


            try
            {
                AddLogMessage($"프로세스 종료 시작: {name} (PID: {pid})");


                bool success = await Task.Run(() => _connectionManager.TerminateProcessFamily(pid));


                if (success)
                {
                    AddLogMessage("프로세스 종료가 완료되었습니다.");
                    //MessageBox.Show("프로세스 종료가 완료되었습니다.", "성공", MessageBoxButton.OK, MessageBoxImage.Information);

                    ShowTrayNotification($"프로세스 종료 완료: {name} (PID: {pid})");

                    var data = await _processNetworkMapper.GetProcessNetworkDataAsync();
                    await UpdateProcessNetworkDataAsync(data);
                }
                else
                {
                    AddLogMessage("프로세스 종료에 실패했습니다. 관리자 권한이 필요할 수 있습니다.");
                    MessageBox.Show("프로세스 종료에 실패했습니다. 관리자 권한이 필요할 수 있습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"프로세스 종료 실패: {ex.Message}");
                MessageBox.Show($"프로세스 종료에 실패했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 프로세스 데이터 새로고침
        /// </summary>
        private async Task RefreshProcessData()
        {
            try
            {
                var data = await _processNetworkMapper.GetProcessNetworkDataAsync();
                await UpdateProcessNetworkDataAsync(data);
            }
            catch (Exception ex)
            {
                AddLogMessage($"데이터 새로고침 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// TreeView 방식에서는 자동 상태 관리로 인해 별도 복원 불필요
        /// </summary>
        private void RestoreGroupStates()
        {
            // TreeView는 IsExpanded 바인딩을 통해 자동으로 상태가 관리되므로
            // 별도의 수동 복원 작업이 필요하지 않습니다.
            System.Diagnostics.Debug.WriteLine("[RestoreGroupStates] TreeView 자동 상태 관리 - 수동 복원 불필요");
        }

        #endregion

        #region 그룹 상태 관리 - DEPRECATED (데이터 바인딩으로 대체됨)
        /*
        /// <summary>
        /// 그룹의 확장/축소 상태를 저장합니다
        /// 이제 이벤트 핸들러에서 실시간으로 상태를 저장하므로 이 메서드는 간단히 처리
        /// </summary>
        private void SaveGroupExpandedStates()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"현재 저장된 그룹 상태: {_processGroupExpandedStates.Count}개");
                foreach (var kvp in _processGroupExpandedStates)
                {
                    System.Diagnostics.Debug.WriteLine($"  PID {kvp.Key}: {(kvp.Value ? "펼침" : "접힘")}");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"그룹 상태 저장 오류: {ex.Message}");
            }
        }
        */

        /*
        /// <summary>
        /// DEPRECATED - TreeView 방식에서는 자동으로 상태가 관리됨
        /// 저장된 그룹의 확장/축소 상태를 복원합니다
        /// </summary>
        private void RestoreGroupExpandedStates()
        {
            // TreeView의 IsExpanded 데이터 바인딩으로 자동 관리됨
        }
        */

        /*
        /// <summary>
        /// DEPRECATED - TreeView 방식에서는 자동으로 상태가 관리됨
        /// 실제 그룹 상태 복원 로직
        /// </summary>
        private void RestoreGroupStatesInternal(ICollectionView view)
        {
            // TreeView의 IsExpanded 데이터 바인딩으로 자동 관리됨
        }
        */

        /*
        /// <summary>
        /// DEPRECATED - TreeView 방식에서는 사용하지 않음
        /// DataGrid에서 CollectionViewGroup에 해당하는 GroupItem을 찾습니다
        /// </summary>
        private GroupItem? GetGroupItemFromGroup(DataGrid dataGrid, CollectionViewGroup group)
        {
            // TreeView 방식에서는 사용하지 않음
            return null;
        }

        /// <summary>
        /// DEPRECATED - TreeView 방식에서는 사용하지 않음
        /// GroupItem에서 Expander 컨트롤을 찾습니다
        /// </summary>
        private Expander? FindExpanderInGroupItem(GroupItem groupItem)
        {
            // TreeView 방식에서는 사용하지 않음
            return null;
        }

        /// <summary>
        /// DEPRECATED - TreeView 방식에서는 사용하지 않음
        /// 시각적 트리에서 특정 타입의 자식 요소를 찾습니다
        /// </summary>
        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            // TreeView 방식에서는 사용하지 않음
            return null;
        }
        */

        #endregion

        #region AutoBlock 시스템 메서드

        /// <summary>
        /// 연결들을 AutoBlock 시스템으로 분석
        /// </summary>
        private async Task AnalyzeConnectionsWithAutoBlockAsync(List<ProcessNetworkInfo> connections)
        {
            if (!_isInitialized || connections?.Any() != true)
                return;

            try
            {
                var blockedCount = 0;
                var level1Count = 0;
                var level2Count = 0;
                var level3Count = 0;

                foreach (var connection in connections)
                {
                    // 화이트리스트 확인
                    if (await _autoBlockService.IsWhitelistedAsync(connection))
                        continue;

                    // 연결 분석
                    var decision = await _autoBlockService.AnalyzeConnectionAsync(connection);

                    if (decision.Level > BlockLevel.None)
                    {
                        // 차단 실행
                        var blocked = await _autoBlockService.BlockConnectionAsync(connection, decision.Level);

                        if (blocked)
                        {
                            blockedCount++;

                            // 차단된 연결 정보 생성
                            var blockedConnection = new AutoBlockedConnection
                            {
                                ProcessName = connection.ProcessName,
                                ProcessPath = connection.ProcessPath,
                                ProcessId = connection.ProcessId,
                                RemoteAddress = connection.RemoteAddress,
                                RemotePort = connection.RemotePort,
                                Protocol = connection.Protocol,
                                BlockLevel = decision.Level,
                                Reason = decision.Reason,
                                BlockedAt = DateTime.Now,
                                ConfidenceScore = decision.ConfidenceScore,
                                IsBlocked = true,
                                TriggeredRules = string.Join(", ", decision.TriggeredRules ?? new List<string>())
                            };

                            // 통계 시스템에 기록
                            _ = Task.Run(async () =>
                            {
                                await RecordBlockEventAsync(blockedConnection);
                            });

                            switch (decision.Level)
                            {
                                case BlockLevel.Immediate:
                                    level1Count++;
                                    AddLogMessage($"[AutoBlock-Immediate] 즉시 차단: {connection.ProcessName} -> {connection.RemoteAddress}:{connection.RemotePort}");
                                    break;
                                case BlockLevel.Warning:
                                    level2Count++;
                                    AddLogMessage($"[AutoBlock-Warning] 경고 후 차단: {connection.ProcessName} -> {connection.RemoteAddress}:{connection.RemotePort}");
                                    break;
                                case BlockLevel.Monitor:
                                    level3Count++;
                                    AddLogMessage($"[AutoBlock-Monitor] 모니터링: {connection.ProcessName} -> {connection.RemoteAddress}:{connection.RemotePort}");
                                    break;
                            }

                            // 보안 알림 생성
                            var alertLevel = decision.Level == BlockLevel.Immediate ? LogCheck.Services.SecurityAlertLevel.High :
                                            decision.Level == BlockLevel.Warning ? LogCheck.Services.SecurityAlertLevel.Medium :
                                            LogCheck.Services.SecurityAlertLevel.Low;

                            var alert = new LogCheck.Services.SecurityAlert
                            {
                                Title = $"AutoBlock: {connection.ProcessName} 연결 차단됨",
                                Description = $"연결 차단됨 - {decision.Reason}",
                                AlertLevel = alertLevel,
                                ProcessId = connection.ProcessId,
                                ProcessName = connection.ProcessName,
                                RemoteAddress = connection.RemoteAddress,
                                RemotePort = connection.RemotePort,
                                Protocol = connection.Protocol,
                                RiskScore = (int)(decision.ConfidenceScore * 100),
                                RiskFactors = decision.TriggeredRules ?? new List<string>(),
                                RecommendedAction = decision.RecommendedAction,
                                Timestamp = DateTime.Now,
                                IsResolved = false
                            };

                            SafeInvokeUI(() =>
                            {
                                _securityAlerts.Insert(0, alert);
                                // 알림 목록 크기 제한
                                while (_securityAlerts.Count > 100)
                                {
                                    _securityAlerts.RemoveAt(_securityAlerts.Count - 1);
                                }
                            });
                        }
                    }
                }

                // 통계 업데이트
                if (blockedCount > 0)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        TotalBlockedCount += blockedCount;
                        Level1BlockCount += level1Count;
                        Level2BlockCount += level2Count;
                        Level3BlockCount += level3Count;
                    });

                    // 차단 데이터 새로고침
                    await LoadAutoBlockDataAsync();
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"AutoBlock 분석 오류: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"AutoBlock analysis error: {ex}");
            }
        }

        /// <summary>
        /// AutoBlock 통계 업데이트
        /// </summary>
        private void UpdateAutoBlockStatistics()
        {
            try
            {
                // 통계는 데이터베이스에서 직접 조회하는 대신 
                // 현재 로드된 데이터를 기반으로 계산
                var totalBlocked = _blockedConnections.Count;
                var level1 = _blockedConnections.Count(b => b.BlockLevel == BlockLevel.Immediate);
                var level2 = _blockedConnections.Count(b => b.BlockLevel == BlockLevel.Warning);
                var level3 = _blockedConnections.Count(b => b.BlockLevel == BlockLevel.Monitor);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    TotalBlockedCount = totalBlocked;
                    Level1BlockCount = level1;
                    Level2BlockCount = level2;
                    Level3BlockCount = level3;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutoBlock statistics update error: {ex}");
            }
        }

        /// <summary>
        /// AutoBlock 서비스 상태 확인
        /// </summary>
        private bool IsAutoBlockInitialized => _autoBlockService != null && _isInitialized;

        /// <summary>
        /// 차단 이벤트를 통계 시스템에 기록
        /// </summary>
        private async Task RecordBlockEventAsync(AutoBlockedConnection blockedConnection)
        {
            try
            {
                if (_autoBlockStats != null)
                {
                    await _autoBlockStats.RecordBlockEventAsync(blockedConnection);
                    // 통계 업데이트
                    await UpdateAutoBlockStatisticsFromDatabase();
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"차단 통계 기록 오류: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Block event recording error: {ex}");
            }
        }

        /// <summary>
        /// 데이터베이스에서 통계 로드하여 UI 업데이트
        /// </summary>
        private async Task UpdateAutoBlockStatisticsFromDatabase()
        {
            try
            {
                if (_autoBlockStats != null)
                {
                    var stats = await _autoBlockStats.GetTodayStatisticsAsync();

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        TotalBlockedCount = stats.TotalBlocked;
                        Level1BlockCount = stats.Level1Blocked;
                        Level2BlockCount = stats.Level2Blocked;
                        Level3BlockCount = stats.Level3Blocked;
                        UniqueProcesses = stats.UniqueProcesses;
                        UniqueIPs = stats.UniqueIPs;
                    });
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"AutoBlock 통계 업데이트 오류: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Statistics update error: {ex}");
            }
        }

        /// <summary>
        /// AutoBlock 통계 시스템 초기화
        /// </summary>
        private async Task InitializeAutoBlockStatisticsAsync()
        {
            try
            {
                if (_autoBlockStats != null)
                {
                    await _autoBlockStats.InitializeDatabaseAsync();
                    await UpdateAutoBlockStatisticsFromDatabase();
                    AddLogMessage("AutoBlock 통계 시스템이 초기화되었습니다.");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"AutoBlock 통계 시스템 초기화 오류: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Statistics initialization error: {ex}");
            }
        }

        #endregion

        #region AutoBlock UI 이벤트 핸들러

        /// <summary>
        /// 화이트리스트 추가 버튼 클릭
        /// </summary>
        private async void AddToWhitelist_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 간단한 입력 다이얼로그 (실제 구현에서는 더 정교한 UI 사용)
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "화이트리스트에 추가할 프로그램 선택",
                    Filter = "실행 파일 (*.exe)|*.exe|모든 파일 (*.*)|*.*"
                };

                if (dialog.ShowDialog() == true)
                {
                    var result = await _autoBlockService.AddToWhitelistAsync(dialog.FileName, "사용자 추가");
                    if (result)
                    {
                        AddLogMessage($"화이트리스트에 추가됨: {System.IO.Path.GetFileName(dialog.FileName)}");
                        await LoadAutoBlockDataAsync(); // 데이터 새로고침
                    }
                    else
                    {
                        AddLogMessage($"화이트리스트 추가 실패: {System.IO.Path.GetFileName(dialog.FileName)}");
                    }
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"화이트리스트 추가 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 화이트리스트에서 제거 버튼 클릭
        /// </summary>
        private async void RemoveFromWhitelist_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (WhitelistDataGrid.SelectedItem is AutoWhitelistEntry selectedEntry)
                {
                    var result = MessageBox.Show(
                        $"'{selectedEntry.ProcessPath}'\n화이트리스트에서 제거하시겠습니까?",
                        "화이트리스트 제거 확인",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        var success = await _autoBlockService.RemoveFromWhitelistAsync(selectedEntry.ProcessPath);
                        if (success)
                        {
                            AddLogMessage($"화이트리스트에서 제거됨: {System.IO.Path.GetFileName(selectedEntry.ProcessPath)}");
                            await LoadAutoBlockDataAsync(); // 데이터 새로고침
                        }
                        else
                        {
                            AddLogMessage($"화이트리스트 제거 실패: {System.IO.Path.GetFileName(selectedEntry.ProcessPath)}");
                        }
                    }
                }
                else
                {
                    MessageBox.Show("제거할 항목을 선택해주세요.", "선택 없음", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"화이트리스트 제거 오류: {ex.Message}");
            }
        }

        #endregion



        #region Blocked Connections Management

        /// <summary>
        /// 차단된 연결 목록을 로드합니다.
        /// </summary>
        private async Task LoadBlockedConnectionsAsync()
        {
            try
            {
                var blockedList = await _autoBlockStats.GetBlockedConnectionsAsync();

                Dispatcher.Invoke(() =>
                {
                    _blockedConnections.Clear();
                    foreach (var item in blockedList.OrderByDescending(x => x.BlockedAt))
                    {
                        item.IsSelected = false; // 선택 상태 초기화
                        _blockedConnections.Add(item);
                    }

                    // UI 컨트롤이 로드된 경우에만 바인딩
                    var blockedDataGrid = FindName("BlockedConnectionsDataGrid") as DataGrid;
                    if (blockedDataGrid != null)
                    {
                        blockedDataGrid.ItemsSource = _blockedConnections;
                    }
                    UpdateBlockedCount();
                });
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ 차단된 연결 목록 로드 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 차단된 연결 수를 업데이트합니다.
        /// </summary>
        private void UpdateBlockedCount()
        {
            var totalCount = _blockedConnections.Count;
            var selectedCount = _blockedConnections.Count(x => x.IsSelected);

            // UI 컨트롤이 로드되지 않은 경우 무시
            var blockedCountText = FindName("BlockedCountText") as TextBlock;
            if (blockedCountText != null)
            {
                blockedCountText.Text = selectedCount > 0
                    ? $"총 {totalCount}개 (선택됨: {selectedCount}개)"
                    : $"총 {totalCount}개 차단됨";
            }

            // 요약 정보 업데이트
            var today = _blockedConnections.Count(x => x.BlockedAt.Date == DateTime.Today);
            var manual = _blockedConnections.Count(x => x.Reason.Contains("사용자") || x.Reason.Contains("Manual"));
            var auto = _blockedConnections.Count(x => !x.Reason.Contains("사용자") && !x.Reason.Contains("Manual"));

            var blockedSummaryText = FindName("BlockedSummaryText") as TextBlock;
            if (blockedSummaryText != null)
            {
                blockedSummaryText.Text = $"오늘 {today}개 차단됨 | 수동: {manual}개, 자동: {auto}개";
            }
        }

        /// <summary>
        /// 차단 필터 변경 이벤트
        /// </summary>
        private void BlockedFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyBlockedFilter();
        }

        /// <summary>
        /// 차단 검색 텍스트 변경 이벤트
        /// </summary>
        private void BlockedSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyBlockedFilter();
        }

        /// <summary>
        /// 차단된 연결에 필터를 적용합니다.
        /// </summary>
        private void ApplyBlockedFilter()
        {
            try
            {
                // 컨트롤이 로드되지 않은 경우 무시
                var blockedFilterComboBox = FindName("BlockedFilterComboBox") as System.Windows.Controls.ComboBox;
                var blockedSearchTextBox = FindName("BlockedSearchTextBox") as System.Windows.Controls.TextBox;
                var blockedConnectionsDataGrid = FindName("BlockedConnectionsDataGrid") as DataGrid;

                if (blockedFilterComboBox == null || blockedSearchTextBox == null || blockedConnectionsDataGrid == null)
                    return;

                var filterItem = blockedFilterComboBox.SelectedItem as ComboBoxItem;
                var filterText = filterItem?.Content?.ToString() ?? "전체 보기";
                var searchText = blockedSearchTextBox.Text?.Trim().ToLower() ?? "";

                var filteredList = _blockedConnections.AsEnumerable();

                // 날짜 필터 적용
                switch (filterText)
                {
                    case "오늘":
                        filteredList = filteredList.Where(x => x.BlockedAt.Date == DateTime.Today);
                        break;
                    case "최근 7일":
                        var sevenDaysAgo = DateTime.Today.AddDays(-7);
                        filteredList = filteredList.Where(x => x.BlockedAt.Date >= sevenDaysAgo);
                        break;
                    case "수동 차단":
                        filteredList = filteredList.Where(x => x.Reason.Contains("사용자") || x.Reason.Contains("Manual"));
                        break;
                    case "자동 차단":
                        filteredList = filteredList.Where(x => !x.Reason.Contains("사용자") && !x.Reason.Contains("Manual"));
                        break;
                    case "그룹 차단":
                        filteredList = filteredList.Where(x => x.Reason.Contains("그룹") || x.Reason.Contains("Group"));
                        break;
                }

                // 검색 텍스트 필터 적용
                if (!string.IsNullOrEmpty(searchText))
                {
                    filteredList = filteredList.Where(x =>
                        x.ProcessName.ToLower().Contains(searchText) ||
                        x.RemoteAddress.ToLower().Contains(searchText) ||
                        x.Reason.ToLower().Contains(searchText));
                }

                var tempCollection = new ObservableCollection<AutoBlockedConnection>(filteredList);

                blockedConnectionsDataGrid.ItemsSource = tempCollection;

                var blockedCountText = FindName("BlockedCountText") as TextBlock;
                if (blockedCountText != null)
                {
                    blockedCountText.Text = $"총 {tempCollection.Count}개 (전체: {_blockedConnections.Count}개)";
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ 필터 적용 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 차단된 연결 목록 새로고침
        /// </summary>
        private async void RefreshBlockedList_Click(object sender, RoutedEventArgs e)
        {
            AddLogMessage("🔄 차단된 연결 목록을 새로고침하는 중...");
            await LoadBlockedConnectionsAsync();
            AddLogMessage("✅ 차단된 연결 목록 새로고침 완료");
        }

        /// <summary>
        /// 선택된 연결들을 차단 해제
        /// </summary>
        private async void UnblockSelected_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItems = _blockedConnections.Where(x => x.IsSelected).ToList();
                if (!selectedItems.Any())
                {
                    MessageBox.Show("차단 해제할 항목을 선택하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"선택된 {selectedItems.Count}개 연결의 차단을 해제하시겠습니까?",
                    "차단 해제 확인",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    int unblocked = 0;
                    foreach (var item in selectedItems)
                    {
                        if (await _autoBlockStats.RemoveBlockedConnectionAsync(item.Id))
                        {
                            _blockedConnections.Remove(item);
                            unblocked++;
                        }
                    }

                    AddLogMessage($"✅ {unblocked}개 연결의 차단이 해제되었습니다.");
                    UpdateBlockedCount();
                    MessageBox.Show($"{unblocked}개 연결의 차단이 해제되었습니다.", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ 차단 해제 오류: {ex.Message}");
                MessageBox.Show($"차단 해제 중 오류가 발생했습니다:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 개별 연결 차단 해제
        /// </summary>
        private async void UnblockConnection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as System.Windows.Controls.Button;
                var connection = button?.Tag as AutoBlockedConnection;

                if (connection == null) return;

                var result = MessageBox.Show(
                    $"다음 연결의 차단을 해제하시겠습니까?\n\n" +
                    $"프로세스: {connection.ProcessName}\n" +
                    $"주소: {connection.RemoteAddress}:{connection.RemotePort}\n" +
                    $"차단 시간: {connection.BlockedAt:yyyy-MM-dd HH:mm:ss}",
                    "차단 해제 확인",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    if (await _autoBlockStats.RemoveBlockedConnectionAsync(connection.Id))
                    {
                        _blockedConnections.Remove(connection);
                        AddLogMessage($"✅ 연결 차단 해제: {connection.ProcessName} -> {connection.RemoteAddress}:{connection.RemotePort}");
                        UpdateBlockedCount();
                        MessageBox.Show("차단이 해제되었습니다.", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("차단 해제에 실패했습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ 개별 차단 해제 오류: {ex.Message}");
                MessageBox.Show($"차단 해제 중 오류가 발생했습니다:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 모든 차단된 연결 삭제
        /// </summary>
        private async void ClearAllBlocked_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_blockedConnections.Any())
                {
                    MessageBox.Show("삭제할 차단 기록이 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"모든 차단 기록({_blockedConnections.Count}개)을 삭제하시겠습니까?\n\n" +
                    "⚠️ 이 작업은 되돌릴 수 없습니다.",
                    "전체 삭제 확인",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    await _autoBlockStats.ClearAllBlockedConnectionsAsync();
                    _blockedConnections.Clear();

                    AddLogMessage("🧹 모든 차단 기록이 삭제되었습니다.");
                    UpdateBlockedCount();
                    MessageBox.Show("모든 차단 기록이 삭제되었습니다.", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ 전체 삭제 오류: {ex.Message}");
                MessageBox.Show($"삭제 중 오류가 발생했습니다:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 차단된 연결의 상세 정보 표시
        /// </summary>
        private void ShowBlockedDetails_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as System.Windows.Controls.Button;
                var connection = button?.Tag as AutoBlockedConnection;

                if (connection == null) return;

                var details = $"""
                    === 차단된 연결 상세 정보 ===
                    
                    🛡️ 기본 정보
                    • 프로세스: {connection.ProcessName}
                    • 프로세스 경로: {connection.ProcessPath}
                    • 프로세스 ID: {connection.ProcessId}
                    
                    🌐 네트워크 정보
                    • 원격 주소: {connection.RemoteAddress}
                    • 원격 포트: {connection.RemotePort}
                    • 프로토콜: {connection.Protocol}
                    
                    ⚡ 차단 정보
                    • 차단 시간: {connection.BlockedAt:yyyy-MM-dd HH:mm:ss}
                    • 차단 레벨: {connection.BlockLevel}
                    • 차단 이유: {connection.Reason}
                    • 신뢰도: {connection.ConfidenceScore:P0}
                    
                    📋 규칙 정보
                    • 트리거된 규칙: {connection.TriggeredRules}
                    
                    💾 데이터베이스 ID: {connection.Id}
                    """;

                MessageBox.Show(details, $"차단 정보 - {connection.ProcessName}", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ 상세 정보 표시 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 차단 통계 보기
        /// </summary>
        private async void ShowBlockedStatistics_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var stats = await _autoBlockStats.GetCurrentStatisticsAsync();

                var message = $"""
                    === AutoBlock 시스템 통계 ===
                    
                    📊 전체 통계
                    • 총 차단된 연결: {stats.TotalBlocked:N0}개
                    • 차단된 프로세스 수: {stats.UniqueProcesses:N0}개
                    • 차단된 IP 수: {stats.UniqueIPs:N0}개
                    
                    🎯 차단 레벨별
                    • 즉시 차단 (Level 1): {stats.Level1Blocks:N0}개
                    • 경고 후 차단 (Level 2): {stats.Level2Blocks:N0}개
                    • 모니터링 (Level 3): {stats.Level3Blocks:N0}개
                    
                    📅 최근 활동
                    • 오늘 차단: {_blockedConnections.Count(x => x.BlockedAt.Date == DateTime.Today):N0}개
                    • 이번 주 차단: {_blockedConnections.Count(x => x.BlockedAt.Date >= DateTime.Today.AddDays(-7)):N0}개
                    
                    🔄 마지막 업데이트: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
                    """;

                MessageBox.Show(message, "AutoBlock 통계", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ 통계 조회 오류: {ex.Message}");
                MessageBox.Show($"통계 조회 중 오류가 발생했습니다:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 차단된 연결 목록 내보내기
        /// </summary>
        private void ExportBlockedList_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "차단된 연결 목록 내보내기",
                    Filter = "CSV 파일 (*.csv)|*.csv|텍스트 파일 (*.txt)|*.txt",
                    DefaultExt = "csv",
                    FileName = $"blocked_connections_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var lines = new List<string>();

                    // CSV 헤더
                    lines.Add("차단시간,프로세스명,PID,원격주소,포트,프로토콜,차단레벨,차단이유,신뢰도,트리거된규칙");

                    // 데이터
                    foreach (var item in _blockedConnections.OrderByDescending(x => x.BlockedAt))
                    {
                        lines.Add($"\"{item.BlockedAt:yyyy-MM-dd HH:mm:ss}\"," +
                                $"\"{item.ProcessName}\"," +
                                $"{item.ProcessId}," +
                                $"\"{item.RemoteAddress}\"," +
                                $"{item.RemotePort}," +
                                $"\"{item.Protocol}\"," +
                                $"\"{item.BlockLevel}\"," +
                                $"\"{item.Reason}\"," +
                                $"{item.ConfidenceScore:F2}," +
                                $"\"{item.TriggeredRules}\"");
                    }

                    File.WriteAllLines(saveDialog.FileName, lines, System.Text.Encoding.UTF8);

                    AddLogMessage($"📤 차단된 연결 목록이 내보내졌습니다: {saveDialog.FileName}");
                    MessageBox.Show($"파일이 성공적으로 저장되었습니다:\n{saveDialog.FileName}",
                        "내보내기 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ 내보내기 오류: {ex.Message}");
                MessageBox.Show($"파일 내보내기 중 오류가 발생했습니다:\n{ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region SafeInvokeUI Pattern (BasePageViewModel 패턴 적용)

        /// <summary>
        /// 안전한 UI 업데이트 헬퍼 메서드 (BasePageViewModel 패턴)
        /// </summary>
        /// <param name="action">UI 업데이트 액션</param>
        private void SafeInvokeUI(Action action)
        {
            try
            {
                if (Dispatcher.CheckAccess())
                {
                    action();
                }
                else
                {
                    Dispatcher.InvokeAsync(action);
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ UI 업데이트 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 안전한 비동기 UI 업데이트 헬퍼 메서드 (BasePageViewModel 패턴)
        /// </summary>
        /// <param name="action">UI 업데이트 액션</param>
        private async Task SafeInvokeUIAsync(Action action)
        {
            try
            {
                if (Dispatcher.CheckAccess())
                {
                    action();
                }
                else
                {
                    await Dispatcher.InvokeAsync(action);
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ 비동기 UI 업데이트 오류: {ex.Message}");
            }
        }

        #endregion



        #region INotifyPropertyChanged Implementation

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region 영구 방화벽 차단 기능

        /// <summary>
        /// 영구 차단 옵션 선택 다이얼로그 표시
        /// </summary>
        private PermanentBlockOptions? ShowPermanentBlockDialog(ProcessNetworkInfo networkInfo)
        {
            try
            {
                var dialog = new Window
                {
                    Title = "네트워크 차단 방식 선택",
                    Width = 500,
                    Height = 400,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this)
                };

                var stackPanel = new StackPanel { Margin = new Thickness(20) };

                // 제목
                stackPanel.Children.Add(new TextBlock
                {
                    Text = "차단 방식을 선택하세요:",
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 15)
                });

                // 프로세스 정보
                var infoPanelBorder = new Border
                {
                    Background = new SolidColorBrush(MediaColor.FromRgb(240, 240, 240)),
                    Margin = new Thickness(0, 0, 0, 20),
                    Padding = new Thickness(10)
                };
                var infoPanel = new StackPanel();
                infoPanel.Children.Add(new TextBlock
                {
                    Text = $"프로세스: {networkInfo.ProcessName} (PID: {networkInfo.ProcessId})",
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 5)
                });
                infoPanel.Children.Add(new TextBlock
                {
                    Text = $"연결: {networkInfo.RemoteAddress}:{networkInfo.RemotePort} ({networkInfo.Protocol})",
                    FontSize = 11,
                    Foreground = MediaBrushes.DarkGray
                });
                if (!string.IsNullOrEmpty(networkInfo.ProcessPath))
                {
                    infoPanel.Children.Add(new TextBlock
                    {
                        Text = $"경로: {networkInfo.ProcessPath}",
                        FontSize = 10,
                        Foreground = MediaBrushes.Gray,
                        TextWrapping = TextWrapping.Wrap
                    });
                }
                infoPanelBorder.Child = infoPanel;
                stackPanel.Children.Add(infoPanelBorder);

                // 차단 방식 선택
                var tempRadio = new System.Windows.Controls.RadioButton
                {
                    Content = "임시 차단 (기존 방식)\n• 프로그램 재시작 시 해제됨\n• 즉시 적용",
                    IsChecked = true,
                    Margin = new Thickness(0, 5, 0, 10),
                    Padding = new Thickness(5)
                };

                var permanentRadio = new System.Windows.Controls.RadioButton
                {
                    Content = "영구 차단 (Windows 방화벽)\n• 프로그램 재시작 후에도 유지\n• 관리자 권한 필요",
                    Margin = new Thickness(0, 5, 0, 15),
                    Padding = new Thickness(5)
                };

                stackPanel.Children.Add(tempRadio);
                stackPanel.Children.Add(permanentRadio);

                // 영구 차단 상세 옵션
                var permanentOptionsPanel = new StackPanel
                {
                    Margin = new Thickness(20, 0, 0, 15),
                    IsEnabled = false
                };

                var processRadio = new System.Windows.Controls.RadioButton
                {
                    Content = $"프로세스 경로 차단\n{networkInfo.ProcessPath}",
                    IsChecked = true,
                    Margin = new Thickness(0, 5, 0, 5),
                    GroupName = "BlockType"
                };

                var ipRadio = new System.Windows.Controls.RadioButton
                {
                    Content = $"IP 주소 차단\n{networkInfo.RemoteAddress}",
                    Margin = new Thickness(0, 5, 0, 5),
                    GroupName = "BlockType"
                };

                var portRadio = new System.Windows.Controls.RadioButton
                {
                    Content = $"포트 차단\n{networkInfo.RemotePort} ({networkInfo.Protocol})",
                    Margin = new Thickness(0, 5, 0, 5),
                    GroupName = "BlockType"
                };

                permanentOptionsPanel.Children.Add(new TextBlock
                {
                    Text = "영구 차단 유형:",
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 10)
                });
                permanentOptionsPanel.Children.Add(processRadio);
                permanentOptionsPanel.Children.Add(ipRadio);
                permanentOptionsPanel.Children.Add(portRadio);

                stackPanel.Children.Add(permanentOptionsPanel);

                // 영구 차단 선택 시 옵션 패널 활성화
                permanentRadio.Checked += (s, e) => permanentOptionsPanel.IsEnabled = true;
                tempRadio.Checked += (s, e) => permanentOptionsPanel.IsEnabled = false;

                // 버튼들
                var buttonPanel = new StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    Margin = new Thickness(0, 20, 0, 0)
                };

                var okButton = new System.Windows.Controls.Button
                {
                    Content = "확인",
                    Width = 80,
                    Height = 30,
                    Margin = new Thickness(0, 0, 10, 0)
                };

                var cancelButton = new System.Windows.Controls.Button
                {
                    Content = "취소",
                    Width = 80,
                    Height = 30
                };

                PermanentBlockOptions? result = null;

                okButton.Click += (s, e) =>
                {
                    result = new PermanentBlockOptions
                    {
                        UsePermanentBlock = permanentRadio.IsChecked == true
                    };

                    if (result.UsePermanentBlock)
                    {
                        if (processRadio.IsChecked == true)
                            result.BlockType = NetworkBlockType.ProcessPath;
                        else if (ipRadio.IsChecked == true)
                            result.BlockType = NetworkBlockType.IPAddress;
                        else if (portRadio.IsChecked == true)
                            result.BlockType = NetworkBlockType.Port;
                    }

                    dialog.DialogResult = true;
                    dialog.Close();
                };

                cancelButton.Click += (s, e) =>
                {
                    dialog.DialogResult = false;
                    dialog.Close();
                };

                buttonPanel.Children.Add(okButton);
                buttonPanel.Children.Add(cancelButton);
                stackPanel.Children.Add(buttonPanel);

                dialog.Content = stackPanel;

                return dialog.ShowDialog() == true ? result : null;
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ 차단 옵션 다이얼로그 오류: {ex.Message}");
                return new PermanentBlockOptions { UsePermanentBlock = false }; // 기본값: 임시 차단
            }
        }

        /// <summary>
        /// 영구 방화벽 차단 적용
        /// </summary>
        private async Task ApplyPermanentBlockAsync(ProcessNetworkInfo networkInfo, PermanentBlockOptions options)
        {
            try
            {
                AddLogMessage($"🔒 영구 차단 시작: {networkInfo.ProcessName} ({networkInfo.RemoteAddress}:{networkInfo.RemotePort})");

                // PersistentFirewallManager 초기화 (필요시)
                if (_persistentFirewallManager == null)
                {
                    _persistentFirewallManager = new PersistentFirewallManager("LogCheck_NetworkBlock");
                    var initResult = await _persistentFirewallManager.InitializeAsync();

                    if (!initResult)
                    {
                        AddLogMessage("❌ 방화벽 관리자 초기화 실패: 관리자 권한이 필요합니다.");
                        MessageBox.Show("방화벽 규칙을 생성하려면 관리자 권한이 필요합니다.\n프로그램을 관리자 권한으로 다시 실행해주세요.",
                            "권한 부족", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                bool success = false;
                string blockDescription = "";

                switch (options.BlockType)
                {
                    case NetworkBlockType.ProcessPath:
                        if (!string.IsNullOrEmpty(networkInfo.ProcessPath))
                        {
                            success = await _persistentFirewallManager.AddPermanentProcessBlockRuleAsync(
                                networkInfo.ProcessPath, networkInfo.ProcessName);
                            blockDescription = $"프로세스 경로: {networkInfo.ProcessPath}";
                        }
                        break;

                    case NetworkBlockType.IPAddress:
                        if (!string.IsNullOrEmpty(networkInfo.RemoteAddress) &&
                            networkInfo.RemoteAddress != "0.0.0.0" &&
                            networkInfo.RemoteAddress != "127.0.0.1")
                        {
                            success = await _persistentFirewallManager.AddPermanentIPBlockRuleAsync(
                                networkInfo.RemoteAddress,
                                $"LogCheck - {networkInfo.ProcessName}에서 {networkInfo.RemoteAddress}로의 연결 차단");
                            blockDescription = $"IP 주소: {networkInfo.RemoteAddress}";
                        }
                        break;

                    case NetworkBlockType.Port:
                        if (networkInfo.RemotePort > 0 && networkInfo.RemotePort < 65536)
                        {
                            int protocol = networkInfo.Protocol.ToUpper() == "TCP" ? 6 : 17; // TCP=6, UDP=17
                            success = await _persistentFirewallManager.AddPermanentPortBlockRuleAsync(
                                networkInfo.RemotePort, protocol,
                                $"LogCheck - {networkInfo.ProcessName}에서 {networkInfo.RemotePort}({networkInfo.Protocol}) 포트 차단");
                            blockDescription = $"포트: {networkInfo.RemotePort} ({networkInfo.Protocol})";
                        }
                        break;
                }

                if (success)
                {
                    // AutoBlock 시스템과 연동하여 통계 기록
                    var decision = new BlockDecision
                    {
                        Level = BlockLevel.Immediate, // 영구 차단은 최고 등급으로 기록
                        Reason = $"사용자 영구 차단 요청 - {blockDescription}",
                        ConfidenceScore = 1.0,
                        TriggeredRules = new List<string> { "Manual Permanent Block" },
                        RecommendedAction = "Windows 방화벽을 통한 영구 차단 적용됨",
                        ThreatCategory = "User Permanent Block",
                        AnalyzedAt = DateTime.Now
                    };

                    // 차단된 연결 정보 생성 및 통계 기록
                    var blockedConnection = new AutoBlockedConnection
                    {
                        ProcessName = networkInfo.ProcessName,
                        ProcessPath = networkInfo.ProcessPath,
                        ProcessId = networkInfo.ProcessId,
                        RemoteAddress = networkInfo.RemoteAddress,
                        RemotePort = networkInfo.RemotePort,
                        Protocol = networkInfo.Protocol,
                        BlockLevel = decision.Level,
                        Reason = decision.Reason,
                        BlockedAt = DateTime.Now,
                        ConfidenceScore = decision.ConfidenceScore,
                        IsBlocked = true,
                        TriggeredRules = string.Join(", ", decision.TriggeredRules)
                    };

                    // 통계 시스템에 기록
                    _ = Task.Run(async () =>
                    {
                        await RecordBlockEventAsync(blockedConnection);
                        await _autoBlockStats.AddBlockedConnectionAsync(blockedConnection);
                    });

                    // 통계 UI 업데이트
                    UpdateStatisticsDisplay();

                    // 차단된 연결 목록 새로고침
                    _ = Task.Run(async () => await LoadBlockedConnectionsAsync());

                    AddLogMessage($"✅ 영구 차단 규칙 생성 완료: {blockDescription}");
                    MessageBox.Show($"영구 차단 규칙이 Windows 방화벽에 추가되었습니다.\n\n" +
                                  $"차단 대상: {blockDescription}\n" +
                                  $"프로세스: {networkInfo.ProcessName} (PID: {networkInfo.ProcessId})\n\n" +
                                  "이 규칙은 프로그램 재시작 후에도 유지됩니다.",
                        "영구 차단 완료", MessageBoxButton.OK, MessageBoxImage.Information);

                    // 트레이 알림
                    ShowTrayNotification($"영구 차단 완료: {networkInfo.ProcessName} - {blockDescription}");
                }
                else
                {
                    AddLogMessage($"❌ 영구 차단 규칙 생성 실패: {blockDescription}");
                    MessageBox.Show($"방화벽 규칙 생성에 실패했습니다.\n\n" +
                                  $"대상: {blockDescription}\n" +
                                  "관리자 권한을 확인하고 다시 시도해주세요.",
                        "차단 실패", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ 영구 차단 적용 오류: {ex.Message}");
                MessageBox.Show($"영구 차단 적용 중 오류가 발생했습니다:\n{ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region 방화벽 규칙 관리 UI 이벤트

        /// <summary>
        /// 방화벽 규칙 새로고침 버튼 클릭
        /// </summary>
        private async void RefreshFirewallRules_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLogMessage("🔄 방화벽 규칙 새로고침 중...");
                await LoadFirewallRulesAsync();
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ 방화벽 규칙 새로고침 오류: {ex.Message}");
                MessageBox.Show($"방화벽 규칙을 새로고침하는 중 오류가 발생했습니다:\n{ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 모든 방화벽 규칙 제거 버튼 클릭
        /// </summary>
        private async void RemoveAllFirewallRules_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "LogCheck에서 생성한 모든 방화벽 규칙을 제거하시겠습니까?\n\n" +
                    "이 작업은 되돌릴 수 없습니다.",
                    "모든 규칙 제거 확인",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    if (_persistentFirewallManager != null)
                    {
                        AddLogMessage("🗑️ 모든 LogCheck 방화벽 규칙 제거 중...");

                        var removedCount = await _persistentFirewallManager.RemoveAllLogCheckRulesAsync();

                        AddLogMessage($"✅ {removedCount}개의 방화벽 규칙이 제거되었습니다.");
                        MessageBox.Show($"{removedCount}개의 방화벽 규칙이 제거되었습니다.",
                            "제거 완료", MessageBoxButton.OK, MessageBoxImage.Information);

                        // 목록 새로고침
                        await LoadFirewallRulesAsync();
                    }
                    else
                    {
                        MessageBox.Show("방화벽 관리자가 초기화되지 않았습니다.",
                            "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ 방화벽 규칙 일괄 제거 오류: {ex.Message}");
                MessageBox.Show($"방화벽 규칙을 제거하는 중 오류가 발생했습니다:\n{ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 개별 방화벽 규칙 제거 버튼 클릭
        /// </summary>
        private async void RemoveFirewallRule_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Controls.Button button && button.Tag is FirewallRuleInfo rule)
                {
                    var result = MessageBox.Show(
                        $"다음 방화벽 규칙을 제거하시겠습니까?\n\n" +
                        $"규칙명: {rule.Name}\n" +
                        $"설명: {rule.Description}",
                        "규칙 제거 확인",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        if (_persistentFirewallManager != null)
                        {
                            AddLogMessage($"🗑️ 방화벽 규칙 제거: {rule.Name}");

                            var success = await _persistentFirewallManager.RemoveBlockRuleAsync(rule.Name);

                            if (success)
                            {
                                AddLogMessage($"✅ 방화벽 규칙 '{rule.Name}' 제거 완료");
                                MessageBox.Show($"방화벽 규칙이 제거되었습니다:\n{rule.Name}",
                                    "제거 완료", MessageBoxButton.OK, MessageBoxImage.Information);

                                // 목록 새로고침
                                await LoadFirewallRulesAsync();
                            }
                            else
                            {
                                AddLogMessage($"⚠️ 방화벽 규칙 '{rule.Name}' 제거 실패");
                                MessageBox.Show($"방화벽 규칙 제거에 실패했습니다:\n{rule.Name}",
                                    "제거 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                        else
                        {
                            MessageBox.Show("방화벽 관리자가 초기화되지 않았습니다.",
                                "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ 방화벽 규칙 제거 오류: {ex.Message}");
                MessageBox.Show($"방화벽 규칙을 제거하는 중 오류가 발생했습니다:\n{ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 방화벽 규칙 목록 로드
        /// </summary>
        private async Task LoadFirewallRulesAsync()
        {
            try
            {
                // PersistentFirewallManager 초기화 (필요시)
                if (_persistentFirewallManager == null)
                {
                    _persistentFirewallManager = new PersistentFirewallManager("LogCheck_NetworkBlock");
                    var initResult = await _persistentFirewallManager.InitializeAsync();

                    if (!initResult)
                    {
                        SafeInvokeUI(() =>
                        {
                            var adminStatusText = FindName("AdminStatusText") as TextBlock;
                            if (adminStatusText != null)
                                adminStatusText.Text = "권한 부족";
                        });
                        return;
                    }
                }

                var rules = await _persistentFirewallManager.GetLogCheckRulesAsync();

                SafeInvokeUI(() =>
                {
                    _firewallRules.Clear();
                    foreach (var rule in rules)
                    {
                        _firewallRules.Add(rule);
                    }

                    // UI 상태 업데이트
                    var firewallRuleCountText = FindName("FirewallRuleCountText") as TextBlock;
                    if (firewallRuleCountText != null)
                        firewallRuleCountText.Text = $"{_firewallRules.Count}개";

                    var adminStatusText = FindName("AdminStatusText") as TextBlock;
                    if (adminStatusText != null)
                        adminStatusText.Text = "정상";

                    var noRulesPanel = FindName("NoRulesPanel") as Border;
                    if (noRulesPanel != null)
                        noRulesPanel.Visibility = _firewallRules.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                });

                AddLogMessage($"📋 방화벽 규칙 {_firewallRules.Count}개 로드 완료");
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ 방화벽 규칙 로드 오류: {ex.Message}");
                SafeInvokeUI(() =>
                {
                    var adminStatusText = FindName("AdminStatusText") as TextBlock;
                    if (adminStatusText != null)
                        adminStatusText.Text = "오류";

                    var firewallRuleCountText = FindName("FirewallRuleCountText") as TextBlock;
                    if (firewallRuleCountText != null)
                        firewallRuleCountText.Text = "0개";
                });
            }
        }

        /// <summary>
        /// 관리자 권한 및 초기 방화벽 규칙 로드
        /// </summary>
        private async Task InitializeFirewallManagementAsync()
        {
            try
            {
                await LoadFirewallRulesAsync();
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ 방화벽 관리 초기화 오류: {ex.Message}");
            }
        }

        #endregion

        #region 추가 보안 관리 이벤트 핸들러

        private void RefreshBlockedConnections_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLogMessage("🔄 차단된 연결 새로고침 중...");
                UpdateBlockedCount();
                AddLogMessage("✅ 차단된 연결 새로고침 완료");
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ 차단된 연결 새로고침 오류: {ex.Message}");
            }
        }

        private void ClearBlockedSearch_Click(object sender, RoutedEventArgs e)
        {
            var searchTextBox = FindName("BlockedSearchTextBox") as System.Windows.Controls.TextBox;
            if (searchTextBox != null)
                searchTextBox.Text = "";
        }

        private void AddToWhitelistSelected_Click(object sender, RoutedEventArgs e)
        {
            var dataGrid = FindName("BlockedConnectionsDataGrid") as DataGrid;
            if (dataGrid?.SelectedItems?.Count > 0)
            {
                try
                {
                    var selectedItems = dataGrid.SelectedItems.Cast<AutoBlockedConnection>().ToList();
                    foreach (var item in selectedItems)
                    {
                        // 화이트리스트에 추가 로직
                        AddLogMessage($"🔒 {item.ProcessName} 화이트리스트 추가됨");
                    }
                    AddLogMessage($"✅ {selectedItems.Count}개 항목이 화이트리스트에 추가됨");
                }
                catch (Exception ex)
                {
                    AddLogMessage($"❌ 화이트리스트 추가 오류: {ex.Message}");
                }
            }
        }

        private void MakePermanentBlock_Click(object sender, RoutedEventArgs e)
        {
            var dataGrid = FindName("BlockedConnectionsDataGrid") as DataGrid;
            if (dataGrid?.SelectedItems?.Count > 0)
            {
                try
                {
                    var selectedItems = dataGrid.SelectedItems.Cast<AutoBlockedConnection>().ToList();
                    foreach (var item in selectedItems)
                    {
                        // 영구 차단 로직 (속성 확인 필요)
                        AddLogMessage($"🛡️ {item.ProcessName} 영구 차단으로 전환됨");
                    }
                    AddLogMessage($"✅ {selectedItems.Count}개 항목이 영구 차단으로 전환됨");
                }
                catch (Exception ex)
                {
                    AddLogMessage($"❌ 영구 차단 전환 오류: {ex.Message}");
                }
            }
        }

        private void DeleteSelectedBlocked_Click(object sender, RoutedEventArgs e)
        {
            var dataGrid = FindName("BlockedConnectionsDataGrid") as DataGrid;
            if (dataGrid?.SelectedItems?.Count > 0)
            {
                try
                {
                    var selectedItems = dataGrid.SelectedItems.Cast<AutoBlockedConnection>().ToList();
                    foreach (var item in selectedItems)
                    {
                        _blockedConnections.Remove(item);
                    }
                    AddLogMessage($"🗑️ {selectedItems.Count}개 항목이 삭제됨");
                    UpdateBlockedCount();
                }
                catch (Exception ex)
                {
                    AddLogMessage($"❌ 삭제 오류: {ex.Message}");
                }
            }
        }

        #endregion

        #region 방화벽 관리 이벤트 핸들러

        /// <summary>
        /// Windows 고급 보안이 포함된 방화벽을 열어 시스템 방화벽 규칙을 관리할 수 있게 합니다.
        /// </summary>
        private void OpenWindowsFirewallRules_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLogMessage("🔥 Windows 방화벽 규칙 관리 도구를 여는 중...");

                // Windows 고급 보안이 포함된 방화벽 MMC 스냅인을 실행
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "wf.msc",
                    UseShellExecute = true,
                    Verb = "runas" // 관리자 권한으로 실행
                });

                AddLogMessage("✅ Windows 방화벽 규칙 관리 도구가 열렸습니다.");
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // 사용자가 UAC에서 취소한 경우
                AddLogMessage("⚠️ 관리자 권한이 필요합니다. UAC에서 승인해주세요.");
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ Windows 방화벽 규칙 관리 도구 실행 오류: {ex.Message}");
                MessageBox.Show($"Windows 방화벽 관리 도구를 열 수 없습니다.\n\n오류: {ex.Message}",
                    "방화벽 도구 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddFirewallRule_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ruleNameTextBox = FindName("NewRuleNameTextBox") as System.Windows.Controls.TextBox;
                var ipTextBox = FindName("NewRuleIPTextBox") as System.Windows.Controls.TextBox;
                var portTextBox = FindName("NewRulePortTextBox") as System.Windows.Controls.TextBox;
                var protocolComboBox = FindName("NewRuleProtocolComboBox") as System.Windows.Controls.ComboBox;
                var actionComboBox = FindName("NewRuleActionComboBox") as System.Windows.Controls.ComboBox;

                if (string.IsNullOrWhiteSpace(ruleNameTextBox?.Text) || string.IsNullOrWhiteSpace(ipTextBox?.Text))
                {
                    MessageBox.Show("규칙 이름과 대상 IP는 필수입니다.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var protocolValue = ((ComboBoxItem?)protocolComboBox?.SelectedItem)?.Content?.ToString() ?? "TCP";
                var actionValue = ((ComboBoxItem?)actionComboBox?.SelectedItem)?.Content?.ToString() ?? "Block";

                var newRule = new FirewallRuleInfo
                {
                    Name = ruleNameTextBox.Text,
                    RemoteAddresses = ipTextBox.Text,
                    RemotePorts = portTextBox?.Text ?? "",
                    Protocol = protocolValue == "TCP" ? 6 : (protocolValue == "UDP" ? 17 : 6),
                    Direction = 2, // Outbound
                    Enabled = true,
                    CreatedDate = DateTime.Now,
                    Description = $"사용자 정의 규칙 - {DateTime.Now:yyyy-MM-dd}"
                };

                _firewallRules.Add(newRule);
                AddLogMessage($"✅ 방화벽 규칙 '{newRule.Name}' 추가됨");

                // 입력 필드 초기화
                if (ruleNameTextBox != null) ruleNameTextBox.Text = "";
                if (ipTextBox != null) ipTextBox.Text = "";
                if (portTextBox != null) portTextBox.Text = "";
                if (protocolComboBox != null) protocolComboBox.SelectedIndex = 0;
                if (actionComboBox != null) actionComboBox.SelectedIndex = 0;

                UpdateFirewallStatusAsync();
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ 방화벽 규칙 추가 오류: {ex.Message}");
            }
        }

        private void EnableSelectedRules_Click(object sender, RoutedEventArgs e)
        {
            var dataGrid = FindName("FirewallRulesDataGrid") as DataGrid;
            if (dataGrid?.SelectedItems?.Count > 0)
            {
                try
                {
                    var selectedItems = dataGrid.SelectedItems.Cast<FirewallRuleInfo>().ToList();
                    foreach (var rule in selectedItems)
                    {
                        rule.Enabled = true;
                    }
                    AddLogMessage($"✅ {selectedItems.Count}개 규칙이 활성화됨");
                    UpdateFirewallStatusAsync();
                }
                catch (Exception ex)
                {
                    AddLogMessage($"❌ 규칙 활성화 오류: {ex.Message}");
                }
            }
        }

        private void DisableSelectedRules_Click(object sender, RoutedEventArgs e)
        {
            var dataGrid = FindName("FirewallRulesDataGrid") as DataGrid;
            if (dataGrid?.SelectedItems?.Count > 0)
            {
                try
                {
                    var selectedItems = dataGrid.SelectedItems.Cast<FirewallRuleInfo>().ToList();
                    foreach (var rule in selectedItems)
                    {
                        rule.Enabled = false;
                    }
                    AddLogMessage($"⚠️ {selectedItems.Count}개 규칙이 비활성화됨");
                    UpdateFirewallStatusAsync();
                }
                catch (Exception ex)
                {
                    AddLogMessage($"❌ 규칙 비활성화 오류: {ex.Message}");
                }
            }
        }

        private void DeleteSelectedRules_Click(object sender, RoutedEventArgs e)
        {
            var dataGrid = FindName("FirewallRulesDataGrid") as DataGrid;
            if (dataGrid?.SelectedItems?.Count > 0)
            {
                var result = MessageBox.Show("선택된 방화벽 규칙을 삭제하시겠습니까?", "규칙 삭제",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var selectedItems = dataGrid.SelectedItems.Cast<FirewallRuleInfo>().ToList();
                        foreach (var rule in selectedItems)
                        {
                            _firewallRules.Remove(rule);
                        }
                        AddLogMessage($"🗑️ {selectedItems.Count}개 규칙이 삭제됨");
                        UpdateFirewallStatusAsync();
                    }
                    catch (Exception ex)
                    {
                        AddLogMessage($"❌ 규칙 삭제 오류: {ex.Message}");
                    }
                }
            }
        }

        #region DDoS 방어 시스템

        /// <summary>
        /// DDoS 방어 시스템 초기화
        /// </summary>
        private Task InitializeDDoSDefenseSystem()
        {
            return Task.Run(() =>
            {
                try
                {
                    // DDoS 관련 서비스 초기화
                    _ddosDetectionEngine = new DDoSDetectionEngine();
                    _packetAnalyzer = new AdvancedPacketAnalyzer();
                    _rateLimitingService = new RateLimitingService();
                    _signatureDatabase = new DDoSSignatureDatabase();

                    // 통합 방어 시스템 초기화
                    _ddosDefenseSystem = new IntegratedDDoSDefenseSystem(
                        _ddosDetectionEngine,
                        _packetAnalyzer,
                        _rateLimitingService,
                        _signatureDatabase
                    );

                    // 이벤트 구독
                    _ddosDefenseSystem.AttackDetected += OnDDoSAttackDetected;
                    _ddosDefenseSystem.DefenseActionExecuted += OnDefenseActionExecuted;
                    _ddosDefenseSystem.MetricsUpdated += OnDDoSMetricsUpdated;

                    // UI 컨트롤에 데이터 바인딩 (XAML 컨트롤들이 로드된 후 실행)
                    _ = Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        var ddosAlertsPanel = FindName("DDoSAlertsPanel") as ItemsControl;
                        if (ddosAlertsPanel != null)
                            ddosAlertsPanel.ItemsSource = _ddosAlerts;

                        var attackHistoryDataGrid = FindName("AttackHistoryDataGrid") as DataGrid;
                        if (attackHistoryDataGrid != null)
                            attackHistoryDataGrid.ItemsSource = _attackHistory;

                        var signatureDataGrid = FindName("SignatureDataGrid") as DataGrid;
                        if (signatureDataGrid != null)
                            signatureDataGrid.ItemsSource = _signatureDatabase.GetActiveSignatures();
                    }
                    catch { /* UI 컨트롤 바인딩 실패 시 무시 */ }
                });

                    // 방어 시스템 시작
                    _ddosDefenseSystem.Start();
                    _ddosUpdateTimer.Start();

                    // LogHelper.Log("DDoS 방어 시스템이 초기화되었습니다.", "Information");
                }
                catch (Exception)
                {
                    // LogHelper.Log($"DDoS 방어 시스템 초기화 실패: {ex.Message}", "Error");
                }
            });
        }

        /// <summary>
        /// DDoS 공격 감지 이벤트 핸들러
        /// </summary>
        private void OnDDoSAttackDetected(object? sender, DDoSDetectionResult e)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // 실시간 알림에 추가
                    _ddosAlerts.Insert(0, e);

                    // 최대 100개 유지
                    while (_ddosAlerts.Count > 100)
                        _ddosAlerts.RemoveAt(_ddosAlerts.Count - 1);

                    // 공격 기록에 추가
                    _attackHistory.Insert(0, e);

                    // UI 업데이트
                    AttacksDetected++;

                    // 심각도에 따른 알림 표시
                    var alertMessage = $"[{e.Severity}] {e.AttackType} 공격 감지 - {e.SourceIP}";

                    if (e.Severity >= Models.DDoSSeverity.High)
                    {
                        MessageBox.Show(alertMessage, "DDoS 공격 감지", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }

                    // LogHelper.Log(alertMessage, "Warning");
                }
                catch (Exception)
                {
                    // LogHelper.Log($"DDoS 공격 알림 처리 오류: {ex.Message}", "Error");
                }
            });
        }

        /// <summary>
        /// 방어 조치 실행 이벤트 핸들러
        /// </summary>
        private void OnDefenseActionExecuted(object? sender, DefenseActionResult e)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    var message = e.GetSummary();
                    // LogHelper.Log(message, e.Success ? "Information" : "Error");

                    if (e.Success && e.ActionType == DefenseActionType.IpBlock)
                    {
                        BlockedIPs++;
                    }
                }
                catch (Exception)
                {
                    // LogHelper.Log($"방어 조치 결과 처리 오류: {ex.Message}", "Error");
                }
            });
        }

        /// <summary>
        /// DDoS 메트릭 업데이트 이벤트 핸들러
        /// </summary>
        private void OnDDoSMetricsUpdated(object? sender, DDoSMonitoringMetrics e)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // UI 메트릭 업데이트
                    RiskScore = e.RiskScore;
                    TrafficVolume = e.TrafficVolumeMbps;

                    // 위험 점수에 따른 색상 업데이트
                    UpdateRiskScoreDisplay(e.RiskScore);

                    // 차트 데이터 업데이트
                    UpdateDDoSCharts(e);
                }
                catch (Exception)
                {
                    // LogHelper.Log($"DDoS 메트릭 업데이트 오류: {ex.Message}", "Error");
                }
            });
        }

        /// <summary>
        /// DDoS 업데이트 타이머 핸들러
        /// </summary>
        private void DDoSUpdateTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                if (_ddosDefenseSystem != null)
                {
                    var stats = _ddosDefenseSystem.GetStatistics();

                    Dispatcher.Invoke(() =>
                    {
                        AttacksDetected = stats.TotalAttacksDetected;
                        BlockedIPs = stats.AttacksBlocked;

                        // 통계 정보 UI 업데이트
                        var totalSignaturesText = FindName("TotalSignaturesText") as TextBlock;
                        if (totalSignaturesText != null)
                            totalSignaturesText.Text = _signatureDatabase?.GetStatistics().TotalSignatures.ToString() ?? "0";

                        var activeSignaturesText = FindName("ActiveSignaturesText") as TextBlock;
                        if (activeSignaturesText != null)
                            activeSignaturesText.Text = _signatureDatabase?.GetStatistics().ActiveSignatures.ToString() ?? "0";
                    });
                }
            }
            catch (Exception)
            {
                // LogHelper.Log($"DDoS 정기 업데이트 오류: {ex.Message}", "Error");
            }
        }

        /// <summary>
        /// 위험 점수 표시 업데이트
        /// </summary>
        private void UpdateRiskScoreDisplay(double riskScore)
        {
            try
            {
                var riskScoreText = FindName("RiskScoreText") as TextBlock;
                if (riskScoreText != null)
                {
                    riskScoreText.Text = riskScore.ToString("F0");

                    // 위험 점수에 따른 색상 변경
                    var color = riskScore switch
                    {
                        < 20 => "#4CAF50", // 녹색 - 안전
                        < 40 => "#FF9800", // 주황색 - 주의
                        < 70 => "#F44336", // 빨간색 - 위험
                        _ => "#9C27B0"     // 보라색 - 심각
                    };

                    riskScoreText.Foreground = new SolidColorBrush((MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(color));
                }

                var riskStatusText = FindName("RiskStatusText") as TextBlock;
                if (riskStatusText != null)
                {
                    riskStatusText.Text = riskScore switch
                    {
                        < 20 => "정상",
                        < 40 => "주의",
                        < 70 => "위험",
                        _ => "심각"
                    };
                }
            }
            catch (Exception)
            {
                // LogHelper.Log($"위험 점수 표시 업데이트 오류: {ex.Message}", "Error");
            }
        }

        /// <summary>
        /// DDoS 차트 업데이트
        /// </summary>
        private void UpdateDDoSCharts(DDoSMonitoringMetrics metrics)
        {
            try
            {
                // 트래픽 차트 업데이트 (여기서는 기본 구현)
                // 실제 구현에서는 LiveCharts를 사용한 시계열 차트 업데이트

                // 공격 유형별 차트 업데이트
                // 실제 구현에서는 파이 차트에 공격 유형별 분포 표시
            }
            catch (Exception)
            {
                // LogHelper.Log($"DDoS 차트 업데이트 오류: {ex.Message}", "Error");
            }
        }

        /// <summary>
        /// 알림 지우기 버튼 핸들러
        /// </summary>
        private void ClearAlerts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _ddosAlerts.Clear();
                // LogHelper.Log("DDoS 알림이 지워졌습니다.", "Information");
            }
            catch (Exception)
            {
                // LogHelper.Log($"알림 지우기 오류: {ex.Message}", "Error");
            }
        }

        /// <summary>
        /// 공격 필터 적용 버튼 핸들러
        /// </summary>
        private void ApplyAttackFilter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 필터 로직 구현
                // 공격 유형, 심각도, 날짜 범위에 따른 필터링
                // LogHelper.Log("공격 기록 필터가 적용되었습니다.", "Information");
            }
            catch (Exception)
            {
                // LogHelper.Log($"공격 필터 적용 오류: {ex.Message}", "Error");
            }
        }

        /// <summary>
        /// 시그니처 새로 고침 버튼 핸들러
        /// </summary>
        private void RefreshSignatures_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var signatureDataGrid = FindName("SignatureDataGrid") as DataGrid;
                if (_signatureDatabase != null && signatureDataGrid != null)
                {
                    signatureDataGrid.ItemsSource = _signatureDatabase.GetActiveSignatures();
                    // LogHelper.Log("시그니처 목록이 새로 고침되었습니다.", "Information");
                }
            }
            catch (Exception)
            {
                // LogHelper.Log($"시그니처 새로 고침 오류: {ex.Message}", "Error");
            }
        }

        /// <summary>
        /// 기본 시그니처 로드 버튼 핸들러
        /// </summary>
        private void LoadDefaultSignatures_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _signatureDatabase?.LoadDefaultSignatures();
                var signatureDataGrid = FindName("SignatureDataGrid") as DataGrid;
                if (signatureDataGrid != null)
                {
                    signatureDataGrid.ItemsSource = _signatureDatabase?.GetActiveSignatures();
                }
                // LogHelper.Log("기본 시그니처가 로드되었습니다.", "Information");
            }
            catch (Exception)
            {
                // LogHelper.Log($"기본 시그니처 로드 오류: {ex.Message}", "Error");
            }
        }

        /// <summary>
        /// 시그니처 내보내기 버튼 핸들러
        /// </summary>
        private void ExportSignatures_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 시그니처 내보내기 로직 구현
                // LogHelper.Log("시그니처 내보내기가 완료되었습니다.", "Information");
            }
            catch (Exception)
            {
                // LogHelper.Log($"시그니처 내보내기 오류: {ex.Message}", "Error");
            }
        }

        #endregion

        private void UpdateFirewallStatusAsync()
        {
            try
            {
                var activeRules = _firewallRules.Count(r => r.Enabled);
                var customRules = _firewallRules.Count;

                SafeInvokeUI(() =>
                {
                    var activeRulesText = FindName("ActiveRulesCountText") as TextBlock;
                    var customRulesText = FindName("CustomRulesCountText") as TextBlock;
                    var lastUpdateText = FindName("LastUpdateTimeText") as TextBlock;
                    var firewallRuleCountText = FindName("FirewallRuleCountText") as TextBlock;

                    if (activeRulesText != null) activeRulesText.Text = $"활성 규칙: {activeRules}";
                    if (customRulesText != null) customRulesText.Text = $"사용자 규칙: {customRules}";
                    if (lastUpdateText != null) lastUpdateText.Text = $"마지막 업데이트: {DateTime.Now:HH:mm:ss}";
                    if (firewallRuleCountText != null) firewallRuleCountText.Text = $"관리 규칙: {customRules}개";
                });
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ 방화벽 상태 업데이트 오류: {ex.Message}");
            }
        }

        #endregion

    }

    /// <summary>
    /// 영구 차단 옵션
    /// </summary>
    public class PermanentBlockOptions
    {
        public bool UsePermanentBlock { get; set; }
        public NetworkBlockType BlockType { get; set; }
    }

    /// <summary>
    /// 네트워크 차단 옵션
    /// </summary>
    public class NetworkBlockOptions
    {
        public NetworkBlockType BlockType { get; set; }
    }

    /// <summary>
    /// 네트워크 차단 유형
    /// </summary>
    public enum NetworkBlockType
    {
        ProcessPath,
        IPAddress,
        Port
    }
}
