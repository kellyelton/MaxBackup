using MaxBackup.Shared;
using Microsoft.Extensions.Logging;

namespace MaxBackup.ServiceApp.Providers;

/// <summary>
/// Factory for creating storage provider instances from configuration.
/// </summary>
public class StorageProviderFactory
{
    private readonly IReadOnlyDictionary<string, ProviderConfig> _providers;
    private readonly ILoggerFactory? _loggerFactory;

    public StorageProviderFactory(IEnumerable<ProviderConfig> providers, ILoggerFactory? loggerFactory = null)
    {
        _providers = providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Creates a storage provider instance for the given provider name.
    /// </summary>
    /// <param name="providerName">Name of the provider from config.</param>
    /// <returns>The storage provider, or null if not found.</returns>
    public IStorageProvider? CreateProvider(string providerName)
    {
        if (!_providers.TryGetValue(providerName, out var config))
        {
            return null;
        }

        return config switch
        {
            AzureBlobProviderConfig azure => new AzureBlobStorageProvider(
                azure, 
                _loggerFactory?.CreateLogger<AzureBlobStorageProvider>()),
            _ => null
        };
    }

    /// <summary>
    /// Checks if a provider with the given name exists.
    /// </summary>
    public bool HasProvider(string providerName)
    {
        return _providers.ContainsKey(providerName);
    }

    /// <summary>
    /// Gets all provider names.
    /// </summary>
    public IEnumerable<string> GetProviderNames() => _providers.Keys;
}
