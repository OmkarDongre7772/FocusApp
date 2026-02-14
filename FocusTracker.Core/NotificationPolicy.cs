using System;

namespace FocusTracker.Core
{
    public class NotificationPolicy
    {
        private const int MaxNotificationsPerHour = 3;
        private static readonly TimeSpan StartupGracePeriod = TimeSpan.FromSeconds(5);

        private readonly DateTime _appStartTime = DateTime.UtcNow;
        private DateTime _snoozedUntil = DateTime.MinValue;

        private int _hourlyCount = 0;
        private DateTime _hourWindowStart = DateTime.UtcNow;

        public void Snooze(TimeSpan duration)
        {
            _snoozedUntil = DateTime.UtcNow.Add(duration);
        }

        public bool CanNotify(string ruleId)
        {
            var now = DateTime.UtcNow;

            if (now - _appStartTime < StartupGracePeriod)
                return false;

            if (now < _snoozedUntil)
                return false;

            if ((now - _hourWindowStart).TotalHours >= 1)
            {
                _hourWindowStart = now;
                _hourlyCount = 0;
            }


            if (_hourlyCount >= MaxNotificationsPerHour)
                return false;

            return true;
        }

        public void RecordNotification()
        {
            _hourlyCount++;
        }

        public DateTime? SnoozedUntil =>
            DateTime.UtcNow < _snoozedUntil ? _snoozedUntil : null;
    }
}