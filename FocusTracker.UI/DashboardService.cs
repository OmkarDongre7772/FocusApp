using Microsoft.Data.Sqlite;
using FocusTracker.Core;
using FocusTracker.UI.Models;
using System.Collections.Generic;

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
            SELECT date, focus_minutes, focus_sessions, fragmentation_score
            FROM daily_summary
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
    }
}
