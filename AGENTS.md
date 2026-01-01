# AI Agent Instructions

This document contains important context and workflows for AI agents working on this repository.

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
6. **Wait for Release**: The Release workflow will run on test
7. **Verify**: Check that the preview release was created correctly
8. **Create PR to master**: PR from feature branch to master
9. **Wait for CI**: The CI workflow runs on PRs
10. **Merge PR**: Use rebase merge strategy
11. **Wait for Release**: The Release workflow will run on master
12. **Verify and Celebrate**: Check stable release, then 🎉

### Critical Rules
- **Feature branches are ALWAYS based on master** - never branch from or rebase onto test
- **Never force push to test** - always pull latest and merge
- **PRs to master come from the feature branch**, not from test
- Test branch may have additional preview commits not yet in master - this is expected

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
