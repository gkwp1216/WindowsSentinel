using System;
using System.Collections.Generic;
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

            // 일별 차단 통계 테이블
            var createDailyStatsTable = @"
                CREATE TABLE IF NOT EXISTS AutoBlockDailyStats (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Date TEXT NOT NULL UNIQUE,
                    TotalBlocked INTEGER DEFAULT 0,
                    Level1Blocked INTEGER DEFAULT 0,
                    Level2Blocked INTEGER DEFAULT 0,
                    Level3Blocked INTEGER DEFAULT 0,
                    UniqueProcesses INTEGER DEFAULT 0,
                    UniqueIPs INTEGER DEFAULT 0,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
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
                    IPAddress TEXT NOT NULL UNIQUE,
                    TotalBlocked INTEGER DEFAULT 0,
                    LastBlockedAt DATETIME,
                    FirstBlockedAt DATETIME,
                    Country TEXT,
                    ThreatLevel TEXT,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                );";

            using var cmd1 = new SqliteCommand(createDailyStatsTable, connection);
            await cmd1.ExecuteNonQueryAsync();

            using var cmd2 = new SqliteCommand(createProcessStatsTable, connection);
            await cmd2.ExecuteNonQueryAsync();

            using var cmd3 = new SqliteCommand(createIPStatsTable, connection);
            await cmd3.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// 차단 이벤트 기록 및 통계 업데이트
        /// </summary>
        public async Task RecordBlockEventAsync(AutoBlockedConnection blockedConnection)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // 1. 일별 통계 업데이트
                await UpdateDailyStatsAsync(connection, blockedConnection, transaction);

                // 2. 프로세스별 통계 업데이트
                await UpdateProcessStatsAsync(connection, blockedConnection, transaction);

                // 3. IP별 통계 업데이트
                await UpdateIPStatsAsync(connection, blockedConnection, transaction);

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task UpdateDailyStatsAsync(SqliteConnection conn, AutoBlockedConnection blocked, SqliteTransaction tx)
        {
            var today = DateTime.Now.Date.ToString("yyyy-MM-dd");

            var sql = @"
                INSERT OR IGNORE INTO AutoBlockDailyStats (Date, TotalBlocked, Level1Blocked, Level2Blocked, Level3Blocked)
                VALUES (@Date, 0, 0, 0, 0);
                
                UPDATE AutoBlockDailyStats SET
                    TotalBlocked = TotalBlocked + 1,
                    Level1Blocked = Level1Blocked + @Level1,
                    Level2Blocked = Level2Blocked + @Level2,
                    Level3Blocked = Level3Blocked + @Level3
                WHERE Date = @Date;";

            var level1 = blocked.BlockLevel == BlockLevel.Immediate ? 1 : 0;
            var level2 = blocked.BlockLevel == BlockLevel.Warning ? 1 : 0;
            var level3 = blocked.BlockLevel == BlockLevel.Monitor ? 1 : 0;

            using var cmd = new SqliteCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@Date", today);
            cmd.Parameters.AddWithValue("@Level1", level1);
            cmd.Parameters.AddWithValue("@Level2", level2);
            cmd.Parameters.AddWithValue("@Level3", level3);
            await cmd.ExecuteNonQueryAsync();

            // 고유 프로세스 및 IP 카운트 업데이트
            await UpdateUniqueCountsAsync(conn, today, tx);
        }

        private async Task UpdateProcessStatsAsync(SqliteConnection conn, AutoBlockedConnection blocked, SqliteTransaction tx)
        {
            var sql = @"
                INSERT OR IGNORE INTO ProcessBlockStats (ProcessName, ProcessPath, TotalBlocked, FirstBlockedAt, LastBlockedAt, UpdatedAt)
                VALUES (@ProcessName, @ProcessPath, 0, @Now, @Now, @Now);
                
                UPDATE ProcessBlockStats SET
                    TotalBlocked = TotalBlocked + 1,
                    LastBlockedAt = @Now,
                    UpdatedAt = @Now
                WHERE ProcessName = @ProcessName;";

            using var cmd = new SqliteCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@ProcessName", blocked.ProcessName ?? "Unknown");
            cmd.Parameters.AddWithValue("@ProcessPath", blocked.ProcessPath ?? "");
            cmd.Parameters.AddWithValue("@Now", DateTime.Now);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task UpdateIPStatsAsync(SqliteConnection conn, AutoBlockedConnection blocked, SqliteTransaction tx)
        {
            var sql = @"
                INSERT OR IGNORE INTO IPBlockStats (IPAddress, TotalBlocked, FirstBlockedAt, LastBlockedAt, ThreatLevel, UpdatedAt)
                VALUES (@IPAddress, 0, @Now, @Now, @ThreatLevel, @Now);
                
                UPDATE IPBlockStats SET
                    TotalBlocked = TotalBlocked + 1,
                    LastBlockedAt = @Now,
                    ThreatLevel = @ThreatLevel,
                    UpdatedAt = @Now
                WHERE IPAddress = @IPAddress;";

            using var cmd = new SqliteCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@IPAddress", blocked.RemoteAddress ?? "Unknown");
            cmd.Parameters.AddWithValue("@ThreatLevel", blocked.BlockLevel.ToString());
            cmd.Parameters.AddWithValue("@Now", DateTime.Now);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task UpdateUniqueCountsAsync(SqliteConnection conn, string date, SqliteTransaction tx)
        {
            // AutoBlockHistory 테이블에서 고유 프로세스 및 IP 카운트 계산
            var updateUniqueCountsSql = @"
                UPDATE AutoBlockDailyStats SET
                    UniqueProcesses = (
                        SELECT COUNT(DISTINCT ProcessName) 
                        FROM AutoBlockHistory 
                        WHERE date(BlockedAt) = @Date
                    ),
                    UniqueIPs = (
                        SELECT COUNT(DISTINCT RemoteAddress) 
                        FROM AutoBlockHistory 
                        WHERE date(BlockedAt) = @Date
                    )
                WHERE Date = @Date;";

            using var cmd = new SqliteCommand(updateUniqueCountsSql, conn, tx);
            cmd.Parameters.AddWithValue("@Date", date);
            await cmd.ExecuteNonQueryAsync();
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
                    ProcessPath = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    ProcessId = reader.GetInt32(3),
                    RemoteAddress = reader.GetString(4),
                    RemotePort = reader.GetInt32(5),
                    Protocol = reader.GetString(6),
                    BlockedAt = DateTime.Parse(reader.GetString(7)),
                    BlockLevel = (BlockLevel)reader.GetInt32(8),
                    Reason = reader.GetString(9),
                    ConfidenceScore = reader.GetDouble(10),
                    TriggeredRules = reader.IsDBNull(11) ? string.Empty : reader.GetString(11)
                });
            }

            return connections;
        }

        /// <summary>
        /// 차단된 연결을 삭제합니다.
        /// </summary>
        public async Task<bool> RemoveBlockedConnectionAsync(int connectionId)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var sql = "DELETE FROM BlockedConnections WHERE Id = @Id";
                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@Id", connectionId);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
            catch
            {
                return false;
            }
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

            // 연결 정보 추가
            var insertSql = @"
                INSERT INTO BlockedConnections 
                (ProcessName, ProcessPath, ProcessId, RemoteAddress, RemotePort, Protocol, 
                 BlockedAt, BlockLevel, Reason, ConfidenceScore, TriggeredRules)
                VALUES 
                (@ProcessName, @ProcessPath, @ProcessId, @RemoteAddress, @RemotePort, @Protocol,
                 @BlockedAt, @BlockLevel, @Reason, @ConfidenceScore, @TriggeredRules)";

            using var cmd = new SqliteCommand(insertSql, dbConnection);
            cmd.Parameters.AddWithValue("@ProcessName", connection.ProcessName);
            cmd.Parameters.AddWithValue("@ProcessPath", connection.ProcessPath ?? string.Empty);
            cmd.Parameters.AddWithValue("@ProcessId", connection.ProcessId);
            cmd.Parameters.AddWithValue("@RemoteAddress", connection.RemoteAddress);
            cmd.Parameters.AddWithValue("@RemotePort", connection.RemotePort);
            cmd.Parameters.AddWithValue("@Protocol", connection.Protocol);
            cmd.Parameters.AddWithValue("@BlockedAt", connection.BlockedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@BlockLevel", connection.BlockLevel);
            cmd.Parameters.AddWithValue("@Reason", connection.Reason);
            cmd.Parameters.AddWithValue("@ConfidenceScore", connection.ConfidenceScore);
            cmd.Parameters.AddWithValue("@TriggeredRules", connection.TriggeredRules ?? string.Empty);

            await cmd.ExecuteNonQueryAsync();
        }
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