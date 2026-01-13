# Review: Duplicate Error Message Mapping

**File:** `Max/ProviderCommand.cs`  
**Line:** 337-344  
**Comment ID:** 2683822857  
**Status:** [x] Accepted

## Copilot's Comment

The friendly error message mapping is duplicated between AzureBlobStorageProvider.GetFriendlyErrorMessage (line 222-233) and ProviderCommand.AddCommand.GetFriendlyErrorMessage (line 335-345). This code duplication creates a maintenance burden. Consider extracting this to a shared utility method or making it part of the provider's TestConnectionAsync result.

## Discussion

**Current situation:**
- `AzureBlobStorageProvider.GetFriendlyErrorMessage()` - private method
- `ProviderCommand.GetFriendlyErrorMessage()` - duplicate private method

**Options:**
1. **Make AzureBlobStorageProvider method public static** - CLI can use it
2. **Move to shared utility class** - e.g., `AzureErrorHelper.GetFriendlyMessage()`
3. **Move to MaxBackup.Shared** - Since CLI already references it
4. **Keep as-is** - The duplication is minor

User chose option 1 (combined with option 3): Create shared utility in MaxBackup.Shared.

## Resolution

**Decision:** Accepted  
**Approach:** Created `MaxBackup.Shared.AzureErrorHelper` with shared `GetFriendlyErrorMessage()` method. Updated both `ProviderCommand.cs` and `AzureBlobStorageProvider.cs` to delegate to it.  
**Committed:** Pending

---
*PR #21 | Created: 2026-01-12 | Resolved: 2026-01-12*
