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

                int totalFocusedSeconds = 0;
                int longestFocusSeconds = 0;
                int totalFragmentation = 0;

                foreach (var s in sessions)
                {
                    int seconds = (int)Math.Round(s.ActualMinutes * 60);

                    totalFocusedSeconds += seconds;
                    totalFragmentation += s.FragmentationScore;

                    if (seconds > longestFocusSeconds)
                        longestFocusSeconds = seconds;
                }

                int sessionCount = sessions.Count;

                int avgFragmentation =
                    sessionCount == 0 ? 0 :
                    (int)Math.Round((double)totalFragmentation / sessionCount);

                // Focus % = Focused time / 24h
                double focusPercentage =
                    totalFocusedSeconds / (24d * 60d * 60d);

                UpsertAggregate(
                    connection,
                    date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    totalFocusedSeconds,
                    longestFocusSeconds,
                    avgFragmentation,
                    focusPercentage
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
              AND datetime(start_time) >= $startUtc
              AND datetime(start_time) < $endUtc;
            """;

            cmd.Parameters.AddWithValue("$startUtc", start.ToUniversalTime().ToString("O"));
            cmd.Parameters.AddWithValue("$endUtc", end.ToUniversalTime().ToString("O"));

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
            FROM daily_local_aggregates
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

        private static void UpsertAggregate(
            SqliteConnection connection,
            string date,
            int totalSeconds,
            int longestSeconds,
            int fragmentation,
            double focusPercentage)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText =
            """
            INSERT INTO daily_local_aggregates
            (date, total_focused_seconds, longest_focus_seconds,
             fragmentation_score, focus_percentage,
             sync_status, computed_at)
            VALUES ($date, $total, $longest, $frag, $percent,
                    'PENDING', $now)
            ON CONFLICT(date)
            DO UPDATE SET
                total_focused_seconds = excluded.total_focused_seconds,
                longest_focus_seconds = excluded.longest_focus_seconds,
                fragmentation_score = excluded.fragmentation_score,
                focus_percentage = excluded.focus_percentage,
                computed_at = excluded.computed_at;
            """;

            cmd.Parameters.AddWithValue("$date", date);
            cmd.Parameters.AddWithValue("$total", totalSeconds);
            cmd.Parameters.AddWithValue("$longest", longestSeconds);
            cmd.Parameters.AddWithValue("$frag", fragmentation);
            cmd.Parameters.AddWithValue("$percent", focusPercentage);
            cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));

            cmd.ExecuteNonQuery();
        }

        private class SessionRow
        {
            public double ActualMinutes { get; set; }
            public int FragmentationScore { get; set; }
        }
    }
}
