---
description: Start the next task from the backlog, following git best practices
---

# Next Task Workflow

Picks up the next task from the backlog, sets up git correctly, and guides you through completion.

---

## Phase 1: Preparation

### Step 1: Check Current Git State

// turbo
```powershell
git status
```

### Step 2: Read the Task Backlog

// turbo
```powershell
Get-Content docs/tasks.md
```

Find the first unchecked `[ ]` task.

### Step 3: Read the Task Details

// turbo
```powershell
Get-Content docs/tasks/<task-name>.md
```

### Step 4: Run New-Branch Workflow

Use `/new-branch` to set up a clean feature branch named after the task.

---

## Phase 2: Implementation

### Step 5: Do the Work

Implement changes, working through Completion Requirements.

// turbo
```powershell
dotnet build MaxBackup.sln --verbosity minimal
```

### Step 6: Run Tests

// turbo
```powershell
dotnet test Max.IntegrationTests --verbosity minimal
```

---

## Phase 3: Commit and PR

### Step 7: Run Commit-Push-PR Workflow

Use `/commit-push-pr` to commit, push, and create PR.

---

## Phase 4: Complete Task

After PR is merged:

### Step 8: Mark Task Complete

Edit `docs/tasks.md` and check off the task:
```markdown
- [x] [task-name](tasks/task-name.md) - description *(completed YYYY-MM-DD)*
```

### Step 9: Clean Up

// turbo
```powershell
git switch master
```

// turbo
```powershell
git pull --ff-only origin master
```

// turbo
```powershell
git branch -d feature/<task-name>
```