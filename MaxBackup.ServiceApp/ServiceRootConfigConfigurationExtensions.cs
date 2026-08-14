using MaxBackup.Shared;
using Microsoft.Extensions.Options;

namespace MaxBackup.ServiceApp;

public static class ServiceRootConfigConfigurationExtensions
{
    public static IServiceCollection ConfigureServiceRootConfig(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ServiceRootConfig>(configuration);
        services.PostConfigure<ServiceRootConfig>(config =>
        {
            config.Providers = BindProviders(configuration.GetSection("Providers"));
        });

        return services;
    }

    private static ProviderConfig[] BindProviders(IConfigurationSection providersSection)
    {
        return providersSection.GetChildren()
            .Select(BindProvider)
            .ToArray();
    }

    private static ProviderConfig BindProvider(IConfigurationSection providerSection)
    {
        var type = providerSection.GetValue<string>("Type");

        return type switch
        {
            "azure-blob" => providerSection.Get<AzureBlobProviderConfig>()
                ?? throw new OptionsValidationException(
                    nameof(ServiceRootConfig),
                    typeof(ServiceRootConfig),
                    ["Azure Blob provider configuration is invalid."]),
            _ => throw new OptionsValidationException(
                nameof(ServiceRootConfig),
                typeof(ServiceRootConfig),
                [$"Unsupported provider type '{type ?? "<missing>"}'."])
        };
    }
}
