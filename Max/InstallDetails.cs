namespace Max;

public class InstallDetails
{
    public string? Path { get; internal set; }

    public InstallStatus Status { get; internal set; }

    public string FullPath { get; internal set; } = string.Empty;

    public string ExePath { get; internal set; } = string.Empty;

    public Version Version { get; internal set; } = new Version(0, 0, 0, 0);
}