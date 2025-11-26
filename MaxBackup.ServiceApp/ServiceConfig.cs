namespace MaxBackup.ServiceApp;

public class ServiceConfig
{
    public int PipeTimeoutSeconds { get; set; } = 30;
    public int WorkerShutdownTimeoutSeconds { get; set; } = 60;
    public List<UserRegistration> RegisteredUsers { get; set; } = new();
}
