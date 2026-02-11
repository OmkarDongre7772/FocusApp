using Microsoft.Data.Sqlite;
using System;

namespace FocusTracker.Core
{
    public class FocusSessionTracker
    {
        private static string ConnectionString =>
            $"Data Source={DatabasePaths.GetDatabasePath()};Cache=Shared";

        private long? _currentSessionId;
        private DateTime? _currentStart;
        private int _plannedMinutes;

        private int _idleSeconds;
        private int _interruptCount;

        public void OnFocusStarted(TimeSpan duration)
        {
            _currentStart = DateTime.UtcNow;
            _plannedMinutes = (int)duration.TotalMinutes;
            _idleSeconds = 0;
            _interruptCount = 0;

            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText =
            """
            INSERT INTO focus_sessions
            (start_time, planned_minutes, completed)
            VALUES ($start, $minutes, 0);
            SELECT last_insert_rowid();
            """;

            cmd.Parameters.AddWithValue("$start", _currentStart.Value.ToString("O"));
            cmd.Parameters.AddWithValue("$minutes", _plannedMinutes);

            _currentSessionId = (long)cmd.ExecuteScalar();
        }

        public void OnFocusEnded(bool completed)
        {
            if (_currentSessionId == null || _currentStart == null)
                return;

            var endTime = DateTime.UtcNow;
            var actualMinutes = (endTime - _currentStart.Value).TotalMinutes;

            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText =
            """
            UPDATE focus_sessions
            SET end_time = $end,
                actual_minutes = $actual,
                completed = $completed,
                idle_seconds = $idle,
                interrupt_count = $interrupts
            WHERE id = $id;
            """;

            cmd.Parameters.AddWithValue("$end", endTime.ToString("O"));
            cmd.Parameters.AddWithValue("$actual", actualMinutes);
            cmd.Parameters.AddWithValue("$completed", completed ? 1 : 0);
            cmd.Parameters.AddWithValue("$idle", _idleSeconds);
            cmd.Parameters.AddWithValue("$interrupts", _interruptCount);
            cmd.Parameters.AddWithValue("$id", _currentSessionId);

            cmd.ExecuteNonQuery();

            _currentSessionId = null;
            _currentStart = null;
        }

        // Phase 5 will use these
        public void AddIdleSeconds(int seconds)
        {
            _idleSeconds += seconds;
        }

        public void AddInterrupt()
        {
            _interruptCount++;
        }
    }
}
