using System.Text.Json;
using LogCheck.Models;
using Microsoft.Data.Sqlite;

namespace LogCheck.Services
{
    /// <summary>
    /// 통합 차단 관리 서비스 구현체
    /// AutoBlock과 수동 차단을 하나의 시스템으로 통합 관리
    /// </summary>
    public class UnifiedBlockingService : IUnifiedBlockingService, IDisposable
    {
        #region 필드 및 상수

        private readonly string _connectionString;
        private readonly IAutoBlockService _autoBlockService;
        private readonly NetworkConnectionManager _connectionManager;
        private readonly object _lockObject = new object();
        private bool _disposed = false;

        // 배치 처리를 위한 필드들
        private readonly List<UnifiedBlockedConnection> _pendingBlocks = new List<UnifiedBlockedConnection>();
        private readonly System.Threading.Timer _batchTimer;
        private const int BATCH_SIZE = 25;
        private const int BATCH_INTERVAL_MS = 3000; // 3초마다 배치 처리

        // 통합 데이터베이스 스키마
        private const string CreateUnifiedBlockedConnectionsTableSql = @"
            CREATE TABLE IF NOT EXISTS UnifiedBlockedConnections (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProcessId INTEGER NOT NULL,
                ProcessName TEXT NOT NULL,
                ProcessPath TEXT,
                RemoteAddress TEXT NOT NULL,
                RemotePort INTEGER NOT NULL,
                LocalPort INTEGER,
                Protocol TEXT,
                
                -- 차단 정보
                BlockSource TEXT NOT NULL, -- 'Manual', 'AutoBlock', 'System', 'Policy'
                BlockLevel INTEGER NOT NULL, -- 0=Info, 1=Warning, 2=Critical
                Reason TEXT NOT NULL,
                UserNote TEXT,
                TriggeredRules TEXT, -- JSON 배열
                ConfidenceScore REAL,
                ThreatCategory TEXT,
                
                -- 시간 정보
                BlockedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UnblockedAt DATETIME NULL,
                IsActive BOOLEAN NOT NULL DEFAULT 1,
                
                -- 부모 프로세스 정보
                ParentProcessId INTEGER,
                ParentProcessName TEXT,
                IsRelatedToChildBlock BOOLEAN DEFAULT 0,
                
                -- 메타데이터
                ErrorMessage TEXT,
                ExecutedActions TEXT -- JSON 배열
            )";

        private const string CreateBlockingRulesTableSql = @"
            CREATE TABLE IF NOT EXISTS BlockingRules (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Description TEXT,
                ApplicableSource TEXT NOT NULL,
                IsEnabled BOOLEAN NOT NULL DEFAULT 1,
                RuleExpression TEXT NOT NULL,
                DefaultLevel INTEGER NOT NULL,
                CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ModifiedAt DATETIME
            )";

        #endregion

        #region 이벤트

        public event EventHandler<UnifiedBlockedConnection>? OnConnectionBlocked;
        public event EventHandler<UnifiedBlockedConnection>? OnConnectionUnblocked;

        #endregion

        #region 생성자

        public UnifiedBlockingService(
            string connectionString,
            IAutoBlockService autoBlockService,
            NetworkConnectionManager connectionManager)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _autoBlockService = autoBlockService ?? throw new ArgumentNullException(nameof(autoBlockService));
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));

            // 배치 타이머 초기화
            _batchTimer = new System.Threading.Timer(ProcessPendingBatch, null,
                BATCH_INTERVAL_MS, BATCH_INTERVAL_MS);
        }

        #endregion

        #region IUnifiedBlockingService 구현

        /// <summary>
        /// 연결 차단 실행 (통합)
        /// </summary>
        public async Task<BlockResult> BlockConnectionAsync(ProcessNetworkInfo process, BlockRequest request)
        {
            try
            {
                var executedActions = new List<string>();
                var blockedConnection = new UnifiedBlockedConnection
                {
                    ProcessInfo = process,
                    Source = request.Source,
                    Level = request.Level,
                    Reason = request.Reason,
                    UserNote = request.UserNote,
                    TriggeredRules = request.TriggeredRules,
                    ConfidenceScore = request.ConfidenceScore,
                    ThreatCategory = request.ThreatCategory,
                    BlockedAt = request.RequestedAt,
                    ParentProcessId = process.ParentProcessId,
                    ParentProcessName = process.ParentProcessName,
                    IsRelatedToChildBlock = process.HasBlockedChildren
                };

                // 1. 네트워크 연결 차단 실행
                bool networkBlocked = false;
                try
                {
                    networkBlocked = await _connectionManager.DisconnectProcessAsync(
                        process.ProcessId, $"통합차단: {request.Reason}");

                    if (networkBlocked)
                    {
                        executedActions.Add("네트워크 연결 차단");
                        process.IsBlocked = true;
                        process.BlockedTime = DateTime.Now;
                        process.BlockReason = request.Reason;
                    }
                }
                catch (Exception ex)
                {
                    executedActions.Add($"네트워크 차단 실패: {ex.Message}");
                }

                // 2. AutoBlock 특화 액션 실행 (AutoBlock인 경우)
                bool autoBlockExecuted = false;
                if (request.Source == BlockSource.AutoBlock)
                {
                    try
                    {
                        autoBlockExecuted = await _autoBlockService.BlockConnectionAsync(process, request.Level);
                        if (autoBlockExecuted)
                        {
                            executedActions.Add("AutoBlock 시스템 규칙 적용");
                        }
                    }
                    catch (Exception ex)
                    {
                        executedActions.Add($"AutoBlock 실행 실패: {ex.Message}");
                    }
                }

                // 3. 차단 기록을 배치 큐에 추가
                blockedConnection.ExecutedActions = executedActions;

                lock (_lockObject)
                {
                    _pendingBlocks.Add(blockedConnection);

                    // 배치 크기에 도달하면 즉시 처리
                    if (_pendingBlocks.Count >= BATCH_SIZE)
                    {
                        _ = Task.Run(async () => await ProcessBatchNow());
                    }
                }

                // 4. 이벤트 발생
                OnConnectionBlocked?.Invoke(this, blockedConnection);

                var result = new BlockResult
                {
                    Success = networkBlocked || autoBlockExecuted,
                    ExecutedActions = executedActions
                };

                if (!result.Success)
                {
                    result.ErrorMessage = "차단 실행에 실패했습니다.";
                }

                return result;
            }
            catch (Exception ex)
            {
                return new BlockResult
                {
                    Success = false,
                    ErrorMessage = $"차단 처리 중 오류 발생: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 연결 차단 해제
        /// </summary>
        public async Task<bool> UnblockConnectionAsync(int connectionId, string reason = "")
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    UPDATE UnifiedBlockedConnections 
                    SET IsActive = 0, UnblockedAt = @unblockedAt, UserNote = @reason 
                    WHERE Id = @id AND IsActive = 1";

                command.Parameters.AddWithValue("@id", connectionId);
                command.Parameters.AddWithValue("@unblockedAt", DateTime.Now);
                command.Parameters.AddWithValue("@reason", string.IsNullOrEmpty(reason) ? "사용자 차단 해제" : reason);

                var rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    // 차단 해제된 연결 정보 조회하여 이벤트 발생
                    var unblockedConnection = await GetBlockedConnectionByIdAsync(connectionId);
                    if (unblockedConnection != null)
                    {
                        OnConnectionUnblocked?.Invoke(this, unblockedConnection);
                    }
                }

                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"차단 해제 실패 (ID: {connectionId}): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 모든 차단된 연결 조회
        /// </summary>
        public async Task<List<UnifiedBlockedConnection>> GetAllBlockedConnectionsAsync(BlockFilter? filter = null)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var whereConditions = new List<string> { "1=1" };
                var parameters = new List<SqliteParameter>();

                if (filter != null)
                {
                    if (filter.Source.HasValue)
                    {
                        whereConditions.Add("BlockSource = @source");
                        parameters.Add(new SqliteParameter("@source", filter.Source.ToString()));
                    }

                    if (filter.Level.HasValue)
                    {
                        whereConditions.Add("BlockLevel = @level");
                        parameters.Add(new SqliteParameter("@level", (int)filter.Level.Value));
                    }

                    if (!string.IsNullOrEmpty(filter.ProcessName))
                    {
                        whereConditions.Add("ProcessName LIKE @processName");
                        parameters.Add(new SqliteParameter("@processName", $"%{filter.ProcessName}%"));
                    }

                    if (!string.IsNullOrEmpty(filter.RemoteAddress))
                    {
                        whereConditions.Add("RemoteAddress LIKE @remoteAddress");
                        parameters.Add(new SqliteParameter("@remoteAddress", $"%{filter.RemoteAddress}%"));
                    }

                    if (filter.FromDate.HasValue)
                    {
                        whereConditions.Add("BlockedAt >= @fromDate");
                        parameters.Add(new SqliteParameter("@fromDate", filter.FromDate.Value));
                    }

                    if (filter.ToDate.HasValue)
                    {
                        whereConditions.Add("BlockedAt <= @toDate");
                        parameters.Add(new SqliteParameter("@toDate", filter.ToDate.Value));
                    }

                    if (filter.IsActive.HasValue)
                    {
                        whereConditions.Add("IsActive = @isActive");
                        parameters.Add(new SqliteParameter("@isActive", filter.IsActive.Value));
                    }
                }

                var query = $@"
                    SELECT * FROM UnifiedBlockedConnections 
                    WHERE {string.Join(" AND ", whereConditions)}
                    ORDER BY BlockedAt DESC";

                var command = new SqliteCommand(query, connection);
                command.Parameters.AddRange(parameters.ToArray());

                var result = new List<UnifiedBlockedConnection>();
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    result.Add(MapFromReader(reader));
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"차단된 연결 조회 실패: {ex.Message}");
                return new List<UnifiedBlockedConnection>();
            }
        }

        /// <summary>
        /// 특정 프로세스의 차단 상태 확인
        /// </summary>
        public async Task<bool> IsConnectionBlockedAsync(ProcessNetworkInfo process)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT COUNT(*) FROM UnifiedBlockedConnections 
                    WHERE ProcessId = @processId 
                      AND ProcessName = @processName 
                      AND RemoteAddress = @remoteAddress 
                      AND RemotePort = @remotePort 
                      AND IsActive = 1";

                command.Parameters.AddWithValue("@processId", process.ProcessId);
                command.Parameters.AddWithValue("@processName", process.ProcessName);
                command.Parameters.AddWithValue("@remoteAddress", process.RemoteAddress);
                command.Parameters.AddWithValue("@remotePort", process.RemotePort);

                var count = Convert.ToInt32(await command.ExecuteScalarAsync());
                return count > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"차단 상태 확인 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 차단 통계 조회
        /// </summary>
        public async Task<BlockingStatistics> GetBlockingStatisticsAsync(TimeSpan? period = null)
        {
            try
            {
                var since = period.HasValue ? DateTime.Now - period.Value : DateTime.Today.AddDays(-30);

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var stats = new BlockingStatistics
                {
                    LastUpdated = DateTime.Now
                };

                // 전체 통계
                var totalQuery = "SELECT COUNT(*) FROM UnifiedBlockedConnections WHERE BlockedAt >= @since";
                var totalCommand = new SqliteCommand(totalQuery, connection);
                totalCommand.Parameters.AddWithValue("@since", since);
                stats.TotalBlocked = Convert.ToInt32(await totalCommand.ExecuteScalarAsync());

                // 소스별 통계
                var sourceQuery = @"
                    SELECT BlockSource, COUNT(*) 
                    FROM UnifiedBlockedConnections 
                    WHERE BlockedAt >= @since 
                    GROUP BY BlockSource";
                var sourceCommand = new SqliteCommand(sourceQuery, connection);
                sourceCommand.Parameters.AddWithValue("@since", since);

                using var sourceReader = await sourceCommand.ExecuteReaderAsync();
                while (await sourceReader.ReadAsync())
                {
                    var source = sourceReader.GetString(0);
                    var count = sourceReader.GetInt32(1);

                    switch (source)
                    {
                        case "Manual":
                            stats.ManualBlocked = count;
                            break;
                        case "AutoBlock":
                            stats.AutoBlocked = count;
                            break;
                        case "System":
                        case "Policy":
                            stats.SystemBlocked += count;
                            break;
                    }
                }

                // 활성 차단 수
                var activeQuery = "SELECT COUNT(*) FROM UnifiedBlockedConnections WHERE IsActive = 1";
                var activeCommand = new SqliteCommand(activeQuery, connection);
                stats.ActiveBlocks = Convert.ToInt32(await activeCommand.ExecuteScalarAsync());

                // 오늘 차단 수
                var todayQuery = "SELECT COUNT(*) FROM UnifiedBlockedConnections WHERE DATE(BlockedAt) = DATE('now')";
                var todayCommand = new SqliteCommand(todayQuery, connection);
                stats.TodayBlocked = Convert.ToInt32(await todayCommand.ExecuteScalarAsync());

                return stats;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"차단 통계 조회 실패: {ex.Message}");
                return new BlockingStatistics { LastUpdated = DateTime.Now };
            }
        }

        /// <summary>
        /// 차단 이력 조회
        /// </summary>
        public async Task<List<UnifiedBlockedConnection>> GetBlockHistoryAsync(DateTime since, int limit = 100)
        {
            var filter = new BlockFilter
            {
                FromDate = since
            };

            var allBlocked = await GetAllBlockedConnectionsAsync(filter);
            return allBlocked.Take(limit).ToList();
        }

        /// <summary>
        /// 차단 규칙 업데이트
        /// </summary>
        public async Task<bool> UpdateBlockingRulesAsync(List<BlockingRule> rules)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();

                foreach (var rule in rules)
                {
                    var command = connection.CreateCommand();
                    command.Transaction = transaction;

                    if (rule.Id == 0)
                    {
                        // 새 규칙 추가
                        command.CommandText = @"
                            INSERT INTO BlockingRules 
                            (Name, Description, ApplicableSource, IsEnabled, RuleExpression, DefaultLevel, CreatedAt)
                            VALUES (@name, @description, @source, @enabled, @expression, @level, @createdAt)";
                        command.Parameters.AddWithValue("@createdAt", DateTime.Now);
                    }
                    else
                    {
                        // 기존 규칙 업데이트
                        command.CommandText = @"
                            UPDATE BlockingRules 
                            SET Name = @name, Description = @description, ApplicableSource = @source, 
                                IsEnabled = @enabled, RuleExpression = @expression, DefaultLevel = @level, 
                                ModifiedAt = @modifiedAt
                            WHERE Id = @id";
                        command.Parameters.AddWithValue("@id", rule.Id);
                        command.Parameters.AddWithValue("@modifiedAt", DateTime.Now);
                    }

                    command.Parameters.AddWithValue("@name", rule.Name);
                    command.Parameters.AddWithValue("@description", rule.Description ?? string.Empty);
                    command.Parameters.AddWithValue("@source", rule.ApplicableSource.ToString());
                    command.Parameters.AddWithValue("@enabled", rule.IsEnabled);
                    command.Parameters.AddWithValue("@expression", rule.RuleExpression);
                    command.Parameters.AddWithValue("@level", (int)rule.DefaultLevel);

                    await command.ExecuteNonQueryAsync();
                }

                transaction.Commit();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"차단 규칙 업데이트 실패: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 초기화 및 배치 처리

        /// <summary>
        /// 서비스 초기화
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                // 테이블 생성
                await ExecuteNonQueryAsync(connection, CreateUnifiedBlockedConnectionsTableSql);
                await ExecuteNonQueryAsync(connection, CreateBlockingRulesTableSql);

                System.Diagnostics.Debug.WriteLine("[UnifiedBlockingService] 데이터베이스 초기화 완료");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UnifiedBlockingService] 초기화 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 배치 처리 타이머 콜백
        /// </summary>
        private async void ProcessPendingBatch(object? state)
        {
            await ProcessBatchNow();
        }

        /// <summary>
        /// 대기 중인 배치 즉시 처리
        /// </summary>
        private async Task ProcessBatchNow()
        {
            List<UnifiedBlockedConnection> batchToProcess;

            lock (_lockObject)
            {
                if (_pendingBlocks.Count == 0)
                    return;

                batchToProcess = new List<UnifiedBlockedConnection>(_pendingBlocks);
                _pendingBlocks.Clear();
            }

            try
            {
                await ProcessBlockedConnectionsBatch(batchToProcess);
                System.Diagnostics.Debug.WriteLine($"[UnifiedBlockingService] 배치 처리 완료: {batchToProcess.Count}개 레코드");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UnifiedBlockingService] 배치 처리 실패: {ex.Message}");

                // 실패한 배치를 다시 큐에 추가
                lock (_lockObject)
                {
                    _pendingBlocks.InsertRange(0, batchToProcess);
                }
            }
        }

        /// <summary>
        /// 차단 연결 배치 처리
        /// </summary>
        private async Task ProcessBlockedConnectionsBatch(List<UnifiedBlockedConnection> connections)
        {
            if (!connections.Any()) return;

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();

            foreach (var blockedConn in connections)
            {
                var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
                    INSERT INTO UnifiedBlockedConnections 
                    (ProcessId, ProcessName, ProcessPath, RemoteAddress, RemotePort, LocalPort, Protocol,
                     BlockSource, BlockLevel, Reason, UserNote, TriggeredRules, ConfidenceScore, ThreatCategory,
                     BlockedAt, ParentProcessId, ParentProcessName, IsRelatedToChildBlock, ExecutedActions)
                    VALUES 
                    (@processId, @processName, @processPath, @remoteAddress, @remotePort, @localPort, @protocol,
                     @blockSource, @blockLevel, @reason, @userNote, @triggeredRules, @confidenceScore, @threatCategory,
                     @blockedAt, @parentProcessId, @parentProcessName, @isRelatedToChildBlock, @executedActions)";

                var processInfo = blockedConn.ProcessInfo;
                command.Parameters.AddWithValue("@processId", processInfo.ProcessId);
                command.Parameters.AddWithValue("@processName", processInfo.ProcessName ?? string.Empty);
                command.Parameters.AddWithValue("@processPath", processInfo.ProcessPath ?? string.Empty);
                command.Parameters.AddWithValue("@remoteAddress", processInfo.RemoteAddress ?? string.Empty);
                command.Parameters.AddWithValue("@remotePort", processInfo.RemotePort);
                command.Parameters.AddWithValue("@localPort", processInfo.LocalPort);
                command.Parameters.AddWithValue("@protocol", processInfo.Protocol ?? "TCP");

                command.Parameters.AddWithValue("@blockSource", blockedConn.Source.ToString());
                command.Parameters.AddWithValue("@blockLevel", (int)blockedConn.Level);
                command.Parameters.AddWithValue("@reason", blockedConn.Reason);
                command.Parameters.AddWithValue("@userNote", blockedConn.UserNote ?? string.Empty);

                var triggeredRulesJson = blockedConn.TriggeredRules != null
                    ? JsonSerializer.Serialize(blockedConn.TriggeredRules)
                    : string.Empty;
                command.Parameters.AddWithValue("@triggeredRules", triggeredRulesJson);

                command.Parameters.AddWithValue("@confidenceScore",
                    blockedConn.ConfidenceScore.HasValue ? (object)blockedConn.ConfidenceScore.Value : DBNull.Value);
                command.Parameters.AddWithValue("@threatCategory", blockedConn.ThreatCategory ?? string.Empty);
                command.Parameters.AddWithValue("@blockedAt", blockedConn.BlockedAt);

                command.Parameters.AddWithValue("@parentProcessId",
                    blockedConn.ParentProcessId.HasValue ? (object)blockedConn.ParentProcessId.Value : DBNull.Value);
                command.Parameters.AddWithValue("@parentProcessName", blockedConn.ParentProcessName ?? string.Empty);
                command.Parameters.AddWithValue("@isRelatedToChildBlock", blockedConn.IsRelatedToChildBlock);

                var executedActionsJson = blockedConn.ExecutedActions != null
                    ? JsonSerializer.Serialize(blockedConn.ExecutedActions)
                    : string.Empty;
                command.Parameters.AddWithValue("@executedActions", executedActionsJson);

                await command.ExecuteNonQueryAsync();
            }

            transaction.Commit();
        }

        #endregion

        #region 헬퍼 메서드

        private async Task<UnifiedBlockedConnection?> GetBlockedConnectionByIdAsync(int id)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = "SELECT * FROM UnifiedBlockedConnections WHERE Id = @id";
                command.Parameters.AddWithValue("@id", id);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return MapFromReader(reader);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private UnifiedBlockedConnection MapFromReader(SqliteDataReader reader)
        {
            var processInfo = new ProcessNetworkInfo
            {
                ProcessId = Convert.ToInt32(reader["ProcessId"]),
                ProcessName = reader["ProcessName"]?.ToString() ?? string.Empty,
                ProcessPath = reader["ProcessPath"]?.ToString() ?? string.Empty,
                RemoteAddress = reader["RemoteAddress"]?.ToString() ?? string.Empty,
                RemotePort = Convert.ToInt32(reader["RemotePort"]),
                LocalPort = Convert.ToInt32(reader["LocalPort"]),
                Protocol = reader["Protocol"]?.ToString() ?? "TCP"
            };

            var triggeredRules = new List<string>();
            if (reader["TriggeredRules"] != DBNull.Value)
            {
                try
                {
                    var rulesJson = reader["TriggeredRules"]?.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(rulesJson))
                    {
                        triggeredRules = JsonSerializer.Deserialize<List<string>>(rulesJson) ?? new List<string>();
                    }
                }
                catch { }
            }

            var executedActions = new List<string>();
            if (reader["ExecutedActions"] != DBNull.Value)
            {
                try
                {
                    var actionsJson = reader["ExecutedActions"]?.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(actionsJson))
                    {
                        executedActions = JsonSerializer.Deserialize<List<string>>(actionsJson) ?? new List<string>();
                    }
                }
                catch { }
            }

            return new UnifiedBlockedConnection
            {
                Id = Convert.ToInt32(reader["Id"]),
                ProcessInfo = processInfo,
                Source = Enum.Parse<BlockSource>(reader["BlockSource"]?.ToString() ?? "Manual"),
                Level = (BlockLevel)Convert.ToInt32(reader["BlockLevel"]),
                Reason = reader["Reason"]?.ToString() ?? string.Empty,
                UserNote = reader["UserNote"]?.ToString(),
                TriggeredRules = triggeredRules,
                ConfidenceScore = reader["ConfidenceScore"] != DBNull.Value ? Convert.ToDouble(reader["ConfidenceScore"]) : null,
                ThreatCategory = reader["ThreatCategory"]?.ToString(),
                BlockedAt = Convert.ToDateTime(reader["BlockedAt"]),
                UnblockedAt = reader["UnblockedAt"] != DBNull.Value ? Convert.ToDateTime(reader["UnblockedAt"]) : null,
                IsActive = Convert.ToBoolean(reader["IsActive"]),
                ParentProcessId = reader["ParentProcessId"] != DBNull.Value ? Convert.ToInt32(reader["ParentProcessId"]) : null,
                ParentProcessName = reader["ParentProcessName"]?.ToString(),
                IsRelatedToChildBlock = Convert.ToBoolean(reader["IsRelatedToChildBlock"]),
                ErrorMessage = reader["ErrorMessage"]?.ToString(),
                ExecutedActions = executedActions
            };
        }

        private async Task ExecuteNonQueryAsync(SqliteConnection connection, string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }

        #endregion

        #region IDisposable 구현

        public void Dispose()
        {
            if (!_disposed)
            {
                _batchTimer?.Dispose();
                _disposed = true;
            }
        }

        #endregion
    }
}