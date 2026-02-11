using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;

namespace FocusTracker.Service;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // Always support both modes
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "FocusTrackerService";
        });

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();

        builder.Services.AddHostedService<Worker>();

        var host = builder.Build();
        host.Run();
    }
}
