using MaxBackup.Shared;
using Xunit;

namespace Max.IntegrationTests;

public class CredentialProtectionTests
{
    [Fact]
    public void Encrypt_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CredentialProtection.Encrypt(null!));
    }

    [Fact]
    public void Encrypt_Empty_ReturnsEncryptedString()
    {
        var result = CredentialProtection.Encrypt("");
        Assert.NotNull(result);
        Assert.StartsWith("enc:", result);
    }

    [Fact]
    public void Encrypt_RoundTrip_EmptyString()
    {
        var original = "";
        var encrypted = CredentialProtection.Encrypt(original);
        var decrypted = CredentialProtection.Decrypt(encrypted);
        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void Decrypt_PlainEmptyString_ReturnsEmptyString()
    {
        // Backward compatibility
        var result = CredentialProtection.Decrypt("");
        Assert.Equal("", result);
    }

    [Fact]
    public void IsEncrypted_EncryptedEmptyString_ReturnsTrue()
    {
        var encrypted = CredentialProtection.Encrypt("");
        Assert.True(CredentialProtection.IsEncrypted(encrypted));
    }

    [Fact]
    public void TryDecrypt_EncryptedEmptyString_ReturnsEmptyString()
    {
        var encrypted = CredentialProtection.Encrypt("");
        var result = CredentialProtection.TryDecrypt(encrypted, out var error);
        Assert.Equal("", result);
        Assert.Null(error);
    }
}
