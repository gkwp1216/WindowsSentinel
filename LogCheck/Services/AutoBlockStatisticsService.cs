using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using LogCheck.Models;
using Microsoft.Data.Sqlite;

namespace LogCheck.Services
{
    public class AutoBlockStatisticsService
    {
        private readonly string _connectionString;

        public AutoBlockStatisticsService(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// 데이터베이스 초기화 및 통계 테이블 생성
        /// </summary>
        public async Task InitializeDatabaseAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // 기본 차단 연결 테이블
            var createBlockedConnectionsTable = @"
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
                    ConfidenceScore REAL DEFAULT 0.0,
                    TriggeredRules TEXT,
                    BlockedAt DATETIME NOT NULL,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                );";

            // 일별 통계 테이블
            var createDailyStatsTable = @"
                CREATE TABLE IF NOT EXISTS AutoBlockDailyStats (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Date DATE NOT NULL UNIQUE,
                    TotalBlocked INTEGER DEFAULT 0,
                    Level1Blocked INTEGER DEFAULT 0,
                    Level2Blocked INTEGER DEFAULT 0,
                    Level3Blocked INTEGER DEFAULT 0,
                    UniqueProcesses INTEGER DEFAULT 0,
                    UniqueIPs INTEGER DEFAULT 0,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                );";

            // 프로세스별 차단 통계 테이블
            var createProcessStatsTable = @"
                CREATE TABLE IF NOT EXISTS ProcessBlockStats (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProcessName TEXT NOT NULL,
                    ProcessPath TEXT,
                    TotalBlocked INTEGER DEFAULT 0,
                    LastBlockedAt DATETIME,
                    FirstBlockedAt DATETIME,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                );";

            // IP별 차단 통계 테이블
            var createIPStatsTable = @"
                CREATE TABLE IF NOT EXISTS IPBlockStats (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    IPAddress TEXT NOT NULL,
                    TotalBlocked INTEGER DEFAULT 0,
                    LastBlockedAt DATETIME,
                    FirstBlockedAt DATETIME,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                );";

            // 테이블들 생성
            await using var cmd = new SqliteCommand(createBlockedConnectionsTable, connection);
            await cmd.ExecuteNonQueryAsync();

            cmd.CommandText = createDailyStatsTable;
            await cmd.ExecuteNonQueryAsync();

            cmd.CommandText = createProcessStatsTable;
            await cmd.ExecuteNonQueryAsync();

            cmd.CommandText = createIPStatsTable;
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// 차단 이벤트 기록 및 통계 업데이트
        /// </summary>
        public async Task RecordBlockEventAsync(AutoBlockedConnection blockedConnection)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var transaction = await connection.BeginTransactionAsync();
            try
            {
                // 차단 연결 기록
                var insertSql = @"
                    INSERT INTO BlockedConnections (
                        ProcessName, ProcessPath, ProcessId, RemoteAddress, RemotePort, 
                        LocalPort, Protocol, BlockLevel, Reason, ConfidenceScore, 
                        TriggeredRules, BlockedAt
                    ) VALUES (
                        @ProcessName, @ProcessPath, @ProcessId, @RemoteAddress, @RemotePort,
                        @LocalPort, @Protocol, @BlockLevel, @Reason, @ConfidenceScore,
                        @TriggeredRules, @BlockedAt
                    )";

                using var insertCmd = new SqliteCommand(insertSql, connection, transaction);
                insertCmd.Parameters.AddWithValue("@ProcessName", blockedConnection.ProcessName);
                insertCmd.Parameters.AddWithValue("@ProcessPath", blockedConnection.ProcessPath ?? "");
                insertCmd.Parameters.AddWithValue("@ProcessId", blockedConnection.ProcessId);
                insertCmd.Parameters.AddWithValue("@RemoteAddress", blockedConnection.RemoteAddress);
                insertCmd.Parameters.AddWithValue("@RemotePort", blockedConnection.RemotePort);
                insertCmd.Parameters.AddWithValue("@LocalPort", blockedConnection.LocalPort);
                insertCmd.Parameters.AddWithValue("@Protocol", blockedConnection.Protocol);
                insertCmd.Parameters.AddWithValue("@BlockLevel", (int)blockedConnection.BlockLevel);
                insertCmd.Parameters.AddWithValue("@Reason", blockedConnection.Reason);
                insertCmd.Parameters.AddWithValue("@ConfidenceScore", blockedConnection.ConfidenceScore);
                insertCmd.Parameters.AddWithValue("@TriggeredRules", blockedConnection.TriggeredRules ?? "");
                insertCmd.Parameters.AddWithValue("@BlockedAt", blockedConnection.BlockedAt.ToString("yyyy-MM-dd HH:mm:ss"));

                await insertCmd.ExecuteNonQueryAsync();

                // 통계 업데이트
                await UpdateDailyStatsAsync(connection, blockedConnection, transaction);
                await UpdateProcessStatsAsync(connection, blockedConnection, transaction);
                await UpdateIPStatsAsync(connection, blockedConnection, transaction);

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                System.Diagnostics.Debug.WriteLine($"차단 이벤트 기록 오류: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 차단된 연결 목록을 가져옵니다.
        /// </summary>
        public async Task<List<AutoBlockedConnection>> GetBlockedConnectionsAsync()
        {
            var connections = new List<AutoBlockedConnection>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT Id, ProcessName, ProcessPath, ProcessId, RemoteAddress, RemotePort, 
                       Protocol, BlockedAt, BlockLevel, Reason, ConfidenceScore, TriggeredRules
                FROM BlockedConnections 
                ORDER BY BlockedAt DESC";

            using var cmd = new SqliteCommand(sql, connection);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                connections.Add(new AutoBlockedConnection
                {
                    Id = reader.GetInt32(0),
                    ProcessName = reader.GetString(1),
                    ProcessPath = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    ProcessId = reader.GetInt32(3),
                    RemoteAddress = reader.GetString(4),
                    RemotePort = reader.GetInt32(5),
                    Protocol = reader.IsDBNull(6) ? "TCP" : reader.GetString(6),
                    BlockedAt = DateTime.Parse(reader.GetString(7)),
                    BlockLevel = (BlockLevel)reader.GetInt32(8),
                    Reason = reader.GetString(9),
                    ConfidenceScore = reader.IsDBNull(10) ? 0.0 : reader.GetDouble(10),
                    TriggeredRules = reader.IsDBNull(11) ? "" : reader.GetString(11),
                    IsBlocked = true
                });
            }

            return connections;
        }

        /// <summary>
        /// 영구 차단된 연결 목록을 가져옵니다 (방화벽 규칙과 연결된)
        /// </summary>
        public async Task<List<PermanentBlockedConnection>> GetPermanentlyBlockedConnectionsAsync()
        {
            var connections = new List<PermanentBlockedConnection>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT DISTINCT 
                    bc.ProcessName, 
                    bc.ProcessPath, 
                    bc.RemoteAddress, 
                    bc.RemotePort, 
                    bc.Protocol,
                    bc.BlockLevel,
                    bc.Reason,
                    bc.BlockedAt,
                    COUNT(*) as BlockCount,
                    MAX(bc.BlockedAt) as LastBlockedAt
                FROM BlockedConnections bc 
                WHERE bc.BlockedAt > datetime('now', '-30 days')
                GROUP BY bc.ProcessName, bc.ProcessPath, bc.RemoteAddress, bc.RemotePort, bc.Protocol
                ORDER BY LastBlockedAt DESC";

            using var cmd = new SqliteCommand(sql, connection);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var blockedConnection = new PermanentBlockedConnection
                {
                    ProcessName = reader.GetString(0),
                    ProcessPath = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    RemoteAddress = reader.GetString(2),
                    RemotePort = reader.GetInt32(3),
                    Protocol = reader.IsDBNull(4) ? "TCP" : reader.GetString(4),
                    BlockLevel = reader.GetInt32(5),
                    Reason = reader.GetString(6),
                    FirstBlockedAt = DateTime.Parse(reader.GetString(7)),
                    LastBlockedAt = DateTime.Parse(reader.GetString(9)),
                    BlockCount = reader.GetInt32(8),
                    IsPermanentlyBlocked = true, // 데이터베이스에서 가져온 것은 영구 차단으로 간주
                    FirewallRuleExists = false // 이후 PersistentFirewallManager로 확인
                };

                connections.Add(blockedConnection);
            }

            return connections;
        }

        /// <summary>
        /// 영구 차단 연결의 방화벽 규칙 존재 여부 업데이트
        /// </summary>
        [SupportedOSPlatform("windows")]
        public async Task<List<PermanentBlockedConnection>> UpdateFirewallRuleStatusAsync(
            List<PermanentBlockedConnection> connections)
        {
            try
            {
                var persistentFirewallManager = new PersistentFirewallManager();
                await persistentFirewallManager.InitializeAsync();

                var firewallRules = await persistentFirewallManager.GetLogCheckRulesAsync();

                foreach (var connection in connections)
                {
                    // 방화벽 규칙 존재 여부 확인
                    connection.FirewallRuleExists = firewallRules.Any(rule =>
                        rule.ApplicationName.Contains(connection.ProcessName, StringComparison.OrdinalIgnoreCase) ||
                        rule.RemoteAddresses.Contains(connection.RemoteAddress));
                }

                return connections;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"방화벽 규칙 상태 업데이트 오류: {ex.Message}");
                return connections;
            }
        }

        /// <summary>
        /// 영구 및 임시 차단 분리 통계 가져오기
        /// </summary>
        public async Task<SeparatedBlockStatistics> GetSeparatedBlockStatisticsAsync()
        {
            var statistics = new SeparatedBlockStatistics();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            try
            {
                // 전체 차단 통계
                var totalSql = @"
                    SELECT COUNT(*) FROM BlockedConnections 
                    WHERE BlockedAt >= datetime('now', '-24 hours')";
                using var totalCmd = new SqliteCommand(totalSql, connection);
                statistics.TotalBlocked = Convert.ToInt32(await totalCmd.ExecuteScalarAsync());

                // 임시 차단 통계 (최근 차단되었지만 영구 차단 목록에 없는 것들)
                var tempSql = @"
                    SELECT COUNT(*) FROM BlockedConnections bc
                    WHERE bc.BlockedAt >= datetime('now', '-24 hours')
                    AND NOT EXISTS (
                        SELECT 1 FROM BlockedConnections bc2 
                        WHERE bc2.ProcessName = bc.ProcessName 
                        AND bc2.RemoteAddress = bc.RemoteAddress
                        AND bc2.BlockedAt < datetime('now', '-7 days')
                    )";
                using var tempCmd = new SqliteCommand(tempSql, connection);
                statistics.TemporaryBlocked = Convert.ToInt32(await tempCmd.ExecuteScalarAsync());

                // 영구 차단 통계 (7일 이상 지속적으로 차단된 연결들)
                var permSql = @"
                    SELECT COUNT(DISTINCT ProcessName, RemoteAddress) 
                    FROM BlockedConnections 
                    WHERE BlockedAt < datetime('now', '-7 days')";
                using var permCmd = new SqliteCommand(permSql, connection);
                statistics.PermanentBlocked = Convert.ToInt32(await permCmd.ExecuteScalarAsync());

                // 최근 1시간 차단
                var recentSql = @"
                    SELECT COUNT(*) FROM BlockedConnections 
                    WHERE BlockedAt >= datetime('now', '-1 hour')";
                using var recentCmd = new SqliteCommand(recentSql, connection);
                statistics.RecentBlocked = Convert.ToInt32(await recentCmd.ExecuteScalarAsync());

                // 성공률 계산
                statistics.BlockSuccessRate = statistics.TotalBlocked > 0 ?
                    Math.Round((double)(statistics.TotalBlocked - statistics.RecentBlocked) / statistics.TotalBlocked * 100, 1) : 100.0;

                return statistics;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"분리 통계 조회 오류: {ex.Message}");
                return statistics;
            }
        }

        /// <summary>
        /// 실시간 통계 조회
        /// </summary>
        public async Task<AutoBlockStatistics> GetCurrentStatisticsAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT 
                    COALESCE(SUM(TotalBlocked), 0) as TotalBlocked,
                    COALESCE(SUM(Level1Blocked), 0) as Level1Blocked,
                    COALESCE(SUM(Level2Blocked), 0) as Level2Blocked,
                    COALESCE(SUM(Level3Blocked), 0) as Level3Blocked,
                    COALESCE(MAX(UniqueProcesses), 0) as UniqueProcesses,
                    COALESCE(MAX(UniqueIPs), 0) as UniqueIPs
                FROM AutoBlockDailyStats 
                WHERE Date >= date('now', '-30 days')";

            using var cmd = new SqliteCommand(sql, connection);
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new AutoBlockStatistics
                {
                    TotalBlocked = reader.GetInt32(0),
                    Level1Blocked = reader.GetInt32(1),
                    Level2Blocked = reader.GetInt32(2),
                    Level3Blocked = reader.GetInt32(3),
                    UniqueProcesses = reader.GetInt32(4),
                    UniqueIPs = reader.GetInt32(5),
                    LastUpdated = DateTime.Now
                };
            }

            return new AutoBlockStatistics();
        }

        /// <summary>
        /// 오늘의 통계 조회
        /// </summary>
        public async Task<AutoBlockStatistics> GetTodayStatisticsAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var today = DateTime.Now.Date.ToString("yyyy-MM-dd");
            var sql = @"
                SELECT 
                    COALESCE(TotalBlocked, 0) as TotalBlocked,
                    COALESCE(Level1Blocked, 0) as Level1Blocked,
                    COALESCE(Level2Blocked, 0) as Level2Blocked,
                    COALESCE(Level3Blocked, 0) as Level3Blocked,
                    COALESCE(UniqueProcesses, 0) as UniqueProcesses,
                    COALESCE(UniqueIPs, 0) as UniqueIPs
                FROM AutoBlockDailyStats 
                WHERE Date = @Today";

            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Today", today);
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new AutoBlockStatistics
                {
                    TotalBlocked = reader.GetInt32(0),
                    Level1Blocked = reader.GetInt32(1),
                    Level2Blocked = reader.GetInt32(2),
                    Level3Blocked = reader.GetInt32(3),
                    UniqueProcesses = reader.GetInt32(4),
                    UniqueIPs = reader.GetInt32(5),
                    LastUpdated = DateTime.Now
                };
            }

            return new AutoBlockStatistics();
        }

        /// <summary>
        /// 차단된 연결을 삭제합니다.
        /// </summary>
        public async Task<bool> RemoveBlockedConnectionAsync(int connectionId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "DELETE FROM BlockedConnections WHERE Id = @Id";
            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", connectionId);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }

        /// <summary>
        /// 모든 차단된 연결을 삭제합니다.
        /// </summary>
        public async Task ClearAllBlockedConnectionsAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "DELETE FROM BlockedConnections";
            using var cmd = new SqliteCommand(sql, connection);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// 차단된 연결을 추가합니다.
        /// </summary>
        public async Task AddBlockedConnectionAsync(AutoBlockedConnection connection)
        {
            using var dbConnection = new SqliteConnection(_connectionString);
            await dbConnection.OpenAsync();

            // BlockedConnections 테이블이 없으면 생성
            var createTableSql = @"
                CREATE TABLE IF NOT EXISTS BlockedConnections (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProcessName TEXT NOT NULL,
                    ProcessPath TEXT,
                    ProcessId INTEGER NOT NULL,
                    RemoteAddress TEXT NOT NULL,
                    RemotePort INTEGER NOT NULL,
                    Protocol TEXT NOT NULL,
                    BlockedAt DATETIME NOT NULL,
                    BlockLevel INTEGER NOT NULL,
                    Reason TEXT NOT NULL,
                    ConfidenceScore REAL DEFAULT 0.0,
                    TriggeredRules TEXT,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                );";

            using var createCmd = new SqliteCommand(createTableSql, dbConnection);
            await createCmd.ExecuteNonQueryAsync();

            // 데이터 삽입
            var insertSql = @"
                INSERT INTO BlockedConnections (
                    ProcessName, ProcessPath, ProcessId, RemoteAddress, RemotePort, 
                    Protocol, BlockedAt, BlockLevel, Reason, ConfidenceScore, TriggeredRules
                ) VALUES (
                    @ProcessName, @ProcessPath, @ProcessId, @RemoteAddress, @RemotePort,
                    @Protocol, @BlockedAt, @BlockLevel, @Reason, @ConfidenceScore, @TriggeredRules
                )";

            using var insertCmd = new SqliteCommand(insertSql, dbConnection);
            insertCmd.Parameters.AddWithValue("@ProcessName", connection.ProcessName);
            insertCmd.Parameters.AddWithValue("@ProcessPath", connection.ProcessPath ?? "");
            insertCmd.Parameters.AddWithValue("@ProcessId", connection.ProcessId);
            insertCmd.Parameters.AddWithValue("@RemoteAddress", connection.RemoteAddress);
            insertCmd.Parameters.AddWithValue("@RemotePort", connection.RemotePort);
            insertCmd.Parameters.AddWithValue("@Protocol", connection.Protocol);
            insertCmd.Parameters.AddWithValue("@BlockedAt", connection.BlockedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            insertCmd.Parameters.AddWithValue("@BlockLevel", (int)connection.BlockLevel);
            insertCmd.Parameters.AddWithValue("@Reason", connection.Reason);
            insertCmd.Parameters.AddWithValue("@ConfidenceScore", connection.ConfidenceScore);
            insertCmd.Parameters.AddWithValue("@TriggeredRules", connection.TriggeredRules ?? "");

            await insertCmd.ExecuteNonQueryAsync();
        }

        #region Private Helper Methods

        private async Task UpdateDailyStatsAsync(SqliteConnection conn, AutoBlockedConnection blocked, SqliteTransaction tx)
        {
            var date = blocked.BlockedAt.Date.ToString("yyyy-MM-dd");
            var upsertSql = @"
                INSERT INTO AutoBlockDailyStats (Date, TotalBlocked, Level1Blocked, Level2Blocked, Level3Blocked, UniqueProcesses, UniqueIPs)
                VALUES (@Date, 1, 
                    CASE WHEN @BlockLevel = 1 THEN 1 ELSE 0 END,
                    CASE WHEN @BlockLevel = 2 THEN 1 ELSE 0 END,
                    CASE WHEN @BlockLevel = 3 THEN 1 ELSE 0 END, 1, 1)
                ON CONFLICT(Date) DO UPDATE SET
                    TotalBlocked = TotalBlocked + 1,
                    Level1Blocked = Level1Blocked + CASE WHEN @BlockLevel = 1 THEN 1 ELSE 0 END,
                    Level2Blocked = Level2Blocked + CASE WHEN @BlockLevel = 2 THEN 1 ELSE 0 END,
                    Level3Blocked = Level3Blocked + CASE WHEN @BlockLevel = 3 THEN 1 ELSE 0 END,
                    UpdatedAt = CURRENT_TIMESTAMP";

            using var cmd = new SqliteCommand(upsertSql, conn, tx);
            cmd.Parameters.AddWithValue("@Date", date);
            cmd.Parameters.AddWithValue("@BlockLevel", (int)blocked.BlockLevel);
            await cmd.ExecuteNonQueryAsync();

            await UpdateUniqueCountsAsync(conn, date, tx);
        }

        private async Task UpdateProcessStatsAsync(SqliteConnection conn, AutoBlockedConnection blocked, SqliteTransaction tx)
        {
            var upsertSql = @"
                INSERT INTO ProcessBlockStats (ProcessName, ProcessPath, TotalBlocked, FirstBlockedAt, LastBlockedAt)
                VALUES (@ProcessName, @ProcessPath, 1, @BlockedAt, @BlockedAt)
                ON CONFLICT(ProcessName) DO UPDATE SET
                    TotalBlocked = TotalBlocked + 1,
                    LastBlockedAt = @BlockedAt,
                    UpdatedAt = CURRENT_TIMESTAMP";

            using var cmd = new SqliteCommand(upsertSql, conn, tx);
            cmd.Parameters.AddWithValue("@ProcessName", blocked.ProcessName);
            cmd.Parameters.AddWithValue("@ProcessPath", blocked.ProcessPath ?? "");
            cmd.Parameters.AddWithValue("@BlockedAt", blocked.BlockedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task UpdateIPStatsAsync(SqliteConnection conn, AutoBlockedConnection blocked, SqliteTransaction tx)
        {
            var upsertSql = @"
                INSERT INTO IPBlockStats (IPAddress, TotalBlocked, FirstBlockedAt, LastBlockedAt)
                VALUES (@IPAddress, 1, @BlockedAt, @BlockedAt)
                ON CONFLICT(IPAddress) DO UPDATE SET
                    TotalBlocked = TotalBlocked + 1,
                    LastBlockedAt = @BlockedAt,
                    UpdatedAt = CURRENT_TIMESTAMP";

            using var cmd = new SqliteCommand(upsertSql, conn, tx);
            cmd.Parameters.AddWithValue("@IPAddress", blocked.RemoteAddress);
            cmd.Parameters.AddWithValue("@BlockedAt", blocked.BlockedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task UpdateUniqueCountsAsync(SqliteConnection conn, string date, SqliteTransaction tx)
        {
            // 해당 날짜의 고유 프로세스 수 업데이트
            var updateProcessCountSql = @"
                UPDATE AutoBlockDailyStats 
                SET UniqueProcesses = (
                    SELECT COUNT(DISTINCT ProcessName) 
                    FROM BlockedConnections 
                    WHERE DATE(BlockedAt) = @Date
                )
                WHERE Date = @Date";

            using var processCmd = new SqliteCommand(updateProcessCountSql, conn, tx);
            processCmd.Parameters.AddWithValue("@Date", date);
            await processCmd.ExecuteNonQueryAsync();

            // 해당 날짜의 고유 IP 수 업데이트
            var updateIPCountSql = @"
                UPDATE AutoBlockDailyStats 
                SET UniqueIPs = (
                    SELECT COUNT(DISTINCT RemoteAddress) 
                    FROM BlockedConnections 
                    WHERE DATE(BlockedAt) = @Date
                )
                WHERE Date = @Date";

            using var ipCmd = new SqliteCommand(updateIPCountSql, conn, tx);
            ipCmd.Parameters.AddWithValue("@Date", date);
            await ipCmd.ExecuteNonQueryAsync();
        }

        #endregion
    }

    /// <summary>
    /// 영구/임시 차단 분리 통계 모델
    /// </summary>
    public class SeparatedBlockStatistics
    {
        public int TotalBlocked { get; set; }
        public int TemporaryBlocked { get; set; }
        public int PermanentBlocked { get; set; }
        public int RecentBlocked { get; set; }
        public double BlockSuccessRate { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }

    public class AutoBlockStatistics
    {
        public int TotalBlocked { get; set; }
        public int Level1Blocked { get; set; }
        public int Level2Blocked { get; set; }
        public int Level3Blocked { get; set; }
        public int UniqueProcesses { get; set; }
        public int UniqueIPs { get; set; }
        public DateTime LastUpdated { get; set; }

        // UI에서 요구하는 추가 속성들
        public int Level1Blocks => Level1Blocked;
        public int Level2Blocks => Level2Blocked;
        public int Level3Blocks => Level3Blocked;
    }
}