using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FocusTracker.Core
{
    public static class FocusConfigLoader
    {
        public static FocusConfig Load()
        {
            var basePath = AppContext.BaseDirectory;
            var fullPath = Path.Combine(basePath, "focus-config.json");

            var json = File.ReadAllText(fullPath);
            var doc = JsonDocument.Parse(json);

            var config = new FocusConfig();

            foreach (var app in doc.RootElement.GetProperty("focusApps").EnumerateArray())
            {
                config.FocusApps.Add(app.GetString()!.ToLowerInvariant());
            }

            foreach (var pair in doc.RootElement.GetProperty("focusPairs").EnumerateArray())
            {
                var a = pair[0].GetString()!.ToLowerInvariant();
                var b = pair[1].GetString()!.ToLowerInvariant();

                config.FocusPairs.Add(Normalize(a, b));
            }

            return config;
        }

        private static (string, string) Normalize(string a, string b)
        {
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase) < 0
                ? (a, b)
                : (b, a);
        }
    }
}