using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace FocusTracker.Core
{
    public class DailyAggregationService
    {
        private static string ConnectionString =>
            $"Data Source={DatabasePaths.GetDatabasePath()};Cache=Shared";

        public void RunAggregationForAllMissingDays()
        {
            var lastAggregated = GetLastAggregatedDate();
            var today = DateTime.Now.Date;

            var startDate = lastAggregated?.AddDays(1)
                ?? GetFirstSessionDate();

            if (startDate == null)
                return;

            while (startDate < today)
            {
                RunAggregationForDate(startDate.Value);
                startDate = startDate.Value.AddDays(1);
            }
        }

        public void RunAggregationForDate(DateTime date)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();

            try
            {
                var sessions = LoadCompletedSessions(connection, date);

                double totalMinutes = 0;
                int totalSessions = 0;
                int totalFragmentation = 0;

                foreach (var s in sessions)
                {
                    totalMinutes += s.ActualMinutes;
                    totalSessions++;
                    totalFragmentation += s.FragmentationScore;
                }

                int avgFragmentation =
                    totalSessions == 0 ? 0 :
                    (int)Math.Round((double)totalFragmentation / totalSessions);

                UpsertDailySummary(
                    connection,
                    date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    totalMinutes,
                    totalSessions,
                    avgFragmentation
                );

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private static List<SessionRow> LoadCompletedSessions(
            SqliteConnection connection,
            DateTime date)
        {
            var list = new List<SessionRow>();

            var start = date;
            var end = date.AddDays(1);

            var cmd = connection.CreateCommand();
            cmd.CommandText =
            """
            SELECT actual_minutes, fragmentation_score
            FROM focus_sessions
            WHERE completed = 1
              AND datetime(start_time, 'localtime') >= $start
              AND datetime(start_time, 'localtime') < $end;
            """;

            cmd.Parameters.AddWithValue("$start", start.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("$end", end.ToString("yyyy-MM-dd HH:mm:ss"));

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new SessionRow
                {
                    ActualMinutes = reader.GetDouble(0),
                    FragmentationScore = reader.GetInt32(1)
                });
            }

            return list;
        }

        private DateTime? GetLastAggregatedDate()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText =
            """
            SELECT date
            FROM daily_summary
            ORDER BY date DESC
            LIMIT 1;
            """;

            var result = cmd.ExecuteScalar();
            if (result == null) return null;

            return DateTime.Parse(result.ToString());
        }

        private DateTime? GetFirstSessionDate()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText =
            """
            SELECT start_time
            FROM focus_sessions
            ORDER BY start_time
            LIMIT 1;
            """;

            var result = cmd.ExecuteScalar();
            if (result == null) return null;

            return DateTime.Parse(result.ToString()).Date;
        }

        private static void UpsertDailySummary(
            SqliteConnection connection,
            string date,
            double minutes,
            int sessions,
            int fragmentation)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText =
            """
            INSERT INTO daily_summary
            (date, focus_minutes, focus_sessions, fragmentation_score)
            VALUES ($date, $minutes, $sessions, $frag)
            ON CONFLICT(date)
            DO UPDATE SET
                focus_minutes = excluded.focus_minutes,
                focus_sessions = excluded.focus_sessions,
                fragmentation_score = excluded.fragmentation_score;
            """;

            cmd.Parameters.AddWithValue("$date", date);
            cmd.Parameters.AddWithValue("$minutes", minutes);
            cmd.Parameters.AddWithValue("$sessions", sessions);
            cmd.Parameters.AddWithValue("$frag", fragmentation);

            cmd.ExecuteNonQuery();
        }

        private class SessionRow
        {
            public double ActualMinutes { get; set; }
            public int FragmentationScore { get; set; }
        }
    }
}
