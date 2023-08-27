using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.ServiceProcess;

namespace Max;

public class ServiceCommand : Command, ICommandHandler
{
    public const string ServiceName = "max backup";

    public ServiceCommand()
        : base("service", "Backup Service Controller") {
        Handler = this;
    }

    public int Invoke(InvocationContext context) {
        var services = ServiceController.GetServices();

        var mservices = services
            .Where(c => c.ServiceName.Equals(ServiceName, StringComparison.InvariantCultureIgnoreCase))
            .ToArray()
        ;

        if (mservices.Length == 0) {
            context.Console.WriteLine("Service is not install");
            return 1;
        }

        if (mservices.Length > 1) {
            context.Console.WriteLine("Multiple services found:");
            foreach (var s in mservices) {
                context.Console.WriteLine(s.ServiceName);
            }

            return -1;
        }

        var service = mservices[0];

        context.Console.WriteLine(service.ServiceName);

        switch (service.Status) {
            case ServiceControllerStatus.Stopped:
                context.Console.WriteLine("Service stopped");
                return 2;
            case ServiceControllerStatus.StartPending:
                context.Console.WriteLine("Service starting...");
                return 3;
            case ServiceControllerStatus.StopPending:
                context.Console.WriteLine("Service stopping...");
                return 4;
            case ServiceControllerStatus.Running:
                context.Console.WriteLine("Service running");
                return 0;
            case ServiceControllerStatus.ContinuePending:
                context.Console.WriteLine("Service continuing...");
                return 5;
            case ServiceControllerStatus.PausePending:
                context.Console.WriteLine("Service pausing...");
                return 6;
            case ServiceControllerStatus.Paused:
                context.Console.WriteLine("Service paused");
                return 7;
        }

        context.Console.Error.WriteLine($"Unexpected service status {service.Status}");

        return -2;
    }

    public Task<int> InvokeAsync(InvocationContext context) {
        var res = Invoke(context);

        return Task.FromResult(res);
    }
}
