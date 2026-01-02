# AI Agent Instructions

This document contains important context and workflows for AI agents working on this repository.

## ⚠️ CRITICAL: Before Making ANY Code Changes

**ALWAYS create a feature branch FIRST, before writing any code:**

```powershell
# Step 1: ALWAYS do this BEFORE making any changes
git checkout master
git pull origin master
git checkout -b feature/your-feature-name   # or fix/your-fix-name

# Step 2: NOW you can make changes
# ... implement the fix/feature ...

# Step 3: Commit your changes
git add -A
git commit -m "Description of changes"
```

**Why this matters:** If you make changes on master and then try to pull, you'll have conflicts or need to stash. Always branch first!

## Branch Strategy

- **master**: Protected branch, requires PRs. Releases are stable (e.g., `v0.2.5`)
- **test**: Preview/staging branch. Releases are marked as pre-release (e.g., `Preview v0.2.4`)
- Cannot push directly to master - all changes must go through PRs
- **Feature branches are ALWAYS based on master, never on test**

## Preferred Workflow for Changes

Follow this 12-step workflow when making changes:

1. **Pull latest master**: `git checkout master && git pull origin master`
2. **Create feature branch**: Branch from master (e.g., `git checkout -b fix/description`)
3. **Make changes**: Implement the fix/feature
4. **Pull latest test**: `git checkout test && git pull origin test`
5. **Merge feature to test**: `git merge <feature-branch> && git push origin test`
6. **Wait for Release workflow**: Use `gh run watch` to wait for completion (see examples below)
7. **Verify**: Check that the preview release was created correctly
8. **Create PR to master**: PR from feature branch to master
9. **Wait for CI workflow**: Use `gh run watch` to wait for completion
10. **Merge PR**: Use rebase merge strategy: `gh pr merge <number> --rebase`
11. **Wait for Release workflow**: Use `gh run watch` to wait for completion
12. **Verify and Celebrate**: Check stable release, then 🎉

### Critical Rules
- **ALWAYS create a feature branch BEFORE making any code changes** - never start coding on master
- **Feature branches are ALWAYS based on master** - never branch from or rebase onto test
- **Never force push to test** - always pull latest and merge
- **PRs to master come from the feature branch**, not from test
- **Wait for workflows to complete before proceeding** - don't create PR until test release succeeds
- Test branch may have additional preview commits not yet in master - this is expected

## GitHub CLI Commands for Workflow Management

### Waiting for Workflows

```powershell
# Watch the most recent workflow run (interactive, shows progress)
gh run watch

# List recent workflow runs
gh run list --limit 5

# Watch a specific run by ID
gh run watch <run-id>

# Wait for a specific workflow on a branch
gh run list --branch test --limit 1 --json databaseId --jq '.[0].databaseId' | ForEach-Object { gh run watch $_ }
```

### Creating and Managing PRs

```powershell
# Create PR from feature branch to master
gh pr create --base master --head feature/your-feature --title "Title" --body "Description"

# Wait for PR checks to complete
gh pr checks <pr-number> --watch

# Merge PR with rebase strategy
gh pr merge <pr-number> --rebase

# View PR status
gh pr view <pr-number>
```

### Complete Workflow Example

```powershell
# === STEP 1: Setup (DO THIS FIRST, BEFORE ANY CODE CHANGES) ===
git checkout master
git pull origin master
git checkout -b feature/my-feature

# === STEP 2: Make your changes ===
# ... edit files ...
git add -A
git commit -m "Implement feature X"

# === STEP 3: Push to test for preview release ===
git checkout test
git pull origin test
git merge feature/my-feature
git push origin test

# === STEP 4: Wait for test release workflow ===
gh run watch  # Wait for it to complete successfully

# === STEP 5: Create PR (only after test release succeeds) ===
git checkout feature/my-feature
git push -u origin feature/my-feature
gh pr create --base master --head feature/my-feature --title "Feature X" --body "Description"

# === STEP 6: Wait for CI checks on PR ===
gh pr checks <pr-number> --watch

# === STEP 7: Merge PR ===
gh pr merge <pr-number> --rebase

# === STEP 8: Wait for master release workflow ===
gh run watch  # Wait for stable release

# === STEP 9: Verify release ===
gh release list --limit 1
```

## GitHub Actions Workflows

### CI Workflow (`ci.yml`)
- **Triggers**: Pull requests only
- **Purpose**: Fast feedback - build, test, verify installer builds
- **Does NOT**: Create releases, version artifacts, or push tags

### Release Workflow (`release.yml`)
- **Triggers**: Pushes to `master` or `test` branches
- **Purpose**: Full release with versioning, installer, and GitHub release creation
- **Concurrency**: Uses global `release` group to ensure only one release runs at a time across all branches
- **Important**: Never run releases in parallel - GitVersion can produce conflicts

## GitVersion Configuration

### Tag Format Requirements
- **MUST use semver format**: `x.y.z` (e.g., `0.2.5`)
- **NOT assembly format**: `x.y.z.0` (e.g., `0.2.5.0`)
- GitVersion reads tags to determine the next version. If tags are in the wrong format, it will return `0.0.1`

### Version Variables
- Use `majorMinorPatch` for tag names and release titles
- Do NOT use `assemblySemVer` for tags (it includes 4 parts)
- `fullSemVer` includes prerelease label (e.g., `0.2.4-preview.1`)

### Branch Labels
- `master`: No label (stable releases)
- `test`: `preview` label (pre-releases)

## Integration Tests

### Requirements
- Integration tests require the CLI to be **published**, not just built
- The `MAX_CLI_PATH` environment variable must point to the published executable
- Use `dotnet publish` instead of `dotnet build` when running integration tests

### Test Environment Setup
```powershell
$env:MAX_CLI_PATH = "path/to/Max/bin/publish/win/max.exe"
dotnet test Max.IntegrationTests/Max.IntegrationTests.csproj
```

## Common Issues & Solutions

### GitVersion Returns 0.0.1
**Cause**: Tags are in wrong format (4-part instead of 3-part semver)
**Solution**: Create proper semver tags: `git tag 0.2.5` not `git tag 0.2.5.0`

### Parallel Builds Causing Version Conflicts
**Cause**: Multiple release workflows running simultaneously
**Solution**: Global concurrency group in release.yml ensures sequential execution

### CI Failing on Integration Tests
**Cause**: Tests need published CLI executable
**Solution**: Use `dotnet publish` before running tests, set `MAX_CLI_PATH`

## PR Merge Strategy

- Prefer **rebase** merges to keep history clean
- When using `gh pr merge`: `gh pr merge <number> --rebase`
