namespace FocusTracker.Core
{
    public class SettingsService
    {
        public AppSettings Current { get; private set; }

        public SettingsService()
        {
            Current = new AppSettings();
        }

        public void Update(AppSettings updated)
        {
            Current = updated;
        }
    }
}
