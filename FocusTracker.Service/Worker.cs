using FocusTracker.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Windows.Forms;
using System.Drawing;

namespace FocusTracker.Service;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly FragmentationConfig _fragConfig;
    private readonly SupabaseOptions _supabaseOptions;

    private AppTracker? _tracker;
    private EventLogger? _eventLogger;
    private Database? _database;
    private FocusModeService? _focusMode;
    private FocusSessionTracker? _focusTracker;
    private IpcServer? _ipcServer;
    private NotificationPolicy? _notificationPolicy;
    private DailyAggregationService? _dailyAggregation;

    private LocalUserRepository? _userRepo;
    private CloudSyncService? _cloudSync;

    // ✅ NEW (no functional interference)
    private NotifyIcon? _notifyIcon;
    private NotificationService? _notificationService;
    private NudgeService? _nudgeService;
    private SettingsService? _settingsService;

    private bool _trackingEnabled;
    private bool _isLoggedIn;
    private DateTime _lastAggregationDate = DateTime.MinValue;

    public Worker(
        ILogger<Worker> logger,
        IOptions<FragmentationConfig> fragOptions,
        IOptions<SupabaseOptions> supabaseOptions)
    {
        _logger = logger;
        _fragConfig = fragOptions.Value;
        _supabaseOptions = supabaseOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FocusTracker Service started.");

        SQLitePCL.Batteries.Init();

        // ===============================
        // Core Setup
        // ===============================
        _database = new Database();
        _eventLogger = new EventLogger(_database);
        _tracker = new AppTracker();
        _focusMode = new FocusModeService();
        _focusTracker = new FocusSessionTracker(_fragConfig);
        _notificationPolicy = new NotificationPolicy();
        _dailyAggregation = new DailyAggregationService();
        _userRepo = new LocalUserRepository();

        // ===============================
        // ✅ Notification + Nudge Setup
        // ===============================
        _settingsService = new SettingsService();

        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Icon = SystemIcons.Information,
            Text = "FocusTracker"
        };

        _notificationService = new NotificationService(_notifyIcon);

        _nudgeService = new NudgeService(
            _notificationService,
            _notificationPolicy,
            _focusMode,
            _settingsService);

        _dailyAggregation.RunAggregationForAllMissingDays();
        _lastAggregationDate = DateTime.Now.Date;

        RefreshTrackingState();

        // ===============================
        // Cloud Setup
        // ===============================
        if (!string.IsNullOrWhiteSpace(_supabaseOptions.Url) &&
            !string.IsNullOrWhiteSpace(_supabaseOptions.AnonPublicKey))
        {
            var httpClient = new HttpClient();

            var supabaseClient =
                new SupabaseClient(httpClient, _supabaseOptions);

            var authClient =
                new SupabaseAuthClient(httpClient, _supabaseOptions);

            _cloudSync = new CloudSyncService(
                _userRepo!,
                new LocalAggregateRepository(),
                supabaseClient,
                authClient,
                _logger);

            _logger.LogInformation("Cloud sync initialized.");
        }

        // ===============================
        // Focus Lifecycle Wiring
        // ===============================
        _focusMode.FocusStartedWithDuration += d =>
            _focusTracker.OnFocusStarted(d);

        _focusMode.FocusEndedWithResult += completed =>
            _focusTracker.OnFocusEnded(completed);

        bool isIdle = false;

        _tracker.AppChanged += app =>
        {
            if (!_trackingEnabled) return;

            _eventLogger.OnAppChanged(app);

            // ✅ Nudge hook (no behavior change)
            _nudgeService?.OnAppChanged();

            if (_focusMode!.IsActive && !isIdle)
            {
                _focusTracker!.AddInterrupt();
                _eventLogger.OnInterrupt();
            }
        };

        _tracker.IdleStarted += () =>
        {
            if (!_trackingEnabled) return;

            _eventLogger!.OnIdleStarted();
            _nudgeService?.OnIdleStarted();
            isIdle = true;
        };

        _tracker.IdleEnded += () =>
        {
            if (!_trackingEnabled) return;

            _eventLogger!.OnIdleEnded();
            _nudgeService?.OnIdleEnded();
            isIdle = false;
        };

        _tracker.Start();

        _ipcServer = new IpcServer(
            _focusMode,
            _notificationPolicy,
            _supabaseOptions);

        _ = _ipcServer.StartAsync(stoppingToken);

        // ===============================
        // LOOP 1 → 1 SECOND PRECISION
        // ===============================
        _ = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                RefreshTrackingState();

                if (_trackingEnabled)
                {
                    _focusMode!.Tick();

                    // ✅ Nudge Tick (no logic change)
                    _nudgeService?.Tick();

                    if (_focusMode.IsActive && isIdle)
                        _focusTracker!.AddIdleSeconds(1);

                    var today = DateTime.Now.Date;

                    if (_lastAggregationDate != today)
                    {
                        _dailyAggregation!
                            .RunAggregationForAllMissingDays();

                        _lastAggregationDate = today;
                    }
                }

                await Task.Delay(1000, stoppingToken);
            }

        }, stoppingToken);

        // ===============================
        // LOOP 2 → CLOUD SYNC LOOP
        // ===============================
        if (_cloudSync != null)
        {
            _ = Task.Run(async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        if (_isLoggedIn)
                        {
                            _dailyAggregation!.RunAggregationForAllMissingDays();
                            await _cloudSync.RunOnceAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Cloud sync failed.");
                    }

                    await Task.Delay(
                        TimeSpan.FromMinutes(
                            _supabaseOptions.SyncIntervalMinutes),
                        stoppingToken);
                }

            }, stoppingToken);
        }

        // ✅ Proper tray disposal on shutdown
        stoppingToken.Register(() =>
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
        });

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private void RefreshTrackingState()
    {
        if (_userRepo == null) return;

        var user = _userRepo.Get();

        _trackingEnabled = user.TrackingEnabled;
        _isLoggedIn = !string.IsNullOrWhiteSpace(user.AccessToken);
    }
}
