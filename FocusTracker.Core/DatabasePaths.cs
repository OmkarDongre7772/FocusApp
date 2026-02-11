using System.IO;

namespace FocusTracker.Core;

public static class DatabasePaths
{
    public static string GetDatabasePath()
    {
        var basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "FocusTracker"
        );

        Directory.CreateDirectory(basePath);

        return Path.Combine(basePath, "focus_tracker.db");
    }
}
