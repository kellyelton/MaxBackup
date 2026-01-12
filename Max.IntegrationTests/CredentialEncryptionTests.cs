using MaxBackup.Shared;
using System.Text.Json;
using Xunit;
using System.Runtime.Versioning;

namespace Max.IntegrationTests;

/// <summary>
/// Integration tests for credential encryption.
/// Verifies that credentials are encrypted when stored in config.
/// </summary>
public class CredentialEncryptionTests : IClassFixture<CliTestHelper>
{
    private readonly CliTestHelper _cli;

    public CredentialEncryptionTests(CliTestHelper cli)
    {
        _cli = cli;
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task AddProvider_EncryptsKeyInConfig()
    {
        // Arrange
        _cli.CreateEmptyConfigFile();
        var providerName = "test-azure-encrypt";
        
        // Act
        var result = await _cli.RunMaxWithConfigAsync(
            "provider", "add",
            "--type", "azure-blob",
            "--name", providerName,
            "--account-name", "testaccount",
            "--account-key", "test-key-123",
            "--container", "testcontainer",
            "--skip-test");

        // Assert
        Assert.True(result.IsSuccess, result.AllOutput);
        
        var configJson = _cli.ReadConfigFile();
        var config = JsonDocument.Parse(configJson);
        var providers = config.RootElement.GetProperty("Providers");
        
        var provider = providers.EnumerateArray().First(p => p.GetProperty("Name").GetString() == providerName);
        var accountKey = provider.GetProperty("AccountKey").GetString();
        
        Assert.StartsWith("enc:", accountKey);
        
        // Verify decryption round-trip works with the same user/machine
        var decrypted = CredentialProtection.Decrypt(accountKey!);
        Assert.Equal("test-key-123", decrypted);
    }
}
