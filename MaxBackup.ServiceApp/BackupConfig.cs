namespace MaxBackup.ServiceApp
{
    public class BackupConfig
    {
        public BackupJobConfig[] Jobs { get; set; } = Array.Empty<BackupJobConfig>();
    }
}