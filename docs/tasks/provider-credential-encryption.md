# Provider Credential Encryption

## Summary

Encrypt sensitive provider credentials (like Azure `AccountKey`) when stored in `maxbackupconfig.json`.

## Context

Currently, Azure Storage account keys are stored in plain text in the config file. This is a security concern, especially on shared machines or if the config file is accidentally committed to source control.

## Approach Options

1. **Windows DPAPI** - Encrypt using current user's credentials (Windows-only, portable only within same user account)
2. **Machine-specific key** - Generate a machine key on first run, store encrypted credentials
3. **Azure Key Vault** - Store credentials in Key Vault (requires Azure subscription)
4. **Prompt for password** - Encrypt with user-provided password (requires password on each service start)

Recommended: **Windows DPAPI** for simplicity and security on Windows.

## Files to Modify

- `MaxBackup.Shared/ProviderConfig.cs` - Add encryption/decryption helpers
- `Max/ProviderCommand.cs` - Encrypt key before saving
- `MaxBackup.ServiceApp/` - Decrypt key when creating provider instance

## Acceptance Criteria

- [ ] Account keys are encrypted in the config file
- [ ] Plain text keys are auto-migrated to encrypted format on service start
- [ ] Decryption happens transparently when provider is instantiated
- [ ] Error handling for decryption failures (e.g., config moved to different machine)

## Technical Notes

```csharp
// Example using DPAPI
using System.Security.Cryptography;

var encrypted = ProtectedData.Protect(
    Encoding.UTF8.GetBytes(plainText),
    optionalEntropy: null,
    DataProtectionScope.CurrentUser);

var decrypted = ProtectedData.Unprotect(
    encrypted,
    optionalEntropy: null,
    DataProtectionScope.CurrentUser);
```

## Dependencies

Can be implemented independently of other tasks.
