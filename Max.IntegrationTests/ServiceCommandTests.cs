namespace Max.IntegrationTests;

/// <summary>
/// Tests for the 'service' command
/// </summary>
public class ServiceCommandTests : IAsyncLifetime
{
    private readonly CliTestHelper _cli;

    public ServiceCommandTests()
    {
        _cli = new CliTestHelper();
    }

    public Task InitializeAsync() => _cli.InitializeAsync();
    public Task DisposeAsync() => _cli.DisposeAsync();

    [Fact]
    public async Task Service_ShowsServiceStatus()
    {
        // Act
        var result = await _cli.RunMaxAsync("service");

        // Assert
        // The service may or may not be installed, so we check for expected outputs
        // Either "Service is not install" (exit code 1) or a status message
        if (result.ExitCode == 1)
        {
            Assert.Contains("Service is not install", result.StandardOutput);
        }
        else
        {
            // Service exists - should show status
            Assert.True(
                result.StandardOutput.Contains("running") ||
                result.StandardOutput.Contains("stopped") ||
                result.StandardOutput.Contains("starting") ||
                result.StandardOutput.Contains("stopping") ||
                result.StandardOutput.Contains("paused") ||
                result.StandardOutput.Contains("max backup"),
                $"Expected service status output but got: {result.StandardOutput}"
            );
        }
    }

    [Fact]
    public async Task Service_WithHelpFlag_ShowsHelp()
    {
        // Act
        var result = await _cli.RunMaxAsync("service", "--help");

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Backup Service Controller", result.StandardOutput);
    }

    [Fact]
    public async Task Service_ReturnsExpectedExitCodes()
    {
        // Act
        var result = await _cli.RunMaxAsync("service");

        // Assert - validate exit code is one of the expected values
        var validExitCodes = new[] { -2, -1, 0, 1, 2, 3, 4, 5, 6, 7 };
        Assert.Contains(result.ExitCode, validExitCodes);
    }

}
