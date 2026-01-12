namespace MaxBackup.ServiceApp.Providers;

/// <summary>
/// Result of a provider connectivity test.
/// </summary>
/// <param name="Success">Whether the connection was successful.</param>
/// <param name="ErrorMessage">Error message if connection failed.</param>
/// <param name="Details">Additional details about the connection (e.g., container info).</param>
public record ProviderTestResult(
    bool Success,
    string? ErrorMessage = null,
    string? Details = null);

/// <summary>
/// Information about a remote file stored at the provider.
/// </summary>
/// <param name="RelativePath">The relative path of the file within the backup destination.</param>
/// <param name="OriginalSize">The original file size in bytes (from metadata).</param>
/// <param name="OriginalMtimeUtc">The original file's last modification time in UTC (from metadata).</param>
public record RemoteFileInfo(
    string RelativePath,
    long OriginalSize,
    DateTime OriginalMtimeUtc);

/// <summary>
/// Abstraction for storage providers that can receive backup files.
/// </summary>
public interface IStorageProvider : IAsyncDisposable
{
    /// <summary>
    /// Tests connectivity and returns any errors.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Test result indicating success or failure with details.</returns>
    Task<ProviderTestResult> TestConnectionAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks if a file exists at the destination and returns its metadata.
    /// Returns null if file doesn't exist.
    /// </summary>
    /// <param name="relativePath">The relative path within the destination.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>File info if exists, null otherwise.</returns>
    Task<RemoteFileInfo?> GetFileInfoAsync(string relativePath, CancellationToken ct = default);

    /// <summary>
    /// Uploads a file to the destination.
    /// </summary>
    /// <param name="localPath">Full path to the local file.</param>
    /// <param name="relativePath">Relative path for the destination.</param>
    /// <param name="localFileInfo">FileInfo for the local file (for metadata).</param>
    /// <param name="progress">Optional progress reporter for bytes uploaded.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UploadFileAsync(
        string localPath,
        string relativePath,
        FileInfo localFileInfo,
        IProgress<long>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Lists all files under a prefix for bulk comparison.
    /// This is more efficient than calling GetFileInfoAsync for each file.
    /// </summary>
    /// <param name="prefix">Optional prefix to filter results.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Async enumerable of remote file info.</returns>
    IAsyncEnumerable<RemoteFileInfo> ListFilesAsync(
        string? prefix = null,
        CancellationToken ct = default);
}
