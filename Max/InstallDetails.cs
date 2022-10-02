namespace Max;

public class InstallDetails
{
    public string? Path { get; internal set; }

    public InstallStatus Status { get; internal set; }

    public string FullPath { get; internal set; }

    public string ExePath { get; internal set; }

    public Version Version { get; internal set; }
}