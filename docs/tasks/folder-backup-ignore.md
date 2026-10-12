# Per-folder backup exclusions

## Summary

Support `.maxbackupignore` files within a backup source tree for repository-specific exclusions.

## Context

A whole-source backup job should preserve repository content while allowing each repository to exclude its own dependencies and generated assets. See the behavior specification in [MAXIGN-folder-backup-ignore](../features/MAXIGN-folder-backup-ignore.md).

## Files to Modify

- `MaxBackup.ServiceApp/BackupFileScanner.cs` - Shared file selection with scoped ignore rules and directory pruning.
- `MaxBackup.ServiceApp/BackupExecutor.cs` - Shared scanning for local and cloud jobs.
- `MaxBackup.ServiceApp/Worker.cs` - Consistent scanning in the legacy local worker.
- `Max.IntegrationTests/BackupFileScannerTests.cs` - Matching, scoping, pruning, failure, refresh, and cancellation checks.
- `Max.IntegrationTests/BackupIgnoreExecutorTests.cs` - Local copy and cloud upload selection checks.
- `README.md` - File format, repository example, and troubleshooting.
- `docs/features/MAXIGN-folder-backup-ignore.md` - Behavior specification.
- `docs/tasks.md` - Task tracking.

## Acceptance Criteria

- [x] Root and nested ignore files apply only within their owning subtree.
- [x] Directory exclusions prune traversal, including large dependency trees.
- [x] Patterns, comments, case behavior, and unsupported syntax match the specification.
- [x] Existing job include/exclude filters remain effective.
- [x] Both local and cloud selection honor folder exclusions.
- [x] Ignore edits take effect on a subsequent scan.
- [x] Invalid/unreadable ignore files prevent that pass's transfers and report the cause.
- [x] Cancellation propagates.
- [x] Existing destination data remains retained.
- [x] Relevant tests and the build pass; usage is documented.

## Technical Notes

Implementation proceeds in parallel on a feature branch created from synchronized `master`:

1. Add a shared scanner using the already installed filesystem glob matcher. Wrap its directory traversal to inherit owner-relative ignore rules and filter directory entries before descending.
2. Route the local and cloud executors through the scanner, preserving existing transfer and system-file behavior. Keep scanning fully materialized before copies or uploads begin.
3. Document the agreed behavior, then review the combined code and run the feature tests, existing tests, and build.

Matching and executor integration are tested against real temporary directories; cloud selection uses a recording test provider without live credentials or uploads. No new dependencies, live configuration edits, or deletion of backup sets are needed. Commit/push/release follow the repository's review checkpoints.

## Dependencies

Uses the existing [backup-executor-provider-integration](backup-executor-provider-integration.md).

## Validation

Implemented and validated on 2026-10-12; authorized for commit and PR creation, with merge and release awaiting PR review.

- `dotnet test Max.IntegrationTests/Max.IntegrationTests.csproj --configuration Release`: 130 passed, including 27 scanner cases and 8 executor cases. CLI cases used the locally published CLI through `MAX_CLI_PATH`.
- `dotnet test MaxBackup.Shared.Tests/MaxBackup.Shared.Tests.csproj --configuration Release`: 8 passed.
- Published the CLI and service into their existing installer staging folders, then ran `dotnet build MaxBackup.sln --configuration Release`: succeeded with zero warnings/errors.
- `Test-Installer.ps1` against the built MSI: retention checks passed, including 384 native condition evaluations. No installer actions were executed.
- Independent review findings about parent-relative job patterns, source-path normalization, and disappearing directories were corrected and covered by regression tests. Final review reported no unresolved actionable findings.
- Cloud selection was tested with a recording provider. Live Azure transfers, service deployment, and a large-tree performance benchmark were not performed.

## Review Checkpoint

On 2026-10-12, the owner authorized committing the validated changes and opening a PR for review. Merge and release remain pending that review. No live backup configuration changes are part of this feature.
