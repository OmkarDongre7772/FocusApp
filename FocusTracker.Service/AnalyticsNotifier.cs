namespace FocusTracker.Service
{
    public class AnalyticsNotifier
    {
        private bool _analyticsUpdated;

        public void MarkUpdated()
        {
            _analyticsUpdated = true;
        }

        public bool ConsumeFlag()
        {
            if (!_analyticsUpdated)
                return false;

            _analyticsUpdated = false;
            return true;
        }
    }
}
