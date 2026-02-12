using FocusTracker.Core;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;

namespace FocusTracker.UI
{
    public partial class MainWindow : Window
    {
        private readonly TrayManager _tray;
        private readonly IpcClient _ipc = new();

        private CancellationTokenSource? _statusCts;

        private DateTime? _currentFocusEnd;
        private DateTime? _currentFocusStart;

        private bool _suppressToggleEvent = false;

        public MainWindow()
        {
            InitializeComponent();

            _tray = new TrayManager(
                onOpen: ShowWindow,
                onExit: ExitApp,
                onSnooze: _ => { },
                onSettings: OpenSettings,   // ✅ proper hook
                onFocusStart: StartFocus,
                onFocusStop: StopFocus
            );

            StartStatusLoop();
        }

        // =========================
        // STATUS LOOP
        // =========================

        private void StartStatusLoop()
        {
            _statusCts = new CancellationTokenSource();
            var token = _statusCts.Token;

            _ = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var response =
                            await _ipc.SendAsync(new IpcRequest
                            {
                                Command = "GetStatus"
                            });

                        if (response?.Status != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                RenderStatus(response.Status);
                            });
                        }
                    }
                    catch
                    {
                        // Silent fail — service may be restarting
                    }

                    await Task.Delay(1000, token);
                }

            }, token);
        }

        // =========================
        // RENDER STATUS
        // =========================

        private void RenderStatus(ServiceStatus status)
        {
            RenderLoginState(status);
            RenderTrackingState(status);
            RenderFocusState(status);
        }

        private void RenderLoginState(ServiceStatus status)
        {
            if (status.IsLoggedIn)
            {
                LoginStatusText.Text =
                    $"Logged in as {status.Username}" +
                    (status.TeamId != null
                        ? $" (Team: {status.TeamId})"
                        : "");

                LoginButton.Content = "Logout";
            }
            else
            {
                LoginStatusText.Text = "Not logged in (Local mode)";
                LoginButton.Content = "Login";
            }

            // ✅ Tracking works in both local and cloud mode
            TrackingToggle.IsEnabled = true;
        }

        private void RenderTrackingState(ServiceStatus status)
        {
            _suppressToggleEvent = true;

            TrackingToggle.IsChecked = status.TrackingEnabled;
            TrackingToggle.Content =
                status.TrackingEnabled
                    ? "Tracking Enabled"
                    : "Tracking Disabled";

            _suppressToggleEvent = false;
        }

        private void RenderFocusState(ServiceStatus status)
        {
            if (status.IsFocusActive &&
                status.FocusEndsAtUtc != null)
            {
                FocusStatusText.Text = "ACTIVE";
                FocusStatusText.Foreground = Brushes.DarkGreen;

                _currentFocusEnd = status.FocusEndsAtUtc.Value;

                if (_currentFocusStart == null)
                    _currentFocusStart = DateTime.UtcNow;

                var total =
                    (_currentFocusEnd.Value - _currentFocusStart.Value).TotalSeconds;

                var remaining =
                    (_currentFocusEnd.Value - DateTime.UtcNow).TotalSeconds;

                if (remaining > 0 && total > 0)
                {
                    double progress =
                        (1 - (remaining / total)) * 100;

                    FocusProgressBar.Value = progress;

                    CountdownText.Text =
                        $"Ends in {TimeSpan.FromSeconds(remaining):mm\\:ss}";
                }
            }
            else
            {
                FocusStatusText.Text = "Inactive";
                FocusStatusText.Foreground = Brushes.Gray;
                FocusProgressBar.Value = 0;
                CountdownText.Text = "";
                _currentFocusStart = null;
            }
        }

        // =========================
        // DASHBOARD
        // =========================

        private void ShowWindow()
        {
            Show();
            Activate();

            var analytics = new AnalyticsService();
            var summary = analytics.GetTodaySummary();

            AppSwitchesText.Text =
                $"Sessions: {summary.FocusSessions}";

            FocusTimeText.Text =
                $"Focus Time: {summary.FocusMinutes:F1} minutes";

            LongestFocusText.Text =
                $"Longest Session: {summary.LongestFocusMinutes:F1} minutes";

            var weekly = analytics.GetLast7Days();

            if (weekly.Days.Count > 0)
            {
                var avgFocus =
                    weekly.Days.Average(d => d.FocusMinutes);

                var avgFrag =
                    weekly.Days.Average(d => d.FragmentationScore);

                WeeklyText.Text =
                    $"Avg Focus: {avgFocus:F1} min/day";

                CompletionTrendText.Text =
                    avgFrag > 60
                        ? "High fragmentation detected"
                        : "Healthy focus stability";

                FragmentationText.Text =
                    $"Fragmentation: {weekly.Days.First().FragmentationScore}/100";
            }
            else
            {
                WeeklyText.Text = "Not enough weekly data";
                CompletionTrendText.Text = "";
                FragmentationText.Text = "";
            }

            var stats = new FocusStatsService().GetStats();

            InterruptText.Text =
                $"Completion Rate: {stats.CompletionRate:P0}";

            IdleText.Text =
                $"Current Streak: {stats.CurrentStreak}";
        }

        private void OpenSettings()
        {
            var settingsService = new SettingsService();

            var window = new SettingsWindow(settingsService)
            {
                Owner = this
            };

            window.ShowDialog();
        }


        // =========================
        // TRACKING TOGGLE
        // =========================

        private async void TrackingToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressToggleEvent) return;

            await _ipc.SendAsync(new IpcRequest
            {
                Command = "ToggleTracking",
                ToggleValue = true
            });
        }

        private async void TrackingToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_suppressToggleEvent) return;

            await _ipc.SendAsync(new IpcRequest
            {
                Command = "ToggleTracking",
                ToggleValue = false
            });
        }

        // =========================
        // LOGIN / LOGOUT
        // =========================

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var status =
                await _ipc.SendAsync(new IpcRequest
                {
                    Command = "GetStatus"
                });

            if (status?.Status?.IsLoggedIn == true)
            {
                await _ipc.SendAsync(new IpcRequest
                {
                    Command = "Logout"
                });
            }
            else
            {
                var login = new LoginWindow
                {
                    Owner = this
                };

                login.ShowDialog();
            }
        }

        // =========================
        // FOCUS CONTROL
        // =========================

        private void StartFocus(TimeSpan duration)
        {
            _ = _ipc.SendAsync(new IpcRequest
            {
                Command = "StartFocus",
                DurationMinutes = (int)duration.TotalMinutes
            });

            _currentFocusStart = DateTime.UtcNow;
        }

        private void StopFocus()
        {
            _ = _ipc.SendAsync(new IpcRequest
            {
                Command = "StopFocus"
            });

            _currentFocusStart = null;
        }

        // =========================
        // EXIT
        // =========================

        private void ExitApp()
        {
            _statusCts?.Cancel();
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