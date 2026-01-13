---
description: Commit staged changes, push to origin, and create a PR
---

EXECUTE THE FOLLOWING WORKFLOW IMMEDIATELY. Do not ask clarifying questions - just start Step 1.

Arguments provided: $ARGUMENTS

## Step 1: Verify Feature Branch

```powershell
git branch --show-current
```

**Check the branch name:**
- ✅ Starts with `feature/`, `fix/`, `docs/`, `refactor/`, `test/`, or `chore/` → proceed
- ❌ Is `master` or `test` → STOP, cannot commit directly to protected branches

## Step 2: Check for Changes

```powershell
git status --short
```

**If no changes:** Inform user "No changes to commit." and STOP.

## Step 3: Build and Test

```powershell
dotnet build MaxBackup.sln --verbosity minimal
dotnet test Max.IntegrationTests --verbosity minimal
```

If either fails: STOP, fix the errors first.

## Step 4: Review Changes

```powershell
git diff --stat
```

Present this to the user for review before proceeding.

## Step 5: Stage and Commit

```powershell
git add -A
```

Determine commit prefix from branch type:

| Branch Prefix | Commit Prefix |
|---------------|---------------|
| `feature/` | `feat:` |
| `fix/` | `fix:` |
| `docs/` | `docs:` |
| `refactor/` | `refactor:` |
| `test/` | `test:` |
| `chore/` | `chore:` |

```powershell
git commit -m "<prefix>: <concise description>"
```

## Step 6: Push to Origin

```powershell
git push -u origin <branch-name>
```

## Step 7: Create Pull Request

```powershell
gh pr create --base master --head <branch-name> --title "<title>" --body "<body>"
```

## Step 8: Report Result

Tell the user: ✅ PR created, provide the link, note CI will run automatically.
