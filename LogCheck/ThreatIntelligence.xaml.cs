using LogCheck.Models;
using LogCheck.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LogCheck
{
    public partial class ThreatIntelligence : Page, INavigable
    {
        private readonly AbuseIPDBClient _abuseIPDBClient;
        private readonly RealTimeIPBlocker _ipBlocker;
        private readonly NetworkConnectionManager _connectionManager;
        private readonly ObservableCollection<BlockedIPAddress> _blockedIPs;
        private readonly ObservableCollection<string> _logMessages;
        private ThreatLookupResult? _currentThreatResult;
        private BlockedIPAddress? _selectedBlockedIP;

        public ThreatIntelligence()
        {
            InitializeComponent();

            // 서비스 초기화
            _abuseIPDBClient = new AbuseIPDBClient();
            _connectionManager = new NetworkConnectionManager();
            _ipBlocker = new RealTimeIPBlocker(_abuseIPDBClient, _connectionManager);

            // 컬렉션 초기화
            _blockedIPs = new ObservableCollection<BlockedIPAddress>();
            _logMessages = new ObservableCollection<string>();

            // UI 바인딩
            BlockedIPsDataGrid.ItemsSource = _blockedIPs;
            LogMessagesControl.ItemsSource = _logMessages;

            // 이벤트 구독
            SubscribeToEvents();

            // 초기 데이터 로드
            InitializeData();

            // 페이지 언로드 시 리소스 정리
            this.Unloaded += (_, __) =>
            {
                try
                {
                    // 이벤트 구독 해제
                    _abuseIPDBClient.ErrorOccurred -= OnAbuseIPDBError;
                    _abuseIPDBClient.ThreatDataReceived -= OnThreatDataReceived;
                    _ipBlocker.IPBlocked -= OnIPBlocked;
                    _ipBlocker.IPUnblocked -= OnIPUnblocked;
                    _ipBlocker.ErrorOccurred -= OnIPBlockerError;
                    _ipBlocker.ThreatDetected -= OnThreatDetected;

                    // 타이머 보유 객체 해제
                    (_ipBlocker as IDisposable)?.Dispose();
                }
                catch { }
            };
        }

        private void SubscribeToEvents()
        {
            _abuseIPDBClient.ErrorOccurred += OnAbuseIPDBError;
            _abuseIPDBClient.ThreatDataReceived += OnThreatDataReceived;
            _ipBlocker.IPBlocked += OnIPBlocked;
            _ipBlocker.IPUnblocked += OnIPUnblocked;
            _ipBlocker.ErrorOccurred += OnIPBlockerError;
            _ipBlocker.ThreatDetected += OnThreatDetected;
        }

        private void InitializeData()
        {
            try
            {
                // 차단된 IP 목록 로드
                LoadBlockedIPs();

                // 통계 업데이트
                UpdateStatistics();

                // API 키 상태 확인
                UpdateApiKeyStatus();

                // 자동 차단 상태 확인
                UpdateAutoBlockStatus();

                AddLogMessage("위협 정보 관리 시스템이 초기화되었습니다.");
            }
            catch (Exception ex)
            {
                AddLogMessage($"초기화 중 오류 발생: {ex.Message}");
            }
        }

        private void LoadBlockedIPs()
        {
            try
            {
                var blockedIPs = _ipBlocker.GetBlockedIPs();
                _blockedIPs.Clear();

                foreach (var blockedIP in blockedIPs)
                {
                    _blockedIPs.Add(blockedIP);
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"차단된 IP 목록 로드 중 오류: {ex.Message}");
            }
        }

        private void UpdateStatistics()
        {
            try
            {
                var (total, active, expired) = _ipBlocker.GetBlockStatistics();

                TotalBlockedText.Text = total.ToString();
                ActiveBlockedText.Text = active.ToString();
                ExpiredBlockedText.Text = expired.ToString();
                ThresholdText.Text = _ipBlocker.AutoBlockThreshold.ToString();
            }
            catch (Exception ex)
            {
                AddLogMessage($"통계 업데이트 중 오류: {ex.Message}");
            }
        }

        private void UpdateApiKeyStatus()
        {
            try
            {
                var status = _abuseIPDBClient.GetApiKeyStatus();
                ApiKeyStatusText.Text = $"API 키: {status}";

                if (_abuseIPDBClient.IsConfigured)
                {
                    ApiKeyStatusText.Foreground = new SolidColorBrush(Colors.Green);
                }
                else
                {
                    ApiKeyStatusText.Foreground = new SolidColorBrush(Colors.Orange);
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"API 키 상태 확인 중 오류: {ex.Message}");
            }
        }

        private void UpdateAutoBlockStatus()
        {
            try
            {
                var isEnabled = _ipBlocker.IsAutoBlockingEnabled;
                AutoBlockToggleButton.Content = isEnabled ? "자동차단 ON" : "자동차단 OFF";

                if (isEnabled)
                {
                    AutoBlockToggleButton.Background = new SolidColorBrush(Colors.Green);
                }
                else
                {
                    AutoBlockToggleButton.Background = new SolidColorBrush(Colors.Gray);
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"자동 차단 상태 확인 중 오류: {ex.Message}");
            }
        }

        #region 이벤트 핸들러

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowLoading(true);
                AddLogMessage("데이터를 새로고침하고 있습니다...");

                await Task.Delay(1000); // UI 업데이트를 위한 지연

                LoadBlockedIPs();
                UpdateStatistics();
                UpdateApiKeyStatus();

                AddLogMessage("데이터 새로고침이 완료되었습니다.");
            }
            catch (Exception ex)
            {
                AddLogMessage($"새로고침 중 오류 발생: {ex.Message}");
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private void AutoBlockToggleButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var newState = !_ipBlocker.IsAutoBlockingEnabled;
                _ipBlocker.IsAutoBlockingEnabled = newState;

                UpdateAutoBlockStatus();

                var message = newState ? "자동 IP 차단이 활성화되었습니다." : "자동 IP 차단이 비활성화되었습니다.";
                AddLogMessage(message);
            }
            catch (Exception ex)
            {
                AddLogMessage($"자동 차단 상태 변경 중 오류: {ex.Message}");
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 설정 다이얼로그 표시 (필요시 구현)
                AddLogMessage("설정 기능은 향후 구현 예정입니다.");
            }
            catch (Exception ex)
            {
                AddLogMessage($"설정 열기 중 오류: {ex.Message}");
            }
        }

        private void IPSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 실시간 검색 기능 (필요시 구현)
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ipAddress = IPSearchBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(ipAddress))
                {
                    System.Windows.MessageBox.Show("IP 주소를 입력해주세요.", "입력 오류", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                ShowLoading(true);
                AddLogMessage($"IP {ipAddress}의 위협 정보를 조회하고 있습니다...");

                var threatResult = await _ipBlocker.CheckAndBlockIPAsync(ipAddress);
                _currentThreatResult = threatResult;

                // 상세 정보 업데이트
                UpdateThreatDetails(threatResult);

                var message = threatResult.IsThreat
                    ? $"IP {ipAddress}는 위협 IP입니다. (점수: {threatResult.ThreatScore})"
                    : $"IP {ipAddress}는 안전합니다.";

                AddLogMessage(message);

                if (threatResult.IsBlocked)
                {
                    AddLogMessage($"IP {ipAddress}가 자동으로 차단되었습니다.");
                    LoadBlockedIPs();
                    UpdateStatistics();
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"IP 검색 중 오류 발생: {ex.Message}");
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private void BlockedIPsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                _selectedBlockedIP = BlockedIPsDataGrid.SelectedItem as BlockedIPAddress;

                if (_selectedBlockedIP != null)
                {
                    // 선택된 IP의 상세 정보 표시
                    UpdateBlockedIPDetails(_selectedBlockedIP);
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"선택된 IP 정보 표시 중 오류: {ex.Message}");
            }
        }

        private async void BlockIPButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentThreatResult == null)
                {
                    System.Windows.MessageBox.Show("먼저 IP 주소를 검색해주세요.", "알림", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    return;
                }

                var ipAddress = _currentThreatResult.IPAddress;
                var reason = $"수동 차단: {_currentThreatResult.ThreatDescription}";

                ShowLoading(true);
                AddLogMessage($"IP {ipAddress}를 차단하고 있습니다...");

                var success = await _ipBlocker.BlockIPAddressAsync(ipAddress, reason);

                if (success)
                {
                    AddLogMessage($"IP {ipAddress}가 성공적으로 차단되었습니다.");
                    LoadBlockedIPs();
                    UpdateStatistics();
                }
                else
                {
                    AddLogMessage($"IP {ipAddress} 차단에 실패했습니다.");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"IP 차단 중 오류 발생: {ex.Message}");
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private async void UnblockIPButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedBlockedIP == null)
                {
                    System.Windows.MessageBox.Show("차단 해제할 IP를 선택해주세요.", "알림", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    return;
                }

                var ipAddress = _selectedBlockedIP.IPAddress;

                var result = System.Windows.MessageBox.Show($"IP {ipAddress}의 차단을 해제하시겠습니까?", "확인",
                    System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    ShowLoading(true);
                    AddLogMessage($"IP {ipAddress}의 차단을 해제하고 있습니다...");

                    var success = await _ipBlocker.UnblockIPAddressAsync(ipAddress);

                    if (success)
                    {
                        AddLogMessage($"IP {ipAddress}의 차단이 해제되었습니다.");
                        LoadBlockedIPs();
                        UpdateStatistics();
                    }
                    else
                    {
                        AddLogMessage($"IP {ipAddress} 차단 해제에 실패했습니다.");
                    }
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"IP 차단 해제 중 오류 발생: {ex.Message}");
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private async void ReportIPButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentThreatResult == null)
                {
                    System.Windows.MessageBox.Show("먼저 IP 주소를 검색해주세요.", "알림", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    return;
                }

                var ipAddress = _currentThreatResult.IPAddress;

                var result = System.Windows.MessageBox.Show($"IP {ipAddress}를 AbuseIPDB에 신고하시겠습니까?", "확인",
                    System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    ShowLoading(true);
                    AddLogMessage($"IP {ipAddress}를 신고하고 있습니다...");

                    // 기본 카테고리 ID (악성 소프트웨어)
                    var categoryId = 1;
                    var comment = "WindowsSentinel에서 자동 신고";

                    var success = await _abuseIPDBClient.ReportIPAsync(ipAddress, categoryId, comment);

                    if (success)
                    {
                        AddLogMessage($"IP {ipAddress} 신고가 완료되었습니다.");
                    }
                    else
                    {
                        AddLogMessage($"IP {ipAddress} 신고에 실패했습니다.");
                    }
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"IP 신고 중 오류 발생: {ex.Message}");
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private async void SaveApiKeyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var apiKey = ApiKeyPasswordBox.Password.Trim();

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    System.Windows.MessageBox.Show("API 키를 입력해주세요.", "입력 오류", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                ShowLoading(true);
                AddLogMessage("API 키를 설정하고 있습니다...");

                _abuseIPDBClient.Configure(apiKey);

                await Task.Delay(500); // UI 업데이트를 위한 지연

                UpdateApiKeyStatus();
                ApiKeyPasswordBox.Password = string.Empty;

                AddLogMessage("API 키가 성공적으로 설정되었습니다.");
            }
            catch (Exception ex)
            {
                AddLogMessage($"API 키 설정 중 오류 발생: {ex.Message}");
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private void ThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                var threshold = (int)e.NewValue;
                ThresholdValueText.Text = threshold.ToString();
                _ipBlocker.AutoBlockThreshold = threshold;

                AddLogMessage($"자동 차단 임계값이 {threshold}로 변경되었습니다.");
            }
            catch (Exception ex)
            {
                AddLogMessage($"임계값 변경 중 오류: {ex.Message}");
            }
        }

        #endregion

        #region 서비스 이벤트 핸들러

        private void OnAbuseIPDBError(object sender, string error)
        {
            Dispatcher.Invoke(() =>
            {
                AddLogMessage($"AbuseIPDB 오류: {error}");
            });
        }

        private void OnThreatDataReceived(object sender, ThreatIntelligenceData threatData)
        {
            Dispatcher.Invoke(() =>
            {
                AddLogMessage($"새로운 위협 정보 수신: {threatData.IPAddress} (점수: {threatData.AbuseConfidenceScore})");
            });
        }

        private void OnIPBlocked(object sender, BlockedIPAddress blockedIP)
        {
            Dispatcher.Invoke(() =>
            {
                AddLogMessage($"IP {blockedIP.IPAddress}가 차단되었습니다: {blockedIP.Reason}");
                LoadBlockedIPs();
                UpdateStatistics();
            });
        }

        private void OnIPUnblocked(object sender, string ipAddress)
        {
            Dispatcher.Invoke(() =>
            {
                AddLogMessage($"IP {ipAddress}의 차단이 해제되었습니다.");
                LoadBlockedIPs();
                UpdateStatistics();
            });
        }

        private void OnIPBlockerError(object sender, string error)
        {
            Dispatcher.Invoke(() =>
            {
                AddLogMessage($"IP 차단 시스템 오류: {error}");
            });
        }

        private void OnThreatDetected(object sender, ThreatLookupResult threatResult)
        {
            Dispatcher.Invoke(() =>
            {
                AddLogMessage($"새로운 위협 탐지: {threatResult.IPAddress} (점수: {threatResult.ThreatScore})");
            });
        }

        #endregion

        #region UI 업데이트 메서드

        private void UpdateThreatDetails(ThreatLookupResult threatResult)
        {
            try
            {
                DetailIPText.Text = threatResult.IPAddress;
                DetailThreatScoreText.Text = threatResult.ThreatScore.ToString();

                var threatLevel = GetThreatLevelText(threatResult.ThreatScore);
                DetailThreatLevelText.Text = threatLevel;

                // 위협 점수에 따른 색상 변경
                var color = GetThreatLevelColor(threatResult.ThreatScore);
                DetailThreatLevelText.Foreground = color;
                DetailThreatScoreText.Foreground = color;

                // 기타 정보는 AbuseIPDB에서 받아온 경우에만 표시
                if (threatResult.Source == "AbuseIPDB")
                {
                    // 실제 구현에서는 ThreatIntelligenceData에서 가져와야 함
                    DetailCountryText.Text = "정보 없음";
                    DetailISPText.Text = "정보 없음";
                    DetailCategoriesText.Text = string.Join(", ", threatResult.Categories);
                }
                else
                {
                    DetailCountryText.Text = "-";
                    DetailISPText.Text = "-";
                    DetailCategoriesText.Text = "-";
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"위협 정보 상세 업데이트 중 오류: {ex.Message}");
            }
        }

        private void UpdateBlockedIPDetails(BlockedIPAddress blockedIP)
        {
            try
            {
                DetailIPText.Text = blockedIP.IPAddress;
                DetailThreatScoreText.Text = blockedIP.ThreatScore.ToString();
                DetailThreatLevelText.Text = "차단됨";
                DetailThreatLevelText.Foreground = new SolidColorBrush(Colors.Red);
                DetailCountryText.Text = "-";
                DetailISPText.Text = "-";
                DetailCategoriesText.Text = string.Join(", ", blockedIP.Categories);
            }
            catch (Exception ex)
            {
                AddLogMessage($"차단된 IP 상세 정보 업데이트 중 오류: {ex.Message}");
            }
        }

        private string GetThreatLevelText(int threatScore)
        {
            return threatScore switch
            {
                0 => "안전",
                <= 25 => "낮음",
                <= 50 => "보통",
                <= 75 => "높음",
                _ => "매우 높음"
            };
        }

        private System.Windows.Media.Brush GetThreatLevelColor(int threatScore)
        {
            return threatScore switch
            {
                0 => new SolidColorBrush(Colors.Green),
                <= 25 => new SolidColorBrush(Colors.LightGreen),
                <= 50 => new SolidColorBrush(Colors.Orange),
                <= 75 => new SolidColorBrush(Colors.Red),
                _ => new SolidColorBrush(Colors.Purple)
            };
        }

        private void ShowLoading(bool show)
        {
            LoadingOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AddLogMessage(string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                var logMessage = $"[{timestamp}] {message}";

                _logMessages.Add(logMessage);

                // 로그 메시지가 너무 많아지면 오래된 것 제거
                while (_logMessages.Count > 100)
                {
                    _logMessages.RemoveAt(0);
                }
            }
            catch (Exception ex)
            {
                // 로그 추가 중 오류가 발생해도 무시
            }
        }

        #endregion

        #region INavigable 인터페이스 구현

        public void OnNavigatedTo()
        {
            try
            {
                // 페이지 진입 시 데이터 새로고침
                LoadBlockedIPs();
                UpdateStatistics();
                UpdateApiKeyStatus();
                UpdateAutoBlockStatus();

                AddLogMessage("위협 정보 관리 페이지에 진입했습니다.");
            }
            catch (Exception ex)
            {
                AddLogMessage($"페이지 진입 중 오류: {ex.Message}");
            }
        }

        public void OnNavigatedFrom()
        {
            try
            {
                AddLogMessage("위협 정보 관리 페이지를 떠났습니다.");
            }
            catch (Exception ex)
            {
                // 오류 무시
            }
        }

        public void Shutdown()
        {
            try
            {
                // 리소스 정리
                _abuseIPDBClient?.Dispose();
                _ipBlocker?.Dispose();

                AddLogMessage("위협 정보 관리 시스템이 종료되었습니다.");
            }
            catch (Exception ex)
            {
                AddLogMessage($"시스템 종료 중 오류: {ex.Message}");
            }
        }

        #endregion
    }
}
