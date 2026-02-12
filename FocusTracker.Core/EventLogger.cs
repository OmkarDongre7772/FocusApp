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
            _database.SaveAppSwitch(app);
        }

        public void OnIdleStarted()
        {
            _database.SaveIdleStart();
        }

        public void OnIdleEnded()
        {
            _database.SaveIdleEnd();
        }
        public void OnInterrupt()
        {
            _database.SaveInterrupt();
        }

    }

}