using LogCheck.Models;

namespace LogCheck.Services
{
    /// <summary>
    /// 통합 차단 관리 서비스 인터페이스
    /// AutoBlock 시스템과 수동 차단을 통합 관리
    /// </summary>
    public interface IUnifiedBlockingService
    {
        /// <summary>
        /// 서비스 초기화 (데이터베이스 스키마 생성 등)
        /// </summary>
        Task<bool> InitializeAsync();

        /// <summary>
        /// 연결 차단 실행 (모든 차단 유형 통합)
        /// </summary>
        Task<BlockResult> BlockConnectionAsync(ProcessNetworkInfo process, BlockRequest request);

        /// <summary>
        /// 연결 차단 해제
        /// </summary>
        Task<bool> UnblockConnectionAsync(int connectionId, string reason = "");

        /// <summary>
        /// 모든 차단된 연결 조회
        /// </summary>
        Task<List<UnifiedBlockedConnection>> GetAllBlockedConnectionsAsync(BlockFilter? filter = null);

        /// <summary>
        /// 특정 프로세스의 차단 상태 확인
        /// </summary>
        Task<bool> IsConnectionBlockedAsync(ProcessNetworkInfo process);

        /// <summary>
        /// 차단 통계 조회
        /// </summary>
        Task<BlockingStatistics> GetBlockingStatisticsAsync(TimeSpan? period = null);

        /// <summary>
        /// 차단 이력 조회
        /// </summary>
        Task<List<UnifiedBlockedConnection>> GetBlockHistoryAsync(DateTime since, int limit = 100);

        /// <summary>
        /// 차단 규칙 업데이트
        /// </summary>
        Task<bool> UpdateBlockingRulesAsync(List<BlockingRule> rules);

        /// <summary>
        /// 이벤트: 새로운 연결이 차단될 때
        /// </summary>
        event EventHandler<UnifiedBlockedConnection>? OnConnectionBlocked;

        /// <summary>
        /// 이벤트: 연결이 차단 해제될 때
        /// </summary>
        event EventHandler<UnifiedBlockedConnection>? OnConnectionUnblocked;
    }

    /// <summary>
    /// 차단 요청 정보
    /// </summary>
    public class BlockRequest
    {
        public BlockSource Source { get; set; }
        public BlockLevel Level { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? UserNote { get; set; }
        public List<string>? TriggeredRules { get; set; }
        public double? ConfidenceScore { get; set; }
        public string? ThreatCategory { get; set; }
        public DateTime RequestedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 차단 결과
    /// </summary>
    public class BlockResult
    {
        public bool Success { get; set; }
        public int? BlockedConnectionId { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string>? ExecutedActions { get; set; }
    }

    /// <summary>
    /// 통합 차단된 연결 정보
    /// </summary>
    public class UnifiedBlockedConnection
    {
        public int Id { get; set; }
        public ProcessNetworkInfo ProcessInfo { get; set; } = new();
        public BlockSource Source { get; set; }
        public BlockLevel Level { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? UserNote { get; set; }
        public List<string>? TriggeredRules { get; set; }
        public double? ConfidenceScore { get; set; }
        public string? ThreatCategory { get; set; }
        public DateTime BlockedAt { get; set; }
        public DateTime? UnblockedAt { get; set; }
        public bool IsActive { get; set; } = true;
        public string? ErrorMessage { get; set; }
        public List<string>? ExecutedActions { get; set; }

        // 부모 프로세스 정보
        public int? ParentProcessId { get; set; }
        public string? ParentProcessName { get; set; }
        public bool IsRelatedToChildBlock { get; set; }

        // UI 표시용 속성
        public string SourceDisplayName => Source switch
        {
            BlockSource.Manual => "수동 차단",
            BlockSource.AutoBlock => "자동 차단",
            BlockSource.System => "시스템 차단",
            BlockSource.Policy => "정책 차단",
            _ => "알 수 없음"
        };

        public string LevelDisplayName => Level switch
        {
            BlockLevel.None => "차단 없음",
            BlockLevel.Monitor => "모니터링",
            BlockLevel.Warning => "경고",
            BlockLevel.Immediate => "즉시 차단",
            _ => "알 수 없음"
        };
    }

    /// <summary>
    /// 차단 소스 유형
    /// </summary>
    public enum BlockSource
    {
        Manual,      // 사용자 수동 차단
        AutoBlock,   // AutoBlock 시스템 자동 차단
        System,      // 시스템 보안 정책 차단
        Policy       // 관리자 정책 차단
    }

    /// <summary>
    /// 차단 필터
    /// </summary>
    public class BlockFilter
    {
        public BlockSource? Source { get; set; }
        public BlockLevel? Level { get; set; }
        public string? ProcessName { get; set; }
        public string? RemoteAddress { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public bool? IsActive { get; set; } = true;
    }

    /// <summary>
    /// 차단 통계
    /// </summary>
    public class BlockingStatistics
    {
        public int TotalBlocked { get; set; }
        public int ManualBlocked { get; set; }
        public int AutoBlocked { get; set; }
        public int SystemBlocked { get; set; }
        public int ActiveBlocks { get; set; }
        public int TodayBlocked { get; set; }
        public Dictionary<BlockLevel, int> BlocksByLevel { get; set; } = new();
        public Dictionary<string, int> BlocksByThreatCategory { get; set; } = new();
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// 차단 규칙
    /// </summary>
    public class BlockingRule
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public BlockSource ApplicableSource { get; set; }
        public bool IsEnabled { get; set; } = true;
        public string RuleExpression { get; set; } = string.Empty;
        public BlockLevel DefaultLevel { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
    }
}