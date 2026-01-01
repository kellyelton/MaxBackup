namespace Max.IntegrationTests;

/// <summary>
/// Tests for the 'jobs' command and its subcommands
/// </summary>
public class JobsCommandTests : IAsyncLifetime
{
    private readonly CliTestHelper _cli;

    public JobsCommandTests()
    {
        _cli = new CliTestHelper();
    }

    public Task InitializeAsync() => _cli.InitializeAsync();
    public Task DisposeAsync() => _cli.DisposeAsync();

    #region jobs (parent command)

    [Fact]
    public async Task Jobs_WithNoArgs_ShowsHelp()
    {
        // Act
        var result = await _cli.RunMaxAsync("jobs");

        // Assert - System.CommandLine 2.0 returns exit code 1 when required subcommand is not provided
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Manage backup jobs", result.StandardOutput);
        Assert.Contains("Required command was not provided", result.AllOutput);
    }

    [Fact]
    public async Task Jobs_WithHelpFlag_ShowsSubcommands()
    {
        // Act
        var result = await _cli.RunMaxAsync("jobs", "--help");

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("list", result.StandardOutput);
        Assert.Contains("create", result.StandardOutput);
        Assert.Contains("modify", result.StandardOutput);
        Assert.Contains("delete", result.StandardOutput);
    }

    #endregion

    #region jobs list

    [Fact]
    public async Task JobsList_WithNoConfigFile_ReturnsError()
    {
        // Act - no config file exists
        var result = await _cli.RunMaxWithConfigAsync("jobs", "list");

        // Assert
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("No jobs found", result.StandardError);
    }

    [Fact]
    public async Task JobsList_WithEmptyJobs_ReturnsError()
    {
        // Arrange
        _cli.CreateEmptyConfigFile();

        // Act
        var result = await _cli.RunMaxWithConfigAsync("jobs", "list");

        // Assert
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("No jobs found", result.StandardError);
    }

    [Fact]
    public async Task JobsList_WithJobs_DisplaysTable()
    {
        // Arrange
        var source = _cli.CreateSourceDirectory();
        var dest = _cli.CreateDestinationDirectory();
        _cli.CreateConfigFile(new TestJob("TestJob1", source, dest));

        // Act
        var result = await _cli.RunMaxWithConfigAsync("jobs", "list");

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("TestJob1", result.StandardOutput);
        Assert.Contains("Name", result.StandardOutput);
        Assert.Contains("Source", result.StandardOutput);
        Assert.Contains("Destination", result.StandardOutput);
    }

    [Fact]
    public async Task JobsList_WithMultipleJobs_DisplaysAll()
    {
        // Arrange
        var source1 = _cli.CreateSourceDirectory("source1");
        var dest1 = _cli.CreateDestinationDirectory("dest1");
        var source2 = _cli.CreateSourceDirectory("source2");
        var dest2 = _cli.CreateDestinationDirectory("dest2");
        
        _cli.CreateConfigFile(
            new TestJob("Job1", source1, dest1),
            new TestJob("Job2", source2, dest2)
        );

        // Act
        var result = await _cli.RunMaxWithConfigAsync("jobs", "list");

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Job1", result.StandardOutput);
        Assert.Contains("Job2", result.StandardOutput);
    }

    [Fact]
    public async Task JobsList_WithVerboseFlag_Works()
    {
        // Arrange
        var source = _cli.CreateSourceDirectory();
        var dest = _cli.CreateDestinationDirectory();
        _cli.CreateConfigFile(new TestJob("TestJob", source, dest));

        // Act
        var result = await _cli.RunMaxWithConfigAsync("--verbose", "jobs", "list");

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("TestJob", result.StandardOutput);
    }

    #endregion

    #region jobs create

    [Fact]
    public async Task JobsCreate_WithValidArgs_CreatesJob()
    {
        // Arrange
        _cli.CreateEmptyConfigFile();
        var source = _cli.CreateSourceDirectory();
        var dest = _cli.CreateDestinationDirectory();

        // Act
        var result = await _cli.RunMaxWithConfigAsync("jobs", "create", "NewJob", source, dest);

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Job 'NewJob' created", result.StandardOutput);
        
        // Verify config file was updated
        var config = _cli.ReadConfigFile();
        Assert.Contains("NewJob", config);
        Assert.Contains(source.Replace("\\", "\\\\"), config);
    }

    [Fact]
    public async Task JobsCreate_WithNoConfigFile_CreatesConfigAndJob()
    {
        // Arrange - no config file exists
        var source = _cli.CreateSourceDirectory();
        var dest = _cli.CreateDestinationDirectory();

        // Act
        var result = await _cli.RunMaxWithConfigAsync("jobs", "create", "NewJob", source, dest);

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Job 'NewJob' created", result.StandardOutput);
        Assert.True(File.Exists(_cli.ConfigFilePath));
    }

    [Fact]
    public async Task JobsCreate_WithIncludePattern_SetsInclude()
    {
        // Arrange
        _cli.CreateEmptyConfigFile();
        var source = _cli.CreateSourceDirectory();
        var dest = _cli.CreateDestinationDirectory();

        // Act
        var result = await _cli.RunMaxWithConfigAsync("jobs", "create", "NewJob", source, dest, "--include", "*.txt");

        // Assert
        Assert.Equal(0, result.ExitCode);
        var config = _cli.ReadConfigFile();
        Assert.Contains("*.txt", config);
    }

    [Fact]
    public async Task JobsCreate_WithExcludePattern_SetsExclude()
    {
        // Arrange
        _cli.CreateEmptyConfigFile();
        var source = _cli.CreateSourceDirectory();
        var dest = _cli.CreateDestinationDirectory();

        // Act
        var result = await _cli.RunMaxWithConfigAsync("jobs", "create", "NewJob", source, dest, "--exclude", "*.log");

        // Assert
        Assert.Equal(0, result.ExitCode);
        var config = _cli.ReadConfigFile();
        Assert.Contains("*.log", config);
    }

    [Fact]
    public async Task JobsCreate_WithMultipleIncludePatterns_SetsAll()
    {
        // Arrange
        _cli.CreateEmptyConfigFile();
        var source = _cli.CreateSourceDirectory();
        var dest = _cli.CreateDestinationDirectory();

        // Act
        var result = await _cli.RunMaxWithConfigAsync("jobs", "create", "NewJob", source, dest, 
            "--include", "*.txt", "--include", "*.md");

        // Assert
        Assert.Equal(0, result.ExitCode);
        var config = _cli.ReadConfigFile();
        Assert.Contains("*.txt", config);
        Assert.Contains("*.md", config);
    }

    [Fact]
    public async Task JobsCreate_WithDuplicateName_ReturnsError()
    {
        // Arrange
        var source = _cli.CreateSourceDirectory();
        var dest = _cli.CreateDestinationDirectory();
        _cli.CreateConfigFile(new TestJob("ExistingJob", source, dest));

        // Act
        var result = await _cli.RunMaxWithConfigAsync("jobs", "create", "ExistingJob", source, dest);

        // Assert
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("already exists", result.StandardError);
    }

    [Fact]
    public async Task JobsCreate_WithMissingName_ReturnsError()
    {
        // Act
        var result = await _cli.RunMaxWithConfigAsync("jobs", "create");

        // Assert
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task JobsCreate_WithMissingSource_ReturnsError()
    {
        // Act
        var result = await _cli.RunMaxWithConfigAsync("jobs", "create", "NewJob");

        // Assert
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task JobsCreate_WithMissingDestination_ReturnsError()
    {
        // Arrange
        var source = _cli.CreateSourceDirectory();

        // Act
        var result = await _cli.RunMaxWithConfigAsync("jobs", "create", "NewJob", source);

        // Assert
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task JobsCreate_WithVerboseFlag_Works()
    {
        // Arrange
        _cli.CreateEmptyConfigFile();
        var source = _cli.CreateSourceDirectory();
        var dest = _cli.CreateDestinationDirectory();

        // Act
        var result = await _cli.RunMaxWithConfigAsync("--verbose", "jobs", "create", "NewJob", source, dest);

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Job 'NewJob' created", result.StandardOutput);
    }

    #endregion

    #region jobs modify

    [Fact]
    public async Task JobsModify_WithNewSource_UpdatesSource()
    {
        // Arrange
        var source1 = _cli.CreateSourceDirectory("source1");
        var source2 = _cli.CreateSourceDirectory("source2");
        var dest = _cli.CreateDestinationDirectory();
        _cli.CreateConfigFile(new TestJob("TestJob", source1, dest));

        // Act
        var result = await _cli.RunMaxWithConfigAsync("jobs", "modify", "TestJob", "--source", source2);

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Job 'TestJob' modified", result.StandardOutput);
        
        var config = _cli.ReadConfigFile();
        Assert.Contains(source2.Replace("\\", "\\\\"), config);
    }

    [Fact]
    public async Task JobsModify_WithNewDestination_UpdatesDestination()
    {
        // Arrange
        var source = _cli.CreateSourceDirectory();
        var dest1 = _cli.CreateDestinationDirectory("dest1");
        var dest2 = _cli.CreateDestinationDirectory("dest2");
        _cli.CreateConfigFile(new TestJob("TestJob", source, dest1));

        // Act
        var result = await _cli.RunMaxWithConfigAsync("jobs", "modify", "TestJob", "--destination", dest2);

        // Assert
        Assert.Equal(0, result.ExitCode);
        var config = _cli.ReadConfigFile();
        Assert.Contains(dest2.Replace("\\", "\\\\"), config);
    }

    [Fact]
    public async Task JobsModify_WithNewInclude_UpdatesInclude()
    {
        // Arrange
        var source = _cli.CreateSourceDirectory();
        var dest = _cli.CreateDestinationDirectory();
        _cli.CreateConfigFile(new TestJob("TestJob", source, dest, new[] { "**" }));

        // Act
        var result = await _cli.RunMaxWithConfigAsync("jobs", "modify", "TestJob", "--include", "*.txt");

        // Assert
        Assert.Equal(0, result.ExitCode);
        var config = _cli.ReadConfigFile();
        Assert.Contains("*.txt", config);
    }

    [Fact]
    public async Task JobsModify_WithNewExclude_UpdatesExclude()
    {
        // Arrange
        var source = _cli.CreateSourceDirectory();
        var dest = _cli.CreateDestinationDirectory();
        _cli.CreateConfigFile(new TestJob("TestJob", source, dest));

        // Act
        var result = await _cli.RunMaxWithConfigAsync("jobs", "modify", "TestJob", "--exclude", "*.tmp");

        // Assert
        Assert.Equal(0, result.ExitCode);
        var config = _cli.ReadConfigFile();
        Assert.Contains("*.tmp", config);
    }

    [Fact]
    public async Task JobsModify_WithNonExistentJob_ReturnsError()
    {
        // Arrange
        _cli.CreateEmptyConfigFile();

        // Act
        var result = await _cli.RunMaxWithConfigAsync("jobs", "modify", "NonExistent", "--source", "C:\\test");

        // Assert
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("not found", result.StandardError);
    }

    [Fact]
    public async Task JobsModify_WithNoConfigFile_ReturnsError()
    {
        // Act
        var result = await _cli.RunMaxWithConfigAsync("jobs", "modify", "TestJob", "--source", "C:\\test");

        // Assert
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("No jobs found", result.StandardError);
    }

    [Fact]
    public async Task JobsModify_WithMissingName_ReturnsError()
    {
        // Act
        var result = await _cli.RunMaxWithConfigAsync("jobs", "modify");

        // Assert
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task JobsModify_WithMultipleOptions_UpdatesAll()
    {
        // Arrange
        var source1 = _cli.CreateSourceDirectory("source1");
        var source2 = _cli.CreateSourceDirectory("source2");
        var dest1 = _cli.CreateDestinationDirectory("dest1");
        var dest2 = _cli.CreateDestinationDirectory("dest2");
        _cli.CreateConfigFile(new TestJob("TestJob", source1, dest1));

        // Act
        var result = await _cli.RunMaxWithConfigAsync("jobs", "modify", "TestJob", 
            "--source", source2, 
            "--destination", dest2);

        // Assert
        Assert.Equal(0, result.ExitCode);
        var config = _cli.ReadConfigFile();
        Assert.Contains(source2.Replace("\\", "\\\\"), config);
        Assert.Contains(dest2.Replace("\\", "\\\\"), config);
    }

    #endregion

    #region jobs delete

    [Fact]
    public async Task JobsDelete_WithExistingJob_DeletesJob()
    {
        // Arrange
        var source = _cli.CreateSourceDirectory();
        var dest = _cli.CreateDestinationDirectory();
        _cli.CreateConfigFile(new TestJob("JobToDelete", source, dest));

        // Act
        var result = await _cli.RunMaxWithConfigAsync("jobs", "delete", "JobToDelete");

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Job 'JobToDelete' deleted", result.StandardOutput);
        
        var config = _cli.ReadConfigFile();
        Assert.DoesNotContain("JobToDelete", config);
    }

    [Fact]
    public async Task JobsDelete_WithNonExistentJob_ReturnsError()
    {
        // Arrange
        _cli.CreateEmptyConfigFile();

        // Act
        var result = await _cli.RunMaxWithConfigAsync("jobs", "delete", "NonExistent");

        // Assert
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("not found", result.StandardError);
    }

    [Fact]
    public async Task JobsDelete_WithNoConfigFile_ReturnsError()
    {
        // Act
        var result = await _cli.RunMaxWithConfigAsync("jobs", "delete", "TestJob");

        // Assert
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("No jobs found", result.StandardError);
    }

    [Fact]
    public async Task JobsDelete_WithMissingName_ReturnsError()
    {
        // Act
        var result = await _cli.RunMaxWithConfigAsync("jobs", "delete");

        // Assert
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task JobsDelete_PreservesOtherJobs()
    {
        // Arrange
        var source1 = _cli.CreateSourceDirectory("source1");
        var dest1 = _cli.CreateDestinationDirectory("dest1");
        var source2 = _cli.CreateSourceDirectory("source2");
        var dest2 = _cli.CreateDestinationDirectory("dest2");
        
        _cli.CreateConfigFile(
            new TestJob("KeepMe", source1, dest1),
            new TestJob("DeleteMe", source2, dest2)
        );

        // Act
        var result = await _cli.RunMaxWithConfigAsync("jobs", "delete", "DeleteMe");

        // Assert
        Assert.Equal(0, result.ExitCode);
        var config = _cli.ReadConfigFile();
        Assert.Contains("KeepMe", config);
        Assert.DoesNotContain("DeleteMe", config);
    }

    #endregion

}
