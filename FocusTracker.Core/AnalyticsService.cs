using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;

namespace FocusTracker.Core
{
    public class AnalyticsService
    {
        private static readonly string ConnectionString = $"Data Source={DatabasePaths.GetDatabasePath()};Cache=Shared";
        private static readonly TimeSpan MinFocusDuration = TimeSpan.FromMinutes(1);

        public TodaySummary GetTodaySummary()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText =
            """
    SELECT actual_minutes
    FROM focus_sessions
    WHERE completed = 1
      AND date(start_time, 'localtime') = date('now','localtime');
    """;

            using var reader = cmd.ExecuteReader();

            double total = 0;
            double longest = 0;
            int count = 0;

            while (reader.Read())
            {
                var minutes = reader.GetDouble(0);

                total += minutes;
                count++;

                if (minutes > longest)
                    longest = minutes;
            }

            return new TodaySummary
            {
                FocusSessions = count,
                FocusMinutes = total,
                LongestFocusMinutes = longest
            };
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

        public static WeeklySummary GetLast7Days()
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText =
            """
    SELECT date,
           total_focused_seconds,
           fragmentation_score
    FROM daily_local_aggregates
    ORDER BY date DESC
    LIMIT 7;
    """;

            var summary = new WeeklySummary();

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var focusedSeconds = reader.GetInt32(1);

                summary.Days.Add(new DailyRow
                {
                    Date = reader.GetString(0),
                    FocusMinutes = focusedSeconds / 60.0,
                    FocusSessions = 0, // not stored anymore
                    FragmentationScore = reader.GetInt32(2)
                });
            }

            return summary;
        }

        private class EventRow
        {
            public DateTime Time { get; set; }
            public string? Type { get; set; }
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
        public string? Date { get; set; }
        public double FocusMinutes { get; set; }
        public int FocusSessions { get; set; }
        public int FragmentationScore { get; set; }
    }
}