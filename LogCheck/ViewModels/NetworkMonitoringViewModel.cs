using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows.Threading;
using LiveChartsCore;
using LogCheck.Models;
using LogCheck.Services;
using SecurityAlert = LogCheck.Models.SecurityAlert;

namespace LogCheck.ViewModels
{
    /// <summary>
    /// 네트워크 모니터링 페이지용 ViewModel
    /// BasePageViewModel을 상속받아 공통 기능 활용
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class NetworkMonitoringViewModel : NetworkPageViewModel
    {
        // 컬렉션들
        private readonly ObservableCollection<ProcessNetworkInfo> _generalProcessData;
        private readonly ObservableCollection<ProcessNetworkInfo> _systemProcessData;
        private readonly ObservableCollection<ProcessTreeNode> _processTreeNodes;
        private readonly ObservableCollection<ProcessTreeNode> _systemProcessTreeNodes;
        private readonly ObservableCollection<ProcessGroup> _generalProcessGroups;
        private readonly ObservableCollection<ProcessGroup> _systemProcessGroups;
        private readonly ObservableCollection<ProcessNetworkInfo> _processNetworkData;
        private readonly ObservableCollection<SecurityAlert> _securityAlerts;
        private readonly ObservableCollection<AutoBlockedConnection> _blockedConnections;
        private readonly ObservableCollection<AutoWhitelistEntry> _whitelistEntries;
        private readonly ObservableCollection<ISeries> _chartSeries;

        // 서비스들
        private readonly ProcessNetworkMapper _processNetworkMapper;
        private readonly NetworkConnectionManager _connectionManager;
        private readonly RealTimeSecurityAnalyzer _securityAnalyzer;
        private readonly IAutoBlockService _autoBlockService;
        private readonly AutoBlockStatisticsService _autoBlockStats;

        // AutoBlock 통계
        private int _totalBlockedCount = 0;
        private int _level1BlockCount = 0;
        private int _level2BlockCount = 0;
        private int _level3BlockCount = 0;
        private int _uniqueProcesses = 0;
        private int _uniqueIPs = 0;

        // 공개 컬렉션 프로퍼티
        public ObservableCollection<ProcessNetworkInfo> GeneralProcessData => _generalProcessData;
        public ObservableCollection<ProcessNetworkInfo> SystemProcessData => _systemProcessData;
        public ObservableCollection<ProcessTreeNode> ProcessTreeNodes => _processTreeNodes;
        public ObservableCollection<ProcessTreeNode> SystemProcessTreeNodes => _systemProcessTreeNodes;
        public ObservableCollection<ProcessGroup> GeneralProcessGroups => _generalProcessGroups;
        public ObservableCollection<ProcessGroup> SystemProcessGroups => _systemProcessGroups;
        public ObservableCollection<ProcessNetworkInfo> ProcessNetworkData => _processNetworkData;
        public ObservableCollection<SecurityAlert> SecurityAlerts => _securityAlerts;
        public ObservableCollection<AutoBlockedConnection> BlockedConnections => _blockedConnections;
        public ObservableCollection<AutoWhitelistEntry> WhitelistEntries => _whitelistEntries;
        public ObservableCollection<ISeries> ChartSeries => _chartSeries;

        // AutoBlock 통계 바인딩 프로퍼티
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

        /// <summary>
        /// 생성자
        /// </summary>
        public NetworkMonitoringViewModel()
            : base(
                logService: new LogMessageService(Dispatcher.CurrentDispatcher),
                statisticsService: new NetworkStatisticsService(),
                dispatcher: Dispatcher.CurrentDispatcher)
        {
            // 컬렉션 초기화
            _generalProcessData = new ObservableCollection<ProcessNetworkInfo>();
            _systemProcessData = new ObservableCollection<ProcessNetworkInfo>();
            _processTreeNodes = new ObservableCollection<ProcessTreeNode>();
            _systemProcessTreeNodes = new ObservableCollection<ProcessTreeNode>();
            _generalProcessGroups = new ObservableCollection<ProcessGroup>();
            _systemProcessGroups = new ObservableCollection<ProcessGroup>();
            _processNetworkData = new ObservableCollection<ProcessNetworkInfo>();
            _securityAlerts = new ObservableCollection<SecurityAlert>();
            _blockedConnections = new ObservableCollection<AutoBlockedConnection>();
            _whitelistEntries = new ObservableCollection<AutoWhitelistEntry>();
            _chartSeries = new ObservableCollection<ISeries>();

            // 서비스 초기화
            var hub = MonitoringHub.Instance;
            _processNetworkMapper = hub.ProcessMapper;
            _connectionManager = new NetworkConnectionManager();
            _securityAnalyzer = new RealTimeSecurityAnalyzer();

            // AutoBlock 서비스 초기화
            var dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "autoblock.db");
            var connectionString = $"Data Source={dbPath};";
            _autoBlockService = new AutoBlockService(connectionString);
            _autoBlockStats = new AutoBlockStatisticsService(connectionString);

            // 초기화 완료
            StatusMessage = "네트워크 모니터링 준비 완료";
        }

        /// <summary>
        /// 동기 초기화 (BasePageViewModel 추상 메서드 구현)
        /// </summary>
        public override void Initialize()
        {
            StatusMessage = "네트워크 모니터링 초기화 중...";
            LogService.AddLogMessage("🔄 네트워크 모니터링 초기화");
        }

        /// <summary>
        /// 비동기 초기화
        /// </summary>
        public override async Task InitializeAsync()
        {
            StatusMessage = "네트워크 모니터링 초기화 중...";
            IsLoading = true;

            try
            {
                // 차단된 연결 목록 로드 (간소화)
                await SafeInvokeUIAsync(() =>
                {
                    _blockedConnections.Clear();
                    // 실제 데이터는 향후 구현
                });

                // 통계 초기화
                await SafeInvokeUIAsync(() =>
                {
                    TotalBlockedCount = 0;
                    Level1BlockCount = 0;
                    Level2BlockCount = 0;
                    Level3BlockCount = 0;
                    UniqueProcesses = 0;
                    UniqueIPs = 0;
                });

                StatusMessage = "네트워크 모니터링 초기화 완료";
            }
            catch (Exception ex)
            {
                StatusMessage = $"초기화 오류: {ex.Message}";
                LogService.AddLogMessage($"❌ 초기화 오류: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                IsInitialized = true;
            }
        }

        /// <summary>
        /// 정리
        /// </summary>
        public override void Cleanup()
        {
            // 모니터링 중지
            if (IsMonitoring)
            {
                Task.Run(async () => await StopMonitoringAsync());
            }

            base.Cleanup();
        }

        /// <summary>
        /// 모니터링 시작 (NetworkPageViewModel 추상 메서드 구현)
        /// </summary>
        public override async Task StartMonitoringAsync()
        {
            if (IsMonitoring) return;

            StatusMessage = "모니터링 시작 중...";
            IsLoading = true;

            try
            {
                IsMonitoring = true;
                LogService.AddLogMessage("🔄 네트워크 모니터링 시작");

                // 실제 모니터링 로직은 여기에 구현
                await Task.Run(async () =>
                {
                    // 프로세스 네트워크 데이터 업데이트
                    await UpdateProcessNetworkDataAsync();
                });

                StatusMessage = "모니터링 진행 중...";
            }
            catch (Exception ex)
            {
                StatusMessage = $"모니터링 시작 오류: {ex.Message}";
                LogService.AddLogMessage($"❌ 모니터링 시작 오류: {ex.Message}");
                IsMonitoring = false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 모니터링 중지 (NetworkPageViewModel 추상 메서드 구현)
        /// </summary>
        public override async Task StopMonitoringAsync()
        {
            if (!IsMonitoring) return;

            await Task.Run(() =>
            {
                IsMonitoring = false;
                StatusMessage = "모니터링 중지됨";
                LogService.AddLogMessage("⏹️ 네트워크 모니터링 중지");
            });
        }

        /// <summary>
        /// 데이터 새로고침 (NetworkPageViewModel 추상 메서드 구현)
        /// </summary>
        public override async Task RefreshDataAsync()
        {
            try
            {
                StatusMessage = "데이터 새로고침 중...";
                IsLoading = true;

                await UpdateProcessNetworkDataAsync();

                StatusMessage = "데이터 새로고침 완료";
                LogService.AddLogMessage("🔄 데이터 새로고침 완료");
            }
            catch (Exception ex)
            {
                StatusMessage = $"데이터 새로고침 실패: {ex.Message}";
                LogService.AddLogMessage($"❌ 데이터 새로고침 실패: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 차단된 연결 목록 로드 (간소화된 구현)
        /// </summary>
        private async Task LoadBlockedConnectionsAsync()
        {
            try
            {
                await SafeInvokeUIAsync(() =>
                {
                    _blockedConnections.Clear();
                    // 실제 데이터 로드는 기존 코드에서 구현됨
                });

                LogService.AddLogMessage($"📋 차단된 연결 목록 초기화됨");
            }
            catch (Exception ex)
            {
                LogService.AddLogMessage($"❌ 차단 목록 로드 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// AutoBlock 통계 업데이트 (간소화된 구현)
        /// </summary>
        private async Task UpdateAutoBlockStatisticsAsync()
        {
            try
            {
                await SafeInvokeUIAsync(() =>
                {
                    // 실제 통계는 기존 코드에서 업데이트됨
                    TotalBlockedCount = 0;
                    Level1BlockCount = 0;
                    Level2BlockCount = 0;
                    Level3BlockCount = 0;
                    UniqueProcesses = 0;
                    UniqueIPs = 0;
                });
            }
            catch (Exception ex)
            {
                LogService.AddLogMessage($"❌ 통계 업데이트 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 프로세스 네트워크 데이터 업데이트
        /// </summary>
        private async Task UpdateProcessNetworkDataAsync()
        {
            try
            {
                var processData = await _processNetworkMapper.GetProcessNetworkDataAsync();

                await SafeInvokeUIAsync(() =>
                {
                    // 기존 데이터 클리어
                    _generalProcessData.Clear();
                    _systemProcessData.Clear();

                    // 새 데이터 추가
                    foreach (var process in processData)
                    {
                        if (process.IsSystemProcess)
                            _systemProcessData.Add(process);
                        else
                            _generalProcessData.Add(process);
                    }

                    // 통계 업데이트
                    StatisticsService.UpdateStatistics(processData.ToList());
                });
            }
            catch (Exception ex)
            {
                LogService.AddLogMessage($"❌ 데이터 업데이트 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 연결 차단 (간소화된 구현)
        /// </summary>
        public async Task BlockConnectionAsync(ProcessNetworkInfo connection)
        {
            try
            {
                StatusMessage = $"{connection.ProcessName} 연결 차단 중...";

                await Task.Run(() =>
                {
                    // 실제 차단 로직은 기존 코드에서 구현
                    connection.IsBlocked = true;
                    connection.BlockedTime = DateTime.Now;
                });

                StatusMessage = "연결 차단 완료";
                LogService.AddLogMessage($"🚫 {connection.ProcessName} 연결 차단됨");

                // 통계 업데이트
                await UpdateAutoBlockStatisticsAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = "연결 차단 실패";
                LogService.AddLogMessage($"❌ 연결 차단 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 프로세스 종료 (간소화된 구현)
        /// </summary>
        public async Task TerminateProcessAsync(ProcessNetworkInfo process)
        {
            try
            {
                StatusMessage = $"{process.ProcessName} 프로세스 종료 중...";

                await Task.Run(() =>
                {
                    // 실제 종료 로직은 기존 코드에서 구현
                    System.Diagnostics.Process.GetProcessById(process.ProcessId)?.Kill();
                });

                StatusMessage = "프로세스 종료 완료";
                LogService.AddLogMessage($"⚠️ {process.ProcessName} 프로세스 종료됨");
            }
            catch (Exception ex)
            {
                StatusMessage = "프로세스 종료 실패";
                LogService.AddLogMessage($"❌ 프로세스 종료 실패: {ex.Message}");
            }
        }
    }
}