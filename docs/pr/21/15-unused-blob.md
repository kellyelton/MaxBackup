# Review: Unused blob Variable

**File:** `Max/ProviderCommand.cs`  
**Line:** 506  
**Comment ID:** 2683822895  
**Status:** [ ] Open  | [ ] Accepted  | [ ] Rejected  | [ ] Deferred

## Copilot's Comment

This assignment to `blob` is useless, since its value is never read.

**Suggestion:**
```csharp
await foreach (var _ in containerClient.GetBlobsAsync().Take(10))
```

## Discussion

**Current code:**
```csharp
await foreach (var blob in containerClient.GetBlobsAsync().Take(10))
{
    blobCount++;
}
```

**Analysis:**
- `blob` variable is never used, only counting
- Using `_` discard is cleaner

**Options:**
1. **Use discard** - `var _` instead of `var blob`
2. **Keep as-is** - Minimal impact

## Resolution

**Decision:** (pending)  
**Approach:** (to be determined)  
**Committed:** No

---
*PR #21 | Created: 2026-01-12*
