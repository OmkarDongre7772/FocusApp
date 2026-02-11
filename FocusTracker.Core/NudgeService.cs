using System;
using System.Collections.Generic;

namespace FocusTracker.Core
{
    public class NudgeService
    {
        private readonly NotificationService _notifications;
        private readonly NotificationPolicy _policy;

        public NudgeService(
            NotificationService notifications,
            NotificationPolicy policy,
            FocusModeService focusMode)
        {
            _notifications = notifications;
            _policy = policy;
            _focusMode = focusMode;
        }

        private readonly Queue<DateTime> _switchTimes = new();
        private DateTime? _focusStart;

        private bool _focusNudged = false;
        private DateTime _lastRuleFire = DateTime.MinValue;

        private static readonly TimeSpan FocusPraiseThreshold = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan FragmentWindow = TimeSpan.FromMinutes(1);
        private const int FragmentSwitchCount = 1;

        // per-rule cooldown
        private static readonly TimeSpan RuleCooldown = TimeSpan.FromMinutes(1);

        private readonly FocusModeService _focusMode;

        public void OnAppChanged()
        {
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
            if (_focusMode.IsActive) return;
            var now = DateTime.UtcNow;

            // 1️⃣ per-rule cooldown
            if (now - _lastRuleFire < RuleCooldown)
                return;

            // 2️⃣ global policy check
            if (!_policy.CanNotify(ruleId))
                return;

            _lastRuleFire = now;
            _policy.RecordNotification();
            _notifications.Show(title, message);
        }
    }
}