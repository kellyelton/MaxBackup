# Review: Windows-Only Platform Check

**File:** MaxBackup.Shared/CredentialProtection.cs
**Line:** 10-11
**Comment ID:** 2683822783
**Status:** [ ] Open | [x] Accepted | [ ] Rejected | [ ] Deferred

## Copilot's Comment

The CredentialProtection class uses Windows DPAPI, which is not available on other platforms. While the class is marked with [SupportedOSPlatform(\ windows\)], we should decide how to handle this explicitly.

## Discussion

User: Yeah let's just ignore and mark this as resolved. We will get warnings or errors if we try and use this code on another platform anyways, there's no need to do anything about this.

## Resolution

**Decision:** Rejected (No code changes needed)
**Approach:** MaxBackup is currently a Windows-only application. The existing [SupportedOSPlatform(\windows\)] attribute is sufficient for build-time and IDE warnings. Explicit runtime guards are deemed unnecessary at this stage.
**Committed:** Yes

---
*PR #21 | Created: 2026-01-12*
