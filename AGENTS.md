# AI Agent Instructions

## ⚠️ STOP! Before Doing ANYTHING, Read This:

**Every time you start new work, you MUST follow these steps IN ORDER:**

### Step 0: Check where you are
```powershell
git status
```
Look at which branch you're on. If you're not on a new feature branch, DO NOT make any changes yet.

### Step 1: Get on master and update it
```powershell
git checkout master
git pull origin master
```

### Step 2: Create a new feature branch FROM MASTER
```powershell
git checkout -b feature/your-feature-name
```

### Step 3: NOW you can make changes
Only after completing steps 0-2.

**This applies to EVERY new task. No exceptions. Even "small" edits. Even documentation changes. ALWAYS.**

If you find yourself editing files and you haven't done these steps, STOP. Discard your changes and start over correctly.

## Branch Rules

- **master**: Protected. Stable releases. Never commit directly.
- **test**: Preview releases. Never commit directly.
- **Feature branches**: ALWAYS create from master. NEVER from test.

## Workflow for Changes

1. `git status` - Check where you are
2. `git checkout master && git pull origin master` - Get latest master
3. `git checkout -b feature/your-feature` - Create feature branch from master
4. Make changes and commit
5. `git checkout test && git pull origin test` - Get latest test
6. `git merge feature/your-feature && git push origin test` - Merge to test
7. Wait for Release workflow (list runs, pick correct one, watch it)
8. Verify preview release created
9. **`<wait on user>`** - Ask user if ready to create PR
10. Create PR from feature branch to master
11. Wait for CI workflow
12. **`<wait on user>`** - Ask user if ready to merge
13. Merge PR: `gh pr merge <number> --rebase`
14. Wait for Release workflow on master
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

## Quick Reference

```powershell
# Check branch
git status

# Workflow runs
gh run list --limit 5
gh run view <run-id>
gh run watch <run-id>

# PRs
gh pr create --base master --head feature/name --title "Title" --body "Body"
gh pr merge <number> --rebase

# Releases
gh release list --limit 3
```
