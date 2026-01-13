using MaxBackup.Shared;
using NUnit.Framework;
using System.Security.Cryptography;
using System.Runtime.Versioning;

namespace MaxBackup.Shared.Tests;

[TestFixture]
[SupportedOSPlatform("windows")]
public class CredentialProtectionTests
{
    [Test]
    public void Encrypt_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CredentialProtection.Encrypt(null!));
    }

    [Test]
    public void Encrypt_Empty_ReturnsEncryptedString()
    {
        var result = CredentialProtection.Encrypt("");
        Assert.That(result, Does.StartWith("enc:"));
    }

    [Test]
    public void Encrypt_RoundTrip_EmptyString()
    {
        var original = "";
        var encrypted = CredentialProtection.Encrypt(original);
        var decrypted = CredentialProtection.Decrypt(encrypted);
        Assert.That(decrypted, Is.EqualTo(original));
    }

    [Test]
    public void Encrypt_RoundTrip_NormalString()
    {
        var original = "SecretPassword123!";
        var encrypted = CredentialProtection.Encrypt(original);
        var decrypted = CredentialProtection.Decrypt(encrypted);
        Assert.That(decrypted, Is.EqualTo(original));
    }

    [Test]
    public void Decrypt_PlainEmptyString_ReturnsEmptyString()
    {
        // Backward compatibility: existing plain text empty strings should be handled correctly
        var result = CredentialProtection.Decrypt("");
        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void IsEncrypted_EncryptedEmptyString_ReturnsTrue()
    {
        var encrypted = CredentialProtection.Encrypt("");
        Assert.That(CredentialProtection.IsEncrypted(encrypted), Is.True);
    }

    [Test]
    public void IsEncrypted_PlainEmptyString_ReturnsFalse()
    {
        Assert.That(CredentialProtection.IsEncrypted(""), Is.False);
    }

    [Test]
    public void TryDecrypt_EncryptedEmptyString_ReturnsEmptyString()
    {
        var encrypted = CredentialProtection.Encrypt("");
        var result = CredentialProtection.TryDecrypt(encrypted, out var error);
        Assert.That(result, Is.EqualTo(""));
        Assert.That(error, Is.Null);
    }
}
