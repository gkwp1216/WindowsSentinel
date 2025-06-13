using System;
using System.Collections.Generic;

namespace WindowsSentinel
{
    public class ChangeLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string ProgramName { get; set; }
        public string Action { get; set; }
        public string Details { get; set; }
        public string Status { get; set; }

        // 설치 날짜와 보안 점수를 저장하는 정적 딕셔너리
        public static Dictionary<DateTime, int> Install_Date { get; } = new Dictionary<DateTime, int>();

        public ChangeLogEntry()
        {
            Timestamp = DateTime.Now;
            ProgramName = string.Empty;
            Action = string.Empty;
            Details = string.Empty;
            Status = string.Empty;
        }

        public ChangeLogEntry(string programName, string action, string details, string status)
        {
            Timestamp = DateTime.Now;
            ProgramName = programName;
            Action = action;
            Details = details;
            Status = status;
        }

        public override string ToString()
        {
            return $"{Timestamp:yyyy-MM-dd HH:mm:ss} - {ProgramName} - {Action} - {Details} - {Status}";
        }

        /// <summary>
        /// 설치 로그 항목을 추가합니다.
        /// </summary>
        /// <param name="time">설치 시간</param>
        /// <param name="score">보안 점수</param>
        public static void AddLogEntry(DateTime time, int score)
        {
            if (Install_Date.ContainsKey(time))
            {
                // 이미 같은 시간에 로그가 있다면 점수를 누적
                Install_Date[time] += score;
            }
            else
            {
                // 새로운 시간의 로그 추가
                Install_Date[time] = score;
            }
        }

        /// <summary>
        /// 특정 시간의 보안 점수를 가져옵니다.
        /// </summary>
        /// <param name="time">조회할 시간</param>
        /// <returns>보안 점수 (로그가 없는 경우 0)</returns>
        public static int GetSecurityScore(DateTime time)
        {
            return Install_Date.TryGetValue(time, out int score) ? score : 0;
        }

        /// <summary>
        /// 모든 로그 항목을 초기화합니다.
        /// </summary>
        public static void ClearLogs()
        {
            Install_Date.Clear();
        }
    }
} 