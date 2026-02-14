using FocusTracker.Core;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Media;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using FocusTracker.UI.Models;
using System.Diagnostics;
using Color = System.Windows.Media.Color;

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

            // ✅ Load dashboard on first startup
            Loaded += (s, e) => LoadDashboard();

            _tray = new TrayManager(
                onOpen: ShowWindow,
                onExit: ExitApp,
                onSettings: OpenSettings,
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
                        var response = await _ipc.SendAsync(new IpcRequest
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
            if (!string.IsNullOrWhiteSpace(status.PendingNudgeTitle))
            {
                _tray.TrayIcon.ShowBalloonTip(
                    3000,
                    status.PendingNudgeTitle,
                    status.PendingNudgeMessage,
                    ToolTipIcon.Info);
            }

            if (status.AnalyticsUpdated)
                LoadDashboard();

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
                    (status.TeamId != null ? $" (Team: {status.TeamId})" : "");

                LoginButton.Content = "Logout";
            }
            else
            {
                LoginStatusText.Text = "Not logged in (Local mode)";
                LoginButton.Content = "Login";
            }

            TrackingToggle.IsEnabled = true;
        }

        private void RenderTrackingState(ServiceStatus status)
        {
            _suppressToggleEvent = true;

            TrackingToggle.IsChecked = status.TrackingEnabled;
            TrackingToggle.Content = status.TrackingEnabled
                ? "Tracking Enabled"
                : "Tracking Disabled";

            _suppressToggleEvent = false;
        }

        private void RenderFocusState(ServiceStatus status)
        {
            if (status.IsFocusActive && status.FocusEndsAtUtc != null)
            {
                FocusStatusText.Text = "ACTIVE";
                FocusStatusText.Foreground =
    (Brush)FindResource("SuccessBrush");

                PrimaryFocusButton.Content = "Stop Focus";
                PrimaryFocusButton.Background =
    (Brush)FindResource("DangerBrush");

                SetQuickButtonsEnabled(false);

                _currentFocusEnd = status.FocusEndsAtUtc.Value;

                if (_currentFocusStart == null)
                    _currentFocusStart = DateTime.UtcNow;

                var total = (_currentFocusEnd.Value - _currentFocusStart.Value).TotalSeconds;
                var remaining = (_currentFocusEnd.Value - DateTime.UtcNow).TotalSeconds;

                if (remaining > 0 && total > 0)
                {
                    double progress = (1 - (remaining / total)) * 100;

                    FocusProgressBar.Value = progress;
                    CountdownText.Text =
                        $"Ends in {TimeSpan.FromSeconds(remaining):mm\\:ss}";
                }
            }
            else
            {
                FocusStatusText.Text = "Inactive";
                FocusStatusText.Foreground =
    (Brush)FindResource("SecondaryTextBrush");


                FocusProgressBar.Value = 0;
                CountdownText.Text = "";

                _currentFocusStart = null;
                _currentFocusEnd = null;

                SetQuickButtonsEnabled(true);

                PrimaryFocusButton.Content = "Start 25 Minute Focus";
                PrimaryFocusButton.Background =
                    (Brush)FindResource("AccentBrush");
            }
        }

        // =========================
        // DASHBOARD
        // =========================
        private void ShowWindow()
        {
            Show();
            Activate();
            LoadDashboard();
        }

        private void LoadDashboard()
        {
            var service = new DashboardService();
            DashboardMetrics metrics = service.GetDashboardMetrics();

            if (metrics == null)
            {
                Debug.WriteLine("LoadDashboard -> Metrics is NULL");
                return;
            }

            Debug.WriteLine("========== DASHBOARD REFRESH ==========");

            // =========================
            // TODAY METRICS
            // =========================

            AppSwitchesText.Text =
                $"Sessions: {metrics.Sessions}";
            Debug.WriteLine($"Sessions -> {metrics.Sessions}");

            ProductivityScoreText.Text =
                $"{metrics.ProductivityScore}/100";
            Debug.WriteLine($"ProductivityScore -> {metrics.ProductivityScore}");

            DeepWorkText.Text =
                $"{metrics.DeepWorkMinutes:F1} min";
            Debug.WriteLine($"DeepWorkMinutes -> {metrics.DeepWorkMinutes}");

            TopDistractionText.Text =
                metrics.TopDistractionApp == null
                    ? "None"
                    : $"{metrics.TopDistractionApp} ({metrics.TopDistractionCount})";
            Debug.WriteLine($"TopDistraction -> {metrics.TopDistractionApp} ({metrics.TopDistractionCount})");

            FocusTimeText.Text =
                $"{metrics.FocusMinutes:F1} min";
            Debug.WriteLine($"FocusMinutes -> {metrics.FocusMinutes}");

            LongestFocusText.Text =
                $"{metrics.AvgSessionMinutes:F1} min";
            Debug.WriteLine($"AvgSessionMinutes -> {metrics.AvgSessionMinutes}");

            FragmentationText.Text =
                $"{metrics.FocusQuality} ({metrics.FragmentationScore}/100)";
            Debug.WriteLine($"FragmentationScore -> {metrics.FragmentationScore}");
            Debug.WriteLine($"FocusQuality -> {metrics.FocusQuality}");

            InterruptText.Text =
                $"{metrics.InterruptsPerSession:F1} per session";
            Debug.WriteLine($"InterruptsPerSession -> {metrics.InterruptsPerSession}");

            IdleText.Text =
                $"{metrics.IdleRatioPercent:F0}%";
            Debug.WriteLine($"IdleRatioPercent -> {metrics.IdleRatioPercent}");
            Debug.WriteLine($"EstimatedTimeLostMinutes -> {metrics.EstimatedTimeLostMinutes}");

            // =========================
            // WEEKLY METRICS
            // =========================

            WeeklyText.Text =
                $"{metrics.WeeklyAvgFocus:F1} min/day  |  Streak: {metrics.CurrentStreak} days";
            Debug.WriteLine($"WeeklyAvgFocus -> {metrics.WeeklyAvgFocus}");
            Debug.WriteLine($"CurrentStreak -> {metrics.CurrentStreak}");

            CompletionTrendText.Text =
                metrics.TrendDirection switch
                {
                    "UP" => $"Trend: ↑ {metrics.TrendPercent:F1}%",
                    "DOWN" => $"Trend: ↓ {metrics.TrendPercent:F1}%",
                    _ => "Trend: Stable"
                };

            Debug.WriteLine($"TrendDirection -> {metrics.TrendDirection}");
            Debug.WriteLine($"TrendPercent -> {metrics.TrendPercent}");

            Debug.WriteLine("=======================================");
            ApplyKpiColorGrading(metrics);

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
            var status = await _ipc.SendAsync(new IpcRequest
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

        private void Focus1_Click(object sender, RoutedEventArgs e)
            => StartFocus(TimeSpan.FromMinutes(1));

        private void Focus25_Click(object sender, RoutedEventArgs e)
            => StartFocus(TimeSpan.FromMinutes(25));

        private void Focus45_Click(object sender, RoutedEventArgs e)
            => StartFocus(TimeSpan.FromMinutes(45));

        private void Focus60_Click(object sender, RoutedEventArgs e)
            => StartFocus(TimeSpan.FromMinutes(60));

        private void PrimaryFocusButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentFocusEnd != null)
                StopFocus();
            else
                StartFocus(TimeSpan.FromMinutes(25));
        }

        private void SetQuickButtonsEnabled(bool enabled)
        {
            foreach (var element in QuickButtonsGrid.Children)
            {
                if (element is System.Windows.Controls.Button btn)
                {
                    btn.IsEnabled = enabled;
                    btn.Opacity = enabled ? 1 : 0.6;
                }
            }
        }


        private void ThemeToggle_Checked(object sender, RoutedEventArgs e)
        {
            // ChatGPT-like neutral dark
            Resources["BackgroundBrush"] =
                new SolidColorBrush(Color.FromRgb(20, 20, 23));  // app background

            Resources["SurfaceBrush"] =
                new SolidColorBrush(Color.FromRgb(32, 33, 36));  // card surface

            Resources["PrimaryTextBrush"] =
                new SolidColorBrush(Color.FromRgb(236, 236, 241)); // primary text

            Resources["SecondaryTextBrush"] =
                new SolidColorBrush(Color.FromRgb(160, 160, 170));

            Resources["BorderBrush"] =
                new SolidColorBrush(Color.FromRgb(60, 60, 68));

            // ChatGPT style accent (soft blue)
            Resources["AccentBrush"] =
                new SolidColorBrush(Color.FromRgb(100, 149, 237));

            Resources["SuccessBrush"] =
                new SolidColorBrush(Color.FromRgb(46, 204, 113));

            Resources["WarningBrush"] =
                new SolidColorBrush(Color.FromRgb(255, 193, 7));

            Resources["DangerBrush"] =
                new SolidColorBrush(Color.FromRgb(255, 99, 99));

            ThemeToggle.Content = "Light";
        }


        private void ThemeToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            Resources["BackgroundBrush"] =
                new SolidColorBrush(Color.FromRgb(244, 246, 251));

            Resources["SurfaceBrush"] =
                Brushes.White;

            Resources["PrimaryTextBrush"] =
                new SolidColorBrush(Color.FromRgb(17, 24, 39));

            Resources["SecondaryTextBrush"] =
                new SolidColorBrush(Color.FromRgb(107, 114, 128));

            Resources["BorderBrush"] =
                new SolidColorBrush(Color.FromRgb(229, 231, 235));

            Resources["AccentBrush"] =
                new SolidColorBrush(Color.FromRgb(37, 99, 235));

            Resources["SuccessBrush"] =
                new SolidColorBrush(Color.FromRgb(5, 150, 105));

            Resources["WarningBrush"] =
                new SolidColorBrush(Color.FromRgb(202, 138, 4));

            Resources["DangerBrush"] =
                new SolidColorBrush(Color.FromRgb(220, 38, 38));

            ThemeToggle.Content = "Dark";
        }

        private void ApplyKpiColorGrading(DashboardMetrics metrics)
        {
            // Productivity Score
            if (metrics.ProductivityScore >= 80)
                ProductivityScoreText.Foreground =
                    (Brush)FindResource("SuccessBrush");
            else if (metrics.ProductivityScore >= 50)
                ProductivityScoreText.Foreground =
                    (Brush)FindResource("WarningBrush");
            else
                ProductivityScoreText.Foreground =
                    (Brush)FindResource("DangerBrush");

            // Trend
            if (metrics.TrendDirection == "UP")
                CompletionTrendText.Foreground =
                    (Brush)FindResource("SuccessBrush");
            else if (metrics.TrendDirection == "DOWN")
                CompletionTrendText.Foreground =
                    (Brush)FindResource("DangerBrush");
            else
                CompletionTrendText.Foreground =
                    (Brush)FindResource("SecondaryTextBrush");
        }

    }
}
