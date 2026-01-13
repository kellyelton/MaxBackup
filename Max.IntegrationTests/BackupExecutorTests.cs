using MaxBackup.ServiceApp;

namespace Max.IntegrationTests;

/// <summary>
/// Tests for BackupExecutor utility methods
/// </summary>
public class BackupExecutorTests
{
    [Theory]
    [InlineData(0, "0 bytes")]
    [InlineData(1, "1 bytes")]
    [InlineData(512, "512 bytes")]
    [InlineData(1023, "1023 bytes")]
    public void FormatBytes_UnderOneKB_ReturnsBytes(long bytes, string expected)
    {
        Assert.Equal(expected, BackupExecutor.FormatBytes(bytes));
    }

    [Theory]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(2048, "2 KB")]
    [InlineData(10240, "10 KB")]
    [InlineData(1048575, "1024 KB")] // Just under 1 MB
    public void FormatBytes_KilobyteRange_ReturnsKB(long bytes, string expected)
    {
        Assert.Equal(expected, BackupExecutor.FormatBytes(bytes));
    }

    [Theory]
    [InlineData(1048576, "1 MB")]           // Exactly 1 MB
    [InlineData(1572864, "1.5 MB")]         // 1.5 MB
    [InlineData(10485760, "10 MB")]         // 10 MB
    [InlineData(1073741823, "1024 MB")]     // Just under 1 GB
    public void FormatBytes_MegabyteRange_ReturnsMB(long bytes, string expected)
    {
        Assert.Equal(expected, BackupExecutor.FormatBytes(bytes));
    }

    [Theory]
    [InlineData(1073741824, "1 GB")]        // Exactly 1 GB
    [InlineData(1610612736, "1.5 GB")]      // 1.5 GB
    [InlineData(10737418240, "10 GB")]      // 10 GB
    public void FormatBytes_GigabyteRange_ReturnsGB(long bytes, string expected)
    {
        Assert.Equal(expected, BackupExecutor.FormatBytes(bytes));
    }
}
