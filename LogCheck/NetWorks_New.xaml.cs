using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
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
    public partial class NetWorks_New : Page, INavigable, INotifyPropertyChanged
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
        private readonly ObservableCollection<string> _logMessages;

        // AutoBlock 시스템
        private readonly IAutoBlockService _autoBlockService;
        private readonly AutoBlockStatisticsService _autoBlockStats;
        private readonly ObservableCollection<AutoBlockedConnection> _blockedConnections;
        private readonly ObservableCollection<AutoWhitelistEntry> _whitelistEntries;
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


        private readonly NotifyIcon _notifyIcon;
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
            _logMessages = new ObservableCollection<string>();
            _chartSeries = new ObservableCollection<ISeries>();
            _chartXAxes = new ObservableCollection<Axis>();
            _chartYAxes = new ObservableCollection<Axis>();

            // AutoBlock 컬렉션 초기화
            _blockedConnections = new ObservableCollection<AutoBlockedConnection>();
            _whitelistEntries = new ObservableCollection<AutoWhitelistEntry>();

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

            SecurityAlertsControl.ItemsSource = _securityAlerts;
            LogMessagesControl.ItemsSource = _logMessages;
            NetworkActivityChart.Series = _chartSeries;
            NetworkActivityChart.XAxes = _chartXAxes;
            NetworkActivityChart.YAxes = _chartYAxes;

            // DataContext 설정 (바인딩을 위해)
            this.DataContext = this;

            // 이벤트 구독
            SubscribeToEvents();
            SubscribeToAutoBlockEvents();

            // UI 초기화
            InitializeUI();

            // 타이머 설정
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _updateTimer.Tick += UpdateTimer_Tick;

            // _notifyIcon 초기화
            _notifyIcon = new NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Information,
                Visible = true
            };

            // 트레이 메뉴 추가
            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            // 로그 파일 생성 비활성화로 인한 로그 열기 메뉴 주석 처리
            /*
            contextMenu.Items.Add("로그 열기", null, (s, e) =>
            {
                try
                {
                    if (File.Exists(_logFilePath))
                    {
                        System.Diagnostics.Process.Start("notepad.exe", _logFilePath);
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("아직 로그 파일이 없습니다.");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"로그 열기 오류: {ex.Message}");
                }
            });
            */
            contextMenu.Items.Add("종료", null, (s, e) =>
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                System.Windows.Application.Current.Shutdown();
            });
            _notifyIcon.ContextMenuStrip = contextMenu;

            // 로그 메시지 추가
            AddLogMessage("네트워크 보안 모니터링 시스템 초기화 완료");

            // ProcessTreeNode 상태 관리 시스템 초기화 (작업 관리자 방식)
            ProcessTreeNode.ClearExpandedStates(); // 이전 세션 상태 초기화 (선택적)
            System.Diagnostics.Debug.WriteLine("[NetWorks_New] ProcessTreeNode 상태 관리 시스템 초기화됨");

            // 앱 종료 시 트레이 아이콘/타이머 정리 (종료 보장)
            System.Windows.Application.Current.Exit += (_, __) =>
            {
                try { _updateTimer?.Stop(); } catch { }
                try { _notifyIcon.Visible = false; } catch { }
                try { _notifyIcon.Dispose(); } catch { }
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

            this.Unloaded += (_, __) =>
            {
                try { _updateTimer?.Stop(); } catch { }
                try { _notifyIcon.Visible = false; } catch { }
                try { _notifyIcon.Dispose(); } catch { }
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

                Dispatcher.Invoke(() =>
                {
                    try
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
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"UI 업데이트 중 오류: {ex.Message}");
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
                AddLogMessage("AutoBlock 시스템이 초기화되었습니다.");

                // System Idle Process 자동 화이트리스트 추가
                await EnsureSystemIdleProcessWhitelistAsync();

                // 초기 통계 및 데이터 로드
                await LoadAutoBlockDataAsync();
            }
            catch (Exception ex)
            {
                AddLogMessage($"AutoBlock 시스템 초기화 실패: {ex.Message}");
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
                        AddLogMessage("✅ System Idle Process가 자동으로 화이트리스트에 추가되었습니다.");
                    }
                    else
                    {
                        AddLogMessage("⚠️ System Idle Process 화이트리스트 추가 실패");
                    }
                }
                else
                {
                    AddLogMessage("ℹ️ System Idle Process가 이미 화이트리스트에 등록되어 있습니다.");
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
            if (SecurityAlertsControl != null)
                SecurityAlertsControl.ItemsSource = _securityAlerts;

            if (LogMessagesControl != null)
                LogMessagesControl.ItemsSource = _logMessages;

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
            try
            {
                AddLogMessage("데이터 새로고침 중...");

                if (_isMonitoring)
                {
                    // 현재 데이터 새로고침
                    var data = await _processNetworkMapper.GetProcessNetworkDataAsync();
                    await UpdateProcessNetworkDataAsync(data);
                }
                else
                {
                    // 모니터링이 중지된 상태에서 새로고침
                    var data = await _processNetworkMapper.GetProcessNetworkDataAsync();
                    await UpdateProcessNetworkDataAsync(data);
                }

                AddLogMessage("데이터 새로고침이 완료되었습니다.");
            }
            catch (Exception ex)
            {
                AddLogMessage($"새로고침 오류: {ex.Message}");
                MessageBox.Show($"데이터 새로고침 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    var result = MessageBox.Show(
                        $"프로세스 '{connection.ProcessName}' (PID: {connection.ProcessId})의 네트워크 연결을 차단하시겠습니까?\n\n" +
                        $"연결 정보: {connection.RemoteAddress}:{connection.RemotePort} ({connection.Protocol})",
                        "연결 차단 확인",
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
                            MessageBox.Show("연결 차단이 완료되었습니다.\n\nAutoBlock 통계에 기록되었습니다.", "성공", MessageBoxButton.OK, MessageBoxImage.Information);

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

        // 트레이 알림 (BalloonTip) 표시 함수
        private void ShowTrayNotification(string message)
        {
            // NotifyIcon 객체 생성
            using (var notifyIcon = new NotifyIcon())
            {
                notifyIcon.Icon = System.Drawing.SystemIcons.Information;  // 아이콘 설정 (정보 아이콘)
                notifyIcon.Visible = true;  // 아이콘 표시

                // 트레이 알림 표시
                notifyIcon.BalloonTipTitle = "네트워크 보안 알림";
                notifyIcon.BalloonTipText = message;
                notifyIcon.ShowBalloonTip(3000);  // 3초 동안 표시

                // 잠시 대기 후 트레이 아이콘 제거
                System.Threading.Tasks.Task.Delay(3000).ContinueWith(t => notifyIcon.Dispose());
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
                                MessageBox.Show("프로세스 종료가 완료되었습니다.", "성공", MessageBoxButton.OK, MessageBoxImage.Information);
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

            // System Idle Process 완전 제외 (실수로 종료되는 것 방지)
            data = data.Where(p => !IsSystemIdleProcess(p)).ToList();
            System.Diagnostics.Debug.WriteLine($"[NetWorks_New] System Idle Process 제외 후 데이터 개수: {data.Count}");

            // IsSystem 자동 판단
            foreach (var item in data)
            {
                item.IsSystem = IsSystemProcess(item.ProcessName, item.ProcessId);
            }

            var general = data.Where(p => !p.IsSystem).ToList();
            var system = data.Where(p => p.IsSystem).ToList(); // System Idle Process는 이미 data에서 제외됨

            System.Diagnostics.Debug.WriteLine($"[NetWorks_New] 일반 프로세스: {general.Count}개, 시스템 프로세스: {system.Count}개");

            try
            {
                // 애플리케이션이 종료 중인지 확인
                if (System.Windows.Application.Current?.Dispatcher?.HasShutdownStarted == true)
                    return;

                // UI가 아직 유효한지 확인
                if (Dispatcher.HasShutdownStarted)
                    return;

                await Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"[NetWorks_New] UI 업데이트 시작 - 기존 일반 프로세스: {_generalProcessData.Count}개, 시스템 프로세스: {_systemProcessData.Count}개");

                        // 스마트 업데이트: 컬렉션을 완전히 지우지 않고 업데이트
                        UpdateCollectionSmart(_generalProcessData, general);
                        UpdateCollectionSmart(_systemProcessData, system);

                        // PID별 그룹화된 데이터 업데이트 (기존)
                        UpdateProcessGroups(_generalProcessGroups, general);
                        UpdateProcessGroups(_systemProcessGroups, system);

                        // 작업 관리자 방식의 TreeView 업데이트 (새로운 방식)
                        UpdateProcessTreeSmart(_processTreeNodes, general);
                        UpdateProcessTreeSmart(_systemProcessTreeNodes, system);

                        System.Diagnostics.Debug.WriteLine($"[NetWorks_New] UI 업데이트 완료 - 새로운 일반 프로세스: {_generalProcessData.Count}개, 시스템 프로세스: {_systemProcessData.Count}개");
                        System.Diagnostics.Debug.WriteLine($"[NetWorks_New] 그룹 업데이트 완료 - 일반 그룹: {_generalProcessGroups.Count}개, 시스템 그룹: {_systemProcessGroups.Count}개");

                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"UI 업데이트 중 오류: {ex.Message}");
                    }
                }, DispatcherPriority.DataBind);

                // 간단한 상태 복원 시도
                _ = Task.Delay(100).ContinueWith(_ =>
                {
                    Dispatcher.BeginInvoke(() => RestoreGroupStates(), DispatcherPriority.Background);
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

            UpdateStatistics(data);
            UpdateChart(data);
            _ = Task.Run(async () =>
            {
                try
                {
                    var alerts = await _securityAnalyzer.AnalyzeConnectionsAsync(data);

                    // 애플리케이션이 종료 중인지 확인
                    if (System.Windows.Application.Current?.Dispatcher?.HasShutdownStarted == true)
                        return;

                    // UI가 아직 유효한지 확인
                    if (Dispatcher.HasShutdownStarted)
                        return;

                    await Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            UpdateSecurityAlerts(alerts);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"보안 알림 업데이트 중 오류: {ex.Message}");
                        }
                    });
                }
                catch (TaskCanceledException)
                {
                    // 종료 시 발생하는 TaskCanceledException은 무시
                    System.Diagnostics.Debug.WriteLine("보안 분석 Task: TaskCanceledException 발생 (정상 종료 과정)");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"보안 분석 Task 예외: {ex.Message}");
                }
            });
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

                // 프로퍼티를 통해 업데이트하여 자동으로 UI가 갱신되도록 함
                TotalConnections = data.Count;
                LowRiskCount = data.Count(x => x.RiskLevel == SecurityRiskLevel.Low);
                MediumRiskCount = data.Count(x => x.RiskLevel == SecurityRiskLevel.Medium);
                HighRiskCount = data.Count(x => x.RiskLevel == SecurityRiskLevel.High);
                TcpCount = data.Count(x => x.Protocol == "TCP");
                UdpCount = data.Count(x => x.Protocol == "UDP");
                IcmpCount = data.Count(x => x.Protocol == "ICMP");
                _totalDataTransferred = data.Sum(x => x.DataTransferred);

                // TotalDataTransferred는 계산된 프로퍼티이므로 수동으로 알림
                OnPropertyChanged(nameof(TotalDataTransferred));

                // UI 업데이트
                if (ActiveConnectionsText != null)
                    ActiveConnectionsText.Text = TotalConnections.ToString();
                if (DangerousConnectionsText != null)
                    DangerousConnectionsText.Text = (HighRiskCount + data.Count(x => x.RiskLevel == SecurityRiskLevel.Critical)).ToString();

                // 통계 표시 업데이트
                UpdateStatisticsDisplay();
            }
            catch (Exception ex)
            {
                AddLogMessage($"통계 업데이트 오류: {ex.Message}");
            }
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
                // ProcessNetworkInfo의 경우 객체 참조가 달라서 간단히 Clear & Add 방식 사용
                // 하지만 CollectionView를 완전히 리셋하지 않도록 하나씩 처리

                // 기존 방식보다 부드럽게 업데이트
                if (collection.Count == 0)
                {
                    // 빈 컬렉션이면 그냥 추가
                    foreach (var item in newItems)
                    {
                        collection.Add(item);
                    }
                }
                else
                {
                    // 하나씩 제거하고 추가하여 UI 깜빡임 최소화
                    collection.Clear();
                    foreach (var item in newItems)
                    {
                        collection.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"스마트 업데이트 실패: {ex.Message}");
                collection.Clear();
                foreach (var item in newItems)
                {
                    collection.Add(item);
                }
            }
        }

        /// <summary>
        /// 차트 업데이트
        /// </summary>
        private void UpdateChart(List<ProcessNetworkInfo> data)
        {
            try
            {
                if (_chartSeries.Count > 0 && _chartSeries[0] is LineSeries<double> lineSeries)
                {
                    data ??= new List<ProcessNetworkInfo>();
                    var chartData = new List<double>();
                    var currentTime = DateTime.Now;

                    // 최근 12개 시간대의 데이터 생성 (2시간 간격)
                    for (int i = 0; i < 12; i++)
                    {
                        var timeSlot = currentTime.AddHours(-22 + (i * 2));
                        var hourData = data.Count(x =>
                            Math.Abs((x.ConnectionStartTime - timeSlot).TotalHours) < 1);

                        // Y축 설정에 맞는 범위로 제한 (0-25)
                        var normalizedData = Math.Max(0, Math.Min(25, hourData));
                        chartData.Add(normalizedData);
                    }

                    // UI 스레드에서 차트 업데이트
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            lineSeries.Values = chartData;

                            // X축 레이블도 실시간으로 업데이트
                            if (_chartXAxes.Count > 0)
                            {
                                var timeLabels = new List<string>();
                                for (int i = 0; i < 12; i++)
                                {
                                    var timeSlot = currentTime.AddHours(-22 + (i * 2));
                                    timeLabels.Add(timeSlot.ToString("HH"));
                                }
                                _chartXAxes[0].Labels = timeLabels;
                            }
                        }
                        catch (Exception uiEx)
                        {
                            AddLogMessage($"차트 UI 업데이트 오류: {uiEx.Message}");
                        }
                    });
                }
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
        /// 로그 메시지 추가
        /// </summary>
        private void AddLogMessage(string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                var logMessage = $"[{timestamp}] {message}";

                // UI에 추가
                Dispatcher.InvokeAsync(() =>
                {
                    _logMessages.Add(logMessage);

                    // 로그 메시지가 너무 많아지면 오래된 것 제거
                    while (_logMessages.Count > 100)
                    {
                        _logMessages.RemoveAt(0);
                    }
                });
                // 파일 로그 생성 비활성화
                // File.AppendAllText(_logFilePath, logMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // 로그 추가 실패 시 콘솔에 출력
                System.Diagnostics.Debug.WriteLine($"로그 메시지 추가 실패: {ex.Message}");
            }
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
                            MessageBox.Show($"그룹 차단이 완료되었습니다.\n\n차단된 연결: {blockedCount}개\nAutoBlock 통계 기록: {autoBlockedCount}개", "성공", MessageBoxButton.OK, MessageBoxImage.Information);

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
                // TreeView 방식: Button의 Tag에서 ProcessTreeNode 가져오기
                var button = sender as System.Windows.Controls.Button;
                var processNode = button?.Tag as ProcessTreeNode;

                if (processNode != null)
                {
                    var result = MessageBox.Show(
                        $"프로세스 '{processNode.ProcessName}' (PID: {processNode.ProcessId})을(를) 강제 종료하시겠습니까?\n\n" +
                        $"이 작업은 되돌릴 수 없으며, 해당 프로세스의 모든 연결({processNode.Connections.Count}개)이 함께 종료됩니다.\n" +
                        $"시스템 프로세스인 경우 시스템 불안정을 야기할 수 있습니다.",
                        "프로세스 종료 확인",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        try
                        {
                            var process = System.Diagnostics.Process.GetProcessById(processNode.ProcessId);

                            string processInfo = $"프로세스명: {process.ProcessName}, PID: {process.Id}, 시작시간: {process.StartTime}";

                            process.Kill();
                            process.WaitForExit(5000); // 5초 대기

                            AddLogMessage($"프로세스 종료 성공 - {processInfo}");

                            // UI 새로고침
                            await RefreshProcessData();
                        }
                        catch (ArgumentException)
                        {
                            AddLogMessage($"프로세스 종료 실패: PID {processNode.ProcessId}에 해당하는 프로세스를 찾을 수 없습니다.");
                        }
                        catch (System.ComponentModel.Win32Exception ex)
                        {
                            AddLogMessage($"프로세스 종료 실패: 권한이 부족하거나 시스템에서 보호하는 프로세스입니다. ({ex.Message})");
                            MessageBox.Show("프로세스 종료 권한이 부족합니다. 관리자 권한으로 실행하세요.", "권한 부족", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"그룹 프로세스 종료 오류: {ex.Message}");
                MessageBox.Show($"프로세스 종료 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
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

                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
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

        /// <summary>
        /// AutoBlock 테스트 버튼 클릭
        /// </summary>
        private async void TestAutoBlock_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLogMessage("🧪 AutoBlock 시스템 테스트를 시작합니다...");

                var testResults = new List<string>();

                // 1. System Idle Process 위장 테스트
                AddLogMessage("1️⃣ System Idle Process 위장 탐지 테스트 중...");
                var forgeryTests = AutoBlockTestHelper.GetSystemIdleProcessForgeryTests();
                foreach (var testCase in forgeryTests)
                {
                    var result = await _autoBlockService.AnalyzeConnectionAsync(testCase);
                    var message = $"   {testCase.ProcessName} (PID:{testCase.ProcessId}) → {result.Level} ({result.ConfidenceScore:P1})";
                    testResults.Add(message);
                    AddLogMessage(message);
                }

                // 2. 의심스러운 포트 테스트
                AddLogMessage("2️⃣ 의심스러운 포트 탐지 테스트 중...");
                var portTests = AutoBlockTestHelper.GetSuspiciousPortTests();
                foreach (var testCase in portTests)
                {
                    var result = await _autoBlockService.AnalyzeConnectionAsync(testCase);
                    var message = $"   {testCase.ProcessName}:{testCase.RemotePort} → {result.Level} ({result.ConfidenceScore:P1})";
                    testResults.Add(message);
                    AddLogMessage(message);
                }

                // 3. 정상 연결 테스트 (허용되어야 함)
                AddLogMessage("3️⃣ 정상 연결 테스트 중...");
                var legitimateTests = AutoBlockTestHelper.GetLegitimateTests();
                foreach (var testCase in legitimateTests)
                {
                    var result = await _autoBlockService.AnalyzeConnectionAsync(testCase);
                    var message = $"   {testCase.ProcessName} → {result.Level} ({result.ConfidenceScore:P1})";
                    testResults.Add(message);
                    AddLogMessage(message);
                }

                // 4. 정상적인 System Idle Process 테스트
                AddLogMessage("4️⃣ 정상적인 System Idle Process 테스트 중...");
                var legitimateIdleTests = AutoBlockTestHelper.GetLegitimateSystemIdleProcessTests();
                foreach (var testCase in legitimateIdleTests)
                {
                    var result = await _autoBlockService.AnalyzeConnectionAsync(testCase);
                    var message = $"   정상 System Idle Process (PID:{testCase.ProcessId}) → {result.Level} ({result.ConfidenceScore:P1})";
                    testResults.Add(message);
                    AddLogMessage(message);
                }

                // 데이터 새로고침
                await LoadAutoBlockDataAsync();

                AddLogMessage($"✅ AutoBlock 테스트 완료! 총 {testResults.Count}건 테스트됨");

                // 테스트 결과 요약 다이얼로그
                var summary = string.Join("\n", testResults);
                MessageBox.Show(
                    $"AutoBlock 테스트가 완료되었습니다!\n\n테스트 결과:\n{summary.Substring(0, Math.Min(500, summary.Length))}...\n\n자세한 결과는 로그를 확인하세요.",
                    "AutoBlock 테스트 완료",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ AutoBlock 테스트 실행 오류: {ex.Message}");
                MessageBox.Show($"테스트 실행 중 오류가 발생했습니다:\n{ex.Message}", "테스트 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region AbuseIPDB AutoBlock Testing

        private async void TestAutoBlockWithAbuseIP_Click(object sender, RoutedEventArgs e)
        {
            if (!_isMonitoring)
            {
                MessageBox.Show("모니터링이 시작되지 않았습니다. 먼저 모니터링을 시작하세요.",
                    "모니터링 필요", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                "AbuseIPDB의 실제 악성 IP에 연결을 시도하여 AutoBlock 기능을 테스트합니다.\n" +
                "이 작업은 실제 보안 위협 IP와 통신을 시도하므로 주의가 필요합니다.\n\n" +
                "계속하시겠습니까?",
                "AbuseIPDB AutoBlock 테스트",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            // UI 버튼 참조
            var button = sender as System.Windows.Controls.Button;
            AbuseIPTestService? abuseService = null;

            try
            {
                AddLogMessage("🔍 AbuseIPDB AutoBlock 테스트 시작...");

                // UI 버튼 비활성화
                if (button != null)
                {
                    button.IsEnabled = false;
                    button.Content = "테스트 진행중...";
                }

                // AbuseIPDB 서비스 초기화 (API 키는 선택사항, 없어도 알려진 악성 IP 사용)
                abuseService = new AbuseIPTestService("");

                AddLogMessage("📡 AbuseIPDB에서 의심스러운 IP 목록 조회 중...");
                var suspiciousIPs = await abuseService.GetSuspiciousIPsAsync(3);

                // ⭐ 중요: BlockRuleEngine에 AbuseIPDB IP들을 악성 목록에 추가
                BlockRuleEngine.AddMaliciousIPs(suspiciousIPs);
                AddLogMessage($"🛡️ {suspiciousIPs.Count}개 악성 IP가 차단 목록에 추가되었습니다."); if (!suspiciousIPs.Any())
                {
                    AddLogMessage("⚠️ 의심스러운 IP를 가져올 수 없습니다. 알려진 악성 IP를 사용합니다.");
                }

                AddLogMessage($"🎯 테스트 대상 IP: {string.Join(", ", suspiciousIPs)}");

                var testResults = new List<string>();
                var totalTests = suspiciousIPs.Count * 3; // IP당 3개 포트 테스트
                var completedTests = 0;

                // 각 IP에 대해 테스트 수행
                foreach (var ip in suspiciousIPs)
                {
                    AddLogMessage($"🔄 {ip} 연결 테스트 시작");

                    // IP 정보 조회
                    var ipInfo = await abuseService.CheckIPAsync(ip);
                    AddLogMessage($"📊 {ip} - 위험도: {ipInfo.AbuseConfidencePercentage}%, 국가: {ipInfo.CountryCode}");

                    // 통계 기록 전 상태 저장
                    var statsBefore = await _autoBlockStats.GetCurrentStatisticsAsync();

                    // 다양한 포트로 연결 시도
                    var testPorts = new[] { 80, 443, 22 };
                    foreach (var port in testPorts)
                    {
                        completedTests++;
                        if (button != null)
                            button.Content = $"테스트 진행중... ({completedTests}/{totalTests})";

                        var connectionResult = await TestSingleIPConnection(ip, port);
                        testResults.Add($"{ip}:{port} - {connectionResult}");

                        // 각 연결 시도 후 잠시 대기 (패킷 캡처 및 분석 시간 확보)
                        await Task.Delay(2000);
                    }

                    // 통계 변화 확인
                    await Task.Delay(1000); // 통계 업데이트 대기
                    var statsAfter = await _autoBlockStats.GetCurrentStatisticsAsync();

                    if (statsAfter.TotalBlocked > statsBefore.TotalBlocked)
                    {
                        var blockedCount = statsAfter.TotalBlocked - statsBefore.TotalBlocked;
                        AddLogMessage($"✅ {ip} 테스트로 {blockedCount}개 연결이 차단되었습니다!");
                        testResults.Add($"🛡️ {ip} → {blockedCount}개 연결 차단됨");
                    }
                    else
                    {
                        AddLogMessage($"⚠️ {ip} 테스트에서 차단된 연결이 감지되지 않았습니다.");
                        testResults.Add($"⚪ {ip} → 차단 감지 안됨");
                    }

                    AddLogMessage($"✅ {ip} 테스트 완료");
                }

                // 최종 통계 업데이트
                UpdateStatisticsDisplay();

                // 테스트 결과 요약
                var summary = string.Join("\n", testResults);
                AddLogMessage("🎉 AbuseIPDB AutoBlock 테스트 완료!");
                AddLogMessage($"📈 테스트 결과:\n{summary}");

                MessageBox.Show(
                    $"AbuseIPDB AutoBlock 테스트가 완료되었습니다!\n\n" +
                    $"테스트 결과:\n{summary}\n\n" +
                    $"자세한 결과는 로그를 확인하세요.",
                    "테스트 완료",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ AbuseIPDB 테스트 실행 오류: {ex.Message}");
                MessageBox.Show($"테스트 실행 중 오류가 발생했습니다:\n{ex.Message}",
                    "테스트 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // UI 버튼 복원
                if (button != null)
                {
                    button.IsEnabled = true;
                    button.Content = "AutoBlock 테스트";
                }

                // AbuseIPDB 서비스 정리
                abuseService?.Dispose();
            }
        }

        private async Task<string> TestSingleIPConnection(string ip, int port)
        {
            try
            {
                AddLogMessage($"🔌 연결 시도: {ip}:{port}");

                using var client = new System.Net.Sockets.TcpClient();
                var connectTask = client.ConnectAsync(ip, port);

                // 10초 타임아웃으로 충분한 패킷 캡처 시간 확보
                if (await Task.WhenAny(connectTask, Task.Delay(10000)) == connectTask)
                {
                    if (client.Connected)
                    {
                        AddLogMessage($"✅ 연결 성공: {ip}:{port}");

                        // 실제 데이터 송신으로 더 많은 트래픽 생성
                        try
                        {
                            var stream = client.GetStream();
                            var data = System.Text.Encoding.UTF8.GetBytes("GET / HTTP/1.1\r\nHost: test\r\nUser-Agent: LogCheck-AutoBlockTest/1.0\r\n\r\n");
                            await stream.WriteAsync(data, 0, data.Length);

                            // 응답 읽기 시도
                            var buffer = new byte[1024];
                            stream.ReadTimeout = 3000;
                            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                            if (bytesRead > 0)
                            {
                                AddLogMessage($"📦 응답 수신: {bytesRead} bytes");
                            }

                            // 연결 유지로 더 많은 패킷 생성
                            await Task.Delay(3000);
                        }
                        catch (Exception dataEx)
                        {
                            AddLogMessage($"⚠️ 데이터 송수신 오류: {dataEx.Message}");
                        }

                        return "연결 성공";
                    }
                }

                AddLogMessage($"⏱️ 연결 타임아웃: {ip}:{port}");
                return "타임아웃";
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ 연결 실패: {ip}:{port} - {ex.Message}");
                return $"실패: {ex.Message}";
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
                    if (BlockedConnectionsDataGrid != null)
                    {
                        BlockedConnectionsDataGrid.ItemsSource = _blockedConnections;
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
            if (BlockedCountText != null)
            {
                BlockedCountText.Text = selectedCount > 0
                    ? $"총 {totalCount}개 (선택됨: {selectedCount}개)"
                    : $"총 {totalCount}개 차단됨";
            }

            // 요약 정보 업데이트
            var today = _blockedConnections.Count(x => x.BlockedAt.Date == DateTime.Today);
            var manual = _blockedConnections.Count(x => x.Reason.Contains("사용자") || x.Reason.Contains("Manual"));
            var auto = _blockedConnections.Count(x => !x.Reason.Contains("사용자") && !x.Reason.Contains("Manual"));

            if (BlockedSummaryText != null)
            {
                BlockedSummaryText.Text = $"오늘 {today}개 차단됨 | 수동: {manual}개, 자동: {auto}개";
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
                if (BlockedFilterComboBox == null || BlockedSearchTextBox == null || BlockedConnectionsDataGrid == null)
                    return;

                var filterItem = BlockedFilterComboBox.SelectedItem as ComboBoxItem;
                var filterText = filterItem?.Content?.ToString() ?? "전체 보기";
                var searchText = BlockedSearchTextBox.Text?.Trim().ToLower() ?? "";

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

                if (BlockedConnectionsDataGrid != null)
                {
                    BlockedConnectionsDataGrid.ItemsSource = tempCollection;
                }

                if (BlockedCountText != null)
                {
                    BlockedCountText.Text = $"총 {tempCollection.Count}개 (전체: {_blockedConnections.Count}개)";
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

        #region INotifyPropertyChanged Implementation

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

    }
}
