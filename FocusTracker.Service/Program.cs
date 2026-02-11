using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FocusTracker.Service;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // Detect if running as Windows Service
        if (!Environment.UserInteractive)
        {
            builder.Services.AddWindowsService(options =>
            {
                options.ServiceName = "FocusTrackerService";
            });
        }

        // Logging configuration
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();

        builder.Services.AddHostedService<Worker>();

        var host = builder.Build();
        host.Run();
    }
}
