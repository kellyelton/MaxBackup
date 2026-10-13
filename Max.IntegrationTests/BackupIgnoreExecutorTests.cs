using System.Reflection;
using MaxBackup.ServiceApp;
using MaxBackup.ServiceApp.Providers;
using Microsoft.Extensions.Logging;

namespace Max.IntegrationTests;

public class BackupIgnoreExecutorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "MaxBackupIgnore-" + Guid.NewGuid());
    private string Source => Path.Combine(root, "source");
    private string Destination => Path.Combine(root, "destination");
    private BackupJobConfig Job => new() { Name = "ignore-test", Source = Source, Destination = Destination, Include = ["**/*"] };

    public BackupIgnoreExecutorTests() => Directory.CreateDirectory(Source);

    [Fact]
    public async Task LocalBackupCopiesSelectedFilesAndRetainsNewlyIgnoredDestination()
    {
        Write("keep.txt", "keep");
        Write("cache/ignored.txt", "ignored");
        await BackupExecutor.RunBackupJobAsync(Job, root, null, CancellationToken.None);
        Write(".maxbackupignore", "cache/\n");
        Write("cache/ignored.txt", "changed");
        Write("cache/new.txt", "new");

        await BackupExecutor.RunBackupJobAsync(Job, root, null, CancellationToken.None);

        Assert.Equal("keep", File.ReadAllText(Path.Combine(Destination, "keep.txt")));
        Assert.Equal("ignored", File.ReadAllText(Path.Combine(Destination, "cache", "ignored.txt")));
        Assert.False(File.Exists(Path.Combine(Destination, "cache", "new.txt")));
        Assert.True(File.Exists(Path.Combine(Destination, ".maxbackupignore")));
    }

    [Fact]
    public async Task WithoutIgnoreFilesExistingIncludeAndExcludeSelectionIsPreserved()
    {
        Write("keep.txt", "keep");
        Write("excluded.txt", "excluded");
        Write("other.bin", "other");
        var job = Job;
        job.Include = ["**/*.txt"];
        job.Exclude = ["excluded.txt"];
        await BackupExecutor.RunBackupJobAsync(job, root, null, CancellationToken.None);
        Assert.Equal(["keep.txt"], Directory.GetFiles(Destination).Select(Path.GetFileName));
    }

    [Fact]
    public async Task MalformedNestedIgnoreStopsWholeLocalScanBeforeCopying()
    {
        Write("keep.txt", "keep");
        Write("nested/.maxbackupignore", "!restore.txt\n");
        var logger = new RecordingLogger();
        await BackupExecutor.RunBackupJobAsync(Job, root, logger, CancellationToken.None);
        Assert.Empty(Directory.GetFiles(Destination, "*", SearchOption.AllDirectories));
        Assert.Contains(logger.Errors, error => error.Contains(".maxbackupignore"));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task InvalidJobPatternsPreserveExistingExceptionBehavior(bool cloud, bool exclude)
    {
        Write("keep.txt", "keep");
        var job = Job;
        if (exclude) job.Exclude = ["a/../b"];
        else job.Include = ["a/../b"];
        var logger = new RecordingLogger();
        var provider = new RecordingProvider();

        await Assert.ThrowsAsync<ArgumentException>(() => cloud
            ? RunCloud(provider, logger, job: job)
            : BackupExecutor.RunBackupJobAsync(job, root, logger, CancellationToken.None));

        Assert.Empty(logger.Errors);
        Assert.Empty(provider.Uploads);
        if (!cloud) Assert.Empty(Directory.GetFiles(Destination, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task CancellationIsPropagatedBeforeAnyCopies()
    {
        Write("keep.txt", "keep");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            BackupExecutor.RunBackupJobAsync(Job, root, null, cancellation.Token));
        Assert.False(Directory.Exists(Destination));
    }

    [Fact]
    public async Task CloudBackupUploadsOnlySelectedFilesAndRetainsExistingRemoteFiles()
    {
        Write("keep.txt", "keep");
        Write("cache/ignored.txt", "ignored");
        Write(".maxbackupignore", "cache/\n");
        var provider = new RecordingProvider();
        await RunCloud(provider);
        Assert.Equal(["prefix/.maxbackupignore", "prefix/keep.txt"], provider.Uploads.Order());
        Assert.Contains("cache/retained.txt", provider.Existing.Keys);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAtScanStartPreservesExistingJobHandling(bool cloud)
    {
        Write("keep.txt", "keep");
        using var cancellation = new CancellationTokenSource();
        var logger = new RecordingLogger(message =>
        {
            if (message.StartsWith("Scanning for files", StringComparison.Ordinal)) cancellation.Cancel();
        });
        var provider = new RecordingProvider();

        if (cloud)
        {
            await RunCloud(provider, logger, cancellation.Token);
            Assert.Contains(logger.Errors, error => error.Contains("Error scanning files"));
        }
        else
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                BackupExecutor.RunBackupJobAsync(Job, root, logger, cancellation.Token));
            Assert.Empty(Directory.GetFiles(Destination, "*", SearchOption.AllDirectories));
            Assert.Empty(logger.Errors);
        }
        Assert.True(cancellation.IsCancellationRequested);
        Assert.Empty(provider.Uploads);
    }

    [Fact]
    public async Task MalformedIgnoreStopsCloudBackupBeforeUploads()
    {
        Write("keep.txt", "keep");
        Write("nested/.maxbackupignore", "!restore.txt\n");
        var provider = new RecordingProvider();
        var logger = new RecordingLogger();
        await RunCloud(provider, logger);
        Assert.Empty(provider.Uploads);
        Assert.Contains(logger.Errors, error => error.Contains(".maxbackupignore"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnreadableNestedIgnoreStopsAllTransfers(bool cloud)
    {
        Write("keep.txt", "keep");
        Write("nested/.maxbackupignore", "cache/\n");
        using var lockedIgnore = new FileStream(Path.Combine(Source, "nested", ".maxbackupignore"),
            FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var logger = new RecordingLogger();
        var provider = new RecordingProvider();

        if (cloud)
            await RunCloud(provider, logger);
        else
            await BackupExecutor.RunBackupJobAsync(Job, root, logger, CancellationToken.None);

        Assert.Empty(provider.Uploads);
        if (!cloud) Assert.Empty(Directory.GetFiles(Destination, "*", SearchOption.AllDirectories));
        Assert.Contains(logger.Errors, error => error.Contains(".maxbackupignore"));
    }

    private Task RunCloud(RecordingProvider provider, ILogger? logger = null, CancellationToken token = default,
        BackupJobConfig? job = null)
    {
        job ??= Job;
        job.Provider = "recording";
        job.Destination = "prefix";
        var method = typeof(BackupExecutor).GetMethod("RunCloudBackupJobAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (Task)method.Invoke(null, [job, root, provider, logger, token])!;
    }

    private void Write(string relativePath, string contents)
    {
        var path = Path.Combine(Source, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

    public void Dispose() => Directory.Delete(root, true);

    private sealed class RecordingLogger(Action<string>? onMessage = null) : ILogger
    {
        public List<string> Errors { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            onMessage?.Invoke(formatter(state, exception));
            if (logLevel == LogLevel.Error) Errors.Add(formatter(state, exception) + exception);
        }
    }

    private sealed class RecordingProvider : IStorageProvider
    {
        public List<string> Uploads { get; } = [];
        public Dictionary<string, RemoteFileInfo> Existing { get; } = new()
        {
            ["cache/retained.txt"] = new("cache/retained.txt", 8, DateTime.UtcNow)
        };
        public Task<ProviderTestResult> TestConnectionAsync(CancellationToken ct = default) => Task.FromResult(new ProviderTestResult(true));
        public Task<RemoteFileInfo?> GetFileInfoAsync(string relativePath, CancellationToken ct = default) => Task.FromResult(Existing.GetValueOrDefault(relativePath));
        public Task UploadFileAsync(string localPath, string relativePath, FileInfo localFileInfo, IProgress<long>? progress = null, CancellationToken ct = default)
        {
            Uploads.Add(relativePath);
            return Task.CompletedTask;
        }
        public async IAsyncEnumerable<RemoteFileInfo> ListFilesAsync(string? prefix = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            foreach (var file in Existing.Values) yield return file;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
