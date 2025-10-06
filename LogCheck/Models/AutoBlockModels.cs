using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LogCheck.Models
{
    /// <summary>
    /// 자동 차단 시스템의 차단 레벨 정의
    /// Rules.md의 3단계 차단 시스템에 따른 분류
    /// </summary>
    public enum BlockLevel
    {
        /// <summary>
        /// 차단하지 않음 (정상 연결)
        /// </summary>
        None = 0,

        /// <summary>
        /// 레벨 3: 모니터링 강화
        /// - 새로운 프로그램의 네트워크 활동
        /// - 비표준 포트 사용
        /// - 주기적 통신 패턴
        /// </summary>
        Monitor = 1,

        /// <summary>
        /// 레벨 2: 경고 후 차단 (사용자 확인 필요)
        /// - 의심스러운 네트워크 패턴
        /// - 알려지지 않은 프로세스
        /// - 외부 국가 IP 연결
        /// </summary>
        Warning = 2,

        /// <summary>
        /// 레벨 1: 즉시 차단 (자동 실행)
        /// - 알려진 악성 IP/도메인
        /// - 의심스러운 포트 사용
        /// - System Idle Process 위장
        /// - 대용량 데이터 전송
        /// </summary>
        Immediate = 3
    }

    /// <summary>
    /// 통계 조회 기간
    /// </summary>
    public enum StatisticsPeriod
    {
        Hour = 0,
        Day = 1,
        Week = 2,
        Month = 3
    }

    /// <summary>
    /// 자동 차단 시스템의 결정 정보
    /// </summary>
    public class BlockDecision
    {
        /// <summary>
        /// 차단 레벨
        /// </summary>
        public BlockLevel Level { get; set; } = BlockLevel.None;

        /// <summary>
        /// 차단/경고 사유
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// 신뢰도 점수 (0.0 ~ 1.0)
        /// </summary>
        public double ConfidenceScore { get; set; } = 0.0;

        /// <summary>
        /// 트리거된 규칙 목록
        /// </summary>
        public List<string> TriggeredRules { get; set; } = new List<string>();

        /// <summary>
        /// 분석 수행 시각
        /// </summary>
        public DateTime AnalyzedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 추가 상세 정보 (JSON 형태로 저장 가능)
        /// </summary>
        public Dictionary<string, object> Details { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// 권장 조치 사항
        /// </summary>
        public string RecommendedAction { get; set; } = string.Empty;

        /// <summary>
        /// 위협 카테고리 (예: 멀웨어, 봇넷, 데이터유출 등)
        /// </summary>
        public string ThreatCategory { get; set; } = string.Empty;
    }

    /// <summary>
    /// 차단된 연결 정보 (자동 차단 시스템용)
    /// </summary>
    public class AutoBlockedConnection : INotifyPropertyChanged
    {
        private bool _isSelected;

        /// <summary>
        /// 고유 식별자
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 프로세스명
        /// </summary>
        public string ProcessName { get; set; } = string.Empty;

        /// <summary>
        /// 프로세스 경로
        /// </summary>
        public string ProcessPath { get; set; } = string.Empty;

        /// <summary>
        /// 프로세스 ID
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// 원격 주소
        /// </summary>
        public string RemoteAddress { get; set; } = string.Empty;

        /// <summary>
        /// 원격 포트
        /// </summary>
        public int RemotePort { get; set; }

        /// <summary>
        /// 로컬 포트
        /// </summary>
        public int LocalPort { get; set; }

        /// <summary>
        /// 프로토콜 (TCP/UDP)
        /// </summary>
        public string Protocol { get; set; } = string.Empty;

        /// <summary>
        /// 차단 레벨
        /// </summary>
        public BlockLevel BlockLevel { get; set; }

        /// <summary>
        /// 차단 사유
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// 트리거된 규칙들
        /// </summary>
        public string TriggeredRules { get; set; } = string.Empty;

        /// <summary>
        /// 신뢰도 점수
        /// </summary>
        public double ConfidenceScore { get; set; }

        /// <summary>
        /// 차단된 시각
        /// </summary>
        public DateTime BlockedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 차단 성공 여부
        /// </summary>
        public bool IsBlocked { get; set; }

        /// <summary>
        /// 오류 메시지 (차단 실패 시)
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// 사용자 조치 (승인, 거부, 화이트리스트 추가 등)
        /// </summary>
        public string UserAction { get; set; } = string.Empty;

        /// <summary>
        /// 위협 카테고리
        /// </summary>
        public string ThreatCategory { get; set; } = string.Empty;

        /// <summary>
        /// 방화벽 규칙 존재 여부 (임시 차단용)
        /// </summary>
        public bool FirewallRuleExists { get; set; } = false;

        /// <summary>
        /// UI에서 선택 상태를 나타내는 속성
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 차단 레벨의 텍스트 표현
        /// </summary>
        public string BlockLevelText
        {
            get
            {
                return BlockLevel switch
                {
                    BlockLevel.Immediate => "즉시 차단",
                    BlockLevel.Warning => "경고 후 차단",
                    BlockLevel.Monitor => "모니터링",
                    _ => "알 수 없음"
                };
            }
        }

        /// <summary>
        /// 차단 레벨에 따른 색상
        /// </summary>
        public string BlockLevelColor
        {
            get
            {
                return BlockLevel switch
                {
                    BlockLevel.Immediate => "#F44336", // Red - 즉시 차단
                    BlockLevel.Warning => "#FF9800",   // Orange - 경고 후 차단
                    BlockLevel.Monitor => "#2196F3",   // Blue - 모니터링
                    _ => "#9E9E9E"                     // Gray - 알 수 없음
                };
            }
        }

        /// <summary>
        /// 원격 주소와 포트를 합쳐서 표시
        /// </summary>
        public string RemoteEndpoint => $"{RemoteAddress}:{RemotePort}";

        /// <summary>
        /// 차단된 시간의 간략한 표현
        /// </summary>
        public string BlockedTimeText
        {
            get
            {
                var diff = DateTime.Now - BlockedAt;

                if (diff.TotalMinutes < 1)
                    return "방금 전";
                else if (diff.TotalMinutes < 60)
                    return $"{(int)diff.TotalMinutes}분 전";
                else if (diff.TotalHours < 24)
                    return $"{(int)diff.TotalHours}시간 전";
                else if (diff.TotalDays < 30)
                    return $"{(int)diff.TotalDays}일 전";
                else
                    return BlockedAt.ToString("yyyy-MM-dd");
            }
        }

        /// <summary>
        /// PropertyChanged 이벤트
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// PropertyChanged 이벤트를 발생시킵니다.
        /// </summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 객체의 문자열 표현
        /// </summary>
        public override string ToString()
        {
            return $"{ProcessName} (PID:{ProcessId}) -> {RemoteAddress}:{RemotePort} [{BlockLevelText}]";
        }
    }

    /// <summary>
    /// 화이트리스트 항목 (자동 차단 시스템용)
    /// </summary>
    public class AutoWhitelistEntry
    {
        /// <summary>
        /// 고유 식별자
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 프로세스 경로
        /// </summary>
        public string ProcessPath { get; set; } = string.Empty;

        /// <summary>
        /// 화이트리스트 추가 사유
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// 추가된 시각
        /// </summary>
        public DateTime AddedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 추가한 사용자 (향후 확장용)
        /// </summary>
        public string AddedBy { get; set; } = "System";

        /// <summary>
        /// 활성 상태 여부
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// 만료 날짜 (null이면 무제한)
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// 설명
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// 차단 통계 정보
    /// </summary>
    public class BlockStatistics
    {
        /// <summary>
        /// 조회 기간
        /// </summary>
        public StatisticsPeriod Period { get; set; }

        /// <summary>
        /// 조회 시작 시각
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 조회 종료 시각
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// 총 차단 건수
        /// </summary>
        public int TotalBlocks { get; set; }

        /// <summary>
        /// 레벨별 차단 건수
        /// </summary>
        public Dictionary<BlockLevel, int> BlocksByLevel { get; set; } = new Dictionary<BlockLevel, int>();

        /// <summary>
        /// 위협 카테고리별 차단 건수
        /// </summary>
        public Dictionary<string, int> BlocksByThreatCategory { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// 상위 차단 대상 IP 주소들
        /// </summary>
        public List<(string IpAddress, int Count)> TopBlockedIPs { get; set; } = new List<(string, int)>();

        /// <summary>
        /// 상위 차단 대상 프로세스들
        /// </summary>
        public List<(string ProcessName, int Count)> TopBlockedProcesses { get; set; } = new List<(string, int)>();

        /// <summary>
        /// 평균 신뢰도 점수
        /// </summary>
        public double AverageConfidenceScore { get; set; }

        /// <summary>
        /// 사용자 개입이 필요했던 건수 (Level 2)
        /// </summary>
        public int UserInterventionRequired { get; set; }

        /// <summary>
        /// 자동 차단 성공률 (%)
        /// </summary>
        public double AutoBlockSuccessRate { get; set; }
    }

    /// <summary>
    /// 영구적으로 차단된 연결 정보 (방화벽 규칙과 연동)
    /// </summary>
    public class PermanentBlockedConnection : INotifyPropertyChanged
    {
        private bool _isPermanentlyBlocked;
        private bool _firewallRuleExists;

        /// <summary>
        /// 프로세스 이름
        /// </summary>
        public string ProcessName { get; set; } = string.Empty;

        /// <summary>
        /// 프로세스 경로
        /// </summary>
        public string ProcessPath { get; set; } = string.Empty;

        /// <summary>
        /// 원격 주소
        /// </summary>
        public string RemoteAddress { get; set; } = string.Empty;

        /// <summary>
        /// 원격 포트
        /// </summary>
        public int RemotePort { get; set; }

        /// <summary>
        /// 프로토콜 (TCP/UDP)
        /// </summary>
        public string Protocol { get; set; } = "TCP";

        /// <summary>
        /// 차단 레벨
        /// </summary>
        public int BlockLevel { get; set; }

        /// <summary>
        /// 차단 이유
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// 첫 차단 시간
        /// </summary>
        public DateTime FirstBlockedAt { get; set; }

        /// <summary>
        /// 마지막 차단 시간
        /// </summary>
        public DateTime LastBlockedAt { get; set; }

        /// <summary>
        /// 차단 횟수
        /// </summary>
        public int BlockCount { get; set; }

        /// <summary>
        /// 영구 차단 여부
        /// </summary>
        public bool IsPermanentlyBlocked
        {
            get => _isPermanentlyBlocked;
            set
            {
                if (_isPermanentlyBlocked != value)
                {
                    _isPermanentlyBlocked = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusColor));
                }
            }
        }

        /// <summary>
        /// 방화벽 규칙 존재 여부
        /// </summary>
        public bool FirewallRuleExists
        {
            get => _firewallRuleExists;
            set
            {
                if (_firewallRuleExists != value)
                {
                    _firewallRuleExists = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusColor));
                }
            }
        }

        /// <summary>
        /// UI 표시용 상태 텍스트
        /// </summary>
        public string StatusText
        {
            get
            {
                if (FirewallRuleExists && IsPermanentlyBlocked)
                    return "영구 차단 중";
                else if (IsPermanentlyBlocked && !FirewallRuleExists)
                    return "규칙 복구 필요";
                else if (!IsPermanentlyBlocked)
                    return "임시 차단";
                else
                    return "상태 불명";
            }
        }

        /// <summary>
        /// UI 표시용 상태 색상
        /// </summary>
        public string StatusColor
        {
            get
            {
                if (FirewallRuleExists && IsPermanentlyBlocked)
                    return "#F44336"; // 빨간색 - 영구 차단 중
                else if (IsPermanentlyBlocked && !FirewallRuleExists)
                    return "#FF9800"; // 주황색 - 복구 필요
                else if (!IsPermanentlyBlocked)
                    return "#4CAF50"; // 초록색 - 임시 차단
                else
                    return "#9E9E9E"; // 회색 - 상태 불명
            }
        }

        /// <summary>
        /// 연결 식별자 (중복 제거용)
        /// </summary>
        public string ConnectionId => $"{ProcessName}_{RemoteAddress}_{RemotePort}_{Protocol}";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}