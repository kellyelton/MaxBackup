---
name: next-task
description: Start the next task from the backlog, following git best practices
license: MIT
compatibility: opencode
metadata:
  workflow: task-management
  audience: developers
---

# Next Task Workflow

Picks up the next task from the backlog, sets up git correctly, and guides you through completion.

## When to Use

Use this skill when:
- You want to start working on the next item in the task backlog
- The user runs `/next-task`
- You need to find and begin the next piece of work

## Workflow Steps

### Phase 1: Preparation

#### Step 1: Check Current Git State

```powershell
git status
```

If there are uncommitted changes, inform the user and ask how to proceed before continuing.

#### Step 2: Read the Task Backlog

```powershell
cat docs/tasks.md
```

Or read the file directly. Find the first unchecked `[ ]` task in the list.

#### Step 3: Read the Task Details

Once you identify the task, read its detail file:

```powershell
cat docs/tasks/<task-name>.md
```

Understand:
- What needs to be done (Summary)
- Why it's needed (Context)
- Specific requirements (Completion Requirements)
- Which files to modify
- Any technical considerations
- Dependencies on other tasks

#### Step 4: Run New-Branch Workflow

Use `/new-branch` to set up a clean feature branch named after the task.

The branch name should match the task filename (e.g., task `aws-s3-provider-core.md` → branch `feature/aws-s3-provider-core`).

### Phase 2: Implementation

#### Step 5: Do the Work

Implement changes, working through the Completion Requirements checklist.

After making changes, build to verify:

```powershell
dotnet build MaxBackup.sln --verbosity minimal
```

#### Step 6: Run Tests

```powershell
dotnet test Max.IntegrationTests --verbosity minimal
```

If tests fail, fix them before proceeding.

### Phase 3: Commit and PR

#### Step 7: Run Commit-Push-PR Workflow

Use `/commit-push-pr` to commit, push, and create PR.

Wait for user confirmation at key checkpoints.

### Phase 4: Complete Task

After PR is merged:

#### Step 8: Mark Task Complete

Edit `docs/tasks.md` and check off the task:

```markdown
- [x] [task-name](tasks/task-name.md) - description *(completed YYYY-MM-DD)*
```

#### Step 9: Clean Up

```powershell
git switch master
git pull --ff-only origin master
git branch -d feature/<task-name>
```

## Important Notes

- Always read the task file thoroughly before starting
- Check dependencies - don't start a task if its prerequisites aren't done
- Follow the Completion Requirements as a checklist
- Use `/commit-push-pr` for the final steps - don't manually create PRs
- Mark the task complete only AFTER the PR is merged
