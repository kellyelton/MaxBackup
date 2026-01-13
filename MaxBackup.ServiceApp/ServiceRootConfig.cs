using MaxBackup.Shared;

namespace MaxBackup.ServiceApp;

/// <summary>
/// Root configuration for the backup service, including providers.
/// </summary>
public class ServiceRootConfig
{
    public ProviderConfig[] Providers { get; set; } = Array.Empty<ProviderConfig>();
    public BackupConfig Backup { get; set; } = new BackupConfig();
}
