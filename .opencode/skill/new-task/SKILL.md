---
name: new-task
description: Create new task(s) in the backlog from a description of work
license: MIT
compatibility: opencode
metadata:
  workflow: task-management
  audience: developers
---

# New-Task Workflow

Creates one or more task files in `docs/tasks/` based on a description of work. Automatically breaks down complex work into bite-sized, actionable tasks.

## When to Use

Use this skill when:
- The user wants to add work to the task backlog
- The user runs `/new-task` or `/new-task <description>`
- You need to break down a feature request into tasks

## Workflow Steps

### Step 1: Get Work Description

**If the user hasn't described the work:**
- Ask: "What work would you like to add to the task backlog?"
- They may describe it verbally or point to a document to read

**If they point to a document:**
- Read the document(s) first
- Understand the scope and requirements

### Step 2: Analyze Complexity

Evaluate the work:

| Size | Criteria | Action |
|------|----------|--------|
| **Small** | Single focused change, ~1-2 hours, touches few files | 1 task |
| **Medium** | Multiple related changes, clear phases | 2-3 tasks |
| **Large** | Significant feature, needs breakdown | 4+ tasks |

**Each task should be:**
- Completable in one focused session
- Independently testable
- Has clear "done" criteria

If work is complex, break it into multiple tasks with dependencies noted.

### Step 3: Create Task File(s)

For each task, create a file in `docs/tasks/`:

**Filename:** `<kebab-case-name>.md`

**Template:**

```markdown
# <Task Title>

## Summary

<One-line description of what this task accomplishes>

## Context

<Why is this needed? Background information.>

## Completion Requirements

- [ ] <Specific, testable requirement>
- [ ] <Another requirement>
- [ ] <etc.>

## Files to Modify

- `path/to/file.cs` - <what changes>
- `path/to/other.cs` - <what changes>

## Technical Notes

<Implementation details, gotchas, references>

## Dependencies

<Links to prerequisite tasks, or "None">
```

### Step 4: Add to Task Backlog

Open `docs/tasks.md` and add the task(s) to the appropriate section.

**Format:**
```markdown
- [ ] [task-name](tasks/task-name.md) - <short description>
```

**Placement rules:**
- Add after any dependencies (tasks it depends on)
- Add before any tasks that depend on it
- Group related tasks together
- Don't use numbers - position defines order

### Step 5: Report to User

Show the user:
- How many tasks were created
- List each task with its summary
- Where they were placed in the backlog
- Ask if the breakdown looks right or needs adjustment

## Task Sizing Guidelines

| Size | Scope | Examples |
|------|-------|----------|
| Too Small | Single line change | "Fix typo", "Update version" |
| Just Right | Focused feature/fix | "Add upload progress logging", "Implement S3 provider" |
| Too Large | Epic/multi-day | "Add cloud backup support" → break down |

When in doubt, prefer smaller tasks. It's easier to combine work than to context-switch mid-task.

## Example

User says: "Add AWS S3 as a storage provider"

**Analysis:** This is Medium complexity, needs 2 tasks:
1. Core S3 implementation
2. CLI integration for S3

**Creates:**
- `docs/tasks/aws-s3-provider-core.md`
- `docs/tasks/aws-s3-provider-cli.md`

**Adds to `docs/tasks.md`:**
```markdown
- [ ] [aws-s3-provider-core](tasks/aws-s3-provider-core.md) - Implement AWSS3StorageProvider class
- [ ] [aws-s3-provider-cli](tasks/aws-s3-provider-cli.md) - Add S3 to provider CLI commands
```

## Important Notes

- Task names should be kebab-case and descriptive
- Each task should be independently completable
- Dependencies should be explicit in the task file
- Position in the backlog determines priority, not numbers
- Ask user to confirm the breakdown before finalizing
