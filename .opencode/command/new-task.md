---
description: Create new task(s) in the backlog
---

EXECUTE THE FOLLOWING WORKFLOW IMMEDIATELY. If no work description provided, ask user what work to add.

Arguments provided: $ARGUMENTS

## Step 1: Get Work Description

If no argument provided, ask: "What work would you like to add to the task backlog?"

## Step 2: Analyze Complexity

| Size | Criteria | Action |
|------|----------|--------|
| **Small** | Single focused change, ~1-2 hours | 1 task |
| **Medium** | Multiple related changes, clear phases | 2-3 tasks |
| **Large** | Significant feature, needs breakdown | 4+ tasks |

Each task should be completable in one focused session.

## Step 3: Create Task File(s)

For each task, create a file in `docs/tasks/<kebab-case-name>.md`:

```markdown
# <Task Title>

## Summary
<One-line description>

## Context
<Why is this needed?>

## Completion Requirements
- [ ] <Specific requirement>
- [ ] <Another requirement>

## Files to Modify
- `path/to/file.cs` - <what changes>

## Technical Notes
<Implementation details>

## Dependencies
<Links to prerequisite tasks, or "None">
```

## Step 4: Add to Task Backlog

Edit `docs/tasks.md` and add the task(s):

```markdown
- [ ] [task-name](tasks/task-name.md) - <short description>
```

Position after dependencies, before dependent tasks.

## Step 5: Report to User

Show: how many tasks created, list each with summary, where placed in backlog.
