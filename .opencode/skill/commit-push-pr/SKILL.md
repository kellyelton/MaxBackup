---
name: commit-push-pr
description: Commit staged changes, push to origin, and create a PR
license: MIT
compatibility: opencode
metadata:
  workflow: git
  audience: developers
---

# Commit-Push-PR Workflow

Commits your current changes, pushes to origin, and creates a pull request. Ensures you're on a proper feature branch first.

## When to Use

Use this skill when:
- You've completed work and want to create a PR
- The user runs `/commit-push-pr`
- You need to commit, push, and create a PR in one flow

## Workflow Steps

### Step 1: Verify Feature Branch

```powershell
git branch --show-current
```

**Check the branch name:**
- ✅ Starts with `feature/`, `fix/`, `docs/`, `refactor/`, `test/`, or `chore/` → proceed
- ❌ Is `master` or `test` → STOP, cannot commit directly to protected branches
- ❌ Is something else unexpected → ask user if they want to run `/init` workflow first

### Step 2: Check for Changes

```powershell
git status --short
```

**If no changes:** Inform user "No changes to commit." and STOP.

### Step 3: Build and Test

Build the solution:

```powershell
dotnet build MaxBackup.sln --verbosity minimal
```

If build fails: STOP, fix the errors first.

Run tests:

```powershell
dotnet test Max.IntegrationTests --verbosity minimal
```

If tests fail: STOP, fix the tests first.

### Step 4: Review Changes

Show a summary of what will be committed:

```powershell
git diff --stat
```

Present this to the user for review before proceeding.

### Step 5: Stage and Commit

Stage all changes:

```powershell
git add -A
```

Determine commit prefix from branch type:

| Branch Prefix | Commit Prefix | Description |
|---------------|---------------|-------------|
| `feature/` | `feat:` | New feature |
| `fix/` | `fix:` | Bug fix |
| `docs/` | `docs:` | Documentation only |
| `refactor/` | `refactor:` | Code refactoring |
| `test/` | `test:` | Test changes |
| `chore/` | `chore:` | Maintenance tasks |

Create commit with appropriate prefix:

```powershell
git commit -m "<prefix>: <concise description>"
```

The description should be:
- Concise (50 chars or less for the first line)
- Descriptive of what changed
- In imperative mood ("Add feature" not "Added feature")

### Step 6: Push to Origin

```powershell
git push origin <branch-name>
```

If this is the first push, use:

```powershell
git push -u origin <branch-name>
```

### Step 7: Create Pull Request

```powershell
gh pr create --base master --head <branch-name> --title "<title>" --body "<body>"
```

**Title:** Should match commit message (or summarize if multiple commits)

**Body should include:**
- Summary of what changed (bullet points)
- Link to related task file if applicable: `Related: docs/tasks/<task-name>.md`
- Any testing notes

Example:
```markdown
## Summary
- Added AWS S3 storage provider implementation
- Integrated with existing IStorageProvider interface
- Added configuration options for bucket and region

## Related
- Task: docs/tasks/aws-s3-provider-core.md

## Testing
- Unit tests added for S3Provider class
- Manual testing with localstack
```

### Step 8: Report Result

Inform the user:
- ✅ PR created successfully
- 🔗 Link to the PR
- ⏳ CI will run automatically
- Next: Wait for CI, then merge or address review comments

## Important Notes

- Never commit directly to `master` or `test` branches
- Always build and test before committing
- Use conventional commit prefixes based on branch type
- The PR body should give reviewers context
- Wait for CI to pass before asking for merge
