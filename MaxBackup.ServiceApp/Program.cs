using System.Runtime.Versioning;
using MaxBackup.ServiceApp;
using Microsoft.Extensions.Configuration.Json;
using Serilog;

// windows only
[assembly:SupportedOSPlatform("windows")]

// Define paths using environment variables
var programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
var maxBackupDataPath = Path.Combine(programDataPath, "MaxBackup");
var maxBackupLogsPath = Path.Combine(maxBackupDataPath, "logs");
var serviceLogPath = Path.Combine(maxBackupLogsPath, "service.log");
var configPath = Path.Combine(maxBackupDataPath, "config.json");

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

        // Service-level logging to %ProgramData%\MaxBackup\logs\service.log
        var logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.Console(restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information)
            .WriteTo.File(
                serviceLogPath,
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
Directory.CreateDirectory(maxBackupLogsPath);

logger.LogInformation("MaxBackup Service starting");
logger.LogInformation("Environment: {env}", env.EnvironmentName);
logger.LogInformation("Service config location: {configPath}", configPath);
logger.LogInformation("Pipe name: \\\\.\\pipe\\MaxBackupPipe");

await host.RunAsync();
