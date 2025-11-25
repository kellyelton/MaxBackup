namespace Max.IntegrationTests;

/// <summary>
/// Tests for the root command and global options
/// </summary>
public class RootCommandTests : IAsyncLifetime
{
    private readonly CliTestHelper _cli;

    public RootCommandTests()
    {
        _cli = new CliTestHelper();
    }

    public Task InitializeAsync() => _cli.InitializeAsync();
    public Task DisposeAsync() => _cli.DisposeAsync();

    [Fact]
    public async Task RootCommand_WithNoArgs_ShowsHelp()
    {
        // Act
        var result = await _cli.RunMaxAsync();

        // Assert
        // System.CommandLine shows help when no subcommand is provided
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Max backup app for windows", result.StandardOutput);
        Assert.Contains("jobs", result.StandardOutput);
        Assert.Contains("service", result.StandardOutput);
    }

    [Fact]
    public async Task RootCommand_WithHelpFlag_ShowsHelp()
    {
        // Act
        var result = await _cli.RunMaxAsync("--help");

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Max backup app for windows", result.StandardOutput);
        Assert.Contains("--verbose", result.StandardOutput);
        Assert.Contains("--config-path", result.StandardOutput);
    }

    [Fact]
    public async Task RootCommand_WithShortHelpFlag_ShowsHelp()
    {
        // Act
        var result = await _cli.RunMaxAsync("-h");

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Max backup app for windows", result.StandardOutput);
    }

    [Fact]
    public async Task RootCommand_WithQuestionMarkHelp_ShowsHelp()
    {
        // Act
        var result = await _cli.RunMaxAsync("-?");

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Max backup app for windows", result.StandardOutput);
    }

    [Fact]
    public async Task RootCommand_WithVerboseOption_IsAccepted()
    {
        // Act - verbose alone without a subcommand still shows help
        var result = await _cli.RunMaxAsync("--verbose");

        // Assert - option is accepted, help is shown
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Max backup app for windows", result.StandardOutput);
    }

    [Fact]
    public async Task RootCommand_WithConfigPathOption_IsAccepted()
    {
        // Arrange
        var configPath = Path.Combine(_cli.TestDirectory, "custom-config.json");

        // Act
        var result = await _cli.RunMaxAsync("--config-path", configPath);

        // Assert - option is accepted, help is shown
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Max backup app for windows", result.StandardOutput);
    }

    [Fact]
    public async Task RootCommand_WithInvalidSubcommand_ShowsError()
    {
        // Act
        var result = await _cli.RunMaxAsync("invalidcommand");

        // Assert
        Assert.NotEqual(0, result.ExitCode);
    }

}
