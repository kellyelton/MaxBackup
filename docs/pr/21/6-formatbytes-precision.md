# Review: FormatBytes Precision

**File:** `MaxBackup.ServiceApp/BackupExecutor.cs`  
**Line:** 299-305  
**Comment ID:** 2683822824  
**Status:** [x] Open  | [x] Accepted  | [ ] Rejected  | [ ] Deferred

## Copilot's Comment

The division operations for byte formatting will truncate to integers, losing precision. For example, 1536 bytes would show as "1 KB" instead of "1.5 KB". Consider using decimal division and formatting with appropriate precision for better user experience.

**Suggestion:**
```csharp
if (bytes < 1024)
{
    return $"{bytes} bytes";
}

double value = bytes;
string[] units = { "bytes", "KB", "MB", "GB", "TB", "PB" };
int unitIndex = 0;

while (value >= 1024d && unitIndex < units.Length - 1)
{
    value /= 1024d;
    unitIndex++;
}

return $"{value:0.##} {units[unitIndex]}";
```

## Discussion

<!-- Agent and user discuss the issue here -->

**Current behavior:**
- 1536 bytes → "1 KB" (truncated)
- 1.5 GB → "1 GB" (truncated)

**Copilot's suggestion:**
- 1536 bytes → "1.5 KB"
- 1.5 GB → "1.5 GB"

**Options:**
1. **Accept the suggestion** - Better precision for user display
2. **Keep as-is** - Integer KB/MB/GB is sufficient for logs
3. **Compromise** - Use 1 decimal place: "1.5 KB"

**User decision:** Accept with modification - keep the switch expression pattern (simpler) but use floating-point division. Add unit tests for debugging future issues.

## Resolution

**Decision:** Accepted (modified)  
**Approach:** Updated to use floating-point division with format `0.#` (one decimal place). Kept switch expression instead of loop. Added 16 unit tests covering bytes/KB/MB/GB ranges.  
**Committed:** Pending

---
*PR #21 | Created: 2026-01-12*
