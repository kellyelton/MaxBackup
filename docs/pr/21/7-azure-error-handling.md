# Review: Missing 403/429 Error Handling

**File:** `MaxBackup.ServiceApp/Providers/AzureBlobStorageProvider.cs`  
**Line:** 99  
**Comment ID:** 2683822838  
**Status:** [ ] Open  | [ ] Accepted  | [ ] Rejected  | [ ] Deferred

## Copilot's Comment

The method checks for 404 errors when getting blob properties, but doesn't handle potential authorization failures (403) or throttling (429) errors that Azure may return. Consider adding specific handling for these common Azure error scenarios to provide better user feedback.

**Suggestion:** Add catch blocks for 403 and 429 status codes with descriptive logging.

## Discussion

<!-- Agent and user discuss the issue here -->

**Current behavior:**
- 404 → returns null (file doesn't exist) - correct
- 403/429/others → exception propagates up, generic error logged

**Considerations:**
- 403 during GetFileInfoAsync would fail early before uploads start
- 429 is rare for blob operations
- The Azure SDK has built-in retry for transient errors

**Options:**
1. **Add the suggested catch blocks** - Better error messages
2. **Keep as-is** - The SDK handles retries, errors bubble up anyway
3. **Log but don't catch** - Let it propagate with better context

## Resolution

**Decision:** (pending)  
**Approach:** (to be determined)  
**Committed:** No

---
*PR #21 | Created: 2026-01-12*
