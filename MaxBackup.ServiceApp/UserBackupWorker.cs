using Microsoft.Extensions.Options;
using Serilog;

namespace MaxBackup.ServiceApp;

public class UserBackupWorker
{
    private readonly UserRegistration _registration;
    private readonly string _userProfilePath;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<UserBackupWorker> _logger;
    private IHost? _host;
    private CancellationTokenSource? _cts;

    public UserRegistration Registration => _registration;

    public UserBackupWorker(
        UserRegistration registration,
        string userProfilePath,
        ILoggerFactory loggerFactory)
    {
        _registration = registration ?? throw new ArgumentNullException(nameof(registration));
        _userProfilePath = userProfilePath ?? throw new ArgumentNullException(nameof(userProfilePath));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<UserBackupWorker>();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = new CancellationTokenSource();

        // Capture these for use in lambdas
        var configPath = _registration.ConfigPath;
        var userProfilePath = _userProfilePath;
        var serviceLogger = _logger;

        _logger.LogInformation("Starting user backup worker for {Username} ({Sid}), config: {ConfigPath}, profile: {ProfilePath}",
            _registration.Username, _registration.Sid, configPath, userProfilePath);

        // Build a mini host for this user
        var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                // Clear default sources
                config.Sources.Clear();

                // Add user config with path expansion
                config.Add(new UserPathExpandingConfigurationSource(configPath, userProfilePath));
            })
            .ConfigureServices((context, services) =>
            {
                // Bind backup config
                services
                    .AddOptions<BackupConfig>()
                    .BindConfiguration("Backup")
                    .ValidateDataAnnotations();

                // Add the backup executor as a hosted service
                services.AddSingleton(sp => userProfilePath);
                services.AddHostedService<BackupExecutorService>();
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();

                // Mandatory log file path - always exists at predictable location
                var mandatoryLogDir = Path.Combine(userProfilePath, ".max", "logs");
                var mandatoryLogPath = Path.Combine(mandatoryLogDir, "backup.log");
                
                // Ensure the directory exists
                if (!Directory.Exists(mandatoryLogDir))
                {
                    Directory.CreateDirectory(mandatoryLogDir);
                }

                // Start with mandatory file sink
                var loggerConfig = new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .Enrich.FromLogContext()
                    .WriteTo.File(
                        mandatoryLogPath,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}");

                // Add any additional sinks from user config (Seq, extra files, etc.)
                var serilogSection = context.Configuration.GetSection("Serilog");
                if (serilogSection.Exists())
                {
                    try
                    {
                        // Ensure log directories exist for any user-configured file sinks
                        var writeTo = serilogSection.GetSection("WriteTo");
                        foreach (var sink in writeTo.GetChildren())
                        {
                            var name = sink.GetValue<string>("Name");
                            if (name?.Equals("File", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                var path = sink.GetSection("Args").GetValue<string>("path");
                                if (!string.IsNullOrEmpty(path))
                                {
                                    serviceLogger.LogDebug("User configured additional file sink: {Path}", path);
                                    UserPathResolver.EnsureDirectoryExists(path);
                                }
                            }
                        }

                        // Layer user config on top of mandatory config
                        loggerConfig = loggerConfig.ReadFrom.Configuration(context.Configuration);
                        serviceLogger.LogDebug("Additional Serilog sinks configured from user config");
                    }
                    catch (Exception ex)
                    {
                        serviceLogger.LogWarning(ex, "Failed to configure additional Serilog sinks from user config, using mandatory file sink only");
                    }
                }

                var logger = loggerConfig.CreateLogger();
                logging.AddSerilog(logger, dispose: true);
                
                serviceLogger.LogInformation("User worker logging configured. Mandatory log: {LogPath}", mandatoryLogPath);
            });

        _host = hostBuilder.Build();

        // Start the host in background
        _ = Task.Run(async () =>
        {
            try
            {
                await _host.RunAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in user backup worker for {Username} ({Sid})", _registration.Username, _registration.Sid);
            }
        }, cancellationToken);

        // Give it a moment to start
        await Task.Delay(500, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }

        if (_host != null)
        {
            try
            {
                await _host.StopAsync(cancellationToken);
                _host.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping user backup worker for {Username} ({Sid})", _registration.Username, _registration.Sid);
            }
        }

        _cts?.Dispose();
    }
}

/// <summary>
/// Hosted service that runs the backup loop for a single user
/// </summary>
internal class BackupExecutorService : BackgroundService
{
    private readonly ILogger<BackupExecutorService> _logger;
    private readonly IOptionsMonitor<BackupConfig> _backupConfigProvider;
    private readonly string _userProfilePath;

    public BackupExecutorService(
        ILogger<BackupExecutorService> logger,
        IOptionsMonitor<BackupConfig> backupConfigProvider,
        string userProfilePath)
    {
        _logger = logger;
        _backupConfigProvider = backupConfigProvider;
        _userProfilePath = userProfilePath;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger?.LogInformation("Starting backup worker for user profile: {UserProfilePath}", _userProfilePath);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var config = _backupConfigProvider.CurrentValue;

                foreach (var job in config.Jobs)
                {
                    if (stoppingToken.IsCancellationRequested)
                        break;

                    await BackupExecutor.RunBackupJobAsync(
                        job,
                        _userProfilePath,
                        _logger,
                        stoppingToken);
                }

                await Task.Delay(10000, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in backup loop");
                await Task.Delay(60000, stoppingToken); // Wait longer on error
            }
        }

        _logger?.LogInformation("Backup worker stopped");
    }
}
