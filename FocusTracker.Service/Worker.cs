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
    private IpcServer? _ipcServer;
    private NotificationPolicy? _notificationPolicy;


    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FocusTracker Service started.");

        SQLitePCL.Batteries.Init();

        _database = new Database();
        _eventLogger = new EventLogger(_database);
        _tracker = new AppTracker();

        _focusMode = new FocusModeService();
        _focusTracker = new FocusSessionTracker();
        _notificationPolicy = new NotificationPolicy();

        _focusMode.FocusStartedWithDuration += d =>
            _focusTracker.OnFocusStarted(d);

        _focusMode.FocusEndedWithResult += completed =>
            _focusTracker.OnFocusEnded(completed);

        _tracker.AppChanged += app => _eventLogger.OnAppChanged(app);
        _tracker.IdleStarted += () => _eventLogger.OnIdleStarted();
        _tracker.IdleEnded += () => _eventLogger.OnIdleEnded();

        _tracker.Start();

        _ipcServer = new IpcServer(_focusMode, _notificationPolicy);
        _ = _ipcServer.StartAsync(stoppingToken);


        // Keep service alive properly
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }


    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("FocusTracker Service stopping.");
        return base.StopAsync(cancellationToken);
    }
}
