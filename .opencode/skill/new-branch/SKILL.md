---
name: new-branch
description: Create a new feature branch from clean master for the specified work
license: MIT
compatibility: opencode
metadata:
  workflow: git
  audience: developers
---

# New Branch Workflow

Sets up a clean feature branch from master for new work.

## When to Use

Use this skill when:
- Starting new work (feature, fix, docs, refactor, test)
- You need a clean branch from latest master
- The user runs `/new-branch` or `/new-branch feature-name`

## Prerequisites

**You MUST know what work you're starting.** If the user hasn't specified:
- Ask them what they want to work on
- They may point you to a task file in `docs/tasks/` - read it first

From the work description, determine:
1. **Branch type**: `feature/`, `fix/`, `docs/`, `refactor/`, `test/`, `chore/`
2. **Branch name**: kebab-case, descriptive

## Workflow Steps

### Step 1: Check for Uncommitted Changes

```powershell
git status --porcelain
```

**If output is NOT empty:**
- STOP and inform the user
- Ask how to proceed: stash, commit, or discard
- Do NOT proceed until resolved

**If output is empty**: proceed to next step.

### Step 2: Switch to Master

```powershell
git switch master
```

### Step 3: Fetch and Reset to Origin

```powershell
git fetch --all --prune --tags
```

```powershell
git reset --hard origin/master
```

This ensures you have the absolute latest master, discarding any local-only commits.

### Step 4: Create Feature Branch

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

Examples:
- `git switch -c feature/backup-executor-provider-integration`
- `git switch -c fix/config-validation-error`

### Step 5: Confirm Ready

Inform the user:
- "Created branch `<branch-name>` from latest master"
- "Ready to start work"

## Important Notes

- This workflow does a hard reset to origin/master - any local-only commits on master will be lost
- Always check for uncommitted changes first
- Branch names should be kebab-case and descriptive
- The branch type prefix is important for commit message prefixes later
