using System.Runtime.Versioning;
using MaxBackup.ServiceApp;
using Microsoft.Extensions.Configuration.Json;
using Serilog;

// windows only
[assembly:SupportedOSPlatform("windows")]

IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options => {
        options.ServiceName = "MaxBackup";
    })
    .ConfigureHostConfiguration(config => {
        config.AddEnvironmentVariables("MAXBACKUP_");
    })
    .ConfigureServices(services => {
        // Register service-level components
        services.AddSingleton<ServiceConfigManager>();
        services.AddSingleton<UserWorkerManager>();
        services.AddHostedService(sp => sp.GetRequiredService<UserWorkerManager>());
        services.AddHostedService<PipeServer>();
    })
    .ConfigureLogging((context, logging) => {
        logging.ClearProviders();

        // Service-level logging to C:\ProgramData\MaxBackup\logs\service.log
        var logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.Console(restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information)
            .WriteTo.File(
                @"C:\ProgramData\MaxBackup\logs\service.log",
                rollingInterval: RollingInterval.Day,
                restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information)
            .CreateLogger()
        ;

        logging.AddSerilog(logger, true);
    })
    .Build();

var logger = host.Services.GetRequiredService<ILogger<IHost>>();

var env = host.Services.GetRequiredService<IHostEnvironment>();

// Ensure log directory exists
Directory.CreateDirectory(@"C:\ProgramData\MaxBackup\logs");

logger.LogInformation("MaxBackup Service starting");
logger.LogInformation("Environment: {env}", env.EnvironmentName);
logger.LogInformation("Service config location: C:\\ProgramData\\MaxBackup\\config.json");
logger.LogInformation("Pipe name: \\\\.\\pipe\\MaxBackupPipe");

await host.RunAsync();
