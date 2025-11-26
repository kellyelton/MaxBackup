using System.CommandLine;
using System.ServiceProcess;
using Spectre.Console;

namespace Max;

public class ServiceCommand : Command
{
    public const string ServiceName = "max backup";

    public ServiceCommand(Option<bool> verboseOption)
        : base("service", "Backup Service Controller")
    {
        // Add subcommands
        Subcommands.Add(new WatchCommand(verboseOption));
        Subcommands.Add(new ServiceStatusCommand());

        // Default action when no subcommand is provided - show service status
        this.SetAction(parseResult =>
        {
            return ServiceStatusCommand.CheckServiceStatus();
        });
    }
}

/// <summary>
/// Subcommand to check service status: max service status
/// </summary>
public class ServiceStatusCommand : Command
{
    public ServiceStatusCommand()
        : base("status", "Check the Windows service status")
    {
        this.SetAction(parseResult => CheckServiceStatus());
    }

    public static int CheckServiceStatus()
    {
        var services = ServiceController.GetServices();

        var mservices = services
            .Where(c => c.ServiceName.Equals(ServiceCommand.ServiceName, StringComparison.InvariantCultureIgnoreCase))
            .ToArray();

        if (mservices.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]✗[/] Service is not installed");
            return 1;
        }

        if (mservices.Length > 1)
        {
            AnsiConsole.MarkupLine("[yellow]Multiple services found:[/]");
            foreach (var s in mservices)
            {
                AnsiConsole.WriteLine($"  {s.ServiceName}");
            }
            return -1;
        }

        var service = mservices[0];

        var (statusColor, statusText, exitCode) = service.Status switch
        {
            ServiceControllerStatus.Stopped => ("red", "Stopped", 2),
            ServiceControllerStatus.StartPending => ("yellow", "Starting...", 3),
            ServiceControllerStatus.StopPending => ("yellow", "Stopping...", 4),
            ServiceControllerStatus.Running => ("green", "Running", 0),
            ServiceControllerStatus.ContinuePending => ("yellow", "Continuing...", 5),
            ServiceControllerStatus.PausePending => ("yellow", "Pausing...", 6),
            ServiceControllerStatus.Paused => ("yellow", "Paused", 7),
            _ => ("red", $"Unknown ({service.Status})", -2)
        };

        AnsiConsole.MarkupLine($"[bold]{service.ServiceName}[/]: [{statusColor}]{statusText}[/]");
        return exitCode;
    }
}
