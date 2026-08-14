using MaxBackup.ServiceApp;
using MaxBackup.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Max.IntegrationTests;

public class ServiceRootConfigTests
{
    [Fact]
    public void ConfigurationBinding_CreatesTypedAzureProvider()
    {
        var values = new Dictionary<string, string?>
        {
            ["Providers:0:Type"] = "azure-blob",
            ["Providers:0:Name"] = "azurebackup420",
            ["Providers:0:AccountName"] = "storage-account",
            ["Providers:0:AccountKey"] = "account-key",
            ["Providers:0:ContainerName"] = "home"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var services = new ServiceCollection();
        services.ConfigureServiceRootConfig(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var rootConfig = serviceProvider.GetRequiredService<IOptions<ServiceRootConfig>>().Value;

        var provider = Assert.IsType<AzureBlobProviderConfig>(Assert.Single(rootConfig.Providers));
        Assert.Equal("azurebackup420", provider.Name);
        Assert.Equal("storage-account", provider.AccountName);
        Assert.Equal("home", provider.ContainerName);
    }
}
