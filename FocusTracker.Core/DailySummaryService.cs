//using Microsoft.Data.Sqlite;
//using System;

//namespace FocusTracker.Core
//{
//    public class DailySummaryService
//    {
//        private static string ConnectionString = $"Data Source={DatabasePaths.GetDatabasePath()};Cache=Shared";

//        public void UpdateToday(
//            double focusMinutes,
//            int focusSessions,
//            int fragmentationScore)
//        {
//            var today = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");

//            using var conn = new SqliteConnection(ConnectionString);
//            conn.Open();

//            var cmd = conn.CreateCommand();
//            cmd.CommandText =
//            """
//            INSERT INTO daily_summary (date, focus_minutes, focus_sessions, fragmentation_score)
//            VALUES ($date, $focus, $sessions, $frag)
//            ON CONFLICT(date) DO UPDATE SET
//                focus_minutes = excluded.focus_minutes,
//                focus_sessions = excluded.focus_sessions,
//                fragmentation_score = excluded.fragmentation_score;
//            """;

//            cmd.Parameters.AddWithValue("$date", today);
//            cmd.Parameters.AddWithValue("$focus", focusMinutes);
//            cmd.Parameters.AddWithValue("$sessions", focusSessions);
//            cmd.Parameters.AddWithValue("$frag", fragmentationScore);

//            cmd.ExecuteNonQuery();
//        }
//    }
//}