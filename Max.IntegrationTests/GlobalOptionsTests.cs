namespace Max.IntegrationTests;

/// <summary>
/// Tests for global options (--verbose, --config-path) across all commands
/// </summary>
public class GlobalOptionsTests : IAsyncLifetime
{
    private readonly CliTestHelper _cli;

    public GlobalOptionsTests()
    {
        _cli = new CliTestHelper();
    }

    public Task InitializeAsync() => _cli.InitializeAsync();
    public Task DisposeAsync() => _cli.DisposeAsync();

    #region --config-path option

    [Fact]
    public async Task ConfigPath_CanBeSpecifiedBeforeCommand()
    {
        // Arrange
        _cli.CreateEmptyConfigFile();
        var source = _cli.CreateSourceDirectory();
        var dest = _cli.CreateDestinationDirectory();

        // Act - config-path before command
        var result = await _cli.RunMaxAsync("--config-path", _cli.ConfigFilePath, "jobs", "create", "Test", source, dest);

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(_cli.ConfigFilePath));
        var config = _cli.ReadConfigFile();
        Assert.Contains("Test", config);
    }

    [Fact]
    public async Task ConfigPath_CanBeSpecifiedAfterCommand()
    {
        // Arrange
        _cli.CreateEmptyConfigFile();
        var source = _cli.CreateSourceDirectory();
        var dest = _cli.CreateDestinationDirectory();

        // Act - config-path after command (recursive option)
        var result = await _cli.RunMaxAsync("jobs", "--config-path", _cli.ConfigFilePath, "create", "Test", source, dest);

        // Assert
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task ConfigPath_UsesCustomPath()
    {
        // Arrange
        var customConfigPath = Path.Combine(_cli.TestDirectory, "custom", "myconfig.json");
        Directory.CreateDirectory(Path.GetDirectoryName(customConfigPath)!);
        
        var source = _cli.CreateSourceDirectory();
        var dest = _cli.CreateDestinationDirectory();

        // Act
        var result = await _cli.RunMaxAsync("--config-path", customConfigPath, "jobs", "create", "CustomTest", source, dest);

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(customConfigPath));
        var config = File.ReadAllText(customConfigPath);
        Assert.Contains("CustomTest", config);
    }

    [Fact]
    public async Task ConfigPath_WorksWithJobsList()
    {
        // Arrange
        var source = _cli.CreateSourceDirectory();
        var dest = _cli.CreateDestinationDirectory();
        _cli.CreateConfigFile(new TestJob("ListTest", source, dest));

        // Act
        var result = await _cli.RunMaxAsync("--config-path", _cli.ConfigFilePath, "jobs", "list");

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("ListTest", result.StandardOutput);
    }

    [Fact]
    public async Task ConfigPath_WorksWithJobsModify()
    {
        // Arrange
        var source = _cli.CreateSourceDirectory();
        var dest = _cli.CreateDestinationDirectory();
        var newDest = _cli.CreateDestinationDirectory("newdest");
        _cli.CreateConfigFile(new TestJob("ModifyTest", source, dest));

        // Act
        var result = await _cli.RunMaxAsync("--config-path", _cli.ConfigFilePath, "jobs", "modify", "ModifyTest", "--destination", newDest);

        // Assert
        Assert.Equal(0, result.ExitCode);
        var config = _cli.ReadConfigFile();
        Assert.Contains(newDest.Replace("\\", "\\\\"), config);
    }

    [Fact]
    public async Task ConfigPath_WorksWithJobsDelete()
    {
        // Arrange
        var source = _cli.CreateSourceDirectory();
        var dest = _cli.CreateDestinationDirectory();
        _cli.CreateConfigFile(new TestJob("DeleteTest", source, dest));

        // Act
        var result = await _cli.RunMaxAsync("--config-path", _cli.ConfigFilePath, "jobs", "delete", "DeleteTest");

        // Assert
        Assert.Equal(0, result.ExitCode);
        var config = _cli.ReadConfigFile();
        Assert.DoesNotContain("DeleteTest", config);
    }

    #endregion

    #region --verbose option

    [Fact]
    public async Task Verbose_CanBeSpecifiedBeforeCommand()
    {
        // Arrange
        var source = _cli.CreateSourceDirectory();
        var dest = _cli.CreateDestinationDirectory();
        _cli.CreateConfigFile(new TestJob("VerboseTest", source, dest));

        // Act
        var result = await _cli.RunMaxAsync("--verbose", "--config-path", _cli.ConfigFilePath, "jobs", "list");

        // Assert
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task Verbose_CanBeSpecifiedAfterCommand()
    {
        // Arrange
        var source = _cli.CreateSourceDirectory();
        var dest = _cli.CreateDestinationDirectory();
        _cli.CreateConfigFile(new TestJob("VerboseTest", source, dest));

        // Act - verbose is recursive, should work after command
        var result = await _cli.RunMaxAsync("--config-path", _cli.ConfigFilePath, "jobs", "--verbose", "list");

        // Assert
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task Verbose_WorksWithJobsCreate()
    {
        // Arrange
        _cli.CreateEmptyConfigFile();
        var source = _cli.CreateSourceDirectory();
        var dest = _cli.CreateDestinationDirectory();

        // Act
        var result = await _cli.RunMaxAsync("--verbose", "--config-path", _cli.ConfigFilePath, "jobs", "create", "VerboseCreate", source, dest);

        // Assert
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task Verbose_WorksWithService()
    {
        // Act
        var result = await _cli.RunMaxAsync("--verbose", "service");

        // Assert
        // Should complete without error (service may or may not be installed)
        var validExitCodes = new[] { -2, -1, 0, 1, 2, 3, 4, 5, 6, 7 };
        Assert.Contains(result.ExitCode, validExitCodes);
    }

    #endregion

    #region Combined options

    [Fact]
    public async Task CombinedOptions_VerboseAndConfigPath_WorkTogether()
    {
        // Arrange
        var source = _cli.CreateSourceDirectory();
        var dest = _cli.CreateDestinationDirectory();
        _cli.CreateConfigFile(new TestJob("CombinedTest", source, dest));

        // Act
        var result = await _cli.RunMaxAsync("--verbose", "--config-path", _cli.ConfigFilePath, "jobs", "list");

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("CombinedTest", result.StandardOutput);
    }

    [Fact]
    public async Task CombinedOptions_OrderDoesNotMatter()
    {
        // Arrange
        var source = _cli.CreateSourceDirectory();
        var dest = _cli.CreateDestinationDirectory();
        _cli.CreateConfigFile(new TestJob("OrderTest", source, dest));

        // Act - different order
        var result = await _cli.RunMaxAsync("--config-path", _cli.ConfigFilePath, "--verbose", "jobs", "list");

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("OrderTest", result.StandardOutput);
    }

    #endregion

}
