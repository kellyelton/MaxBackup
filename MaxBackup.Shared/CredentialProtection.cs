using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace MaxBackup.Shared;

/// <summary>
/// Provides encryption/decryption for sensitive configuration values using Windows DPAPI.
/// </summary>
[SupportedOSPlatform("windows")]
public static class CredentialProtection
{
    private const string EncryptedPrefix = "enc:";

    /// <summary>
    /// Encrypts a value using DPAPI with CurrentUser scope.
    /// The encrypted value is returned as a Base64 string with "enc:" prefix.
    /// </summary>
    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = ProtectedData.Protect(
            plainBytes,
            optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);

        return EncryptedPrefix + Convert.ToBase64String(encryptedBytes);
    }

    /// <summary>
    /// Decrypts a value that was encrypted with Encrypt().
    /// If the value doesn't have the "enc:" prefix, it's returned as-is (plain text).
    /// </summary>
    public static string Decrypt(string encryptedValue)
    {
        if (string.IsNullOrEmpty(encryptedValue))
            return encryptedValue;

        // If not encrypted, return as-is (supports plain text for migration)
        if (!encryptedValue.StartsWith(EncryptedPrefix, StringComparison.Ordinal))
            return encryptedValue;

        var base64 = encryptedValue[EncryptedPrefix.Length..];
        var encryptedBytes = Convert.FromBase64String(base64);
        var plainBytes = ProtectedData.Unprotect(
            encryptedBytes,
            optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);

        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <summary>
    /// Checks if a value is encrypted (has the "enc:" prefix).
    /// </summary>
    public static bool IsEncrypted(string value)
    {
        return !string.IsNullOrEmpty(value) && 
               value.StartsWith(EncryptedPrefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// Attempts to decrypt a value, returning null if decryption fails.
    /// This is useful for graceful handling of config moved between machines.
    /// </summary>
    public static string? TryDecrypt(string encryptedValue, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrEmpty(encryptedValue))
            return encryptedValue;

        // Plain text - return as-is
        if (!encryptedValue.StartsWith(EncryptedPrefix, StringComparison.Ordinal))
            return encryptedValue;

        try
        {
            return Decrypt(encryptedValue);
        }
        catch (CryptographicException)
        {
            errorMessage = "Cannot decrypt credential. The config may have been created on a different machine or user account.";
            return null;
        }
        catch (FormatException)
        {
            errorMessage = "Invalid encrypted credential format.";
            return null;
        }
    }
}
