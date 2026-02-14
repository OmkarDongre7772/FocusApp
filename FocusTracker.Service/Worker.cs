using FocusTracker.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FocusTracker.Service;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly FragmentationConfig _fragConfig;
    private readonly SupabaseOptions _supabaseOptions;

    // Core
    private AppTracker? _tracker;
    private EventLogger? _eventLogger;
    private Database? _database;
    private FocusModeService? _focusMode;
    private FocusSessionTracker? _focusTracker;
    private NotificationPolicy? _notificationPolicy;
    private DailyAggregationService? _dailyAggregation;

    // IPC
    private IpcServer? _ipcServer;
    private AnalyticsNotifier? _analyticsNotifier;

    // User / Cloud
    private LocalUserRepository? _userRepo;
    private CloudSyncService? _cloudSync;

    // Nudges
    private NudgeService? _nudgeService;
    private SettingsService? _settingsService;

    // Runtime state
    private bool _trackingEnabled;
    private bool _isLoggedIn;
    private DateTime _lastAggregationDate = DateTime.MinValue;

    private bool _isIdle;

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
        _logger.LogInformation("FocusTracker Service starting...");

        SQLitePCL.Batteries.Init();

        InitializeCore();
        InitializeNudges();
        InitializeCloud();
        WireFocusLifecycle();
        WireTrackerEvents();

        StartIpcServer(stoppingToken);
        StartMainLoop(stoppingToken);
        StartCloudLoop(stoppingToken);

        _logger.LogInformation("FocusTracker Service started successfully.");

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void InitializeCore()
    {
        _database = new Database();
        _eventLogger = new EventLogger(_database);
        _tracker = new AppTracker();
        _focusMode = new FocusModeService();
        _focusTracker = new FocusSessionTracker(_fragConfig);
        _notificationPolicy = new NotificationPolicy();
        _dailyAggregation = new DailyAggregationService();
        _userRepo = new LocalUserRepository();
        _analyticsNotifier = new AnalyticsNotifier();

        _dailyAggregation.RunAggregationForAllMissingDays();
        _lastAggregationDate = DateTime.Now.Date;

        RefreshTrackingState();
    }

    private void InitializeNudges()
    {
        _settingsService = new SettingsService();

        _nudgeService = new NudgeService(
            notifications: null,
            policy: _notificationPolicy!,
            focusMode: _focusMode!,
            settings: _settingsService);
    }

    private void InitializeCloud()
    {
        if (string.IsNullOrWhiteSpace(_supabaseOptions.Url) ||
            string.IsNullOrWhiteSpace(_supabaseOptions.AnonPublicKey))
        {
            _logger.LogInformation("Cloud sync disabled (no configuration).");
            return;
        }

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

    // =========================================================
    // EVENT WIRING
    // =========================================================

    private void WireFocusLifecycle()
    {
        _focusMode!.FocusStartedWithDuration += duration =>
        {
            _focusTracker!.OnFocusStarted(duration);
            _nudgeService?.OnFocusStarted(duration);
        };

        _focusMode.FocusEndedWithResult += completed =>
        {
            _focusTracker!.OnFocusEnded(completed);
            _nudgeService?.OnFocusEnded(completed);

            _analyticsNotifier?.MarkUpdated();
        };
    }

    private void WireTrackerEvents()
    {
        _tracker!.AppChanged += app =>
        {
            if (!_trackingEnabled) return;

            _eventLogger!.OnAppChanged(app);
            _nudgeService?.OnAppChanged();

            if (_focusMode!.IsActive && !_isIdle)
            {
                _focusTracker!.AddInterrupt();
                _eventLogger.OnInterrupt();
                _nudgeService?.OnInterrupt();
            }
        };

        _tracker.IdleStarted += () =>
        {
            if (!_trackingEnabled) return;

            _isIdle = true;
            _eventLogger!.OnIdleStarted();
            _nudgeService?.OnIdleStarted();
        };

        _tracker.IdleEnded += () =>
        {
            if (!_trackingEnabled) return;

            _isIdle = false;
            _eventLogger!.OnIdleEnded();
            _nudgeService?.OnIdleEnded();
        };

        _tracker.Start();
    }

    // =========================================================
    // IPC
    // =========================================================

    private void StartIpcServer(CancellationToken token)
    {
        _ipcServer = new IpcServer(
            _focusMode!,
            _notificationPolicy!,
            _supabaseOptions,
            _nudgeService,
            _analyticsNotifier);

        _ = _ipcServer.StartAsync(token);
    }

    // =========================================================
    // MAIN LOOP (1 sec precision)
    // =========================================================

    private void StartMainLoop(CancellationToken token)
    {
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    RefreshTrackingState();

                    if (_trackingEnabled)
                    {
                        _focusMode!.Tick();
                        _nudgeService?.Tick();

                        if (_focusMode.IsActive && _isIdle)
                            _focusTracker!.AddIdleSeconds(1);

                        HandleDailyAggregationRollover();
                        _analyticsNotifier?.MarkUpdated();
                    }

                    await Task.Delay(1000, token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Main loop error.");
                }
            }

        }, token);
    }

    private void HandleDailyAggregationRollover()
    {
        var today = DateTime.Now.Date;

        if (_lastAggregationDate == today) return;

        _dailyAggregation!.RunAggregationForAllMissingDays();
        _analyticsNotifier?.MarkUpdated();

        _lastAggregationDate = today;
    }

    // =========================================================
    // CLOUD LOOP
    // =========================================================

    private void StartCloudLoop(CancellationToken token)
    {
        if (_cloudSync == null) return;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_isLoggedIn)
                    {
                        _dailyAggregation!
                            .RunAggregationForAllMissingDays();

                        await _cloudSync.RunOnceAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Cloud sync error.");
                }

                await Task.Delay(
                    TimeSpan.FromMinutes(_supabaseOptions.SyncIntervalMinutes),
                    token);
            }

        }, token);
    }

    // =========================================================
    // STATE REFRESH
    // =========================================================

    private void RefreshTrackingState()
    {
        var user = _userRepo!.Get();

        _trackingEnabled = user.TrackingEnabled;
        _isLoggedIn = !string.IsNullOrWhiteSpace(user.AccessToken);
    }
}
