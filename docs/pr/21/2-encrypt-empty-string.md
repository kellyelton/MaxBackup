# Review: Empty String Handling in Encrypt

**File:** `MaxBackup.Shared/CredentialProtection.cs`  
**Line:** 21  
**Comment ID:** 2683822790  
**Status:** [x] Accepted

## Copilot's Comment

The Encrypt method returns the plainText unchanged if it's null or empty. This means empty strings won't get the "enc:" prefix, which could cause issues with IsEncrypted checks. Consider returning "enc:" + empty base64 for empty strings, or document this behavior clearly.

**Suggestion:**
```csharp
if (plainText is null)
```

## Discussion

<!-- Agent and user discuss the issue here -->

**Current behavior:**
- `Encrypt("")` → returns `""`
- `IsEncrypted("")` → returns `false`
- `Decrypt("")` → returns `""`

**Is this a problem?**
- Empty account keys would fail validation before reaching encryption
- The CLI requires a non-empty key
- Round-trip: empty → encrypt → decrypt → empty ✓

**Options:**
1. **Keep as-is** - Empty strings are validated before encryption
2. **Change to null-only** - Use `is null` instead of `IsNullOrEmpty`
3. **Encrypt empty strings** - Return `enc:` + base64 of empty

## Resolution

**Decision:** Accepted
**Approach:** Throw ArgumentNullException on null, encrypt empty strings. Created NUnit test project.
**Committed:** Yes

---
*PR #21 | Created: 2026-01-12*
