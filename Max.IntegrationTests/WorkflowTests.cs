namespace Max.IntegrationTests;

/// <summary>
/// End-to-end workflow tests that combine multiple commands
/// </summary>
public class WorkflowTests : IAsyncLifetime
{
    private readonly CliTestHelper _cli;

    public WorkflowTests()
    {
        _cli = new CliTestHelper();
    }

    public Task InitializeAsync() => _cli.InitializeAsync();
    public Task DisposeAsync() => _cli.DisposeAsync();

    [Fact]
    public async Task Workflow_CreateListModifyDelete()
    {
        // Arrange
        var source = _cli.CreateSourceDirectory();
        var dest = _cli.CreateDestinationDirectory();
        var newDest = _cli.CreateDestinationDirectory("newdest");

        // Step 1: Create a job
        var createResult = await _cli.RunMaxWithConfigAsync("jobs", "create", "WorkflowJob", source, dest);
        Assert.Equal(0, createResult.ExitCode);
        Assert.Contains("created", createResult.StandardOutput);

        // Step 2: List jobs - should show the created job
        var listResult1 = await _cli.RunMaxWithConfigAsync("jobs", "list");
        Assert.Equal(0, listResult1.ExitCode);
        Assert.Contains("WorkflowJob", listResult1.StandardOutput);

        // Step 3: Modify the job
        var modifyResult = await _cli.RunMaxWithConfigAsync("jobs", "modify", "WorkflowJob", "--destination", newDest);
        Assert.Equal(0, modifyResult.ExitCode);
        Assert.Contains("modified", modifyResult.StandardOutput);

        // Step 4: Verify modification via list
        var listResult2 = await _cli.RunMaxWithConfigAsync("jobs", "list");
        Assert.Equal(0, listResult2.ExitCode);
        Assert.Contains("WorkflowJob", listResult2.StandardOutput);

        // Step 5: Delete the job
        var deleteResult = await _cli.RunMaxWithConfigAsync("jobs", "delete", "WorkflowJob");
        Assert.Equal(0, deleteResult.ExitCode);
        Assert.Contains("deleted", deleteResult.StandardOutput);

        // Step 6: Verify deletion - list should show no jobs
        var listResult3 = await _cli.RunMaxWithConfigAsync("jobs", "list");
        Assert.Equal(1, listResult3.ExitCode);
        Assert.Contains("No jobs found", listResult3.StandardError);
    }

    [Fact]
    public async Task Workflow_CreateMultipleJobs()
    {
        // Arrange
        var source1 = _cli.CreateSourceDirectory("src1");
        var dest1 = _cli.CreateDestinationDirectory("dst1");
        var source2 = _cli.CreateSourceDirectory("src2");
        var dest2 = _cli.CreateDestinationDirectory("dst2");
        var source3 = _cli.CreateSourceDirectory("src3");
        var dest3 = _cli.CreateDestinationDirectory("dst3");

        // Create multiple jobs
        var create1 = await _cli.RunMaxWithConfigAsync("jobs", "create", "Job1", source1, dest1);
        Assert.Equal(0, create1.ExitCode);

        var create2 = await _cli.RunMaxWithConfigAsync("jobs", "create", "Job2", source2, dest2);
        Assert.Equal(0, create2.ExitCode);

        var create3 = await _cli.RunMaxWithConfigAsync("jobs", "create", "Job3", source3, dest3);
        Assert.Equal(0, create3.ExitCode);

        // List all jobs
        var listResult = await _cli.RunMaxWithConfigAsync("jobs", "list");
        Assert.Equal(0, listResult.ExitCode);
        Assert.Contains("Job1", listResult.StandardOutput);
        Assert.Contains("Job2", listResult.StandardOutput);
        Assert.Contains("Job3", listResult.StandardOutput);

        // Delete middle job
        var deleteResult = await _cli.RunMaxWithConfigAsync("jobs", "delete", "Job2");
        Assert.Equal(0, deleteResult.ExitCode);

        // Verify Job1 and Job3 still exist
        var listResult2 = await _cli.RunMaxWithConfigAsync("jobs", "list");
        Assert.Equal(0, listResult2.ExitCode);
        Assert.Contains("Job1", listResult2.StandardOutput);
        Assert.DoesNotContain("Job2", listResult2.StandardOutput);
        Assert.Contains("Job3", listResult2.StandardOutput);
    }

    [Fact]
    public async Task Workflow_CreateJobWithAllOptions()
    {
        // Arrange
        var source = _cli.CreateSourceDirectory();
        var dest = _cli.CreateDestinationDirectory();

        // Create job with include and exclude patterns
        var createResult = await _cli.RunMaxWithConfigAsync("jobs", "create", "FullOptionsJob", 
            source, dest,
            "--include", "*.txt",
            "--include", "*.md",
            "--exclude", "*.tmp",
            "--exclude", "*.log");

        Assert.Equal(0, createResult.ExitCode);

        // Verify config file has all the patterns
        var config = _cli.ReadConfigFile();
        Assert.Contains("FullOptionsJob", config);
        Assert.Contains("*.txt", config);
        Assert.Contains("*.md", config);
        Assert.Contains("*.tmp", config);
        Assert.Contains("*.log", config);
    }

    [Fact]
    public async Task Workflow_ModifyPreservesUnchangedFields()
    {
        // Arrange
        var source = _cli.CreateSourceDirectory();
        var dest = _cli.CreateDestinationDirectory();
        _cli.CreateConfigFile(new TestJob("PreserveJob", source, dest, 
            new[] { "*.txt", "*.md" }, 
            new[] { "*.tmp" }));

        // Modify only the destination
        var newDest = _cli.CreateDestinationDirectory("newdest");
        var modifyResult = await _cli.RunMaxWithConfigAsync("jobs", "modify", "PreserveJob", "--destination", newDest);
        Assert.Equal(0, modifyResult.ExitCode);

        // Verify source and patterns are preserved
        var config = _cli.ReadConfigFile();
        Assert.Contains(source.Replace("\\", "\\\\"), config);
        Assert.Contains(newDest.Replace("\\", "\\\\"), config);
        Assert.Contains("*.txt", config);
        Assert.Contains("*.md", config);
        Assert.Contains("*.tmp", config);
    }

}
