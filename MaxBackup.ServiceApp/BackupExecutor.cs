using System.Diagnostics;
using Microsoft.Extensions.FileSystemGlobbing;

namespace MaxBackup.ServiceApp;

public static class BackupExecutor
{
    public static async Task RunBackupJobAsync(
        BackupJobConfig config,
        string userProfilePath,
        ILogger? logger,
        CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();

        using (logger?.BeginScope(config.GetScope()))
        {
            logger?.LogInformation("Running job {name}", config.Name);

            // Expand paths using user profile
            var source = UserPathResolver.ExpandUserPath(config.Source, userProfilePath);
            var destination = UserPathResolver.ExpandUserPath(config.Destination, userProfilePath);

            if (!Directory.Exists(source))
            {
                logger?.LogWarning("Can't run job {name} because the source {source} doesn't exist.", config.Name, source);
                return;
            }

            if (!Directory.Exists(destination))
            {
                logger?.LogInformation("Creating destination path {destination} for config {config}", destination, config.Name);

                try
                {
                    Directory.CreateDirectory(destination);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Could not create destination path {destination} for config {config}", destination, config.Name);
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

            // Add default excludes
            var drive = Path.GetPathRoot(source);
            if (drive != null && drive.Length == 3 && drive.EndsWith(":\\", StringComparison.OrdinalIgnoreCase))
            {
                matcher.AddExclude("$Recycle.Bin");
                matcher.AddExclude("System Volume Information");
                matcher.AddExclude("*~");
            }

            logger?.LogInformation("Scanning for files {path}", source);

            HashSet<string> sources = new HashSet<string>();
            var tcs = new TaskCompletionSource();

            using (stoppingToken.Register(() => tcs.TrySetCanceled(stoppingToken)))
            {
                var matchTask = Task.Run(() => matcher.GetResultsInFullPath(source), stoppingToken);

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
                    logger?.LogError(ex, "Error scanning files for job {name}. Skipping job.", config.Name);
                    return;
                }

                if (results == null)
                {
                    logger?.LogWarning("Null results from matcher");
                    return;
                }

                // Do some post filtering of paths
                foreach (var sourceFile in results)
                {
                    stoppingToken.ThrowIfCancellationRequested();

                    // Check if the path is a system file like ".849C9593-D756-4E56-8D6E-42412F2A707B"
                    var filename = Path.GetFileName(sourceFile);

                    if (string.IsNullOrWhiteSpace(filename))
                    {
                        sources.Add(sourceFile);
                        continue;
                    }

                    if (filename[0] == '.' && (filename.Length == 37 || filename.Length == 33))
                    {
                        // check if the filename is a guid
                        var guidString = filename.Substring(1);
                        if (Guid.TryParse(guidString, out var guid))
                        {
                            // Check if the file has the System attribute set
                            try
                            {
                                var attributes = File.GetAttributes(sourceFile);
                                if ((attributes & FileAttributes.System) == FileAttributes.System)
                                {
                                    logger?.LogDebug("Skipping system file {file}", sourceFile);
                                    continue;
                                }
                            }
                            catch
                            {
                                // If we can't read attributes, include the file
                            }
                        }
                    }

                    sources.Add(sourceFile);
                }
            }

            logger?.LogInformation("Backing up files to {path}", destination);

            var sourcesArray = sources.ToArray();

            await Task.Run(() => BackupFiles(sourcesArray, source, destination, logger, stoppingToken), stoppingToken);
        }
    }

    private static void BackupFiles(
        string[] sources,
        string sourceRoot,
        string destinationRoot,
        ILogger? logger,
        CancellationToken stoppingToken)
    {
        logger?.LogInformation("Backing up {fileCount} files in {sourceRoot} to {destinationRoot}", sources.Length, sourceRoot, destinationRoot);

        if (sources.Length == 0) return;

        var backupCount = 0;
        var upToDateCount = 0;
        var errorCount = 0;
        var missingCount = 0;
        var backupByteCount = 0L;
        var stopWatch = new Stopwatch();
        stopWatch.Start();
        var nextSleepTime = DateTime.Now.AddMilliseconds(500);
        var sleepTime = TimeSpan.FromMilliseconds(10);
        var nextReportTime = DateTime.Now.AddSeconds(30);

        foreach (var source in sources)
        {
            stoppingToken.ThrowIfCancellationRequested();

            if (DateTime.Now >= nextSleepTime)
            {
                Thread.Sleep(sleepTime);
                stoppingToken.ThrowIfCancellationRequested();
                nextSleepTime = DateTime.Now.AddMilliseconds(500);
            }

            if (DateTime.Now >= nextReportTime)
            {
                var processed = backupCount + upToDateCount + errorCount + missingCount;
                var percent = (processed / (double)sources.Length) * 100;
                logger?.LogInformation("Backing up...{percent:0.00}% {processed}/{total}", percent, processed, sources.Length);
                nextReportTime = DateTime.Now.AddSeconds(30);
            }

            var relativePath = Path.GetRelativePath(sourceRoot, source);
            var destination = Path.Combine(destinationRoot, relativePath);

            logger?.LogDebug("Backing up file {file} to {destination}", source, destination);

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
                logger?.LogError(ex, "Could not create directory {dir}", dir);
                errorCount++;
                continue;
            }

            try
            {
                if (File.Exists(destination))
                {
                    var fi = new FileInfo(destination);
                    fi.Attributes &= ~FileAttributes.Hidden;
                    fi.Attributes &= ~FileAttributes.ReadOnly;

                    var sourceWriteDate = File.GetLastWriteTimeUtc(source);
                    var destWriteDate = fi.LastWriteTimeUtc;

                    if (sourceWriteDate == destWriteDate)
                    {
                        logger?.LogDebug("Destination file already up to date. Skipping. {file}", destination);
                        upToDateCount++;
                        continue;
                    }
                }

                File.Copy(source, destination, true);

                backupCount++;

                {
                    var fi = new FileInfo(destination);
                    backupByteCount += fi.Length;
                }
            }
            catch (FileNotFoundException ex)
            {
                logger?.LogDebug(ex, "File not found {file}", source);
                missingCount++;
                continue;
            }
            catch (DirectoryNotFoundException ex)
            {
                logger?.LogDebug(ex, ex.Message);
                missingCount++;
                continue;
            }
            catch (IOException ex) when (ex.HResult == -2147024864)
            {
                // file in use by another process
                logger?.LogWarning(ex.Message);
                errorCount++;
                continue;
            }
            catch (UnauthorizedAccessException ex)
            {
                logger?.LogWarning(ex.Message);
                errorCount++;
                continue;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Could not back up {file} to {destination}", source, destination);
                errorCount++;
                continue;
            }

            try
            {
                var createTime = File.GetCreationTime(source);
                File.SetCreationTime(destination, createTime);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Could not update creation time for {file}", destination);
            }

            try
            {
                var writeTime = File.GetLastWriteTime(source);
                File.SetLastWriteTime(destination, writeTime);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Could not update write time for {file}", destination);
            }
        }

        stopWatch.Stop();

        var elapsedLogText = stopWatch.Elapsed.ToString("g");

        string backupSize;
        if (backupByteCount < 1024)
        {
            backupSize = $"{backupByteCount} bytes";
        }
        else if (backupByteCount < 1024 * 1024)
        {
            backupSize = $"{backupByteCount / 1024} KB";
        }
        else if (backupByteCount < 1024 * 1024 * 1024)
        {
            backupSize = $"{backupByteCount / (1024 * 1024)} MB";
        }
        else
        {
            backupSize = $"{backupByteCount / (1024 * 1024 * 1024)} GB";
        }

        if (sources.Length == upToDateCount)
        {
            logger?.LogInformation("All files already up to date. {fileCount} up to date files skipped. {elapsed}", upToDateCount, elapsedLogText);
        }
        else
        {
            logger?.LogInformation("Backed up {backupCount}/{totalCount} ({backupSize}) files in {elapsed}", backupCount, sources.Length, backupSize, elapsedLogText);

            if (upToDateCount > 0)
            {
                logger?.LogInformation("Skipped {upToDateCount}/{totalCount} files that were already up to date", upToDateCount, sources.Length);
            }
        }

        if (errorCount > 0)
        {
            logger?.LogWarning("Skipped {errorCount}/{totalCount} due to errors", errorCount, sources.Length);
        }
        if (missingCount > 0)
        {
            logger?.LogWarning("Skipped {missingCount}/{totalCount} that disappeared during the backup", missingCount, sources.Length);
        }
    }
}
