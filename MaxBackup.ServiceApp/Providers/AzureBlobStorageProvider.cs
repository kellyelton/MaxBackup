using System.Runtime.CompilerServices;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using MaxBackup.Shared;
using Microsoft.Extensions.Logging;

namespace MaxBackup.ServiceApp.Providers;

/// <summary>
/// Azure Blob Storage implementation of IStorageProvider.
/// Follows the design outlined in docs/AzureBlobStorageProvider.md.
/// </summary>
public class AzureBlobStorageProvider : IStorageProvider
{
    private readonly BlobContainerClient _containerClient;
    private readonly string? _prefix;
    private readonly ILogger? _logger;
    private readonly string _containerName;

    // Metadata keys as defined in the design doc
    private const string MetadataOriginalSize = "mb_orig_size";
    private const string MetadataOriginalMtime = "mb_orig_mtime_utc";
    private const string MetadataSource = "mb_source";

    public AzureBlobStorageProvider(AzureBlobProviderConfig config, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(config);

        var connectionString = config.BuildConnectionString();
        _containerClient = new BlobContainerClient(connectionString, config.ContainerName);
        _containerName = config.ContainerName;
        _prefix = config.BlobPrefix?.Trim('/');
        _logger = logger;
    }

    /// <summary>
    /// Creates a provider instance for testing/verification purposes.
    /// </summary>
    public static AzureBlobStorageProvider Create(AzureBlobProviderConfig config, ILogger? logger = null)
    {
        return new AzureBlobStorageProvider(config, logger);
    }

    public async Task<ProviderTestResult> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            _logger?.LogDebug("Testing connection to Azure Blob Storage container '{Container}'", _containerName);

            // Try to create the container if it doesn't exist
            var response = await _containerClient.CreateIfNotExistsAsync(
                PublicAccessType.None,
                cancellationToken: ct);

            if (response?.Value != null)
            {
                _logger?.LogInformation("Created container '{Container}'", _containerName);
                return new ProviderTestResult(true, Details: $"Container '{_containerName}' created");
            }
            else
            {
                // Container already exists, verify we can list blobs
                await _containerClient.GetBlobsAsync(cancellationToken: ct)
                    .GetAsyncEnumerator(ct)
                    .MoveNextAsync();

                _logger?.LogDebug("Connection to container '{Container}' successful", _containerName);
                return new ProviderTestResult(true, Details: $"Container '{_containerName}' exists");
            }
        }
        catch (RequestFailedException ex)
        {
            _logger?.LogError(ex, "Azure Blob Storage connection failed: {Message}", ex.Message);
            return new ProviderTestResult(false, GetFriendlyErrorMessage(ex));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error testing Azure Blob Storage connection");
            return new ProviderTestResult(false, ex.Message);
        }
    }

    public async Task<RemoteFileInfo?> GetFileInfoAsync(string relativePath, CancellationToken ct = default)
    {
        var blobName = BuildBlobName(relativePath);
        var blobClient = _containerClient.GetBlobClient(blobName);

        try
        {
            _logger?.LogDebug("Getting properties for blob '{BlobName}'", blobName);
            var properties = await blobClient.GetPropertiesAsync(cancellationToken: ct);
            return ParseRemoteFileInfo(relativePath, properties.Value.Metadata);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger?.LogDebug("Blob '{BlobName}' not found", blobName);
            return null;
        }
    }

    public async Task UploadFileAsync(
        string localPath,
        string relativePath,
        FileInfo localFileInfo,
        IProgress<long>? progress = null,
        CancellationToken ct = default)
    {
        var blobName = BuildBlobName(relativePath);
        var blobClient = _containerClient.GetBlobClient(blobName);

        _logger?.LogDebug("Uploading '{LocalPath}' to blob '{BlobName}'", localPath, blobName);

        var metadata = new Dictionary<string, string>
        {
            [MetadataOriginalSize] = localFileInfo.Length.ToString(),
            [MetadataOriginalMtime] = localFileInfo.LastWriteTimeUtc.ToString("O"),
            [MetadataSource] = Environment.MachineName
        };

        var uploadOptions = new BlobUploadOptions
        {
            Metadata = metadata,
            ProgressHandler = progress != null
                ? new Progress<long>(bytesTransferred => progress?.Report(bytesTransferred))
                : null
        };

        await using var stream = File.OpenRead(localPath);
        await blobClient.UploadAsync(stream, uploadOptions, ct);

        _logger?.LogInformation("Uploaded '{RelativePath}' ({Size} bytes) to Azure Blob Storage",
            relativePath, localFileInfo.Length);
    }

    public async IAsyncEnumerable<RemoteFileInfo> ListFilesAsync(
        string? prefix = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var searchPrefix = BuildBlobName(prefix ?? string.Empty);

        _logger?.LogDebug("Listing blobs with prefix '{Prefix}'", searchPrefix);

        await foreach (var blobItem in _containerClient.GetBlobsAsync(
            traits: BlobTraits.Metadata,
            prefix: searchPrefix,
            cancellationToken: ct))
        {
            var relativePath = ExtractRelativePath(blobItem.Name);
            var fileInfo = ParseRemoteFileInfo(relativePath, blobItem.Metadata);

            if (fileInfo != null)
            {
                yield return fileInfo;
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        // BlobContainerClient doesn't require disposal
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Builds the full blob name from a relative path, including the configured prefix.
    /// </summary>
    private string BuildBlobName(string relativePath)
    {
        // Normalize: use forward slashes, trim leading/trailing slashes
        var normalized = relativePath
            .Replace('\\', '/')
            .Trim('/');

        if (string.IsNullOrEmpty(_prefix))
        {
            return normalized;
        }

        return string.IsNullOrEmpty(normalized)
            ? _prefix
            : $"{_prefix}/{normalized}";
    }

    /// <summary>
    /// Extracts the relative path from a blob name by removing the prefix.
    /// </summary>
    private string ExtractRelativePath(string blobName)
    {
        if (string.IsNullOrEmpty(_prefix))
        {
            return blobName;
        }

        var prefixWithSlash = _prefix + "/";
        return blobName.StartsWith(prefixWithSlash, StringComparison.Ordinal)
            ? blobName[prefixWithSlash.Length..]
            : blobName;
    }

    /// <summary>
    /// Parses remote file info from blob metadata.
    /// </summary>
    private RemoteFileInfo? ParseRemoteFileInfo(string relativePath, IDictionary<string, string> metadata)
    {
        if (metadata.TryGetValue(MetadataOriginalSize, out var sizeStr) &&
            metadata.TryGetValue(MetadataOriginalMtime, out var mtimeStr) &&
            long.TryParse(sizeStr, out var size) &&
            DateTime.TryParse(mtimeStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var mtime))
        {
            return new RemoteFileInfo(relativePath, size, mtime.ToUniversalTime());
        }

        // Blob exists but doesn't have our metadata - treat as missing
        _logger?.LogWarning("Blob '{RelativePath}' missing MaxBackup metadata", relativePath);
        return null;
    }

    /// <summary>
    /// Converts Azure SDK errors to user-friendly messages.
    /// </summary>
    private static string GetFriendlyErrorMessage(RequestFailedException ex) =>
        Shared.AzureErrorHelper.GetFriendlyErrorMessage(ex);
}
