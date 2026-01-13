---
description: Create a new feature branch from a clean master for the specified work
---

# New Branch Workflow

Sets up a clean feature branch from master for new work.

---

## Prerequisites

**You MUST know what work you're starting.** If the user hasn't specified:
- Ask them what they want to work on
- They may point you to a task file in `docs/tasks/` - read it first

From the work description, determine:
1. **Branch type**: `feature/`, `fix/`, `docs/`, `refactor/`, `test/`
2. **Branch name**: kebab-case, descriptive

---

## Step 1: Check for Uncommitted Changes

// turbo
```powershell
git status --porcelain
```

**If output is NOT empty:**
- STOP and inform the user
- Ask how to proceed: stash, commit, or discard

**If output is empty**: proceed.

---

## Step 2: Switch to Master

// turbo
```powershell
git switch master
```

---

## Step 3: Fetch and Reset to Origin

// turbo
```powershell
git fetch --all --prune --tags
```

```powershell
git reset --hard origin/master
```

---

## Step 4: Create Feature Branch

// turbo
```powershell
git switch -c <type>/<branch-name>
```

Examples:
- `git switch -c feature/backup-executor-provider-integration`
- `git switch -c fix/config-validation-error`

---

## Step 5: Confirm Ready

Inform the user:
- "Created branch `<branch-name>` from latest master"
- "Ready to start work"
