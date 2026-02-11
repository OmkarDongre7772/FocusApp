using Microsoft.Data.Sqlite;
using System;

namespace FocusTracker.Core
{
    public class FocusSessionTracker
    {
        private static string ConnectionString = $"Data Source={DatabasePaths.GetDatabasePath()};Cache=Shared";

        private DateTime? _currentStart;
        private int _plannedMinutes;

        public void OnFocusStarted(TimeSpan duration)
        {
            _currentStart = DateTime.UtcNow;
            _plannedMinutes = (int)duration.TotalMinutes;
        }

        public void OnFocusEnded(bool completed)
        {
            if (_currentStart == null)
                return;

            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText =
            """
            INSERT INTO focus_sessions (start_time, planned_minutes, completed)
            VALUES ($start, $minutes, $completed);
            """;

            cmd.Parameters.AddWithValue("$start", _currentStart.Value.ToString("O"));
            cmd.Parameters.AddWithValue("$minutes", _plannedMinutes);
            cmd.Parameters.AddWithValue("$completed", completed ? 1 : 0);

            cmd.ExecuteNonQuery();

            _currentStart = null;
        }
    }
}