using Microsoft.Data.Sqlite;
using FocusTracker.Core;
using FocusTracker.UI.Models;

namespace FocusTracker.UI
{
    public class DashboardService
    {
        private static string ConnectionString =>
            $"Data Source={DatabasePaths.GetDatabasePath()};Cache=Shared";

        public List<DailySummaryModel> GetDailySummaries()
        {
            var list = new List<DailySummaryModel>();

            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText =
            """
            SELECT date, focus_minutes, focus_sessions, fragmentation_score FROM
            daily_local_aggregates
            ORDER BY date DESC;
            """;

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new DailySummaryModel
                {
                    Date = reader.GetString(0),
                    FocusMinutes = reader.GetDouble(1),
                    FocusSessions = reader.GetInt32(2),
                    FragmentationScore = reader.GetInt32(3)
                });
            }

            return list;
        }

        public List<FocusSessionModel> GetRecentSessions()
        {
            var list = new List<FocusSessionModel>();

            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText =
            """
            SELECT start_time, actual_minutes,
                   fragmentation_score, interrupt_count, idle_seconds
            FROM focus_sessions
            WHERE completed = 1
            ORDER BY start_time DESC
            LIMIT 20;
            """;

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new FocusSessionModel
                {
                    StartTime = reader.GetString(0),
                    ActualMinutes = reader.GetDouble(1),
                    FragmentationScore = reader.GetInt32(2),
                    InterruptCount = reader.GetInt32(3),
                    IdleSeconds = reader.GetInt32(4)
                });
            }

            return list;
        }
        public DashboardMetrics GetDashboardMetrics()
        {
            var metrics = new DashboardMetrics();

            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");


            // ===============================
            // TODAY METRICS
            // ===============================

            var cmd = connection.CreateCommand();
            cmd.CommandText =
            """
    SELECT 
        COUNT(*),
        IFNULL(SUM(actual_minutes),0),
        IFNULL(SUM(idle_seconds),0),
        IFNULL(SUM(interrupt_count),0),
        IFNULL(AVG(actual_minutes),0),
        IFNULL(AVG(fragmentation_score),0)
    FROM focus_sessions
    WHERE date(start_time) = $today
      AND completed = 1;
    """;

            cmd.Parameters.AddWithValue("$today", today);

            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                var sessions = reader.GetInt32(0);
                var totalMinutes = reader.GetDouble(1);
                var totalIdleSeconds = reader.GetDouble(2);
                var totalInterrupts = reader.GetDouble(3);
                var avgSession = reader.GetDouble(4);
                var fragScore = reader.GetDouble(5);

                metrics.Sessions = sessions;
                metrics.FocusMinutes = totalMinutes;
                metrics.DeepWorkMinutes = totalMinutes - (totalIdleSeconds / 60);
                metrics.AvgSessionMinutes = avgSession;
                metrics.FragmentationScore = (int)fragScore;

                metrics.InterruptsPerSession =
                    sessions == 0 ? 0 : totalInterrupts / sessions;

                metrics.IdleRatioPercent =
                    totalMinutes == 0 ? 0 :
                    (totalIdleSeconds / 60) / totalMinutes * 100;

                metrics.EstimatedTimeLostMinutes = totalInterrupts * 2;

                // Quality Label
                metrics.FocusQuality =
                    fragScore < 30 ? "Excellent" :
                    fragScore < 60 ? "Stable" :
                    fragScore < 80 ? "Distracted" :
                    "Chaotic";

                // Productivity Score
                metrics.ProductivityScore =
                    (int)(
                        (100 - fragScore) * 0.4 +
                        (metrics.DeepWorkMinutes / Math.Max(totalMinutes, 1) * 100) * 0.3 +
                        (metrics.Sessions > 0 ? 100 : 0) * 0.3
                    );
            }

            // ===============================
            // TOP DISTRACTION
            // ===============================

            var distractionCmd = connection.CreateCommand();
            distractionCmd.CommandText =
            """
                SELECT app_name, COUNT(*)
                FROM events
                WHERE event_type = 'APP_SWITCH'
                  AND local_date = $today
                GROUP BY app_name
                ORDER BY COUNT(*) DESC
                LIMIT 1;
                """;

            distractionCmd.Parameters.AddWithValue("$today", today);

            using var dReader = distractionCmd.ExecuteReader();
            if (dReader.Read())
            {
                metrics.TopDistractionApp = dReader.GetString(0);
                metrics.TopDistractionCount = dReader.GetInt32(1);
            }
            // ===============================
            // WEEKLY METRICS
            // ===============================

            var weeklyCmd = connection.CreateCommand();
            weeklyCmd.CommandText =
            """
SELECT date, total_focused_seconds
FROM daily_local_aggregates
ORDER BY date DESC
LIMIT 7;
""";

            var weeklyFocusList = new List<double>();
            var weeklyDates = new List<string>();

            using var wReader = weeklyCmd.ExecuteReader();
            {
                while (wReader.Read())
                {
                    weeklyDates.Add(wReader.GetString(0));
                    weeklyFocusList.Add(wReader.GetDouble(1));
                }
            }

            if (weeklyFocusList.Count > 0)
            {
                metrics.WeeklyAvgFocus =
                    weeklyFocusList.Average()/60;

                // ===========================
                // STREAK CALCULATION
                // ===========================

                int streak = 0;
                foreach (var minutes in weeklyFocusList)
                {
                    if (minutes > 0)
                        streak++;
                    else
                        break;
                }

                metrics.CurrentStreak = streak;

                // ===========================
                // TREND CALCULATION
                // ===========================

                if (weeklyFocusList.Count >= 2)
                {
                    var todayFocus = weeklyFocusList[0];
                    var yesterdayFocus = weeklyFocusList[1];

                    if (yesterdayFocus > 0)
                    {
                        var change =
                            ((todayFocus - yesterdayFocus)
                            / yesterdayFocus) * 100;

                        metrics.TrendPercent = Math.Abs(change);

                        if (change > 5)
                            metrics.TrendDirection = "UP";
                        else if (change < -5)
                            metrics.TrendDirection = "DOWN";
                        else
                            metrics.TrendDirection = "STABLE";
                    }
                }
            }


            return metrics;
        }

    }
}
