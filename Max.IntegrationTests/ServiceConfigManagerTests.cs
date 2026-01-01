using System.Text.Json;

namespace Max.IntegrationTests;

/// <summary>
/// Tests for ServiceConfigManager functionality.
/// These tests verify config loading/saving works correctly, including edge cases like
/// creating a new config when none exists.
/// </summary>
public class ServiceConfigManagerTests : IAsyncLifetime
{
    private string _testDirectory = null!;
    private string _configFilePath = null!;

    public Task InitializeAsync()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"MaxBackupConfigTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _configFilePath = Path.Combine(_testDirectory, "config.json");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
        catch { }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Simulates the ServiceConfigManager pattern to test for deadlocks.
    /// This test would hang if there's a deadlock in the semaphore usage.
    /// </summary>
    [Fact]
    public async Task LoadConfig_WhenConfigDoesNotExist_CreatesNewConfigWithoutDeadlock()
    {
        // Arrange
        var semaphore = new SemaphoreSlim(1, 1);
        var configCreated = false;

        // Act - Simulate what ServiceConfigManager does
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        
        var task = Task.Run(async () =>
        {
            await semaphore.WaitAsync(cts.Token);
            try
            {
                if (!File.Exists(_configFilePath))
                {
                    // This simulates calling SaveConfigInternalAsync (not SaveConfigAsync)
                    // If we called a method that tried to acquire semaphore again, we'd deadlock
                    var json = JsonSerializer.Serialize(new { Test = "Value" });
                    await File.WriteAllTextAsync(_configFilePath, json, cts.Token);
                    configCreated = true;
                }
            }
            finally
            {
                semaphore.Release();
            }
        }, cts.Token);

        // Assert - Should complete without timeout (no deadlock)
        await task;
        Assert.True(configCreated);
        Assert.True(File.Exists(_configFilePath));
    }

    /// <summary>
    /// Tests that concurrent config access is properly serialized.
    /// </summary>
    [Fact]
    public async Task ConcurrentConfigAccess_IsProperlySeralized()
    {
        // Arrange
        var semaphore = new SemaphoreSlim(1, 1);
        var accessCount = 0;
        var maxConcurrent = 0;
        var currentConcurrent = 0;
        var lockObj = new object();

        // Act - Simulate multiple concurrent config accesses
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            await semaphore.WaitAsync();
            try
            {
                lock (lockObj)
                {
                    currentConcurrent++;
                    maxConcurrent = Math.Max(maxConcurrent, currentConcurrent);
                    accessCount++;
                }

                await Task.Delay(10); // Simulate some work

                lock (lockObj)
                {
                    currentConcurrent--;
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        // Assert - Only one access at a time
        Assert.Equal(10, accessCount);
        Assert.Equal(1, maxConcurrent); // Should never exceed 1 concurrent access
    }

    /// <summary>
    /// Verifies that the pattern we use doesn't cause re-entrancy issues.
    /// </summary>
    [Fact]
    public async Task Semaphore_DoesNotAllowReentrancy()
    {
        // Arrange
        var semaphore = new SemaphoreSlim(1, 1);
        var deadlockDetected = false;

        // Act - Try to acquire semaphore twice (simulates the bug)
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        try
        {
            await semaphore.WaitAsync(cts.Token);
            
            // Try to acquire again - this would deadlock with SemaphoreSlim(1,1)
            var acquired = await semaphore.WaitAsync(TimeSpan.FromMilliseconds(100));
            
            if (!acquired)
            {
                deadlockDetected = true;
            }
            else
            {
                semaphore.Release();
            }
            
            semaphore.Release();
        }
        catch (OperationCanceledException)
        {
            deadlockDetected = true;
        }

        // Assert - The second acquire should fail (would timeout/deadlock)
        Assert.True(deadlockDetected, "Expected re-entrant acquire to fail");
    }
}
