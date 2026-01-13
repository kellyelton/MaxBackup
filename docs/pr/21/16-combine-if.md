# Review: Combine If Statements

**File:** `Max/JobsCommand.cs`  
**Line:** 123-129  
**Comment ID:** 2683822904  
**Status:** [ ] Open  | [x] Accepted  | [ ] Rejected  | [ ] Deferred

## Copilot's Comment

These 'if' statements can be combined.

**Suggestion:**
```csharp
if (!string.IsNullOrEmpty(provider) &&
    (config.Providers == null || !config.Providers.Any(p => p.Name == provider)))
{
    Console.Error.WriteLine($"Provider '{provider}' not found...");
    return 1;
```

## Discussion

**Current code:**
```csharp
if (!string.IsNullOrEmpty(provider))
{
    if (config.Providers == null || !config.Providers.Any(p => p.Name == provider))
    {
        Console.Error.WriteLine(...);
        return 1;
    }
}
```

**Analysis:**
- Both versions are equally correct
- One-liner is more compact
- Nested version may be more readable

**Options:**
1. **Combine** - More concise
2. **Keep nested** - More readable with clear intent

## Resolution

**Decision:** Accepted  
**Approach:** Combined nested if statements as suggested  
**Committed:** Yes

---
*PR #21 | Created: 2026-01-12*
