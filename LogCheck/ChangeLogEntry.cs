using System;
using System.Collections.Generic;
using System.Linq;

namespace LogCheck
{
    public static class ChangeLogEntry
    {
        public static Dictionary<DateTime, int> Install_Date { get; } = new Dictionary<DateTime, int>();

        public static void AddLogEntry(DateTime time, int score)
        {
            if (!Install_Date.ContainsKey(time))
            {
                Install_Date[time] = score;
            }
        }

        public static int GetTotalScore()
        {
            return Install_Date.Values.Sum();
        }

        public static void ClearLog()
        {
            Install_Date.Clear();
        }
    }
} 