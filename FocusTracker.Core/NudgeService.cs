using System;
using System.Collections.Generic;

namespace FocusTracker.Core
{
    public class NudgeService
    {
        private readonly NotificationService _notifications;
        private readonly NotificationPolicy _policy;
        private readonly SettingsService _settings;

        public NudgeService(
    NotificationService notifications,
    NotificationPolicy policy,
    FocusModeService focusMode,
    SettingsService settings)
        {
            _notifications = notifications;
            _policy = policy;
            _focusMode = focusMode;
            _settings = settings;
        }


        private readonly Queue<DateTime> _switchTimes = new();
        private DateTime? _focusStart;

        private bool _focusNudged = false;
        private readonly Dictionary<string, DateTime> _ruleCooldowns = new();


        private static readonly TimeSpan FocusPraiseThreshold = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan FragmentWindow = TimeSpan.FromMinutes(1);
        private const int FragmentSwitchCount = 5;

        // per-rule cooldown
        private static readonly TimeSpan RuleCooldown = TimeSpan.FromMinutes(1);

        private readonly FocusModeService _focusMode;

        private (string Title, string Message)? _lastNudge;

        public (string Title, string Message)? ConsumeNudge()
        {
            var n = _lastNudge;
            _lastNudge = null;
            return n;
        }


        public void OnAppChanged()
        {
            Console.WriteLine("App changed event");

            var now = DateTime.UtcNow;

            _switchTimes.Enqueue(now);

            while (_switchTimes.Count > 0 &&
                   now - _switchTimes.Peek() > FragmentWindow)
            {
                _switchTimes.Dequeue();
            }

            if (_switchTimes.Count >= FragmentSwitchCount)
            {
                TryNudge(
                    ruleId: "FRAGMENTATION",
                    title: "Too many switches",
                    message: "Lots of switching detected. Try a 25-minute focus session?"
                );
            }

            _focusStart = now;
            _focusNudged = false;
        }

        public void OnIdleStarted()
        {
            _focusStart = null;
            _focusNudged = false;
        }

        public void OnIdleEnded()
        {
            _focusStart = DateTime.UtcNow;
            _focusNudged = false;
        }

        public void Tick()
        {
            if (_focusStart == null || _focusNudged)
                return;

            var duration = DateTime.UtcNow - _focusStart.Value;

            if (duration >= FocusPraiseThreshold)
            {
                TryNudge(
                    ruleId: "FOCUS_PRAISE",
                    title: "Great focus!",
                    message: "Nice focus streak — keep it going 🚀"
                );

                _focusNudged = true;
            }
        }

        private void TryNudge(string ruleId, string title, string message)
        {
            //if (!_settings.Current.NudgesEnabled)
            //    return;
            Console.WriteLine("TRYING NUDGE: " + ruleId);
            Console.WriteLine("NudgesEnabled: " + _settings.Current.NudgesEnabled);
            Console.WriteLine("Focus active: " + _focusMode.IsActive);
            Console.WriteLine("Policy allows: " + _policy.CanNotify(ruleId));


            if (_focusMode.IsActive)
                return;

            var now = DateTime.UtcNow;

            if (_ruleCooldowns.TryGetValue(ruleId, out var lastFire))
            {
                if (now - lastFire < RuleCooldown)
                    return;
            }

            if (!_policy.CanNotify(ruleId))
                return;

            _ruleCooldowns[ruleId] = now;
            _policy.RecordNotification();
            _lastNudge = (title, message);
        }
    }
}