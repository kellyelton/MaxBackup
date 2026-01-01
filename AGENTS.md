# MaxBackup Development Workflow

This document outlines the standard workflow for making changes to MaxBackup. This workflow ensures that all changes are properly tested in the `test` branch before being merged to `master`.

## Prerequisites

- Git installed and configured
- GitHub CLI (`gh`) installed and authenticated
- .NET SDK 10.0.x installed
- WiX Toolset installed (`dotnet tool install --global wix`)

## Workflow Steps

### 1. Sync Master and Test Branches Locally

First, ensure your local `master` and `test` branches are up to date with the remote repository.

```bash
# Fetch all remote changes
git fetch origin

# Switch to master and pull latest changes
git checkout master
git pull origin master

# Switch to test and pull latest changes
git checkout test
git pull origin test
```

Alternatively, using `gh` CLI:

```bash
# Sync branches
gh repo sync
git checkout master
git pull
git checkout test
git pull
```

### 2. Make a New Branch for New Changes

Create a new feature branch from `master` for your changes. Use a descriptive name that reflects the work being done.

```bash
# Create and switch to a new branch
git checkout master
git checkout -b feature/my-new-feature
```

### 3. Make Changes on New Branch

Make your code changes on the feature branch:

```bash
# Make your code changes
# ... edit files ...

# Stage your changes
git add .

# Commit your changes with a descriptive message
git commit -m "Add feature: description of changes"

# Push your feature branch to remote
git push -u origin feature/my-new-feature
```

### 4. Merge and Push Changes to Test

Merge your feature branch into the `test` branch to trigger a preview release.

```bash
# Switch to test branch
git checkout test

# Merge your feature branch
git merge feature/my-new-feature

# Push to test branch (this triggers the release workflow)
git push origin test
```

### 5. Wait for Release to Complete Successfully

Monitor the release workflow to ensure it completes successfully.

```bash
# View recent workflow runs
gh run list --branch test --limit 5

# Watch the latest workflow run
gh run watch

# Or view details of a specific run
gh run view <run-id>
```

You can also monitor the workflow in the GitHub Actions UI:
- Navigate to: https://github.com/kellyelton/MaxBackup/actions

### 6. Verify Release Details, Version Number, etc.

Once the release workflow completes, verify the preview release was created correctly.

```bash
# List recent releases
gh release list --limit 5

# View details of the latest release
gh release view --web

# Or view specific release details
gh release view <tag-name>
```

Verify:
- The release tag matches the expected version (e.g., `1.2.3-preview.4`)
- The release is marked as a "pre-release"
- The MSI installer artifact is attached
- Release notes are generated correctly

### 7. Make a PR from New Branch to Master

If the test release is successful, create a pull request to merge your feature branch into `master`.

```bash
# Create a pull request using gh CLI
gh pr create --base master --head feature/my-new-feature --title "Add feature: description" --body "Description of changes and why they're needed"

# Or create it interactively
gh pr create --base master --head feature/my-new-feature
```

Alternatively, you can create the PR via the GitHub web UI.

### 8. Wait for Actions to Complete Successfully

Monitor the CI workflow that runs on your pull request.

```bash
# View PR checks status
gh pr checks

# Watch workflow run for the PR
gh run watch

# View PR details
gh pr view
```

The CI workflow will:
- Build the solution
- Run all tests
- Build the installer

Ensure all checks pass before proceeding.

### 9. Merge PR

Once all checks pass and the PR is approved, merge it into `master`.

```bash
# Merge the PR (using squash merge)
gh pr merge --squash --delete-branch

# Or use merge commit
gh pr merge --merge --delete-branch

# Or rebase merge
gh pr merge --rebase --delete-branch
```

You can also merge via the GitHub web UI.

### 10. Wait for Release Action to Complete Successfully

After merging to `master`, the release workflow will automatically trigger. Monitor it to ensure it completes successfully.

```bash
# Switch to master branch locally
git checkout master
git pull origin master

# View recent workflow runs on master
gh run list --branch master --limit 5

# Watch the latest workflow run
gh run watch

# Or view details of a specific run
gh run view <run-id>
```

### 11. Verify Release Version Number etc.

Verify the stable release was created correctly.

```bash
# List recent releases
gh release list --limit 5

# View details of the latest release
gh release view --web

# Or view specific release details
gh release view <tag-name>
```

Verify:
- The release tag matches the expected version (e.g., `1.2.3`)
- The release is **not** marked as a "pre-release"
- The MSI installer artifact is attached
- Release notes are generated correctly
- The version number has been properly incremented

### 12. Celebrate! 🎉

If everything was successful, you've successfully released a new version of MaxBackup!

```bash
# Optional: Clean up your local feature branch
git branch -d feature/my-new-feature
```

## Common Commands Reference

### GitHub CLI Quick Reference

```bash
# View repository status
gh repo view

# List open PRs
gh pr list

# List recent releases
gh release list

# View workflow runs
gh run list

# View workflow logs
gh run view <run-id> --log

# List repository branches
gh api repos/{owner}/{repo}/branches --jq '.[].name'
```

### Troubleshooting

#### Release Workflow Failed

If the release workflow fails:

1. Check the workflow logs:
   ```bash
   gh run view <run-id> --log-failed
   ```

2. Common issues:
   - Test failures: Review test logs and fix failing tests
   - Build errors: Check build output for compilation errors
   - Version conflicts: Ensure GitVersion configuration is correct

#### CI Workflow Failed on PR

If CI checks fail on your PR:

1. Check the workflow logs:
   ```bash
   gh pr checks
   gh run view <run-id> --log-failed
   ```

2. Fix issues locally:
   ```bash
   # Make fixes on your feature branch
   git checkout feature/my-new-feature
   # ... make fixes ...
   git commit -am "Fix CI issues"
   git push origin feature/my-new-feature
   ```

The PR will automatically re-run checks after pushing new commits.

## Additional Notes

- **Concurrency**: The release workflow uses a concurrency group, so only one release can run at a time across all branches. If another release is in progress, wait for it to complete.

- **Version Numbering**: GitVersion automatically determines version numbers based on branch and commit history:
  - `master` branch: produces stable versions (e.g., `1.2.3`)
  - `test` branch: produces preview versions (e.g., `1.2.3-preview.4`)

- **Branch Protection**: Ensure branch protection rules are configured on `master` to require:
  - Pull request reviews
  - Passing CI checks
  - Up-to-date branches

- **Testing**: Always ensure tests pass locally before pushing:
  ```bash
  dotnet test Max.IntegrationTests\Max.IntegrationTests.csproj --configuration Release
  ```
