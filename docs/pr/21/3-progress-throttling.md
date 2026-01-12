# Review: Progress Callback Throttling

**File:** `MaxBackup.ServiceApp/BackupExecutor.cs`  
**Line:** 231-238  
**Comment ID:** 2683822801  
**Status:** [ ] Open  | [ ] Accepted  | [ ] Rejected  | [ ] Deferred

## Copilot's Comment

The progress callback creates a new Progress instance that reports on every byte transferred, which for large files could create excessive log entries. The conditional check on line 233 only filters which entries get logged, but the Progress object still fires for every chunk. Consider throttling the progress reporting at the Progress creation level to avoid unnecessary work.

## Discussion

<!-- Agent and user discuss the issue here -->

**Current behavior:**
- Creates `Progress<long>` for every file
- Azure SDK calls the progress handler per-chunk (typically 4MB blocks)
- Conditional inside handler prevents logging for small files
- For 1GB file: ~256 callbacks, but only logs ~256 times

**Analysis:**
- The Progress callback itself is lightweight (just a conditional + maybe a log call)
- Azure SDK chunks are typically 4MB, so not excessive
- The overhead is minimal compared to actual network I/O

**Options:**
1. **Keep as-is** - The overhead is negligible for 4MB chunk sizes
2. **Time-based throttle** - Only log every N seconds
3. **Percentage-based throttle** - Only log at 25%, 50%, 75%, 100%

## Resolution

**Decision:** (pending)  
**Approach:** (to be determined)  
**Committed:** No

---
*PR #21 | Created: 2026-01-12*
