# Review: Duplicate Regex Validation

**File:** `Max/ProviderCommand.cs`  
**Line:** 54  
**Comment ID:** 2683822810  
**Status:** [ ] Open  | [x] Accepted  | [ ] Rejected  | [ ] Deferred

## Copilot's Comment

The validation regex pattern on line 54 is duplicated from the GeneratedRegex pattern on line 44. This creates maintenance issues if the pattern needs to change. Remove the inline regex check and use the ProviderNameRegex() method directly in ValidateProviderName.

**Suggestion:**
```csharp
if (!ProviderNameRegex().IsMatch(name))
```

## Discussion

<!-- Agent and user discuss the issue here -->

**Problem:**
- `ProviderCommand.cs` duplicates the regex from `MaxBackup.Shared/ProviderConfig.cs`
- The CLI doesn't have access to the GeneratedRegex method in ProviderConfig

**Options:**
1. **Use existing ProviderConfig.ValidateNameGetError()** - Already exists in Shared project
2. **Keep duplication** - CLI is a separate project, some duplication is acceptable
3. **Create shared validation helper** - Extract to a utility in Shared

## Resolution

**Decision:** Accepted  
**Approach:** Removed the `ValidateProviderName()` method from `ProviderCommand.cs` and replaced calls with `MaxBackup.Shared.ProviderConfig.ValidateNameGetError()`. Used fully qualified name since there's a local `ProviderConfig` record in `JobsCommand.cs` that shadows the Shared type.  
**Committed:** No (pending commit)

---
*PR #21 | Created: 2026-01-12*
