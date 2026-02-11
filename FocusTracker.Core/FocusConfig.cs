using System.Collections.Generic;

namespace FocusTracker.Core
{
    public class FocusConfig
    {
        public HashSet<string> FocusApps { get; set; } = new();
        public HashSet<(string, string)> FocusPairs { get; set; } = new();
    }
}