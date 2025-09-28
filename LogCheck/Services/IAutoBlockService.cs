using LogCheck.Models;

namespace LogCheck.Services
{
    /// <summary>
    /// 자동 차단 서비스의 핵심 인터페이스
    /// Rules.md 기반 3단계 자동 차단 시스템을 구현하기 위한 서비스 계약
    /// </summary>
    public interface IAutoBlockService
    {
        /// <summary>
        /// 프로세스 네트워크 연결을 분석하여 차단 결정을 내림
        /// </summary>
        /// <param name="processInfo">분석할 프로세스 네트워크 정보</param>
        /// <returns>차단 결정 정보 (레벨, 이유, 신뢰도 등)</returns>
        Task<BlockDecision> AnalyzeConnectionAsync(ProcessNetworkInfo processInfo);

        /// <summary>
        /// 결정된 차단 레벨에 따라 실제 차단 작업을 수행
        /// </summary>
        /// <param name="processInfo">차단할 프로세스 네트워크 정보</param>
        /// <param name="level">차단 레벨 (즉시/경고/모니터링)</param>
        /// <returns>차단 작업 완료 여부</returns>
        Task<bool> BlockConnectionAsync(ProcessNetworkInfo processInfo, BlockLevel level);

        /// <summary>
        /// 프로세스가 화이트리스트에 포함되어 있는지 확인
        /// </summary>
        /// <param name="processInfo">확인할 프로세스 정보</param>
        /// <returns>화이트리스트 포함 여부</returns>
        Task<bool> IsWhitelistedAsync(ProcessNetworkInfo processInfo);

        /// <summary>
        /// 프로세스를 화이트리스트에 추가
        /// </summary>
        /// <param name="processPath">프로세스 경로</param>
        /// <param name="reason">화이트리스트 추가 사유</param>
        /// <returns>추가 성공 여부</returns>
        Task<bool> AddToWhitelistAsync(string processPath, string reason);

        /// <summary>
        /// 화이트리스트에서 프로세스 제거
        /// </summary>
        /// <param name="processPath">제거할 프로세스 경로</param>
        /// <returns>제거 성공 여부</returns>
        Task<bool> RemoveFromWhitelistAsync(string processPath);

        /// <summary>
        /// 차단 이력을 조회
        /// </summary>
        /// <param name="since">조회 시작 시점</param>
        /// <param name="maxResults">최대 조회 건수</param>
        /// <returns>차단된 연결 이력 목록</returns>
        Task<List<AutoBlockedConnection>> GetBlockHistoryAsync(DateTime since, int maxResults = 100);

        /// <summary>
        /// 화이트리스트 전체 목록을 조회
        /// </summary>
        /// <returns>화이트리스트 항목 목록</returns>
        Task<List<AutoWhitelistEntry>> GetWhitelistAsync();

        /// <summary>
        /// 서비스 초기화
        /// </summary>
        /// <returns>초기화 성공 여부</returns>
        Task<bool> InitializeAsync();

        /// <summary>
        /// 서비스 정리 (리소스 해제)
        /// </summary>
        /// <returns>정리 완료 여부</returns>
        Task<bool> CleanupAsync();

        /// <summary>
        /// 차단 통계 정보를 조회
        /// </summary>
        /// <param name="period">조회 기간 (시간, 일, 주, 월)</param>
        /// <returns>차단 통계 정보</returns>
        Task<BlockStatistics> GetBlockStatisticsAsync(StatisticsPeriod period);
    }
}