using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;

namespace FocusTracker.Core
{
    public class AnalyticsService
    {
        private static string ConnectionString = $"Data Source={DatabasePaths.GetDatabasePath()};Cache=Shared";
        private static readonly TimeSpan MinFocusDuration = TimeSpan.FromMinutes(2);

        public TodaySummary GetTodaySummary()
        {
            var events = LoadTodayEvents();

            TimeSpan totalFocusTime = TimeSpan.Zero;
            TimeSpan longestFocus = TimeSpan.Zero;
            int focusSessions = 0;

            DateTime? sessionStart = null;
            bool inFocus = false;

            foreach (var e in events)
            {
                if (e.Type == "APP_CHANGED")
                {
                    if (!inFocus)
                    {
                        // start focus
                        sessionStart = e.Time;
                        inFocus = true;
                    }
                    else
                    {
                        // app changed while focusing → end previous session
                        EndSession(e.Time);
                        sessionStart = e.Time;
                        inFocus = true;
                    }
                }

                if (e.Type == "IDLE_STARTED")
                {
                    if (inFocus)
                    {
                        EndSession(e.Time);
                        inFocus = false;
                        sessionStart = null;
                    }
                }
            }

            // end session at "now" if still focusing
            if (inFocus && sessionStart != null)
            {
                EndSession(DateTime.UtcNow);
            }

            return new TodaySummary
            {
                FocusSessions = focusSessions,
                FocusMinutes = totalFocusTime.TotalMinutes,
                LongestFocusMinutes = longestFocus.TotalMinutes
            };

            // 🔒 local helper
            void EndSession(DateTime endTime)
            {
                if (sessionStart == null) return;

                var duration = endTime - sessionStart.Value;

                if (duration >= MinFocusDuration)
                {
                    focusSessions++;
                    totalFocusTime += duration;

                    if (duration > longestFocus)
                        longestFocus = duration;
                }
            }
        }

        private List<EventRow> LoadTodayEvents()
        {
            var list = new List<EventRow>();

            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText =
            """
            SELECT time, type
            FROM events
            WHERE date(time) = date('now')
            ORDER BY time;
            """;

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new EventRow
                {
                    Time = DateTime.Parse(reader.GetString(0)),
                    Type = reader.GetString(1)
                });
            }

            return list;
        }

        public WeeklySummary GetLast7Days()
        {
            using var conn = new SqliteConnection($"Data Source={DatabasePaths.GetDatabasePath()};Cache=Shared");
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText =
            """
    SELECT date, focus_minutes, focus_sessions, fragmentation_score
    FROM daily_summary
    ORDER BY date DESC
    LIMIT 7;
    """;

            var summary = new WeeklySummary();

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                summary.Days.Add(new DailyRow
                {
                    Date = reader.GetString(0),
                    FocusMinutes = reader.GetDouble(1),
                    FocusSessions = reader.GetInt32(2),
                    FragmentationScore = reader.GetInt32(3)
                });
            }

            return summary;
        }

        private class EventRow
        {
            public DateTime Time { get; set; }
            public string Type { get; set; }
        }
    }

    public class TodaySummary
    {
        public int FocusSessions { get; set; }
        public double FocusMinutes { get; set; }
        public double LongestFocusMinutes { get; set; }
    }
    public class WeeklySummary
    {
        public List<DailyRow> Days { get; } = new();
    }

    public class DailyRow
    {
        public string Date { get; set; }
        public double FocusMinutes { get; set; }
        public int FocusSessions { get; set; }
        public int FragmentationScore { get; set; }
    }
}