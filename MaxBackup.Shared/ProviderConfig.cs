using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MaxBackup.Shared;

/// <summary>
/// Base configuration for all storage providers.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "Type")]
[JsonDerivedType(typeof(AzureBlobProviderConfig), "azure-blob")]
public abstract partial class ProviderConfig
{
    /// <summary>
    /// Unique name for this provider instance (e.g., "azure-main", "personal-backup").
    /// Must be lowercase, alphanumeric with dashes/underscores, max 32 chars.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Validates the provider name format.
    /// </summary>
    public static bool IsValidName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (name.Length > 32) return false;
        return ProviderNameRegex().IsMatch(name);
    }

    /// <summary>
    /// Gets validation error message for the name, or null if valid.
    /// </summary>
    public static string? ValidateNameGetError(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Provider name cannot be empty";
        if (name.Length > 32)
            return "Provider name must be 32 characters or less";
        if (!ProviderNameRegex().IsMatch(name))
            return "Provider name must start with a lowercase letter and contain only lowercase letters, numbers, dashes, and underscores";
        return null;
    }

    // Regex: starts with lowercase letter, followed by lowercase alphanumeric, dash, or underscore
    [GeneratedRegex(@"^[a-z][a-z0-9_-]*$")]
    private static partial Regex ProviderNameRegex();
}

/// <summary>
/// Azure Blob Storage provider configuration.
/// </summary>
public class AzureBlobProviderConfig : ProviderConfig
{
    /// <summary>
    /// Azure Storage Account name.
    /// </summary>
    public required string AccountName { get; set; }

    /// <summary>
    /// Azure Storage Account access key.
    /// Note: In a future version, this should be stored encrypted.
    /// </summary>
    public required string AccountKey { get; set; }

    /// <summary>
    /// Container name for backups. Will be created if it doesn't exist.
    /// </summary>
    public required string ContainerName { get; set; }

    /// <summary>
    /// Optional prefix for all blob names (e.g., "maxbackup/mypc").
    /// </summary>
    public string? BlobPrefix { get; set; }

    /// <summary>
    /// Builds the connection string for this provider.
    /// </summary>
    public string BuildConnectionString()
    {
        return $"DefaultEndpointsProtocol=https;AccountName={AccountName};AccountKey={AccountKey};EndpointSuffix=core.windows.net";
    }
}
