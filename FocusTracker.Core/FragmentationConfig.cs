namespace FocusTracker.Core
{
    public class FragmentationConfig
    {
        public double IdleWeight { get; set; }
        public double InterruptWeight { get; set; }
        public double EarlyStopWeight { get; set; }

        public int MaxInterruptThreshold { get; set; }
        public int MaxIdleThresholdSeconds { get; set; }
    }
}
