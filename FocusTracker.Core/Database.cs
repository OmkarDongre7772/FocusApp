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
            CreateDailyLocalAggregatesTable(connection);
            CreateFocusSessionsTable(connection);
            CreateLocalUserTable(connection);


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

        private static void CreateDailyLocalAggregatesTable(SqliteConnection connection)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText =
            """
    CREATE TABLE IF NOT EXISTS daily_local_aggregates (
        date TEXT PRIMARY KEY,

        total_focused_seconds INTEGER NOT NULL,
        longest_focus_seconds INTEGER NOT NULL,

        fragmentation_score INTEGER NOT NULL,
        focus_percentage REAL NOT NULL,

        sync_status TEXT NOT NULL DEFAULT 'PENDING',
        computed_at TEXT NOT NULL
    );
    """;

            cmd.ExecuteNonQuery();
        }

        private static void CreateLocalUserTable(SqliteConnection connection)
        {
            var cmd = connection.CreateCommand();

            // 1️⃣ Create base table if not exists
            cmd.CommandText =
            """
    CREATE TABLE IF NOT EXISTS local_user (
        user_id TEXT,
        username TEXT,
        team_id TEXT,
        access_token TEXT,
        refresh_token TEXT,
        token_expiry_utc TEXT,
        tracking_enabled INTEGER NOT NULL DEFAULT 1,
        last_login TEXT,
        updated_at TEXT NOT NULL
    );
    """;

            cmd.ExecuteNonQuery();

            // 2️⃣ Ensure single row exists
            cmd.CommandText =
            """
    INSERT INTO local_user (tracking_enabled, updated_at)
    SELECT 1, $now
    WHERE NOT EXISTS (SELECT 1 FROM local_user);
    """;

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
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
