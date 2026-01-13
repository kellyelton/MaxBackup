---
description: Commit staged changes, push to origin, and create a PR
---

# Commit-Push-PR Workflow

Commits your current changes, pushes to origin, and creates a pull request. Ensures you're on a proper feature branch first.

---

## Step 1: Verify Feature Branch

// turbo
```powershell
git branch --show-current
```

**Check the branch name:**
- ✅ Starts with `feature/`, `fix/`, `docs/`, `refactor/`, `test/`, or `chore/` → proceed
- ❌ Is `master` or `test` → STOP, cannot commit directly
- ❌ Is something else unexpected → ask user if they want to run `/init` workflow

---

## Step 2: Check for Changes

// turbo
```powershell
git status --short
```

**If no changes:** Inform user "No changes to commit." and STOP.

---

## Step 3: Build and Test

// turbo
```powershell
dotnet build MaxBackup.sln --verbosity minimal
```

If build fails: STOP, fix the errors first.

// turbo
```powershell
dotnet test Max.IntegrationTests --verbosity minimal
```

If tests fail: STOP, fix the tests first.

---

## Step 4: Review Changes

// turbo
```powershell
git diff --stat
```

---

## Step 5: Stage and Commit

// turbo
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

---

## Step 6: Push to Origin

```powershell
git push origin <branch-name>
```

---

## Step 7: Create Pull Request

```powershell
gh pr create --base master --head <branch-name> --title "<title>" --body "<body>"
```

**Body should include:**
- Summary of what changed
- Link to related task file if applicable

---

## Step 8: Report Result

Inform the user:
- PR created successfully
- Link to the PR
- CI will run automatically
