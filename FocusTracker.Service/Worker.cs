using FocusTracker.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FocusTracker.Service;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    private AppTracker? _tracker;
    private EventLogger? _eventLogger;
    private Database? _database;
    private FocusModeService? _focusMode;
    private FocusSessionTracker? _focusTracker;
    private IpcServer? _ipcServer;
    private NotificationPolicy? _notificationPolicy;
    private readonly FragmentationConfig _fragConfig;

    private DailyAggregationService? _dailyAggregation;
    private DateTime _lastAggregationDate = DateTime.MinValue;


    public Worker( ILogger<Worker> logger, IOptions<FragmentationConfig> fragOptions)
    {
        _logger = logger;
        _fragConfig = fragOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FocusTracker Service started.");

        SQLitePCL.Batteries.Init();

        _database = new Database();
        _eventLogger = new EventLogger(_database);
        _tracker = new AppTracker();

        _focusMode = new FocusModeService();
        _focusTracker = new FocusSessionTracker(_fragConfig);
        _notificationPolicy = new NotificationPolicy();

        _dailyAggregation = new DailyAggregationService();

        // Run recovery on startup
        _dailyAggregation.RunAggregationForAllMissingDays();
        _lastAggregationDate = DateTime.Now.Date;


        bool isIdle = false;


        // ===== Focus lifecycle =====

        _focusMode.FocusStartedWithDuration += d =>
            _focusTracker.OnFocusStarted(d);

        _focusMode.FocusEndedWithResult += completed =>
            _focusTracker.OnFocusEnded(completed);

        // ===== Activity Logging =====

        _tracker.AppChanged += app =>
        {
            _eventLogger.OnAppChanged(app);

            if (_focusMode.IsActive && !isIdle)
            {
                _focusTracker.AddInterrupt();
            }
        };

        _tracker.IdleStarted += () =>
        {
            _eventLogger.OnIdleStarted();
            isIdle = true;
        };

        _tracker.IdleEnded += () =>
        {
            _eventLogger.OnIdleEnded();
            isIdle = false;
        };


        _tracker.Start();

        _ipcServer = new IpcServer(_focusMode, _notificationPolicy);
        _ = _ipcServer.StartAsync(stoppingToken);

        // Focus timer engine
        _ = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _focusMode?.Tick();

                if (_focusMode?.IsActive == true && isIdle)
                {
                    _focusTracker?.AddIdleSeconds(1);
                }

                // 🔹 DAILY AGGREGATION CHECK (Local Midnight)
                var today = DateTime.Now.Date;

                if (_lastAggregationDate != today)
                {
                    _dailyAggregation.RunAggregationForAllMissingDays();
                    _lastAggregationDate = today;
                }


                await Task.Delay(1000, stoppingToken);
            }

        }, stoppingToken);

    }


    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("FocusTracker Service stopping.");
        return base.StopAsync(cancellationToken);
    }
}
