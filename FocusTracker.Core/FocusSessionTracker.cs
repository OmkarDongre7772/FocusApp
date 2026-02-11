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

        private readonly FragmentationConfig _config;

        public FocusSessionTracker(FragmentationConfig config)
        {
            _config = config;
        }

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

            var fragmentationScore = CalculateFragmentationScore(completed);

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
                interrupt_count = $interrupts,
                fragmentation_score = $score
            WHERE id = $id;
            """;

            cmd.Parameters.AddWithValue("$end", endTime.ToString("O"));
            cmd.Parameters.AddWithValue("$actual", actualMinutes);
            cmd.Parameters.AddWithValue("$completed", completed ? 1 : 0);
            cmd.Parameters.AddWithValue("$idle", _idleSeconds);
            cmd.Parameters.AddWithValue("$interrupts", _interruptCount);
            cmd.Parameters.AddWithValue("$score", fragmentationScore);
            cmd.Parameters.AddWithValue("$id", _currentSessionId);

            cmd.ExecuteNonQuery();

            _currentSessionId = null;
            _currentStart = null;
        }

        private int CalculateFragmentationScore(bool completed)
        {
            double idleRatio = Math.Min(1.0,
                (double)_idleSeconds / _config.MaxIdleThresholdSeconds);

            double interruptRatio = Math.Min(1.0,
                (double)_interruptCount / _config.MaxInterruptThreshold);

            double earlyStopPenalty = completed ? 0 : 1;

            double score =
                (idleRatio * _config.IdleWeight) +
                (interruptRatio * _config.InterruptWeight) +
                (earlyStopPenalty * _config.EarlyStopWeight);

            return (int)Math.Round(Math.Clamp(score * 100, 0, 100));
        }

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
