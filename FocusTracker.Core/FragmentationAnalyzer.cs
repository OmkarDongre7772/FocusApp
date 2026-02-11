using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusTracker.Core
{
    public class FragmentationAnalyzer
    {
        private static string ConnectionString => $"Data Source={DatabasePaths.GetDatabasePath()};Cache=Shared";

        private static readonly TimeSpan MinObservation = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan PostIdleIgnore = TimeSpan.FromSeconds(30);

        private const int ExpectedSwitchesPerHour = 20;

        private readonly FocusConfig _config;

        public FragmentationAnalyzer()
        {
            _config = FocusConfigLoader.Load();
        }

        public FragmentationResult Analyze()
        {
            var events = LoadRecentEvents();

            if (events.Count < 2)
                return FragmentationResult.NotEnoughData();

            if (events[^1].Time - events[0].Time < MinObservation)
                return FragmentationResult.NotEnoughData();

            var activeApps = new HashSet<string>();
            int validSwitches = 0;
            DateTime? lastIdle = null;

            foreach (var e in events)
            {
                if (e.Type == "IDLE_STARTED")
                {
                    lastIdle = e.Time;
                    continue;
                }

                if (e.Type == "APP_CHANGED")
                {
                    if (lastIdle != null &&
                        e.Time - lastIdle < PostIdleIgnore)
                        continue;

                    validSwitches++;
                    activeApps.Add(e.App);
                }
            }

            var focusAppsUsed = activeApps
                .Where(a => _config.FocusApps.Contains(a))
                .ToList();

            // ✅ Focused cases
            if (focusAppsUsed.Count == 1)
                return FragmentationResult.Focused(1, validSwitches);

            if (focusAppsUsed.Count == 2)
            {
                var pair = NormalizePair(focusAppsUsed[0], focusAppsUsed[1]);
                if (_config.FocusPairs.Contains(pair))
                    return FragmentationResult.Focused(2, validSwitches);
            }

            double ratio = (double)validSwitches / ExpectedSwitchesPerHour;
            int score = (int)Math.Min(ratio * 100, 100);

            return FragmentationResult.Fragmented(score, validSwitches, focusAppsUsed.Count);
        }

        private static (string, string) NormalizePair(string a, string b)
        {
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase) < 0
                ? (a, b)
                : (b, a);
        }

        private List<EventRow> LoadRecentEvents()
        {
            var list = new List<EventRow>();

            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText =
            """
            SELECT time, type, data
            FROM events
            WHERE time >= datetime('now', '-60 minutes')
            ORDER BY time;
            """;

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new EventRow
                {
                    Time = DateTime.Parse(reader.GetString(0)),
                    Type = reader.GetString(1),
                    App = reader.IsDBNull(2)
                        ? "unknown"
                        : reader.GetString(2).ToLowerInvariant()
                });
            }

            return list;
        }

        private class EventRow
        {
            public DateTime Time { get; set; }
            public string Type { get; set; }
            public string App { get; set; }
        }
    }

    public class FragmentationResult
    {
        public bool HasData { get; private set; }
        public bool IsFocused { get; private set; }
        public int Score { get; private set; }
        public int Switches { get; private set; }
        public int FocusedApps { get; private set; }

        public static FragmentationResult NotEnoughData()
            => new() { HasData = false };

        public static FragmentationResult Focused(int apps, int switches)
            => new()
            {
                HasData = true,
                IsFocused = true,
                FocusedApps = apps,
                Switches = switches
            };

        public static FragmentationResult Fragmented(int score, int switches, int apps)
            => new()
            {
                HasData = true,
                IsFocused = false,
                Score = score,
                Switches = switches,
                FocusedApps = apps
            };
    }
}