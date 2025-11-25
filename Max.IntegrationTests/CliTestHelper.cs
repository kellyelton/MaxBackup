using System.Diagnostics;
using System.Text;

namespace Max.IntegrationTests;

/// <summary>
/// Helper class for running CLI commands and capturing output.
/// Implements IAsyncLifetime for proper xUnit async setup/teardown.
/// </summary>
public class CliTestHelper : IAsyncLifetime, IDisposable
{
    private static readonly string TestRootDirectory = Path.Combine(Path.GetTempPath(), "MaxBackupTests");
    
    private readonly string _testDirectory;
    private readonly string _configFilePath;
    private readonly string _maxExePath;
    private bool _disposed;

    public string TestDirectory => _testDirectory;
    public string ConfigFilePath => _configFilePath;

    public CliTestHelper()
    {
        // Create a unique test directory for each test instance
        _testDirectory = Path.Combine(TestRootDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
        
        _configFilePath = Path.Combine(_testDirectory, "testconfig.json");
        
        // Check for CI environment variable first, then fall back to debug build paths
        var ciExePath = Environment.GetEnvironmentVariable("MAX_CLI_PATH");
        if (!string.IsNullOrEmpty(ciExePath) && File.Exists(ciExePath))
        {
            _maxExePath = ciExePath;
            return;
        }
        
        // Find the max executable - look for the debug build
        var solutionDir = FindSolutionDirectory();
        
        // Use exe paths (RID-specific build produces exe)
        var possiblePaths = new[]
        {
            Path.Combine(solutionDir, "Max", "bin", "Debug", "net10.0", "win-x64", "max.exe"),
            Path.Combine(solutionDir, "Max", "bin", "Debug", "net10.0", "max.exe"),
        };
        
        _maxExePath = possiblePaths.FirstOrDefault(File.Exists) 
            ?? throw new InvalidOperationException($"Max executable not found. Searched paths:\n{string.Join("\n", possiblePaths)}\nSet MAX_CLI_PATH environment variable or build the Max project first.");
    }

    /// <summary>
    /// xUnit async initialization - called before each test
    /// </summary>
    public Task InitializeAsync()
    {
        // Clean up any orphaned test directories from previous failed runs (older than 1 hour)
        CleanupOrphanedTestDirectories(TimeSpan.FromHours(1));
        return Task.CompletedTask;
    }

    /// <summary>
    /// xUnit async cleanup - called after each test
    /// </summary>
    public async Task DisposeAsync()
    {
        await Task.Run(() => CleanupTestDirectory());
    }

    /// <summary>
    /// Cleans up orphaned test directories from previous runs
    /// </summary>
    private static void CleanupOrphanedTestDirectories(TimeSpan maxAge)
    {
        try
        {
            if (!Directory.Exists(TestRootDirectory))
                return;

            var cutoffTime = DateTime.UtcNow - maxAge;
            
            foreach (var dir in Directory.GetDirectories(TestRootDirectory))
            {
                try
                {
                    var dirInfo = new DirectoryInfo(dir);
                    if (dirInfo.CreationTimeUtc < cutoffTime)
                    {
                        ForceDeleteDirectory(dir);
                    }
                }
                catch
                {
                    // Ignore errors cleaning up old directories
                }
            }
        }
        catch
        {
            // Ignore errors during orphan cleanup
        }
    }

    /// <summary>
    /// Cleans up the test directory with retries
    /// </summary>
    private void CleanupTestDirectory()
    {
        if (_disposed) return;
        _disposed = true;

        const int maxRetries = 3;
        const int retryDelayMs = 100;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                if (Directory.Exists(_testDirectory))
                {
                    ForceDeleteDirectory(_testDirectory);
                }
                return; // Success
            }
            catch
            {
                if (i < maxRetries - 1)
                {
                    Thread.Sleep(retryDelayMs * (i + 1)); // Exponential backoff
                }
                // On final retry, just ignore the error
            }
        }
    }

    /// <summary>
    /// Force deletes a directory by clearing read-only attributes first
    /// </summary>
    private static void ForceDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        // Clear read-only attributes on all files
        foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
        {
            try
            {
                var attributes = File.GetAttributes(file);
                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
                }
            }
            catch
            {
                // Ignore individual file attribute errors
            }
        }

        // Clear read-only attributes on all directories
        foreach (var dir in Directory.GetDirectories(path, "*", SearchOption.AllDirectories))
        {
            try
            {
                var attributes = File.GetAttributes(dir);
                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(dir, attributes & ~FileAttributes.ReadOnly);
                }
            }
            catch
            {
                // Ignore individual directory attribute errors
            }
        }

        // Now delete the directory
        Directory.Delete(path, true);
    }

    private static string FindSolutionDirectory()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "MaxBackup.sln")))
            {
                return dir;
            }
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Could not find solution directory");
    }

    /// <summary>
    /// Runs the max CLI with the specified arguments
    /// </summary>
    public async Task<CliResult> RunMaxAsync(params string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _maxExePath,
            Arguments = string.Join(" ", args.Select(EscapeArgument)),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _testDirectory
        };

        using var process = new Process { StartInfo = startInfo };

        process.Start();
        
        // Read output synchronously to ensure we capture everything
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        var completed = await Task.Run(() => process.WaitForExit(30000)); // 30 second timeout
        
        if (!completed)
        {
            process.Kill(true);
            throw new TimeoutException("CLI command timed out after 30 seconds");
        }

        // Ensure streams are fully read
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return new CliResult(
            process.ExitCode,
            stdout.Trim(),
            stderr.Trim()
        );
    }

    /// <summary>
    /// Runs the max CLI with a custom config path
    /// </summary>
    public Task<CliResult> RunMaxWithConfigAsync(params string[] args)
    {
        var allArgs = new List<string> { "--config-path", _configFilePath };
        allArgs.AddRange(args);
        return RunMaxAsync(allArgs.ToArray());
    }

    /// <summary>
    /// Creates a test config file with the specified jobs
    /// </summary>
    public void CreateConfigFile(params TestJob[] jobs)
    {
        var jobsJson = string.Join(",\n", jobs.Select(j => j.ToJson()));
        var json = $$"""
        {
          "Backup": {
            "Jobs": [
              {{jobsJson}}
            ]
          }
        }
        """;
        File.WriteAllText(_configFilePath, json);
    }

    /// <summary>
    /// Creates an empty config file
    /// </summary>
    public void CreateEmptyConfigFile()
    {
        var json = """
        {
          "Backup": {
            "Jobs": []
          }
        }
        """;
        File.WriteAllText(_configFilePath, json);
    }

    /// <summary>
    /// Reads and parses the config file
    /// </summary>
    public string ReadConfigFile()
    {
        return File.Exists(_configFilePath) ? File.ReadAllText(_configFilePath) : string.Empty;
    }

    /// <summary>
    /// Creates a test source directory with some files
    /// </summary>
    public string CreateSourceDirectory(string name = "source")
    {
        var dir = Path.Combine(_testDirectory, name);
        Directory.CreateDirectory(dir);
        
        // Create some test files
        File.WriteAllText(Path.Combine(dir, "file1.txt"), "test content 1");
        File.WriteAllText(Path.Combine(dir, "file2.txt"), "test content 2");
        
        return dir;
    }

    /// <summary>
    /// Creates a test destination directory
    /// </summary>
    public string CreateDestinationDirectory(string name = "destination")
    {
        var dir = Path.Combine(_testDirectory, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string EscapeArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg))
            return "\"\"";
        
        if (arg.Contains(' ') || arg.Contains('"'))
            return $"\"{arg.Replace("\"", "\\\"")}\"";
        
        return arg;
    }

    /// <summary>
    /// Synchronous dispose for IDisposable - calls cleanup
    /// </summary>
    public void Dispose()
    {
        CleanupTestDirectory();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Represents the result of a CLI command execution
/// </summary>
public record CliResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool IsSuccess => ExitCode == 0;
    
    public string AllOutput => string.IsNullOrEmpty(StandardError) 
        ? StandardOutput 
        : $"{StandardOutput}\n{StandardError}";
}

/// <summary>
/// Helper for creating test job configurations
/// </summary>
public record TestJob(string Name, string Source, string Destination, string[]? Include = null, string[]? Exclude = null)
{
    public string ToJson()
    {
        var include = Include ?? new[] { "**" };
        var exclude = Exclude ?? Array.Empty<string>();
        
        return $$"""
              {
                "Name": "{{Name}}",
                "Source": "{{Source.Replace("\\", "\\\\")}}",
                "Destination": "{{Destination.Replace("\\", "\\\\")}}",
                "Include": [{{string.Join(", ", include.Select(i => $"\"{i}\""))}}],
                "Exclude": [{{string.Join(", ", exclude.Select(e => $"\"{e}\""))}}]
              }
        """;
    }
}
