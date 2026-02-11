using FocusTracker.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FocusTracker.Service;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    private AppTracker? _tracker;
    private EventLogger? _eventLogger;
    private Database? _database;
    private FocusModeService? _focusMode;
    private FocusSessionTracker? _focusTracker;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FocusTracker Service started.");
        SQLitePCL.Batteries.Init();


        _database = new Database();
        _eventLogger = new EventLogger(_database);
        _tracker = new AppTracker();

        _tracker.AppChanged += app =>
        {
            _eventLogger.OnAppChanged(app);
        };

        _tracker.IdleStarted += () =>
        {
            _eventLogger.OnIdleStarted();
        };

        _tracker.IdleEnded += () =>
        {
            _eventLogger.OnIdleEnded();
        };

        _tracker.Start();

//      Focus Mode
        
        _focusMode = new FocusModeService();
        _focusTracker = new FocusSessionTracker();

        _focusMode.FocusStartedWithDuration += d =>
            _focusTracker.OnFocusStarted(d);
        _focusMode.FocusEndedWithResult += completed =>
            _focusTracker.OnFocusEnded(completed);

        var focusTimer = new System.Timers.Timer(1000);
        focusTimer.Elapsed += (s, e) => _focusMode?.Tick();
        focusTimer.Start();
        _focusMode.Start(TimeSpan.FromSeconds(10));

        //      Daily Summary Timer
        var dailyTimer = new System.Timers.Timer(
            TimeSpan.FromMilliseconds(10000).TotalMilliseconds);

        dailyTimer.Elapsed += (s, e) =>
        {
            try
            {
                var analytics = new AnalyticsService();
                var today = analytics.GetTodaySummary();

                var frag = new FragmentationAnalyzer().Analyze();
                int fragScore = frag.HasData ? frag.Score : 0;

                new DailySummaryService().UpdateToday(
                    today.FocusMinutes,
                    today.FocusSessions,
                    fragScore
                );

                _logger.LogInformation("Daily summary updated.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating daily summary.");
            }
        };

        dailyTimer.Start();


        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("FocusTracker Service stopping.");
        return base.StopAsync(cancellationToken);
    }
}
