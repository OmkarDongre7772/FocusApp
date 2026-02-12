using Microsoft.Data.Sqlite;
using System;

namespace FocusTracker.Core
{
    public class LocalUserRepository
    {
        private static string ConnectionString =>
            $"Data Source={DatabasePaths.GetDatabasePath()};Cache=Shared";

        public LocalUser Get()
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText =
            """
            SELECT user_id, username, team_id,
                   access_token, refresh_token, token_expiry_utc,
                   tracking_enabled, last_login
            FROM local_user
            LIMIT 1;
            
            """;

            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
                throw new Exception("local_user row missing.");

            return new LocalUser
            {
                UserId = reader.IsDBNull(0) ? null : reader.GetString(0),
                Username = reader.IsDBNull(1) ? null : reader.GetString(1),
                TeamId = reader.IsDBNull(2) ? null : reader.GetString(2),
                AccessToken = reader.IsDBNull(3) ? null : reader.GetString(3),
                RefreshToken = reader.IsDBNull(4) ? null : reader.GetString(4),
                TokenExpiryUtc = reader.IsDBNull(5)
        ? null
        : DateTime.Parse(reader.GetString(5)),

                TrackingEnabled = reader.GetInt32(6) == 1,
                LastLogin = reader.IsDBNull(7)
        ? null
        : DateTime.Parse(reader.GetString(7))
            };

        }

        public void SetTracking(bool enabled)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText =
            """
            UPDATE local_user
            SET tracking_enabled = $enabled,
                updated_at = $now;
            """;

            cmd.Parameters.AddWithValue("$enabled", enabled ? 1 : 0);
            cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));

            cmd.ExecuteNonQuery();
        }

        public void SaveAuth(
     string userId,
     string username,
     string? teamId,
     string accessToken,
     string refreshToken,
     DateTime expiryUtc)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText =
            """
    UPDATE local_user
    SET user_id = $userId,
        username = $username,
        team_id = $team,
        access_token = $access,
        refresh_token = $refresh,
        token_expiry_utc = $expiry,
        last_login = $now,
        updated_at = $now;
    """;

            cmd.Parameters.AddWithValue("$userId", userId);
            cmd.Parameters.AddWithValue("$username", username);
            cmd.Parameters.AddWithValue("$team", (object?)teamId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$access", accessToken);
            cmd.Parameters.AddWithValue("$refresh", refreshToken);
            cmd.Parameters.AddWithValue("$expiry", expiryUtc.ToString("O"));
            cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));

            cmd.ExecuteNonQuery();
        }


        public void Logout()
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText =
            """
    UPDATE local_user
    SET user_id = NULL,
        username = NULL,
        team_id = NULL,
        access_token = NULL,
        refresh_token = NULL,
        token_expiry_utc = NULL,
        tracking_enabled = 0,
        updated_at = $now;
    """;

            cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));

            cmd.ExecuteNonQuery();
        }


        public bool IsLoggedIn()
        {
            return Get().Username != null;
        }

    }
}
