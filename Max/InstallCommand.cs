using System.CommandLine;
using System.Diagnostics;

namespace Max;

public class InstallCommand : Command
{
    public const string EV_InstallLocation = "MaxPath";
    public const string EXE_NAME = "max.exe";

    public readonly Version ThisVersion;

    public InstallCommand()
        : base("install", "Install Max")
    {
        var ass = typeof(InstallCommand).Assembly;
        ThisVersion = ass?.GetName().Version ?? new Version(0, 0, 0, 0);

        this.SetAction(parseResult =>
        {
            var current_install = GetInstallDetails();

            if (current_install is not null)
            {
                if (current_install.Version == ThisVersion)
                {
                    Console.WriteLine($"Already installed: {current_install.Version} {current_install.ExePath}");
                    return ReturnCodes.MaxAlreadyInstalled;
                }
                else if (current_install.Version > ThisVersion)
                {
                    Console.WriteLine($"Already installed: {current_install.Version} {current_install.ExePath}");
                    return ReturnCodes.MaxAlreadyInstalled;
                }
                else
                {
                    Console.WriteLine($"Older version installed: {current_install.Version} {current_install.ExePath}");
                }

                // Stop service
            }
            //TODO: Install
            // Create directories
            // Set envvar path
            // Add to %PATH% as well
            // Configure service
            // Start Service

            throw new NotImplementedException();
        });
    }

    public InstallDetails GetInstallDetails()
    {
        var details = new InstallDetails();

        var max_path = Environment.GetEnvironmentVariable(EV_InstallLocation);

        details.Path = max_path;

        if (max_path is null)
        {
            details.Status = InstallStatus.NotInstalled;
            return details;
        }

        if (string.IsNullOrWhiteSpace(max_path))
        {
            details.Status = InstallStatus.EmptyPath;
            return details;
        }

        max_path = Environment.ExpandEnvironmentVariables(max_path);
        max_path = Path.GetFullPath(max_path);

        details.FullPath = max_path;

        if (!Directory.Exists(max_path))
        {
            details.Status = InstallStatus.PathDirectoryMissing;
            return details;
        }

        var exe_path = Path.Combine(max_path, EXE_NAME);
        details.ExePath = exe_path;

        if (!File.Exists(exe_path))
        {
            details.Status = InstallStatus.MaxExeMissing;
            return details;
        }

        var version_info = FileVersionInfo.GetVersionInfo(exe_path);

        var version_string = version_info.ProductVersion;

        if (!Version.TryParse(version_string, out var version))
        {
            details.Status = InstallStatus.MaxExeInvalidVersion;
            return details;
        };

        details.Version = version;

        details.Status = InstallStatus.Installed;

        return details;
    }
}
