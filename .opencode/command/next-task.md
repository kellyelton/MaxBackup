---
description: Start the next task from the backlog
---

EXECUTE THE FOLLOWING WORKFLOW IMMEDIATELY. Do not ask clarifying questions - just start Step 1.

Arguments provided: $ARGUMENTS

## Step 1: Check Current Git State

```powershell
git status
```

If uncommitted changes exist, inform user and ask how to proceed.

## Step 2: Read the Task Backlog

Read `docs/tasks.md` and find the first unchecked `[ ]` task.

## Step 3: Read Task Details

Read `docs/tasks/<task-name>.md` to understand:
- What needs to be done (Summary)
- Why (Context)
- Specific requirements (Completion Requirements)
- Which files to modify
- Dependencies

## Step 4: Create Feature Branch

Run `/new-branch` with the task name. Branch should match task filename (e.g., `feature/aws-s3-provider-core`).

## Step 5: Do the Work

Implement changes per the Completion Requirements checklist.

```powershell
dotnet build MaxBackup.sln --verbosity minimal
```

## Step 6: Run Tests

```powershell
dotnet test Max.IntegrationTests --verbosity minimal
```

Fix any failures before proceeding.

## Step 7: Commit and PR

Run `/commit-push-pr` to commit, push, and create PR.

## Step 8: After PR Merged

Edit `docs/tasks.md` and check off the task:
```markdown
- [x] [task-name](tasks/task-name.md) - description *(completed YYYY-MM-DD)*
```

Clean up:
```powershell
git switch master
git pull --ff-only origin master
git branch -d feature/<task-name>
```
