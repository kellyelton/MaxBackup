# Review: Job Provider Documentation

**File:** `Max/JobsCommand.cs`  
**Line:** 254-260  
**Comment ID:** 2683822869  
**Status:** [ ] Open  | [ ] Accepted  | [ ] Rejected  | [ ] Deferred

## Copilot's Comment

The Job record now has a Provider parameter but there's no documentation explaining what happens when Provider is set versus when it's null. Add XML documentation comments to clarify that null means local backup and non-null means cloud backup to the specified provider.

## Discussion

<!-- Agent and user discuss the issue here -->

**Current code:**
```csharp
public record Job(
    string Name, 
    string Source, 
    string Destination, 
    string[] Include, 
    string[] Exclude,
    string? Provider = null);
```

**Options:**
1. **Add XML docs** - Document the Provider behavior
2. **Skip** - This is a CLI internal record, not public API
3. **Add docs to BackupJobConfig** - The actual config model in ServiceApp

## Resolution

**Decision:** (pending)  
**Approach:** (to be determined)  
**Committed:** No

---
*PR #21 | Created: 2026-01-12*
