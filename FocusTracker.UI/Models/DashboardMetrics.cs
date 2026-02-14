namespace FocusTracker.UI.Models
{
    public class DashboardMetrics
    {
        // TODAY
        public double FocusMinutes { get; set; }
        public double DeepWorkMinutes { get; set; }
        public int Sessions { get; set; }
        public double AvgSessionMinutes { get; set; }
        public double InterruptsPerSession { get; set; }
        public double IdleRatioPercent { get; set; }
        public int FragmentationScore { get; set; }
        public string FocusQuality { get; set; } = "";
        public int ProductivityScore { get; set; }

        // BEHAVIOR
        public string? TopDistractionApp { get; set; }
        public int TopDistractionCount { get; set; }
        public string? PeakFocusHour { get; set; }
        public double EstimatedTimeLostMinutes { get; set; }

        // WEEKLY
        public double WeeklyAvgFocus { get; set; }
        public double TrendPercent { get; set; }
        public string TrendDirection { get; set; } = "";
        public int CurrentStreak { get; set; }
    }
}

