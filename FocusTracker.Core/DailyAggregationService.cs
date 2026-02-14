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
                var events = LoadEventsForDate(connection, date);

                int totalFocusedSeconds = 0;
                int longestFocusSeconds = 0;

                DateTime? focusStart = null;
                DateTime? lastTime = null;

                string? lastApp = null;

                var config = FocusConfigLoader.Load();

                foreach (var e in events)
                {
                    if (e.EventType == "APP_SWITCH")
                    {
                        if (focusStart == null)
                        {
                            focusStart = e.UtcTime;
                        }
                        else
                        {
                            if (!IsContinuousFocus(lastApp, e.AppName, config))
                            {
                                var segment = (int)(e.UtcTime - focusStart.Value).TotalSeconds;

                                totalFocusedSeconds += segment;
                                longestFocusSeconds = Math.Max(longestFocusSeconds, segment);

                                focusStart = e.UtcTime;
                            }
                        }

                        lastApp = e.AppName;
                    }

                    if (e.EventType == "IDLE_START" && focusStart != null)
                    {
                        var segment = (int)(e.UtcTime - focusStart.Value).TotalSeconds;

                        totalFocusedSeconds += segment;
                        longestFocusSeconds = Math.Max(longestFocusSeconds, segment);

                        focusStart = null;
                    }

                    lastTime = e.UtcTime;
                }

                if (focusStart != null && lastTime != null)
                {
                    var segment = (int)(lastTime.Value - focusStart.Value).TotalSeconds;

                    totalFocusedSeconds += segment;
                    longestFocusSeconds = Math.Max(longestFocusSeconds, segment);
                }

                double focusPercentage =
                    totalFocusedSeconds / (24d * 60d * 60d);
                int interruptCount = events.Count(e => e.EventType == "INTERRUPT");

                int idleSeconds = 0;

                for (int i = 1; i < events.Count; i++)
                {
                    if (events[i - 1].EventType == "IDLE_START" &&
                        events[i].EventType == "IDLE_END")
                    {
                        idleSeconds +=
                            (int)(events[i].UtcTime - events[i - 1].UtcTime)
                            .TotalSeconds;
                    }
                }

                var fragConfig = new FragmentationConfig
                {
                    IdleWeight = 0.4,
                    InterruptWeight = 0.4,
                    EarlyStopWeight = 0.2,
                    MaxInterruptThreshold = 20,
                    MaxIdleThresholdSeconds = 900
                };

                double idleRatio = Math.Min(1.0,
                    (double)idleSeconds / fragConfig.MaxIdleThresholdSeconds);

                double interruptRatio = Math.Min(1.0,
                    (double)interruptCount / fragConfig.MaxInterruptThreshold);

                double score =
                    (idleRatio * fragConfig.IdleWeight) +
                    (interruptRatio * fragConfig.InterruptWeight);

                int fragmentation =
                    (int)Math.Round(Math.Clamp(score * 100, 0, 100));


                UpsertAggregate(
                    connection,
                    date.ToString("yyyy-MM-dd"),
                    totalFocusedSeconds,
                    longestFocusSeconds,
                    fragmentation,
                    focusPercentage
                );

                DeleteEventsForDate(connection, date);
                CleanupOldSyncedAggregates(connection);

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

        //Helpers
        private List<EventRow> LoadEventsForDate(
                                SqliteConnection connection,
                                DateTime date){
            var list = new List<EventRow>();

            var cmd = connection.CreateCommand();
            cmd.CommandText =
            """
    SELECT utc_time, event_type, app_name
    FROM events
    WHERE local_date = $date
    ORDER BY utc_time;
    """;

            cmd.Parameters.AddWithValue("$date", date.ToString("yyyy-MM-dd"));

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new EventRow
                {
                    UtcTime = DateTime.Parse(reader.GetString(0)),
                    EventType = reader.GetString(1),
                    AppName = reader.IsDBNull(2)
                        ? null
                        : reader.GetString(2)
                });
            }

            return list;
        }

        private static void DeleteEventsForDate(
            SqliteConnection connection,
            DateTime date){
            var cmd = connection.CreateCommand();
            cmd.CommandText =
            """
    DELETE FROM events
    WHERE local_date = $date;
    """;

            cmd.Parameters.AddWithValue("$date", date.ToString("yyyy-MM-dd"));
            cmd.ExecuteNonQuery();
        }

        private static bool IsContinuousFocus(
            string? previous,
            string? current,
            FocusConfig config)
        {
            if (previous == null || current == null)
                return false;

            if (previous == current)
                return true;

            var pair = string.Compare(previous, current,
                StringComparison.OrdinalIgnoreCase) < 0
                ? (previous, current)
                : (current, previous);

            return config.FocusPairs.Contains(pair);
        }

        private class EventRow
        {
            public DateTime UtcTime { get; set; }
            public string? EventType { get; set; }
            public string? AppName { get; set; }
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
        private static void CleanupOldSyncedAggregates(
    SqliteConnection connection)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText =
            """
    DELETE FROM daily_local_aggregates
    WHERE sync_status = 'SYNCED'
      AND date < date('now', '-30 days');
    """;

            cmd.ExecuteNonQuery();
        }

    }
}
