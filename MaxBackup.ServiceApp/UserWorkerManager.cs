using MaxBackup.Shared;

namespace MaxBackup.ServiceApp;

public class UserWorkerManager : BackgroundService
{
    private readonly ILogger<UserWorkerManager> _logger;
    private readonly ServiceConfigManager _configManager;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Dictionary<string, UserBackupWorker> _workers = new();
    private readonly SemaphoreSlim _workerLock = new(1, 1);
    private ServiceConfig _config = new();

    public UserWorkerManager(
        ILogger<UserWorkerManager> logger,
        ServiceConfigManager configManager,
        ILoggerFactory loggerFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("UserWorkerManager starting");

        // Load config and start workers
        _config = await _configManager.LoadConfigAsync();
        await StartAllWorkersAsync(stoppingToken);

        // Keep running
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("UserWorkerManager stopping");
        await StopAllWorkersAsync();
        await base.StopAsync(cancellationToken);
    }

    private async Task StartAllWorkersAsync(CancellationToken cancellationToken)
    {
        foreach (var registration in _config.RegisteredUsers)
        {
            await StartWorkerAsync(registration, cancellationToken);
        }
    }

    private async Task StopAllWorkersAsync()
    {
        await _workerLock.WaitAsync();
        try
        {
            var stopTasks = _workers.Values.Select(w => StopWorkerInternalAsync(w)).ToList();
            await Task.WhenAll(stopTasks);
            _workers.Clear();
        }
        finally
        {
            _workerLock.Release();
        }
    }

    private async Task StartWorkerAsync(UserRegistration registration, CancellationToken cancellationToken)
    {
        await _workerLock.WaitAsync(cancellationToken);
        try
        {
            await StartWorkerInternalAsync(registration, cancellationToken);
        }
        finally
        {
            _workerLock.Release();
        }
    }

    /// <summary>
    /// Internal method to start a worker. Caller must already hold _workerLock.
    /// </summary>
    private async Task StartWorkerInternalAsync(UserRegistration registration, CancellationToken cancellationToken)
    {
        if (_workers.ContainsKey(registration.Sid))
        {
            _logger.LogWarning("Worker already running for SID {sid}", registration.Sid);
            return;
        }

        // Resolve user profile path
        var userProfilePath = UserPathResolver.ResolveUserProfilePath(registration.Sid);
        if (userProfilePath == null)
        {
            _logger.LogWarning("Cannot resolve user profile path for SID {sid}, will retry in 1 minute", registration.Sid);
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        await StartWorkerAsync(registration, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception occurred during retry of StartWorkerAsync for SID {sid}", registration.Sid);
                }
            }, cancellationToken);
            return;
        }

        _logger.LogInformation("Starting worker for user {username} (SID: {sid})", registration.Username, registration.Sid);

        var worker = new UserBackupWorker(
            registration,
            userProfilePath,
            _loggerFactory);

        try
        {
            await worker.StartAsync(cancellationToken);
            _workers[registration.Sid] = worker;
            _logger.LogInformation("Worker started for user {username}", registration.Username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start worker for user {username} (SID: {sid})", registration.Username, registration.Sid);
        }
    }

    /// <summary>
    /// Internal method to stop a worker. Caller must already hold _workerLock.
    /// </summary>
    private async Task StopWorkerInternalAsync(UserBackupWorker worker)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.WorkerShutdownTimeoutSeconds));
            await worker.StopAsync(cts.Token);
            _logger.LogInformation("Worker stopped for user {username}", worker.Registration.Username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping worker for user {username}", worker.Registration.Username);
        }
    }

    public async Task<PipeResponse> RegisterUserAsync(string sid, string username, string configPath, CancellationToken cancellationToken)
    {
        await _workerLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogDebug("RegisterUserAsync: acquired lock for {username}", username);
            
            // Check if already registered
            if (_config.RegisteredUsers.Any(u => u.Sid == sid))
            {
                return new PipeResponse
                {
                    Status = "Error",
                    Message = $"User {username} is already registered. Use 'max unregister' first to change configuration."
                };
            }

            // Validate SID can be resolved
            _logger.LogDebug("RegisterUserAsync: resolving user profile path for {sid}", sid);
            var userProfilePath = UserPathResolver.ResolveUserProfilePath(sid);
            if (userProfilePath == null)
            {
                return new PipeResponse
                {
                    Status = "Error",
                    Message = "Cannot resolve user profile path. Please check your user account configuration."
                };
            }
            _logger.LogDebug("RegisterUserAsync: resolved path to {path}", userProfilePath);

            // Create registration
            var registration = new UserRegistration
            {
                Sid = sid,
                Username = username,
                ConfigPath = configPath,
                RegisteredAt = DateTime.UtcNow
            };

            // Save to config
            _logger.LogDebug("RegisterUserAsync: saving config");
            _config.RegisteredUsers.Add(registration);
            await _configManager.SaveConfigAsync(_config);
            _logger.LogDebug("RegisterUserAsync: config saved");

            // Start worker (using internal method since we already hold the lock)
            _logger.LogDebug("RegisterUserAsync: starting worker");
            await StartWorkerInternalAsync(registration, cancellationToken);
            _logger.LogDebug("RegisterUserAsync: worker started");

            return new PipeResponse
            {
                Status = "Success",
                Message = $"User {username} registered successfully. Backup worker started."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering user {username}", username);
            return new PipeResponse
            {
                Status = "Error",
                Message = $"Registration failed: {ex.Message}"
            };
        }
        finally
        {
            _workerLock.Release();
            _logger.LogDebug("RegisterUserAsync: released lock");
        }
    }

    public async Task<PipeResponse> UnregisterUserAsync(string sid, string username, CancellationToken cancellationToken)
    {
        await _workerLock.WaitAsync(cancellationToken);
        try
        {
            var registration = _config.RegisteredUsers.FirstOrDefault(u => u.Sid == sid);
            if (registration == null)
            {
                return new PipeResponse
                {
                    Status = "Error",
                    Message = $"User {username} is not registered. Use 'max register' to register."
                };
            }

            // Stop worker if running (using internal method since we already hold the lock)
            if (_workers.TryGetValue(sid, out var worker))
            {
                await StopWorkerInternalAsync(worker);
                _workers.Remove(sid);
            }

            // Remove from config
            _config.RegisteredUsers.Remove(registration);
            await _configManager.SaveConfigAsync(_config);

            return new PipeResponse
            {
                Status = "Success",
                Message = $"User {username} unregistered successfully. Backup worker stopped."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unregistering user {username}", username);
            return new PipeResponse
            {
                Status = "Error",
                Message = $"Unregistration failed: {ex.Message}"
            };
        }
        finally
        {
            _workerLock.Release();
        }
    }

    public async Task<PipeResponse> GetUserStatusAsync(string sid, string username)
    {
        await _workerLock.WaitAsync();
        try
        {
            var registration = _config.RegisteredUsers.FirstOrDefault(u => u.Sid == sid);
            if (registration == null)
            {
                return new PipeResponse
                {
                    Status = "Info",
                    Message = "Not registered. Use 'max register' to start backing up your files."
                };
            }

            var workerRunning = _workers.ContainsKey(sid);
            var message = $"Registered: Yes\nConfig: {registration.ConfigPath}\nWorker: {(workerRunning ? "Running" : "Stopped")}\nRegistered At: {registration.RegisteredAt:yyyy-MM-dd HH:mm:ss}";

            return new PipeResponse
            {
                Status = "Success",
                Message = message
            };
        }
        finally
        {
            _workerLock.Release();
        }
    }
}
