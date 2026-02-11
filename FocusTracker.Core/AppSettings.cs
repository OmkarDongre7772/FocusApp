namespace FocusTracker.Core
{
    public class AppSettings
    {
        public bool NudgesEnabled { get; set; } = true;

        // Fragmentation sensitivity (1 = strict, 3 = relaxed)
        public int FragmentationSensitivity { get; set; } = 2;

        // Minutes before praising focus
        public int FocusPraiseMinutes { get; set; } = 1;

        // Safety cap
        public int MaxNotificationsPerHour { get; set; } = 3;

        // Default snooze (minutes)
        public int DefaultSnoozeMinutes { get; set; } = 30;
    }
}