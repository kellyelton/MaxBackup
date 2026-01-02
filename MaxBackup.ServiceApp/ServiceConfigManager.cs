using System.Text.Json;

namespace MaxBackup.ServiceApp;

public class ServiceConfigManager
{
    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), 
        "MaxBackup");
    private static readonly string ConfigFile = Path.Combine(ConfigDirectory, "config.json");
    private const int LockTimeoutSeconds = 15;
    private readonly ILogger<ServiceConfigManager> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public ServiceConfigManager(ILogger<ServiceConfigManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        EnsureConfigDirectoryExists();
    }

    private void EnsureConfigDirectoryExists()
    {
        if (!Directory.Exists(ConfigDirectory))
        {
            Directory.CreateDirectory(ConfigDirectory);
            _logger.LogInformation("Created config directory at {path}", ConfigDirectory);
        }
    }

    public async Task<ServiceConfig> LoadConfigAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (!File.Exists(ConfigFile))
            {
                _logger.LogInformation("Config file not found, creating new config");
                var newConfig = new ServiceConfig();
                // Call internal save method to avoid deadlock (we already hold the semaphore)
                await SaveConfigInternalAsync(newConfig);
                return newConfig;
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(LockTimeoutSeconds));
            var startTime = DateTime.UtcNow;
            var delay = 100;

            while (true)
            {
                try
                {
                    using var stream = new FileStream(ConfigFile, FileMode.Open, FileAccess.Read, FileShare.None);
                    using var reader = new StreamReader(stream);
                    var json = await reader.ReadToEndAsync(cts.Token);
                    var config = JsonSerializer.Deserialize<ServiceConfig>(json) ?? new ServiceConfig();
                    _logger.LogDebug("Loaded config with {count} registered users", config.RegisteredUsers.Count);
                    return config;
                }
                catch (IOException) when (!cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(delay, cts.Token);
                    delay = Math.Min(delay * 2, 1000); // Exponential backoff, max 1 second
                }
                catch (OperationCanceledException)
                {
                    var elapsed = DateTime.UtcNow - startTime;
                    _logger.LogError("Config file locked after {seconds} seconds", elapsed.TotalSeconds);
                    throw new TimeoutException("Config file locked. Try again shortly, restart service, or check service logs.");
                }
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SaveConfigAsync(ServiceConfig config)
    {
        await _semaphore.WaitAsync();
        try
        {
            await SaveConfigInternalAsync(config);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Internal save method that doesn't acquire the semaphore.
    /// Used by LoadConfigAsync when it needs to create a new config while already holding the lock.
    /// </summary>
    private async Task SaveConfigInternalAsync(ServiceConfig config)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(LockTimeoutSeconds));
        var startTime = DateTime.UtcNow;
        var delay = 100;

        while (true)
        {
            try
            {
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                using var stream = new FileStream(ConfigFile, FileMode.Create, FileAccess.Write, FileShare.None);
                using var writer = new StreamWriter(stream);
                await writer.WriteAsync(json);
                await writer.FlushAsync();
                _logger.LogDebug("Saved config with {count} registered users", config.RegisteredUsers.Count);
                return;
            }
            catch (IOException) when (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(delay, cts.Token);
                delay = Math.Min(delay * 2, 1000);
            }
            catch (OperationCanceledException)
            {
                var elapsed = DateTime.UtcNow - startTime;
                _logger.LogError("Config file locked after {seconds} seconds during save", elapsed.TotalSeconds);
                throw new TimeoutException("Config file locked. Try again shortly, restart service, or check service logs.");
            }
        }
    }
}
