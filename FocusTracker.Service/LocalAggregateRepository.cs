using Microsoft.Data.Sqlite;
using FocusTracker.Core;

namespace FocusTracker.Service;

public class LocalAggregateRepository
{
    private static string ConnectionString =>
        $"Data Source={DatabasePaths.GetDatabasePath()};Cache=Shared";

    public List<LocalAggregateRow> GetPending()
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT date,
                   total_focused_seconds,
                   longest_focus_seconds,
                   fragmentation_score,
                   focus_percentage
            FROM daily_local_aggregates
            WHERE sync_status = 'PENDING';
        """;

        using var reader = cmd.ExecuteReader();

        var list = new List<LocalAggregateRow>();

        while (reader.Read())
        {
            list.Add(new LocalAggregateRow
            {
                Date = DateTime.Parse(reader.GetString(0)),
                TotalFocusedSeconds = reader.GetInt32(1),
                LongestFocusSeconds = reader.GetInt32(2),
                FragmentationScore = reader.GetInt32(3),
                FocusPercentage = reader.GetDouble(4)
            });
        }

        return list;
    }

    public void MarkSynced(DateTime date)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE daily_local_aggregates
            SET sync_status = 'SYNCED'
            WHERE date = $date;
        """;

        cmd.Parameters.AddWithValue("$date", date.ToString("yyyy-MM-dd"));
        cmd.ExecuteNonQuery();
    }
}

public class LocalAggregateRow
{
    public DateTime Date { get; set; }
    public int TotalFocusedSeconds { get; set; }
    public int LongestFocusSeconds { get; set; }
    public int FragmentationScore { get; set; }
    public double FocusPercentage { get; set; }
}
