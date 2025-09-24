using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LogCheck.Models;
using LogCheck.Services;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives; // Popup 사용시
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
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
    public partial class NetWorks_New : Page, INavigable
    {
        private ToggleButton _selectedButton;

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

        // 통계 데이터
        private int _totalConnections = 0;
        private int _lowRiskCount = 0;
        private int _mediumRiskCount = 0;
        private int _highRiskCount = 0;
        private int _tcpCount = 0;
        private int _udpCount = 0;
        private int _icmpCount = 0;
        private long _totalDataTransferred = 0;

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

            // 서비스 초기화
            // 전역 허브의 인스턴스를 사용하여 중복 실행 방지
            var hub = MonitoringHub.Instance;
            _processNetworkMapper = hub.ProcessMapper;
            _connectionManager = new NetworkConnectionManager();
            _securityAnalyzer = new RealTimeSecurityAnalyzer();
            _captureService = hub.Capture;

            // 로그 파일 경로 설정 비활성화
            // _logFilePath = System.IO.Path.Combine(
            //     AppDomain.CurrentDomain.BaseDirectory,
            //     @"..\..\..\monitoring_log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt"
            // );

            // XAML 로드 (이 시점에 SelectionChanged가 발생해도 컬렉션은 준비됨)
            InitializeComponent();

            // TreeView 바인딩
            if (ProcessTreeView != null)
                ProcessTreeView.ItemsSource = _processTreeNodes;

            // 기존 데이터 바인딩
            GeneralProcessDataGrid.ItemsSource = _generalProcessData;
            SystemProcessDataGrid.ItemsSource = _systemProcessData;

            SecurityAlertsControl.ItemsSource = _securityAlerts;
            LogMessagesControl.ItemsSource = _logMessages;
            NetworkActivityChart.Series = _chartSeries;
            NetworkActivityChart.XAxes = _chartXAxes;
            NetworkActivityChart.YAxes = _chartYAxes;

            // 이벤트 구독
            SubscribeToEvents();

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
                MonitoringStatusText2.Text = "모니터링 중";
                MonitoringStatusIndicator.Fill = new SolidColorBrush(Colors.Green);
                // 새로 추가된 런타임 구성 요약 갱신
                UpdateRuntimeConfigText();
                _updateTimer.Start();
            }

            // 허브 이벤트 구독
            SubscribeHub();
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
                        if (MonitoringStatusText2 != null)
                            MonitoringStatusText2.Text = MonitoringStatusText?.Text ?? "";
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
                // 샘플 데이터로 차트 초기화
                var sampleData = new List<double> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                var sampleLabels = new List<string> { "00", "02", "04", "06", "08", "10", "12", "14", "16", "18", "20", "22" };

                var lineSeries = new LineSeries<double>
                {
                    Values = sampleData,
                    Name = "네트워크 활동",
                    Stroke = new SolidColorPaint(SKColors.Blue, 2),
                    Fill = new SolidColorPaint(SKColors.Blue.WithAlpha(50))
                };

                _chartSeries.Add(lineSeries);

                _chartXAxes.Add(new Axis
                {
                    Labels = sampleLabels,
                    LabelsRotation = 0
                });

                _chartYAxes.Add(new Axis
                {
                    Name = "활동 수준",
                    MinStep = 1
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
                MonitoringStatusText2.Text = "모니터링 중";
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
                MonitoringStatusText2.Text = "대기 중";
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

                        var success = await _connectionManager.DisconnectProcessAsync(
                            connection.ProcessId,
                            "사용자 요청 - 보안 위협 탐지");

                        if (success)
                        {
                            AddLogMessage("연결 차단이 완료되었습니다.");
                            MessageBox.Show("연결 차단이 완료되었습니다.", "성공", MessageBoxButton.OK, MessageBoxImage.Information);

                            // NotifyIcon 사용하여 트레이 알림
                            ShowTrayNotification($"연결 차단 완료: {connection.ProcessName} - {connection.RemoteAddress}:{connection.RemotePort}");
                        }
                        else
                        {
                            AddLogMessage("연결 차단에 실패했습니다.");
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
                    if (MonitoringStatusText2 != null) MonitoringStatusText2.Text = $"모니터링 중 ({pps} pps)";

                    // 주기적으로 데이터 업데이트
                    System.Diagnostics.Debug.WriteLine("[NetWorks_New] 프로세스 데이터 가져오기 시작");
                    var data = await _processNetworkMapper.GetProcessNetworkDataAsync();
                    System.Diagnostics.Debug.WriteLine($"[NetWorks_New] 프로세스 데이터 가져오기 완료: {data?.Count ?? 0}개");
                    await UpdateProcessNetworkDataAsync(data ?? new List<ProcessNetworkInfo>());
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

            // IsSystem 자동 판단
            foreach (var item in data)
            {
                item.IsSystem = IsSystemProcess(item.ProcessName, item.ProcessId);
            }

            var general = data.Where(p => !p.IsSystem).ToList();
            var system = data.Where(p => p.IsSystem).ToList();

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
        /// 통계 업데이트
        /// </summary>
        private void UpdateStatistics(List<ProcessNetworkInfo> data)
        {
            try
            {
                data ??= new List<ProcessNetworkInfo>();

                _totalConnections = data.Count;
                _lowRiskCount = data.Count(x => x.RiskLevel == SecurityRiskLevel.Low);
                _mediumRiskCount = data.Count(x => x.RiskLevel == SecurityRiskLevel.Medium);
                _highRiskCount = data.Count(x => x.RiskLevel == SecurityRiskLevel.High);
                _tcpCount = data.Count(x => x.Protocol == "TCP");
                _udpCount = data.Count(x => x.Protocol == "UDP");
                _icmpCount = data.Count(x => x.Protocol == "ICMP");
                _totalDataTransferred = data.Sum(x => x.DataTransferred);

                // UI 업데이트
                if (ActiveConnectionsText != null)
                    ActiveConnectionsText.Text = _totalConnections.ToString();
                if (DangerousConnectionsText != null)
                    DangerousConnectionsText.Text = (_highRiskCount + data.Count(x => x.RiskLevel == SecurityRiskLevel.Critical)).ToString();

                // DataContext 업데이트 (실제로는 INotifyPropertyChanged 구현 필요)
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
                // 간단한 차트 업데이트 (실제로는 더 정교한 구현 필요)
                if (_chartSeries.Count > 0 && _chartSeries[0] is LineSeries<double> lineSeries)
                {
                    data ??= new List<ProcessNetworkInfo>();
                    var chartData = new List<double>();
                    var currentHour = DateTime.Now.Hour;

                    // 24시간 데이터 시뮬레이션
                    for (int i = 0; i < 12; i++)
                    {
                        var hour = (currentHour - 11 + i + 24) % 24;
                        var hourData = data.Count(x => x.ConnectionStartTime.Hour == hour);
                        chartData.Add(hourData);
                    }

                    lineSeries.Values = chartData;
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
                        foreach (var connection in processNode.Connections.ToList())
                        {
                            try
                            {
                                // 임시로 간단한 차단 로직 구현 (실제로는 방화벽 규칙 추가)
                                connection.IsBlocked = true;
                                connection.BlockedTime = DateTime.Now;
                                connection.BlockReason = "사용자가 그룹 단위로 차단";
                                blockedCount++;
                            }
                            catch (Exception ex)
                            {
                                AddLogMessage($"연결 차단 실패 ({connection.RemoteAddress}:{connection.RemotePort}): {ex.Message}");
                            }
                        }

                        AddLogMessage($"프로세스 그룹 '{processNode.ProcessName}'에서 {blockedCount}개 연결을 차단했습니다.");

                        if (blockedCount > 0)
                        {
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

    }
}
