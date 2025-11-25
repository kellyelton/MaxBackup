using System.CommandLine;
using System.ServiceProcess;

namespace Max;

public class ServiceCommand : Command
{
    public const string ServiceName = "max backup";

    public ServiceCommand()
        : base("service", "Backup Service Controller")
    {
        this.SetAction(parseResult =>
        {
            var services = ServiceController.GetServices();

            var mservices = services
                .Where(c => c.ServiceName.Equals(ServiceName, StringComparison.InvariantCultureIgnoreCase))
                .ToArray();

            if (mservices.Length == 0)
            {
                Console.WriteLine("Service is not install");
                return 1;
            }

            if (mservices.Length > 1)
            {
                Console.WriteLine("Multiple services found:");
                foreach (var s in mservices)
                {
                    Console.WriteLine(s.ServiceName);
                }
                return -1;
            }

            var service = mservices[0];

            Console.WriteLine(service.ServiceName);

            switch (service.Status)
            {
                case ServiceControllerStatus.Stopped:
                    Console.WriteLine("Service stopped");
                    return 2;
                case ServiceControllerStatus.StartPending:
                    Console.WriteLine("Service starting...");
                    return 3;
                case ServiceControllerStatus.StopPending:
                    Console.WriteLine("Service stopping...");
                    return 4;
                case ServiceControllerStatus.Running:
                    Console.WriteLine("Service running");
                    return 0;
                case ServiceControllerStatus.ContinuePending:
                    Console.WriteLine("Service continuing...");
                    return 5;
                case ServiceControllerStatus.PausePending:
                    Console.WriteLine("Service pausing...");
                    return 6;
                case ServiceControllerStatus.Paused:
                    Console.WriteLine("Service paused");
                    return 7;
            }

            Console.Error.WriteLine($"Unexpected service status {service.Status}");

            return -2;
        });
    }
}
