using System.Diagnostics;
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

                { // Add default excludes
                    // check if the source is the root of a drive
                    var drive = Path.GetPathRoot(config.Source);
                    if (drive != null && drive.Length == 3 && drive.EndsWith(":\\", StringComparison.OrdinalIgnoreCase))
                    {
                        matcher.AddExclude("$Recycle.Bin");
                        matcher.AddExclude("System Volume Information");
                        matcher.AddExclude("*~");
                    }
                }

                _logger.LogInformation("Scanning for files {path}", config.Source);

                HashSet<string> sources = new HashSet<string>();
                var tcs = new TaskCompletionSource();

                using (stoppingToken.Register(() => tcs.TrySetCanceled(stoppingToken)))
                {
                    var matchTask = Task.Run(() => matcher.GetResultsInFullPath(config.Source), stoppingToken);

                    var completedTask = await Task.WhenAny(tcs.Task, matchTask);

                    if (completedTask == tcs.Task)
                    {
                        throw new OperationCanceledException(stoppingToken);
                    }

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

                    // Do some post filtering of paths
                    foreach (var source in results)
                    {
                        // Check if the path is a system file like ".849C9593-D756-4E56-8D6E-42412F2A707B"
                        var filename = System.IO.Path.GetFileName(source);

                        if (string.IsNullOrWhiteSpace(filename))
                        {
                            sources.Add(source);
                            continue;
                        }

                        if (filename[0] == '.' && (filename.Length == 37 || filename.Length == 33))
                        {
                            // check if the filename is a guid
                            var guidString = filename.Substring(1);
                            if (Guid.TryParse(guidString, out var guid))
                            {
                                // Check if the file has the System attribute set
                                var attributes = File.GetAttributes(source);
                                if ((attributes & FileAttributes.System) == FileAttributes.System)
                                {
                                    _logger.LogDebug("Skipping system file {file}", source);

                                    continue;
                                }
                            }
                        }

                        sources.Add(source);
                    }
                }

                _logger.LogInformation("Backing up files to {path}", config.Destination);

                var sourcesArray = sources.ToArray();

                await Task.Run(() => BackupFiles(sourcesArray, config.Source, config.Destination, stoppingToken), stoppingToken);
            }
        }

        private void BackupFiles(string[] sources, string sourceRoot, string destinationRoot, CancellationToken stoppingToken) {
            _logger.LogInformation("Backing up {fileCount} files in {sourceRoot} to {destinationRoot}", sources.Length, sourceRoot, destinationRoot);

            if (sources.Length == 0) return;

            var backupCount = 0;
            var upToDateCount = 0;
            var errorCount = 0;
            var missingCount = 0;
            var backupByteCount = 0;
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var lastSleepTime = DateTime.Now;
            var nextSleepTime = DateTime.Now.AddMilliseconds(500);
            var sleepTime = TimeSpan.FromMilliseconds(10);
            var lastReportTime = DateTime.Now;
            var nextReportTime = DateTime.Now.AddSeconds(30);
            foreach (var source in sources) {
                stoppingToken.ThrowIfCancellationRequested();

                if (DateTime.Now >= nextSleepTime) {
                    Thread.Sleep(sleepTime);

                    stoppingToken.ThrowIfCancellationRequested();

                    lastSleepTime = DateTime.Now;
                    nextSleepTime = DateTime.Now.AddMilliseconds(500);
                }

                if (DateTime.Now >= nextReportTime) {
                    var processed = backupCount + upToDateCount + errorCount + missingCount;
                    var percent = (processed / (double)sources.Length) * 100;
                    _logger.LogInformation("Backing up...{percent:0.00}% {processed}/{total}", percent, processed, sources.Length);

                    lastReportTime = DateTime.Now;
                    nextReportTime = DateTime.Now.AddSeconds(30);
                }

                var relativePath = Path.GetRelativePath(sourceRoot, source);

                var destination = Path.Combine(destinationRoot, relativePath);

                _logger.LogDebug("Backing up file {file} to {destination}", source, destination);

                var dir = Path.GetDirectoryName(destination);
                try {
                    if (dir == null) throw new InvalidOperationException("Parent directory returned null");

                    if (!Directory.Exists(dir)) {
                        Directory.CreateDirectory(dir);
                    }
                } catch (Exception ex) {
                    _logger.LogError(ex, "Could not create directory {dir}", dir);

                    errorCount++;

                    continue;
                }

                try {
                    if (File.Exists(destination)) {
                        var fi = new FileInfo(destination);
                        fi.Attributes &= ~FileAttributes.Hidden;
                        fi.Attributes &= ~FileAttributes.ReadOnly;

                        var existingWriteDate = File.GetLastWriteTimeUtc(source);
                        var destWriteDate = fi.LastWriteTimeUtc;

                        if (existingWriteDate == destWriteDate) {
                            _logger.LogDebug("Destination file already up to date. Skipping. {file}", destination);

                            upToDateCount++;

                            continue;
                        }
                    }

                    File.Copy(source, destination, true);

                    backupCount++;

                    {
                        var fi = new FileInfo(destination);
                        backupByteCount += (int)fi.Length;
                    }
                } catch (FileNotFoundException ex) {
                    _logger.LogDebug(ex, "File not found {file}", source);

                    missingCount++;

                    continue;
                } catch (DirectoryNotFoundException ex) {
                    _logger.LogDebug(ex, ex.Message);

                    missingCount++;

                    continue;
                } catch (IOException ex) when (ex.HResult == -2147024864) {
                    // file in use by another process
                    _logger.LogWarning(ex.Message);

                    errorCount++;

                    continue;
                } catch (UnauthorizedAccessException ex) {
                    _logger.LogWarning(ex.Message);

                    errorCount++;

                    continue;
                } catch (Exception ex) {
                    _logger.LogError(ex, "Could not back up {file} to {destination}", source, destination);

                    errorCount++;

                    continue;
                }

                try {
                    var createTime = File.GetCreationTime(source);
                    File.SetCreationTime(destination, createTime);
                } catch (Exception ex) {
                    _logger.LogError(ex, "Could not update creation time for {file}", destination);
                }

                try {
                    var writeTime = File.GetLastWriteTime(source);
                    File.SetLastWriteTime(destination, writeTime);
                } catch (Exception ex) {
                    _logger.LogError(ex, "Could not update write time for {file}", destination);
                }
            }

            stopWatch.Stop();

            var elapsedLogText = stopWatch.Elapsed.ToString("g");

            string backupSize;
            if (backupByteCount < 1024) {
                backupSize = $"{backupByteCount} bytes";
            } else if (backupByteCount < 1024 * 1024) {
                backupSize = $"{backupByteCount / 1024} KB";
            } else if (backupByteCount < 1024 * 1024 * 1024) {
                backupSize = $"{backupByteCount / (1024 * 1024)} MB";
            } else {
                backupSize = $"{backupByteCount / (1024 * 1024 * 1024)} GB";
            }

            if (sources.Length == upToDateCount) {
                _logger.LogInformation("All files already up to date. {fileCount} up to date files skipped. {elapsed}", upToDateCount, elapsedLogText);
            } else {
                _logger.LogInformation("Backed up {backupCount}/{totalCount} ({backupSize}) files in {elapsed}", backupCount, sources.Length, backupSize, elapsedLogText);

                if (upToDateCount > 0) {
                    _logger.LogInformation("Skipped {upToDateCount}/{totalCount} files that were already up to date", upToDateCount, sources.Length);
                }
            }

            if (errorCount > 0) {
                _logger.LogWarning("Skipped {errorCount}/{totalCount} due to errors", errorCount, sources.Length);
            }
            if (missingCount > 0) {
                _logger.LogWarning("Skipped {missingCount}/{totalCount} that disappeared during the backup", missingCount, sources.Length);
            }
        }
    }
}
