---
description: Create a new feature branch from clean master
---

EXECUTE THE FOLLOWING WORKFLOW IMMEDIATELY. If no work description provided, ask user what they want to work on.

Arguments provided: $ARGUMENTS

## Step 1: Check for Uncommitted Changes

```powershell
git status --porcelain
```

**If output is NOT empty:** STOP and inform the user. Ask how to proceed: stash, commit, or discard.

## Step 2: Switch to Master

```powershell
git switch master
```

## Step 3: Fetch and Reset to Origin

```powershell
git fetch --all --prune --tags
git reset --hard origin/master
```

## Step 4: Create Feature Branch

Determine branch name from work description:

| Work Type | Branch Prefix | Example |
|-----------|---------------|---------|
| New feature | `feature/` | `feature/aws-s3-provider` |
| Bug fix | `fix/` | `fix/config-validation` |
| Documentation | `docs/` | `docs/api-reference` |
| Refactoring | `refactor/` | `refactor/storage-layer` |
| Tests | `test/` | `test/backup-executor` |
| Maintenance | `chore/` | `chore/update-dependencies` |

```powershell
git switch -c <type>/<branch-name>
```

## Step 5: Confirm Ready

Tell user: "Created branch `<branch-name>` from latest master. Ready to start work."
