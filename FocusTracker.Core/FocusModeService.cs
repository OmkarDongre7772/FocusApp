using System;

namespace FocusTracker.Core
{
    public class FocusModeService
    {
        public bool IsActive { get; private set; }
        public DateTime? EndsAt { get; private set; }

        public event Action? FocusStarted;
        public event Action? FocusEnded;
        public event Action<TimeSpan>? FocusStartedWithDuration;
        public event Action<bool>? FocusEndedWithResult;

        public void Start(TimeSpan duration)
        {
            IsActive = true;
            EndsAt = DateTime.UtcNow.Add(duration);
            FocusStartedWithDuration?.Invoke(duration);
        }

        public void Stop(bool completed = false)
        {
            IsActive = false;
            EndsAt = null;
            FocusEndedWithResult?.Invoke(completed);
        }

        public void Tick()
        {
            if (!IsActive || EndsAt == null)
                return;

            if (DateTime.UtcNow >= EndsAt.Value)
            {
                Stop(completed: true);
            }
        }
    }
}