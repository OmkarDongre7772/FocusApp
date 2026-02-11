using System.Diagnostics;

namespace FocusTracker.Core
{
    public class EventLogger
    {
        private readonly Database _database;

        public EventLogger(Database database)
        {
            _database = database;
        }

        public void OnAppChanged(string app)
        {
            Debug.WriteLine("APP: " + app);
            _database.SaveEvent("APP_CHANGED", app);
        }

        public void OnIdleStarted()
        {
            Debug.WriteLine("USER IS IDLE");
            _database.SaveEvent("IDLE_STARTED", null);
        }

        public void OnIdleEnded()
        {
            Debug.WriteLine("USER IS ACTIVE AGAIN");
            _database.SaveEvent("IDLE_ENDED", null);
        }
    }
}