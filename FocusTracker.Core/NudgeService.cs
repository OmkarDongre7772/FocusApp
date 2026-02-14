using System;
using System.Collections.Generic;

namespace FocusTracker.Core
{
    public class NudgeService
    {
        private readonly NotificationPolicy _policy;
        private readonly SettingsService _settings;
        private readonly FocusModeService _focusMode;

        public NudgeService(
            NotificationService notifications,
            NotificationPolicy policy,
            FocusModeService focusMode,
            SettingsService settings)
        {
            _policy = policy;
            _focusMode = focusMode;
            _settings = settings;
        }

        // =============================
        // INTERNAL STATE
        // =============================

        private readonly Queue<DateTime> _switchTimes = new();
        private readonly Dictionary<string, DateTime> _ruleCooldowns = new();

        private DateTime? _focusStartUtc;
        private DateTime? _idleStartUtc;

        private bool _focusPraised;
        private bool _focusInterrupted;

        private (string Title, string Message)? _lastNudge;

        // =============================
        // TUNABLE THRESHOLDS
        // =============================

        private static readonly TimeSpan FragmentWindow = TimeSpan.FromMinutes(2);
        private const int FragmentSwitchThreshold = 8;

        private static readonly TimeSpan DeepFocusPraiseThreshold = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan IdleInterruptThreshold = TimeSpan.FromSeconds(30);

        private static readonly TimeSpan RuleCooldown = TimeSpan.FromMinutes(15);

        // =============================
        // PUBLIC API
        // =============================

        public (string Title, string Message)? ConsumeNudge()
        {
            var n = _lastNudge;
            _lastNudge = null;
            return n;
        }

        // =============================
        // FOCUS LIFECYCLE
        // =============================

        public void OnFocusStarted(TimeSpan duration)
        {
            _focusStartUtc = DateTime.UtcNow;
            _focusPraised = false;
            _focusInterrupted = false;
            _idleStartUtc = null;

            SendSystemNotification(
                "Focus Started",
                $"Stay locked in for {duration.TotalMinutes:0} minutes 💪");
        }

        public void OnFocusEnded(bool completed)
        {
            if (_focusStartUtc == null)
                return;

            var total = DateTime.UtcNow - _focusStartUtc.Value;

            if (completed)
            {
                SendSystemNotification(
                    "Session Completed 🎯",
                    $"Great work! {total.TotalMinutes:0} minutes done.");
            }
            else if (total.TotalMinutes >= 2)
            {
                SendSystemNotification(
                    "Session Stopped",
                    "Focus session ended early.");
            }

            _focusStartUtc = null;
            _idleStartUtc = null;
        }

        public void OnInterrupt()
        {
            if (!_focusMode.IsActive || _focusInterrupted)
                return;

            _focusInterrupted = true;

            SendSystemNotification(
                "Focus Interrupted",
                "You switched away from your focus task.");
        }

        // =============================
        // IDLE EVENTS (RESTORED PROPERLY)
        // =============================

        public void OnIdleStarted()
        {
            _idleStartUtc = DateTime.UtcNow;
        }

        public void OnIdleEnded()
        {
            if (_focusMode.IsActive &&
                _idleStartUtc != null)
            {
                var idleDuration = DateTime.UtcNow - _idleStartUtc.Value;

                if (idleDuration >= IdleInterruptThreshold)
                {
                    SendSystemNotification(
                        "Break Detected",
                        "You were idle during focus mode.");
                }
            }

            _idleStartUtc = null;
        }

        // =============================
        // APP SWITCH TRACKING
        // =============================

        public void OnAppChanged()
        {
            var now = DateTime.UtcNow;

            _switchTimes.Enqueue(now);

            while (_switchTimes.Count > 0 &&
                   now - _switchTimes.Peek() > FragmentWindow)
            {
                _switchTimes.Dequeue();
            }

            if (!_focusMode.IsActive &&
                _switchTimes.Count >= FragmentSwitchThreshold)
            {
                TryBehaviorNudge(
                    "FRAGMENTATION",
                    "High Context Switching",
                    "You're switching apps frequently. Start a focus session?");
            }
        }

        // =============================
        // TICK LOOP
        // =============================

        public void Tick()
        {
            if (!_focusMode.IsActive || _focusStartUtc == null)
                return;

            var duration = DateTime.UtcNow - _focusStartUtc.Value;

            if (!_focusPraised &&
                duration >= DeepFocusPraiseThreshold)
            {
                TryBehaviorNudge(
                    "DEEP_WORK",
                    "Deep Focus Achieved 🔥",
                    "You've been focused for 10+ minutes. Keep going!");

                _focusPraised = true;
            }
        }

        // =============================
        // CORE NUDGE LOGIC
        // =============================

        private void TryBehaviorNudge(string ruleId, string title, string message)
        {
            if (!_settings.Current.NudgesEnabled)
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

        private void SendSystemNotification(string title, string message)
        {
            _lastNudge = (title, message);
        }
    }
}
