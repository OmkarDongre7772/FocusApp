using FocusTracker.Core;
using FocusTracker.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Windows Service Support
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "FocusTrackerService";
});

// Logging (Console + File via built-in provider)
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Configuration binding
builder.Services.Configure<FragmentationConfig>(
    builder.Configuration.GetSection("Fragmentation"));

builder.Services.Configure<SupabaseOptions>(
    builder.Configuration.GetSection("Supabase"));

// Hosted service
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

host.Run();
