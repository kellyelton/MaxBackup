using MaxBackup.ServiceApp;
using Microsoft.Extensions.Configuration.Json;
using Serilog;

IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options => {
        options.ServiceName = "MaxBackup";
    })
    .ConfigureAppConfiguration((context, config) => {
        //if (context.HostingEnvironment.IsProduction()) {
            var configpath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            configpath = Path.Combine(configpath, "maxbackupconfig.json");

            config.AddJsonFile(configpath, true, true);
        //}
    })
    .ConfigureHostConfiguration(config => {
        config.AddEnvironmentVariables("MAXBACKUP_");
    })
    .ConfigureServices(services => {
        services
            .AddOptions<BackupConfig>()
            .BindConfiguration("Backup")
            .ValidateDataAnnotations()
        ;
        services.AddHostedService<Worker>();
    })
    .ConfigureLogging((context, logging) => {
        logging.ClearProviders();

        var logger = new LoggerConfiguration()
            .ReadFrom
            .Configuration(context.Configuration)
            .CreateLogger()
        ;

        logging.AddSerilog(logger, true);
    })
    .Build();

var logger = host.Services.GetRequiredService<ILogger<IHost>>();

var config = (ConfigurationRoot)host.Services.GetRequiredService<IConfiguration>();

var env = host.Services.GetRequiredService<IHostEnvironment>();

logger.LogInformation("Environment: {env}", env.EnvironmentName);

var fileproviders = config.Providers
    .OfType<JsonConfigurationProvider>()
    .ToArray()
;

foreach (var provider in fileproviders) {
    logger.LogInformation("Config File: {file}", provider.Source.Path);
}

logger.LogInformation("Running");

await host.RunAsync();
