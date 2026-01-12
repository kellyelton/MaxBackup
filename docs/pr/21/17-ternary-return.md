# Review: Use Ternary for Branches

**File:** `Max/ProviderCommand.cs`  
**Line:** 104-111  
**Comment ID:** 2683822922  
**Status:** [ ] Open  | [ ] Accepted  | [ ] Rejected  | [ ] Deferred

## Copilot's Comment

Both branches of this 'if' statement return - consider using '?' to express intent better.

**Suggestion:**
```csharp
return await (isInteractive
    ? RunInteractiveAsync(config, configFilePath, name, accountName, accountKey, container, blobPrefix)
    : RunNonInteractiveAsync(config, configFilePath, type!, name!, accountName, accountKey, container, blobPrefix));
```

## Discussion

**Current code:**
```csharp
if (isInteractive)
{
    return await RunInteractiveAsync(...);
}
else
{
    return await RunNonInteractiveAsync(...);
}
```

**Analysis:**
- Both work identically
- The if/else version is more readable with long parameter lists
- The ternary is more compact but harder to read

**Options:**
1. **Keep if/else** - More readable for complex calls
2. **Use ternary** - More concise

## Resolution

**Decision:** (pending)  
**Approach:** (to be determined)  
**Committed:** No

---
*PR #21 | Created: 2026-01-12*
