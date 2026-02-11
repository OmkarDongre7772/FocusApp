using Microsoft.Data.Sqlite;
using System;
using System.Linq;

namespace FocusTracker.Core
{
    public class FocusStatsService
    {
        private static string ConnectionString = $"Data Source={DatabasePaths.GetDatabasePath()};Cache=Shared";

        public FocusStats GetStats()
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText =
            """
            SELECT completed FROM focus_sessions
            ORDER BY start_time DESC;
            """;

            var results = new List<bool>();

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(reader.GetInt32(0) == 1);
            }


            int total = results.Count;
            int completed = results.Count(r => r);

            int streak = 0;
            foreach (var r in results)
            {
                if (!r) break;
                streak++;
            }

            int longestStreak = ComputeLongestStreak(results);

            return new FocusStats
            {
                TotalSessions = total,
                CompletedSessions = completed,
                CompletionRate = total == 0 ? 0 : (double)completed / total,
                CurrentStreak = streak,
                LongestStreak = longestStreak
            };
        }

        private int ComputeLongestStreak(List<bool> results)
        {
            int max = 0, current = 0;

            foreach (var r in results)
            {
                if (r)
                {
                    current++;
                    max = Math.Max(max, current);
                }
                else
                {
                    current = 0;
                }
            }

            return max;
        }
    }

    public class FocusStats
    {
        public int TotalSessions { get; set; }
        public int CompletedSessions { get; set; }
        public double CompletionRate { get; set; }
        public int CurrentStreak { get; set; }
        public int LongestStreak { get; set; }
    }
}