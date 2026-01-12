# Review: DateTime UTC Parsing

**File:** `MaxBackup.ServiceApp/Providers/AzureBlobStorageProvider.cs`  
**Line:** 204-212  
**Comment ID:** 2683822847  
**Status:** [ ] Open  | [ ] Accepted  | [ ] Rejected  | [ ] Deferred

## Copilot's Comment

The blob metadata parsing doesn't validate that the parsed DateTime is actually in UTC. DateTime.TryParse can succeed with local time strings, and ToUniversalTime() on line 211 may introduce timezone conversion errors if the stored time wasn't originally in UTC format. Consider using DateTime.TryParseExact with DateTimeStyles.RoundtripKind to ensure proper UTC handling.

## Discussion

<!-- Agent and user discuss the issue here -->

**Current code:**
```csharp
DateTime.TryParse(mtimeStr, out var mtime)
...
return new RemoteFileInfo(relativePath, size, mtime.ToUniversalTime());
```

**The issue:**
- We store with `"O"` format (ISO 8601 round-trip) which includes `Z` for UTC
- Parsing should preserve this, but `TryParse` might misinterpret

**Better approach:**
```csharp
DateTime.TryParse(mtimeStr, null, DateTimeStyles.RoundtripKind, out var mtime)
```

**Options:**
1. **Use RoundtripKind** - More robust parsing
2. **Use TryParseExact with "O"** - Strictest validation
3. **Keep as-is** - Works in practice due to "O" format storage

## Resolution

**Decision:** (pending)  
**Approach:** (to be determined)  
**Committed:** No

---
*PR #21 | Created: 2026-01-12*
