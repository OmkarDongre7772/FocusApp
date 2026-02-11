namespace FocusTracker.UI.Models
{
    public class DailySummaryModel
    {
        public string Date { get; set; }
        public double FocusMinutes { get; set; }
        public int FocusSessions { get; set; }
        public int FragmentationScore { get; set; }
    }
}
