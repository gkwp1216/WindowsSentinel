using System.Diagnostics;
using System.Net;
using System.Runtime.Versioning;
using System.Security.Principal;
using LogCheck.Models;
using Microsoft.Data.Sqlite;

namespace LogCheck.Services
{
    /// <summary>
    /// 자동 차단 서비스 구현 클래스
    /// IAutoBlockService 인터페이스의 구현체로 실제 차단 로직을 담당
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class AutoBlockService : IAutoBlockService
    {
        #region 필드 및 상수

        private readonly BlockRuleEngine _ruleEngine;
        private readonly string _connectionString;
        private readonly HashSet<string> _whitelist;
        private bool _isInitialized = false;
        private bool _disposed = false;

        // 데이터베이스 테이블 생성 SQL
        private const string CreateBlockedConnectionsTableSql = @"
            CREATE TABLE IF NOT EXISTS BlockedConnections (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProcessName TEXT NOT NULL,
                ProcessPath TEXT,
                ProcessId INTEGER NOT NULL,
                RemoteAddress TEXT NOT NULL,
                RemotePort INTEGER NOT NULL,
                LocalPort INTEGER,
                Protocol TEXT,
                BlockLevel INTEGER NOT NULL,
                Reason TEXT NOT NULL,
                TriggeredRules TEXT,
                ConfidenceScore REAL,
                BlockedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                IsBlocked BOOLEAN DEFAULT 0,
                ErrorMessage TEXT,
                UserAction TEXT,
                ThreatCategory TEXT
            )";

        private const string CreateWhitelistTableSql = @"
            CREATE TABLE IF NOT EXISTS Whitelist (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProcessPath TEXT UNIQUE NOT NULL,
                Reason TEXT NOT NULL,
                AddedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                AddedBy TEXT DEFAULT 'System',
                IsActive BOOLEAN DEFAULT 1,
                ExpiresAt DATETIME NULL,
                Description TEXT
            )";

        #endregion

        #region 생성자

        /// <summary>
        /// AutoBlockService 기본 생성자 (기본 데이터베이스 경로 사용)
        /// </summary>
        public AutoBlockService() : this("Data Source=autoblock.db")
        {
        }

        /// <summary>
        /// AutoBlockService 생성자
        /// </summary>
        /// <param name="connectionString">SQLite 데이터베이스 연결 문자열</param>
        public AutoBlockService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _ruleEngine = new BlockRuleEngine();
            _whitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        #endregion

        #region IAutoBlockService 구현

        /// <summary>
        /// 서비스 초기화
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            if (_isInitialized)
                return true;

            try
            {
                // 데이터베이스 초기화
                await InitializeDatabaseAsync();

                // 화이트리스트 로드
                await LoadWhitelistAsync();

                _isInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize AutoBlockService: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 프로세스 네트워크 연결 분석
        /// </summary>
        public async Task<BlockDecision> AnalyzeConnectionAsync(ProcessNetworkInfo processInfo)
        {
            if (!_isInitialized)
                await InitializeAsync();

            if (processInfo == null)
                throw new ArgumentNullException(nameof(processInfo));

            // 화이트리스트 체크 먼저
            if (await IsWhitelistedAsync(processInfo))
            {
                return new BlockDecision
                {
                    Level = BlockLevel.None,
                    Reason = "Whitelisted process",
                    ConfidenceScore = 0.0,
                    RecommendedAction = "연결 허용됨 (화이트리스트)",
                    AnalyzedAt = DateTime.Now
                };
            }

            // 규칙 엔진을 통한 분석 수행
            return await _ruleEngine.EvaluateConnectionAsync(processInfo);
        }

        /// <summary>
        /// 차단 결정에 따른 실제 차단 수행
        /// </summary>
        public async Task<bool> BlockConnectionAsync(ProcessNetworkInfo processInfo, BlockLevel level)
        {
            if (!_isInitialized)
                await InitializeAsync();

            if (processInfo == null)
                throw new ArgumentNullException(nameof(processInfo));

            bool success = false;
            string errorMessage = string.Empty;

            try
            {
                switch (level)
                {
                    case BlockLevel.Immediate:
                        success = await ExecuteImmediateBlockAsync(processInfo);
                        break;

                    case BlockLevel.Warning:
                        success = await ExecuteWarningBlockAsync(processInfo);
                        break;

                    case BlockLevel.Monitor:
                        success = await ExecuteEnhancedMonitoringAsync(processInfo);
                        break;

                    default:
                        success = true; // 차단하지 않음
                        break;
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine($"Failed to block connection: {ex.Message}");
            }

            // 차단 기록 저장
            await LogBlockActionAsync(processInfo, level, success, errorMessage);

            return success;
        }

        /// <summary>
        /// 화이트리스트 확인
        /// </summary>
        public async Task<bool> IsWhitelistedAsync(ProcessNetworkInfo processInfo)
        {
            if (processInfo?.ProcessPath == null && processInfo?.ProcessName == null)
                return false;

            // 🔥 NEW: 사설 IP 체크 - VPN 및 내부 네트워크 보호
            if (IsPrivateIP(processInfo.RemoteAddress))
            {
                System.Diagnostics.Debug.WriteLine($"차단 제외: {processInfo.RemoteAddress} (사설 IP)");
                return true; // 사설 IP는 항상 화이트리스트
            }

            // 🔥 NEW: 시스템 프로세스 체크 - 정상 시스템 프로세스 보호
            if (IsSystemProcess(processInfo.ProcessPath ?? processInfo.ProcessName ?? ""))
            {
                System.Diagnostics.Debug.WriteLine($"차단 제외: {processInfo.ProcessName} (시스템 프로세스)");
                return true; // 시스템 프로세스는 항상 화이트리스트
            }

            // System Idle Process 특별 처리
            if (IsLegitimateSystemIdleProcess(processInfo))
            {
                return true; // 정상적인 System Idle Process는 항상 화이트리스트
            }

            // 일반적인 화이트리스트 확인 (ProcessPath 또는 ProcessName 기준)
            var pathToCheck = processInfo.ProcessPath ?? processInfo.ProcessName ?? "";
            return await Task.FromResult(_whitelist.Contains(pathToCheck.ToLowerInvariant()));
        }

        /// <summary>
        /// 정상적인 System Idle Process인지 확인
        /// </summary>
        private bool IsLegitimateSystemIdleProcess(ProcessNetworkInfo processInfo)
        {
            if (processInfo?.ProcessName != "System Idle Process")
                return false;

            // 정상적인 System Idle Process 조건:
            // 1. PID가 0이어야 함
            // 2. ProcessPath가 비어있거나 .exe로 끝나지 않아야 함
            // 3. 네트워크 연결이 없어야 함 (실제로는 연결이 있을 수 있으므로 이 조건은 제외)

            return processInfo.ProcessId == 0 &&
                   (string.IsNullOrEmpty(processInfo.ProcessPath) ||
                    !processInfo.ProcessPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 화이트리스트에 프로세스 추가
        /// </summary>
        public async Task<bool> AddToWhitelistAsync(string processPath, string reason)
        {
            if (string.IsNullOrWhiteSpace(processPath))
                throw new ArgumentException("Process path cannot be null or empty", nameof(processPath));

            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Reason cannot be null or empty", nameof(reason));

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT OR REPLACE INTO Whitelist (ProcessPath, Reason, AddedAt, IsActive)
                    VALUES (@processPath, @reason, @addedAt, 1)";

                command.Parameters.AddWithValue("@processPath", processPath);
                command.Parameters.AddWithValue("@reason", reason);
                command.Parameters.AddWithValue("@addedAt", DateTime.Now);

                await command.ExecuteNonQueryAsync();

                // 메모리 캐시에도 추가
                _whitelist.Add(processPath.ToLowerInvariant());

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to add to whitelist: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 화이트리스트에서 프로세스 제거
        /// </summary>
        public async Task<bool> RemoveFromWhitelistAsync(string processPath)
        {
            if (string.IsNullOrWhiteSpace(processPath))
                return false;

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    UPDATE Whitelist SET IsActive = 0 
                    WHERE ProcessPath = @processPath";

                command.Parameters.AddWithValue("@processPath", processPath);

                var affectedRows = await command.ExecuteNonQueryAsync();

                if (affectedRows > 0)
                {
                    // 메모리 캐시에서도 제거
                    _whitelist.Remove(processPath.ToLowerInvariant());
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to remove from whitelist: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 차단 이력 조회
        /// </summary>
        public async Task<List<AutoBlockedConnection>> GetBlockHistoryAsync(DateTime since, int maxResults = 100)
        {
            var result = new List<AutoBlockedConnection>();

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT * FROM BlockedConnections 
                    WHERE BlockedAt >= @since 
                    ORDER BY BlockedAt DESC 
                    LIMIT @maxResults";

                command.Parameters.AddWithValue("@since", since);
                command.Parameters.AddWithValue("@maxResults", maxResults);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new AutoBlockedConnection
                    {
                        Id = reader.GetInt32(0), // Id
                        ProcessName = reader.GetString(1), // ProcessName
                        ProcessPath = reader.IsDBNull(2) ? string.Empty : reader.GetString(2), // ProcessPath
                        ProcessId = reader.GetInt32(3), // ProcessId
                        RemoteAddress = reader.GetString(4), // RemoteAddress
                        RemotePort = reader.GetInt32(5), // RemotePort
                        LocalPort = reader.IsDBNull(6) ? 0 : reader.GetInt32(6), // LocalPort
                        Protocol = reader.IsDBNull(7) ? string.Empty : reader.GetString(7), // Protocol
                        BlockLevel = (BlockLevel)reader.GetInt32(8), // BlockLevel
                        Reason = reader.GetString(9), // Reason
                        TriggeredRules = reader.IsDBNull(10) ? string.Empty : reader.GetString(10), // TriggeredRules
                        ConfidenceScore = reader.IsDBNull(11) ? 0.0 : reader.GetDouble(11), // ConfidenceScore
                        BlockedAt = reader.GetDateTime(12), // BlockedAt
                        IsBlocked = reader.GetBoolean(13), // IsBlocked
                        ErrorMessage = reader.IsDBNull(14) ? string.Empty : reader.GetString(14), // ErrorMessage
                        UserAction = reader.IsDBNull(15) ? string.Empty : reader.GetString(15), // UserAction
                        ThreatCategory = reader.IsDBNull(16) ? string.Empty : reader.GetString(16) // ThreatCategory
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get block history: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 화이트리스트 전체 조회
        /// </summary>
        public async Task<List<AutoWhitelistEntry>> GetWhitelistAsync()
        {
            var result = new List<AutoWhitelistEntry>();

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT * FROM Whitelist 
                    WHERE IsActive = 1 
                    ORDER BY AddedAt DESC";

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new AutoWhitelistEntry
                    {
                        Id = reader.GetInt32(0), // Id
                        ProcessPath = reader.GetString(1), // ProcessPath
                        Reason = reader.GetString(2), // Reason
                        AddedAt = reader.GetDateTime(3), // AddedAt
                        AddedBy = reader.IsDBNull(4) ? "System" : reader.GetString(4), // AddedBy
                        IsActive = reader.GetBoolean(5), // IsActive
                        ExpiresAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6), // ExpiresAt
                        Description = reader.IsDBNull(7) ? string.Empty : reader.GetString(7) // Description
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get whitelist: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 차단 통계 조회
        /// </summary>
        public async Task<BlockStatistics> GetBlockStatisticsAsync(StatisticsPeriod period)
        {
            var stats = new BlockStatistics
            {
                Period = period,
                EndTime = DateTime.Now
            };

            // 기간 설정
            switch (period)
            {
                case StatisticsPeriod.Hour:
                    stats.StartTime = stats.EndTime.AddHours(-1);
                    break;
                case StatisticsPeriod.Day:
                    stats.StartTime = stats.EndTime.AddDays(-1);
                    break;
                case StatisticsPeriod.Week:
                    stats.StartTime = stats.EndTime.AddDays(-7);
                    break;
                case StatisticsPeriod.Month:
                    stats.StartTime = stats.EndTime.AddDays(-30);
                    break;
            }

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                // 총 차단 건수
                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT COUNT(*) FROM BlockedConnections 
                    WHERE BlockedAt >= @startTime AND BlockedAt <= @endTime";
                command.Parameters.AddWithValue("@startTime", stats.StartTime);
                command.Parameters.AddWithValue("@endTime", stats.EndTime);

                stats.TotalBlocks = Convert.ToInt32(await command.ExecuteScalarAsync());

                // 레벨별 통계 등은 추후 구현
                // TODO: 상세 통계 구현
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get statistics: {ex.Message}");
            }

            return stats;
        }

        /// <summary>
        /// 서비스 정리
        /// </summary>
        public async Task<bool> CleanupAsync()
        {
            if (_disposed)
                return true;

            try
            {
                _whitelist.Clear();
                _disposed = true;
                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to cleanup AutoBlockService: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 비공개 메서드

        /// <summary>
        /// 데이터베이스 초기화
        /// </summary>
        private async Task InitializeDatabaseAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // BlockedConnections 테이블 생성
            var command = connection.CreateCommand();
            command.CommandText = CreateBlockedConnectionsTableSql;
            await command.ExecuteNonQueryAsync();

            // Whitelist 테이블 생성
            command = connection.CreateCommand();
            command.CommandText = CreateWhitelistTableSql;
            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// 화이트리스트를 메모리로 로드
        /// </summary>
        private async Task LoadWhitelistAsync()
        {
            _whitelist.Clear();

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = "SELECT ProcessPath FROM Whitelist WHERE IsActive = 1";

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    _whitelist.Add(reader.GetString(0).ToLowerInvariant()); // ProcessPath
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load whitelist: {ex.Message}");
            }
        }

        /// <summary>
        /// Level 1: 즉시 차단 실행
        /// </summary>
        private async Task<bool> ExecuteImmediateBlockAsync(ProcessNetworkInfo processInfo)
        {
            bool success = true;

            try
            {
                // 1. TCP 연결 강제 종료 (netsh 명령어 사용)
                await TerminateConnectionAsync(processInfo);

                // 2. 프로세스 강제 종료
                await TerminateProcessAsync(processInfo.ProcessId);

                // 3. 방화벽 규칙 추가
                await AddFirewallRuleAsync(processInfo);

                // 4. 사용자 긴급 알림 (UI 스레드에서 처리하도록 이벤트 발생)
                OnCriticalThreatBlocked?.Invoke(processInfo);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to execute immediate block: {ex.Message}");
                success = false;
            }

            return success;
        }

        /// <summary>
        /// Level 2: 경고 차단 실행 (사용자 확인 필요)
        /// </summary>
        private async Task<bool> ExecuteWarningBlockAsync(ProcessNetworkInfo processInfo)
        {
            // 사용자 확인을 위한 이벤트 발생
            OnWarningThreatDetected?.Invoke(processInfo);

            return await Task.FromResult(true);
        }

        /// <summary>
        /// Level 3: 강화된 모니터링 실행
        /// </summary>
        private async Task<bool> ExecuteEnhancedMonitoringAsync(ProcessNetworkInfo processInfo)
        {
            // 강화된 모니터링 시작을 위한 이벤트 발생
            OnMonitoringRequired?.Invoke(processInfo);

            return await Task.FromResult(true);
        }

        /// <summary>
        /// TCP 연결 종료
        /// </summary>
        private async Task TerminateConnectionAsync(ProcessNetworkInfo processInfo)
        {
            // netstat으로 연결 찾아서 강제 종료
            // 실제 구현에서는 WinAPI나 netsh 명령어 사용
            await Task.Delay(100); // 시뮬레이션
        }

        /// <summary>
        /// 프로세스 강제 종료
        /// </summary>
        private async Task TerminateProcessAsync(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                if (process != null && !process.HasExited)
                {
                    process.Kill();
                    await process.WaitForExitAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to terminate process {processId}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 방화벽 규칙 추가
        /// </summary>
        [SupportedOSPlatform("windows")]
        private async Task AddFirewallRuleAsync(ProcessNetworkInfo processInfo)
        {
            try
            {
                if (!IsRunningAsAdmin())
                {
                    System.Diagnostics.Debug.WriteLine("관리자 권한 필요: 방화벽 규칙 추가 불가");
                    return;
                }

                var ruleName = $"AutoBlock_{processInfo.ProcessName}_{DateTime.Now:yyyyMMddHHmmss}";

                var startInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"advfirewall firewall add rule name=\"{ruleName}\" dir=out action=block program=\"{processInfo.ProcessPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    if (process.ExitCode != 0)
                    {
                        var error = await process.StandardError.ReadToEndAsync();
                        throw new InvalidOperationException($"방화벽 규칙 추가 실패: {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to add firewall rule: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 차단 기록 저장
        /// </summary>
        private async Task LogBlockActionAsync(ProcessNetworkInfo processInfo, BlockLevel level, bool success, string errorMessage)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO BlockedConnections 
                    (ProcessName, ProcessPath, ProcessId, RemoteAddress, RemotePort, LocalPort, 
                     Protocol, BlockLevel, Reason, IsBlocked, ErrorMessage, BlockedAt)
                    VALUES 
                    (@processName, @processPath, @processId, @remoteAddress, @remotePort, @localPort,
                     @protocol, @blockLevel, @reason, @isBlocked, @errorMessage, @blockedAt)";

                command.Parameters.AddWithValue("@processName", processInfo.ProcessName ?? string.Empty);
                command.Parameters.AddWithValue("@processPath", processInfo.ProcessPath ?? string.Empty);
                command.Parameters.AddWithValue("@processId", processInfo.ProcessId);
                command.Parameters.AddWithValue("@remoteAddress", processInfo.RemoteAddress ?? string.Empty);
                command.Parameters.AddWithValue("@remotePort", processInfo.RemotePort);
                command.Parameters.AddWithValue("@localPort", processInfo.LocalPort);
                command.Parameters.AddWithValue("@protocol", processInfo.Protocol ?? "TCP");
                command.Parameters.AddWithValue("@blockLevel", (int)level);
                command.Parameters.AddWithValue("@reason", $"Level {(int)level} block");
                command.Parameters.AddWithValue("@isBlocked", success);
                command.Parameters.AddWithValue("@errorMessage", errorMessage ?? string.Empty);
                command.Parameters.AddWithValue("@blockedAt", DateTime.Now);

                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to log block action: {ex.Message}");
            }
        }

        /// <summary>
        /// 관리자 권한 확인
        /// </summary>
        [SupportedOSPlatform("windows")]
        private bool IsRunningAsAdmin()
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

        #region UI 지원 메소드

        /// <summary>
        /// 차단된 연결 목록 조회 (UI용)
        /// </summary>
        public async Task<List<AutoBlockedConnection>> GetBlockedConnectionsAsync()
        {
            var connections = new List<AutoBlockedConnection>();

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    SELECT ProcessName, ProcessPath, ProcessId, RemoteAddress, RemotePort, 
                           LocalPort, Protocol, BlockLevel, Reason, TriggeredRules, 
                           ConfidenceScore, BlockedAt, IsBlocked, ErrorMessage, 
                           UserAction, ThreatCategory
                    FROM BlockedConnections 
                    WHERE IsBlocked = 1 
                    ORDER BY BlockedAt DESC";

                using var command = new SqliteCommand(sql, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var blockedConnection = new AutoBlockedConnection
                    {
                        ProcessName = reader.GetString(reader.GetOrdinal("ProcessName")),
                        ProcessPath = reader.IsDBNull(reader.GetOrdinal("ProcessPath")) ? string.Empty : reader.GetString(reader.GetOrdinal("ProcessPath")),
                        ProcessId = reader.GetInt32(reader.GetOrdinal("ProcessId")),
                        RemoteAddress = reader.GetString(reader.GetOrdinal("RemoteAddress")),
                        RemotePort = reader.GetInt32(reader.GetOrdinal("RemotePort")),
                        LocalPort = reader.IsDBNull(reader.GetOrdinal("LocalPort")) ? 0 : reader.GetInt32(reader.GetOrdinal("LocalPort")),
                        Protocol = reader.IsDBNull(reader.GetOrdinal("Protocol")) ? "TCP" : reader.GetString(reader.GetOrdinal("Protocol")),
                        BlockLevel = (BlockLevel)reader.GetInt32(reader.GetOrdinal("BlockLevel")),
                        Reason = reader.GetString(reader.GetOrdinal("Reason")),
                        TriggeredRules = reader.IsDBNull(reader.GetOrdinal("TriggeredRules")) ? string.Empty : reader.GetString(reader.GetOrdinal("TriggeredRules")),
                        ConfidenceScore = reader.IsDBNull(reader.GetOrdinal("ConfidenceScore")) ? 0.0 : reader.GetDouble(reader.GetOrdinal("ConfidenceScore")),
                        BlockedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("BlockedAt"))),
                        IsBlocked = reader.GetBoolean(reader.GetOrdinal("IsBlocked")),
                        ErrorMessage = reader.IsDBNull(reader.GetOrdinal("ErrorMessage")) ? string.Empty : reader.GetString(reader.GetOrdinal("ErrorMessage")),
                        UserAction = reader.IsDBNull(reader.GetOrdinal("UserAction")) ? string.Empty : reader.GetString(reader.GetOrdinal("UserAction")),
                        ThreatCategory = reader.IsDBNull(reader.GetOrdinal("ThreatCategory")) ? string.Empty : reader.GetString(reader.GetOrdinal("ThreatCategory")),
                        FirewallRuleExists = false // 기본값, 필요시 실제 방화벽 상태 확인
                    };

                    connections.Add(blockedConnection);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get blocked connections: {ex.Message}");
            }

            return connections;
        }

        /// <summary>
        /// 특정 연결의 차단 해제 (UI용)
        /// </summary>
        public async Task<bool> UnblockConnectionAsync(string remoteAddress)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    UPDATE BlockedConnections 
                    SET IsBlocked = 0, UserAction = 'Unblocked by user', 
                        BlockedAt = CURRENT_TIMESTAMP 
                    WHERE RemoteAddress = @remoteAddress AND IsBlocked = 1";

                using var command = new SqliteCommand(sql, connection);
                command.Parameters.AddWithValue("@remoteAddress", remoteAddress);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to unblock connection: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 화이트리스트에 주소 추가 (UI용 오버로드)
        /// </summary>
        public async Task<bool> AddAddressToWhitelistAsync(string address, string reason)
        {
            try
            {
                // 임시로 프로세스 경로로 처리 (실제로는 IP 주소 화이트리스트 테이블이 필요)
                return await AddToWhitelistAsync(address, reason);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to add address to whitelist: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 이벤트

        /// <summary>
        /// 긴급 위협 차단 시 발생하는 이벤트
        /// </summary>
        public event Action<ProcessNetworkInfo>? OnCriticalThreatBlocked;

        /// <summary>
        /// 경고 위협 탐지 시 발생하는 이벤트
        /// </summary>
        public event Action<ProcessNetworkInfo>? OnWarningThreatDetected;

        /// <summary>
        /// 모니터링 필요 시 발생하는 이벤트
        /// </summary>
        public event Action<ProcessNetworkInfo>? OnMonitoringRequired;

        #endregion

        #region 사설 IP 및 시스템 프로세스 체크 (긴급 수정)

        /// <summary>
        /// 사설 IP인지 확인 (VPN 및 내부 네트워크 오탐 방지)
        /// </summary>
        private bool IsPrivateIP(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return false;

            try
            {
                if (!IPAddress.TryParse(ipAddress, out IPAddress? ip) || ip == null)
                    return false;

                var bytes = ip.GetAddressBytes();

                // 사설 IP 대역 체크 (RFC 1918)
                return (bytes[0] == 10) ||                                          // 10.0.0.0/8
                       (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||     // 172.16.0.0/12
                       (bytes[0] == 192 && bytes[1] == 168) ||                      // 192.168.0.0/16
                       (bytes[0] == 127) ||                                         // 127.0.0.0/8 (로컬호스트)
                       (bytes[0] == 169 && bytes[1] == 254);                        // 169.254.0.0/16 (링크로컬)
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 시스템 프로세스인지 확인 (정상 시스템 프로세스 오탐 방지)
        /// </summary>
        private bool IsSystemProcess(string processPath)
        {
            if (string.IsNullOrWhiteSpace(processPath))
                return false;

            var processName = System.IO.Path.GetFileName(processPath).ToLower();
            var fullPath = processPath.ToLower();

            // 시스템 프로세스 화이트리스트
            var systemProcesses = new[]
            {
                "notepad.exe", "mspaint.exe", "calc.exe", "calculator.exe",
                "explorer.exe", "taskmgr.exe", "regedit.exe",

                "cmd.exe", "powershell.exe", "winlogon.exe",
                "csrss.exe", "lsass.exe", "services.exe", "svchost.exe",
                "dwm.exe", "wininit.exe", "smss.exe", "spoolsv.exe"
            };

            // 프로세스 이름으로 체크
            if (systemProcesses.Contains(processName))
                return true;

            // Windows 시스템 폴더 체크
            if (fullPath.StartsWith(@"c:\windows\") ||

                fullPath.StartsWith(@"c:\windows\system32\") ||
                fullPath.StartsWith(@"c:\windows\syswow64\"))
                return true;

            return false;
        }

        #endregion
    }
}