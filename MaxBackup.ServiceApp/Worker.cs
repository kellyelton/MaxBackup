using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Options;

namespace MaxBackup.ServiceApp
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IOptionsMonitor<BackupConfig> _backupConfigProvider;

        public Worker(ILogger<Worker> logger, IOptionsMonitor<BackupConfig> backupConfigProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _backupConfigProvider = backupConfigProvider ?? throw new ArgumentNullException(nameof(backupConfigProvider));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting worker");

            while (!stoppingToken.IsCancellationRequested)
            {
                var config = _backupConfigProvider.CurrentValue;

                foreach (var job in config.Jobs)
                {
                    stoppingToken.ThrowIfCancellationRequested();

                    await RunBackupJob(job, stoppingToken).ConfigureAwait(false);
                }

                await Task.Delay(10000, stoppingToken).ConfigureAwait(false);
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogWarning("Shutting down");

            return Task.CompletedTask;
        }

        private async Task RunBackupJob(BackupJobConfig config, CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            using (_logger.BeginScope(config.GetScope()))
            {
                _logger.LogInformation("Running job {name}", config.Name);

                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                config.Source = config.Source.Replace("~", userProfile);
                config.Source = Environment.ExpandEnvironmentVariables(config.Source);

                config.Destination = config.Destination.Replace("~", userProfile);
                config.Destination = Environment.ExpandEnvironmentVariables(config.Destination);

                if (!Directory.Exists(config.Source))
                {
                    _logger.LogWarning("Can't run job {name} because the source {source} doesn't exist.", config.Name, config.Source);

                    return;
                }

                if (!Directory.Exists(config.Destination))
                {
                    _logger.LogInformation("Creating destination path {destination} for config {config}", config.Destination, config.Name);

                    try
                    {
                        Directory.CreateDirectory(config.Destination);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Could not create destination path {destination} for config {config}", config.Destination, config.Name);

                        return;
                    }
                }

                var matcher = new Matcher();
                foreach (var include in config.Include)
                {
                    matcher.AddInclude(include);
                }
                foreach (var exclude in config.Exclude)
                {
                    matcher.AddExclude(exclude);
                }

                _logger.LogInformation("Scanning for files {path}", config.Source);

                string[] sources;
                var tcs = new TaskCompletionSource();

                using (stoppingToken.Register(() => tcs.TrySetCanceled(stoppingToken)))
                {
                    var matchTask = Task.Run(() => matcher.GetResultsInFullPath(config.Source), stoppingToken);

                    var completedTask = await Task.WhenAny(tcs.Task, matchTask);

                    if (completedTask == tcs.Task)
                    {
                        throw new OperationCanceledException(stoppingToken);
                    }
                    else
                    {
                        IEnumerable<string> results;

                        try
                        {
                            results = await matchTask;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error scanning files for job {name}. Skipping job.", config.Name);

                            return;
                        }

                        if (results == null)
                        {
                            _logger.LogWarning("Null results from matcher");

                            return;
                        }

                        sources = results.ToArray();
                    }
                }

                _logger.LogInformation("Backing up files to {path}", config.Destination);

                await Task.Run(() => BackupFiles(sources, config.Source, config.Destination, stoppingToken), stoppingToken);
            }
        }

        private void BackupFiles(string[] sources, string sourceRoot, string destinationRoot, CancellationToken stoppingToken)
        {
            foreach (var source in sources)
            {
                stoppingToken.ThrowIfCancellationRequested();

                var relativePath = Path.GetRelativePath(sourceRoot, source);

                var destination = Path.Combine(destinationRoot, relativePath);

                _logger.LogDebug("Backing up file {file} to {destination}", source, destination);

                var dir = Path.GetDirectoryName(destination);
                try
                {
                    if (dir == null) throw new InvalidOperationException("Parent directory returned null");

                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not create directory {dir}", dir);

                    continue;
                }

                try
                {
                    if (File.Exists(destination))
                    {
                        var fi = new FileInfo(destination);
                        fi.Attributes &= ~FileAttributes.Hidden;
                        fi.Attributes &= ~FileAttributes.ReadOnly;

                        var existingWriteDate = File.GetLastWriteTimeUtc(source);
                        var destWriteDate = fi.LastWriteTimeUtc;

                        if (existingWriteDate == destWriteDate)
                        {
                            _logger.LogDebug("Destination file already up to date. Skipping. {file}", destination);

                            continue;
                        }
                    }

                    File.Copy(source, destination, true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not back up {file} to {destination}", source, destination);

                    continue;
                }

                try
                {
                    var createTime = File.GetCreationTime(source);
                    File.SetCreationTime(destination, createTime);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not update creation time for {file}", destination);
                }

                try
                {
                    var writeTime = File.GetLastWriteTime(source);
                    File.SetLastWriteTime(destination, writeTime);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not update write time for {file}", destination);
                }
            }
        }
    }
}
