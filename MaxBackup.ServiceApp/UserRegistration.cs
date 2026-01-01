namespace MaxBackup.ServiceApp;

public class UserRegistration
{
    public required string Sid { get; set; }
    public required string Username { get; set; }
    public required string ConfigPath { get; set; }
    public DateTime RegisteredAt { get; set; }
}
