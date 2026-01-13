# Review: Time Comparison Tolerance

**File:** `MaxBackup.ServiceApp/BackupExecutor.cs`  
**Line:** 216  
**Comment ID:** 2683822877  
**Status:** [ ] Open  | [x] Accepted  | [ ] Rejected  | [ ] Deferred

## Copilot's Comment

The time comparison tolerance of 2 seconds may be too tight for some cloud storage systems where metadata could have slight timestamp drift. Additionally, using Math.Abs on a TimeSpan's TotalSeconds could mask issues if timestamps are significantly different. Consider increasing the tolerance to 3-5 seconds or using a more robust comparison that handles clock skew.

**Suggestion:**
```csharp
var timeDifference = (localMtime - remoteFile.OriginalMtimeUtc).Duration();
var timesMatch = timeDifference <= TimeSpan.FromSeconds(5);
```

## Discussion

**Current code:**
```csharp
var timesMatch = Math.Abs((localMtime - remoteFile.OriginalMtimeUtc).TotalSeconds) < 2;
```

**Analysis:**
- We store the exact mtime in blob metadata in ISO 8601 format
- We're comparing our own stored metadata, not Azure's blob timestamp
- Since we control both write and read of the mtime, there's no cloud drift
- Any tolerance could actually cause bugs - files modified within the tolerance window would be incorrectly skipped

**User decision:** Remove tolerance entirely. Use exact timestamp comparison since we store and retrieve our own metadata.

## Resolution

**Decision:** Modified  
**Approach:** Removed tolerance entirely, using exact equality comparison (`==`) instead of tolerance-based comparison. Added comment explaining why no tolerance is used to prevent future regression.
**Committed:** No

**Change made:**
```csharp
// Before
var timesMatch = Math.Abs((localMtime - remoteFile.OriginalMtimeUtc).TotalSeconds) < 2;

// After (with explanatory comment)
// Note: We use exact timestamp comparison (no tolerance) because we store the exact
// mtime in blob metadata ourselves. Any tolerance could skip legitimately changed files.
var timesMatch = localMtime == remoteFile.OriginalMtimeUtc;
```

---
*PR #21 | Created: 2026-01-12 | Resolved: 2026-01-12*
