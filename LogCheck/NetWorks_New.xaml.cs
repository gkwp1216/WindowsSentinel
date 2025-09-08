using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using LogCheck.Models;
using LogCheck.Services;
using MaterialDesignThemes.Wpf;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives; // Popup 사용시
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrayNotify;
using Controls = System.Windows.Controls;
using MediaColor = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;
using SecurityAlert = LogCheck.Services.SecurityAlert;
using MediaBrushes = System.Windows.Media.Brushes;

namespace LogCheck
{
    /// <summary>
    /// NetWorks_New.xaml에 대한 상호작용 논리
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class NetWorks_New : Page, INavigable
    {
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

        private readonly NotifyIcon _notifyIcon;

        public NetWorks_New()
        {
            InitializeComponent();

            // XAML 컨트롤은 자동으로 바인딩됨

            // 서비스 초기화
            _processNetworkMapper = new ProcessNetworkMapper();
            _connectionManager = new NetworkConnectionManager();
            _securityAnalyzer = new RealTimeSecurityAnalyzer();

            // 데이터 컬렉션 초기화
            _processNetworkData = new ObservableCollection<ProcessNetworkInfo>();
            _securityAlerts = new ObservableCollection<SecurityAlert>();
            _logMessages = new ObservableCollection<string>();

            // 차트 초기화
            _chartSeries = new ObservableCollection<ISeries>();
            _chartXAxes = new ObservableCollection<Axis>();
            _chartYAxes = new ObservableCollection<Axis>();

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

            //_notifyIcon 초기화
            _notifyIcon = new NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Information,
                Visible = true
            };

            // 로그 메시지 추가
            AddLogMessage("네트워크 보안 모니터링 시스템 초기화 완료");
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
        }

        /// <summary>
        /// UI 초기화
        /// </summary>
        private void InitializeUI()
        {
            // DataGrid 바인딩
            ProcessNetworkDataGrid.ItemsSource = _processNetworkData;

            // 보안 경고 컨트롤 바인딩
            SecurityAlertsControl.ItemsSource = _securityAlerts;

            // 로그 메시지 컨트롤 바인딩
            LogMessagesControl.ItemsSource = _logMessages;

            // 차트 바인딩
            NetworkActivityChart.Series = _chartSeries;
            NetworkActivityChart.XAxes = _chartXAxes;
            NetworkActivityChart.YAxes = _chartYAxes;

            // 네트워크 인터페이스 초기화
            InitializeNetworkInterfaces();

            // 차트 초기화
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
                NetworkInterfaceComboBox.Items.Add("모든 인터페이스");
                NetworkInterfaceComboBox.Items.Add("이더넷");
                NetworkInterfaceComboBox.Items.Add("Wi-Fi");
                NetworkInterfaceComboBox.SelectedIndex = 0;
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

                // 모니터링 시작
                await _processNetworkMapper.StartMonitoringAsync();
                _isMonitoring = true;

                // UI 상태 업데이트
                StartMonitoringButton.Visibility = Visibility.Collapsed;
                StopMonitoringButton.Visibility = Visibility.Visible;
                MonitoringStatusText.Text = "모니터링 중";
                MonitoringStatusText2.Text = "모니터링 중";
                MonitoringStatusIndicator.Fill = new SolidColorBrush(Colors.Green);

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

                // 모니터링 중지
                await _processNetworkMapper.StopMonitoringAsync();
                _isMonitoring = false;

                // UI 상태 업데이트
                StartMonitoringButton.Visibility = Visibility.Visible;
                StopMonitoringButton.Visibility = Visibility.Collapsed;
                MonitoringStatusText.Text = "대기 중";
                MonitoringStatusText2.Text = "대기 중";
                MonitoringStatusIndicator.Fill = new SolidColorBrush(Colors.Gray);

                // 타이머 중지
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
                var selectedItem = ProtocolFilterComboBox.SelectedItem as Controls.ComboBoxItem;
                if (selectedItem == null) return;

                var protocol = selectedItem.Content.ToString();
                var view = CollectionViewSource.GetDefaultView(_processNetworkData);

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
                var selectedItem = ProcessNetworkDataGrid.SelectedItem as ProcessNetworkInfo;
                if (selectedItem != null)
                {
                    // 선택된 항목에 대한 상세 정보 표시 (필요시 구현)
                    AddLogMessage($"선택됨: {selectedItem.ProcessName} (PID: {selectedItem.ProcessId}) - {selectedItem.RemoteAddress}:{selectedItem.RemotePort}");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"선택 변경 오류: {ex.Message}");
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
                    var result = MessageBox.Show(
                        $"프로세스 '{connection.ProcessName}' (PID: {connection.ProcessId})을(를) 강제 종료하시겠습니까?\n\n" +
                        "⚠️ 주의: 이 작업은 데이터 손실을 야기할 수 있습니다.",
                        "프로세스 종료 확인",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        AddLogMessage($"프로세스 종료 시작: {connection.ProcessName} (PID: {connection.ProcessId})");

                        // 프로세스 종료 (실제로는 NetworkConnectionManager에서 처리)
                        var success = await _connectionManager.DisconnectProcessAsync(
                            connection.ProcessId, 
                            "사용자 요청 - 프로세스 종료");

                        if (success)
                        {
                            AddLogMessage("프로세스 종료가 완료되었습니다.");
                            MessageBox.Show("프로세스 종료가 완료되었습니다.", "성공", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            AddLogMessage("프로세스 종료에 실패했습니다.");
                            MessageBox.Show("프로세스 종료에 실패했습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
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
                if (_isMonitoring)
                {
                    // 주기적으로 데이터 업데이트
                    var data = await _processNetworkMapper.GetProcessNetworkDataAsync();
                    await UpdateProcessNetworkDataAsync(data);
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"타이머 업데이트 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 프로세스-네트워크 데이터 업데이트
        /// </summary>
        private async Task UpdateProcessNetworkDataAsync(List<ProcessNetworkInfo> data)
        {
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    // 데이터 컬렉션 업데이트
                    _processNetworkData.Clear();
                    foreach (var item in data)
                    {
                        _processNetworkData.Add(item);
                    }

                    // 통계 업데이트
                    UpdateStatistics(data);

                    // 차트 업데이트
                    UpdateChart(data);

                    // 보안 분석 실행
                    _ = Task.Run(async () =>
                    {
                        var alerts = await _securityAnalyzer.AnalyzeConnectionsAsync(data);
                        await Dispatcher.InvokeAsync(() =>
                        {
                            UpdateSecurityAlerts(alerts);
                        });
                    });
                });
            }
            catch (Exception ex)
            {
                AddLogMessage($"데이터 업데이트 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 통계 업데이트
        /// </summary>
        private void UpdateStatistics(List<ProcessNetworkInfo> data)
        {
            try
            {
                _totalConnections = data.Count;
                _lowRiskCount = data.Count(x => x.RiskLevel == SecurityRiskLevel.Low);
                _mediumRiskCount = data.Count(x => x.RiskLevel == SecurityRiskLevel.Medium);
                _highRiskCount = data.Count(x => x.RiskLevel == SecurityRiskLevel.High);
                _tcpCount = data.Count(x => x.Protocol == "TCP");
                _udpCount = data.Count(x => x.Protocol == "UDP");
                _icmpCount = data.Count(x => x.Protocol == "ICMP");
                _totalDataTransferred = data.Sum(x => x.DataTransferred);

                // UI 업데이트
                ActiveConnectionsText.Text = _totalConnections.ToString();
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
        /// 차트 업데이트
        /// </summary>
        private void UpdateChart(List<ProcessNetworkInfo> data)
        {
            try
            {
                // 간단한 차트 업데이트 (실제로는 더 정교한 구현 필요)
                if (_chartSeries.Count > 0 && _chartSeries[0] is LineSeries<double> lineSeries)
                {
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

                Dispatcher.InvokeAsync(() =>
                {
                    _logMessages.Add(logMessage);
                    
                    // 로그 메시지가 너무 많아지면 오래된 것 제거
                    while (_logMessages.Count > 100)
                    {
                        _logMessages.RemoveAt(0);
                    }
                });
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
                _updateTimer?.Stop();
                _ = _processNetworkMapper?.StopMonitoringAsync();
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
            Dispatcher.Invoke(() =>
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
            });
        }
    }
}
