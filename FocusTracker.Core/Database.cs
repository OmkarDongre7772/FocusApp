using Microsoft.Data.Sqlite;
using System;

namespace FocusTracker.Core
{
    public class Database
    {
        private static string ConnectionString =>
            $"Data Source={DatabasePaths.GetDatabasePath()};Cache=Shared";

        public Database()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            CreateEventsTable(connection);
            CreateSettingsTable(connection);
            CreateDailySummaryTable(connection);
            CreateFocusSessionsTable(connection);

            Console.WriteLine("Database constructor executed.");
        }

        private static void CreateEventsTable(SqliteConnection connection)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                time TEXT NOT NULL,
                type TEXT NOT NULL,
                data TEXT
            );
            """;

            cmd.ExecuteNonQuery();
        }

        private static void CreateFocusSessionsTable(SqliteConnection connection)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS focus_sessions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,

                start_time TEXT NOT NULL,
                end_time TEXT,

                planned_minutes INTEGER NOT NULL,
                actual_minutes REAL,

                completed INTEGER NOT NULL,

                idle_seconds INTEGER NOT NULL DEFAULT 0,
                interrupt_count INTEGER NOT NULL DEFAULT 0,

                fragmentation_score INTEGER NOT NULL DEFAULT 0
            );
            """;

            cmd.ExecuteNonQuery();
        }

        private static void CreateSettingsTable(SqliteConnection connection)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS app_settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;

            cmd.ExecuteNonQuery();
        }

        private static void CreateDailySummaryTable(SqliteConnection connection)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS daily_summary (
                date TEXT PRIMARY KEY,
                focus_minutes REAL NOT NULL,
                focus_sessions INTEGER NOT NULL,
                fragmentation_score INTEGER NOT NULL
            );
            """;

            cmd.ExecuteNonQuery();
        }

        public void SaveEvent(string type, string? data)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText =
            """
            INSERT INTO events (time, type, data)
            VALUES ($time, $type, $data);
            """;

            cmd.Parameters.AddWithValue("$time", DateTime.UtcNow.ToString("O"));
            cmd.Parameters.AddWithValue("$type", type);
            cmd.Parameters.AddWithValue("$data", data ?? "");

            cmd.ExecuteNonQuery();
        }
    }
}
