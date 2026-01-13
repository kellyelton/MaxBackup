# Reorder Release Deploy Steps

## Summary
Move Chocolatey deploy step before WinGet deploy step in the release workflow.

## Context
Chocolatey has stricter moderation requirements and is more likely to fail (e.g., pending approvals block new submissions). By running Chocolatey first, we fail fast if there are issues, avoiding the situation where WinGet succeeds but Chocolatey fails.

## Completion Requirements
- [ ] Move "Push to Chocolatey" step before "Submit to WinGet" step in release.yml
- [ ] Verify workflow syntax is valid
- [ ] Test passes CI

## Files to Modify
- `.github/workflows/release.yml` - Reorder steps

## Technical Notes
Current order (lines 98-120):
1. Submit to WinGet (lines 98-104)
2. Push to Chocolatey (lines 106-120)

New order:
1. Push to Chocolatey
2. Submit to WinGet

## Dependencies
None
