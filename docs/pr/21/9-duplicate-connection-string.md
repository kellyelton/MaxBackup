# Review: Duplicate Connection String

**File:** `Max/ProviderCommand.cs`  
**Line:** 306  
**Comment ID:** 2683822852  
**Status:** [ ] Open  | [ ] Accepted  | [ ] Rejected  | [ ] Deferred

## Copilot's Comment

The connection string is built inline multiple times (lines 306, 497) with the same format. This duplicates the logic already present in AzureBlobProviderConfig.BuildConnectionString(). Consider using the BuildConnectionString method from the config object for consistency and maintainability.

## Discussion

<!-- Agent and user discuss the issue here -->

**Current situation:**
- `AzureBlobProviderConfig.BuildConnectionString()` exists in Shared project
- CLI duplicates the connection string format in 2 places or more

**Challenge:**
- CLI uses raw credentials for verification BEFORE saving the encrypted config
- Can't use BuildConnectionString because we don't have a config object yet

**Options:**
1. **Add static helper** - `AzureBlobProviderConfig.BuildConnectionString(accountName, accountKey)`
2. **Create temporary config** - Build a config object just to use the method
3. **Keep as-is** - The duplication is minor and intentional

## Resolution

**Decision:** (pending)  
**Approach:** (to be determined)  
**Committed:** No

---
*PR #21 | Created: 2026-01-12*
