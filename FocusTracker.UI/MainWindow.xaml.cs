using FocusTracker.Core;
using FocusTracker.UI;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Forms; // ✅ needed for ToolTipIcon
using Application = System.Windows.Application;


namespace FocusTracker.UI
{
    public partial class MainWindow : Window
    {
        private readonly TrayManager _tray;
        private readonly NudgeService _nudges;
        private readonly NotificationPolicy _notificationPolicy;
        private readonly SettingsService _settings;

        public MainWindow()
        {
            InitializeComponent();

            // ===== Core infrastructure =====

            _notificationPolicy = new NotificationPolicy();
            _settings = new SettingsService();

            // ===== Tray =====
            _tray = new TrayManager(
                onOpen: ShowWindow,
                onExit: ExitApp,
                onSnooze: SnoozeNotifications,
                onSettings: OpenSettings,
                onFocusStart: StartFocus,
                onFocusStop: StopFocus
            );

            // ===== Notifications & Nudges =====
            var notificationService = new NotificationService(_tray.TrayIcon);
            //_nudges = new NudgeService(
            //    notificationService,
            //    _notificationPolicy,
            //    _focusMode
            //)
        }

        // ===== Tray Actions =====

        private void OpenSettings()
        {
            Dispatcher.Invoke(() =>
            {
                if (!IsVisible)
                {
                    Show();
                    WindowState = WindowState.Normal;
                }

                var win = new SettingsWindow(_settings)
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                win.ShowDialog();
            });
        }

        private void SnoozeNotifications(TimeSpan duration)
        {
            _notificationPolicy.Snooze(duration);

            _tray.TrayIcon.ShowBalloonTip(
                2000,
                "Notifications Snoozed",
                $"Notifications paused for {duration.TotalMinutes} minutes",
                ToolTipIcon.Info
            );
        }

        private void StartFocus(TimeSpan duration)
        {
            _tray.TrayIcon.ShowBalloonTip(
                2000,
                "Focus Mode Started",
                $"Deep focus for {duration.TotalMinutes} minutes",
                ToolTipIcon.Info
            );
        }


        private void StopFocus()
        {
            _tray.TrayIcon.ShowBalloonTip(
                2000,
                "Focus Mode Ended",
                "Welcome back 👋",
                ToolTipIcon.Info
            );
        }

        // ===== UI =====

        private void ShowWindow()
        {
            Dispatcher.Invoke(() =>
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();

                var analytics = new AnalyticsService();
                var summary = analytics.GetTodaySummary();

                AppSwitchesText.Text = $"Focus Sessions: {summary.FocusSessions}";
                FocusTimeText.Text = $"Focused Time: {summary.FocusMinutes:F1} minutes";
                LongestFocusText.Text = $"Longest Focus: {summary.LongestFocusMinutes:F1} minutes";

                // Weekly trend
                var weekly = analytics.GetLast7Days();
                WeeklyText.Text =
                    weekly.Days.Count == 0
                        ? "No weekly data yet"
                        : $"7-day avg focus: {weekly.Days.Average(d => d.FocusMinutes):F1} min/day";

                // Suggestions
                SuggestionsList.ItemsSource =
                    new SuggestionEngine().GetSuggestions();

                NotificationStatusText.Text = "Service-managed focus mode";

                // Fragmentation
                var fragmentation = new FragmentationAnalyzer().Analyze();
                FragmentationText.Text =
                    !fragmentation.HasData
                        ? "Fragmentation: Not enough data yet"
                        : fragmentation.Score == 0
                            ? $"Focused (using {fragmentation.FocusedApps} apps)"
                            : $"Fragmentation: {fragmentation.Score}/100 " +
                              $"({fragmentation.Switches} switches, {fragmentation.FocusedApps} apps)";

                var stats = new FocusStatsService().GetStats();
                FocusStatsText.Text =
                    $"Sessions: {stats.TotalSessions}, " +
                    $"Completed: {stats.CompletedSessions}, " +
                    $"Completion: {stats.CompletionRate:P0}\n" +
                    $"Current streak: {stats.CurrentStreak}, " +
                    $"Best streak: {stats.LongestStreak}";
            });
        }

        private void ExitApp()
        {
            _tray.Dispose();
            Application.Current.Shutdown();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }
    }
}