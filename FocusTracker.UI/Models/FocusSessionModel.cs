namespace FocusTracker.UI.Models
{
    public class FocusSessionModel
    {
        public string StartTime { get; set; }
        public double ActualMinutes { get; set; }
        public int FragmentationScore { get; set; }
        public int InterruptCount { get; set; }
        public int IdleSeconds { get; set; }
    }
}
