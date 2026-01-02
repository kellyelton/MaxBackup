# AI Agent Instructions

## ⚠️ STOP! Before Doing ANYTHING, Read This:

**Every time you start new work, you MUST follow these steps IN ORDER:**

### Step 0: Check where you are
```powershell
git status
```
Look at which branch you're on. If you're not on a new feature branch, DO NOT make any changes yet.

### Step 1: Get on master and sync with origin
```powershell
git switch master
git pull origin master
```
⚠️ **CRITICAL**: Master MUST be up to date with origin before creating a branch, or you WILL have merge conflicts later!

### Step 2: Create a new feature branch FROM MASTER
```powershell
git switch -c feature/your-feature-name
```

### Step 3: NOW you can make changes
Only after completing steps 0-2.

**This applies to EVERY new task. No exceptions. Even "small" edits. Even documentation changes. ALWAYS.**

### 🚨 Made Changes on Wrong Branch? Here's How to Recover:
```powershell
git stash                              # Save your changes
git switch master                      # Go to master
git pull origin master                 # Sync with origin
git switch -c feature/correct-branch   # Create correct branch
git stash pop                          # Restore your changes
```
Never lose work - always stash first!

## Branch Rules

- **master**: Protected. Stable releases. Never commit directly.
- **test**: Preview releases. Never commit directly.
- **Feature branches**: ALWAYS create from master. NEVER from test.

## Workflow for Changes

1. `git status` - Check where you are
2. `git switch master && git pull origin master` - Get latest master (MUST sync with origin!)
3. `git switch -c feature/your-feature` - Create feature branch from master
4. Make changes and commit
5. `git switch test && git pull origin test` - Get latest test
6. `git merge feature/your-feature && git push origin test` - Merge to test
7. Wait for Release workflow (list runs, pick correct one, watch it)
   - ℹ️ If only markdown/docs changed, Release workflow will be skipped automatically
8. Verify preview release created (if applicable)
9. **`<wait on user>`** - Ask user if ready to create PR
10. Create PR from feature branch to master
11. Wait for CI workflow
12. **`<wait on user>`** - Ask user if ready to merge
13. Merge PR: `gh pr merge <number> --rebase`
14. Wait for Release workflow on master (skipped for docs-only changes)
15. Verify stable release 🎉

## Critical Rules

### Always Ask User Before:
- Merging master into test (or vice versa)
- Hard resets (`git reset --hard`)
- Rebasing
- Force pushing (`git push --force`)

User must explicitly request these by name.

### Before Acting on Any Item:
Never grab 1 item and immediately act on it. Always:
1. List multiple items (e.g., `gh run list --limit 5`)
2. Verify you have the correct item
3. Then act

### gh run watch Needs a Run ID
`gh run watch` without arguments blocks. Always:
```powershell
gh run list --branch test --limit 3   # List first
gh run watch <run-id>                  # Then watch specific one
```

### Alternate Buffer Issue
If terminal shows "The command opened the alternate buffer", stop and tell user.
Use JSON output instead: `gh run list --json status,conclusion`

### Docs-Only Changes Skip Release
Changes to only markdown files (*.md), LICENSE, or .gitignore will NOT trigger a release.
The Release workflow uses path filters to skip unnecessary builds.

## Quick Reference

```powershell
# Check branch
git status

# Modern git commands (use these!)
git switch master                    # Switch to master
git switch -c feature/name           # Create and switch to new branch
git switch -                         # Switch to previous branch

# Recover from wrong branch
git stash && git switch master && git pull origin master && git switch -c feature/name && git stash pop

# Workflow runs
gh run list --limit 5
gh run list --json status,conclusion  # Avoids alternate buffer
gh run watch <run-id>

# PRs
gh pr create --base master --head feature/name --title "Title" --body "Body"
gh pr merge <number> --rebase

# Releases
gh release list --limit 3
```
