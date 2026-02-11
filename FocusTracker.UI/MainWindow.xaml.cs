using FocusTracker.Core;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace FocusTracker.UI
{
    public partial class MainWindow : Window
    {
        private readonly TrayManager _tray;
        private readonly NotificationPolicy _notificationPolicy;
        private readonly SettingsService _settings;
        private readonly IpcClient _ipc = new();

        public MainWindow()
        {
            InitializeComponent();

            _notificationPolicy = new NotificationPolicy();
            _settings = new SettingsService();

            _tray = new TrayManager(
                onOpen: ShowWindow,
                onExit: ExitApp,
                onSnooze: SnoozeNotifications,
                onSettings: OpenSettings,
                onFocusStart: StartFocus,
                onFocusStop: StopFocus
            );
        }

        // =======================
        // Tray Actions
        // =======================

        private void OpenSettings()
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
            _ = Task.Run(async () =>
            {
                Console.WriteLine("Terminal-> Start Focus Clicked");
                Debug.WriteLine("Debug-> Start Focus Clicked");
                var response = await _ipc.SendAsync(new IpcRequest
                {
                    Command = "StartFocus",
                    DurationMinutes = (int)duration.TotalMinutes
                });

                Application.Current.Dispatcher.Invoke(() =>
                {
                    _tray.TrayIcon.ShowBalloonTip(
                        2000,
                        "Focus Mode",
                        response?.Message ?? "No response",
                        ToolTipIcon.Info);
                });
            });
        }

        private void StopFocus()
        {
            _ = Task.Run(async () =>
            {
                Console.WriteLine("Terminal-> Stop Focus Clicked");
                Debug.WriteLine("Debug-> Stop Focus Clicked");
                var response = await _ipc.SendAsync(new IpcRequest
                {
                    Command = "StopFocus"
                });

                Application.Current.Dispatcher.Invoke(() =>
                {
                    _tray.TrayIcon.ShowBalloonTip(
                        2000,
                        "Focus Mode",
                        response?.Message ?? "No response",
                        ToolTipIcon.Info);
                });
            });
        }

        // =======================
        // UI
        // =======================

        private void ShowWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();

            var analytics = new AnalyticsService();
            var summary = analytics.GetTodaySummary();

            AppSwitchesText.Text = $"Focus Sessions: {summary.FocusSessions}";
            FocusTimeText.Text = $"Focused Time: {summary.FocusMinutes:F1} minutes";
            LongestFocusText.Text = $"Longest Focus: {summary.LongestFocusMinutes:F1} minutes";

            var weekly = analytics.GetLast7Days();

            WeeklyText.Text =
                weekly.Days.Count == 0
                    ? "No weekly data yet"
                    : $"7-day avg focus: {weekly.Days.Average(d => d.FocusMinutes):F1} min/day";

            SuggestionsList.ItemsSource =
                new SuggestionEngine().GetSuggestions();

            NotificationStatusText.Text = "Service-managed focus mode";

            if (weekly.Days.Count > 0)
            {
                var latest = weekly.Days.First();
                FragmentationText.Text =
                    $"Fragmentation: {latest.FragmentationScore}/100";
            }
            else
            {
                FragmentationText.Text = "Fragmentation: No data yet";
            }

            var stats = new FocusStatsService().GetStats();
            FocusStatsText.Text =
                $"Sessions: {stats.TotalSessions}, " +
                $"Completed: {stats.CompletedSessions}, " +
                $"Completion: {stats.CompletionRate:P0}\n" +
                $"Current streak: {stats.CurrentStreak}, " +
                $"Best streak: {stats.LongestStreak}";
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
