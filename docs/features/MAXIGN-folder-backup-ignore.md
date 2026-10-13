# Per-folder backup exclusions

## Overview

MaxBackup users can place a `.maxbackupignore` text file in a backed-up folder to exclude rebuildable files and directories from future local and cloud backups. For example, a job covering `F:\Source` can keep one repository's `node_modules` and generated assets out of backups without changing that job or other repositories. This document uses the requirement prefix `MAXIGN`.

## Key Decisions

- MAXIGNKD-1: Rules are relative to the folder containing the ignore file. This lets a repository carry its own backup preferences when moved within a source tree.
- MAXIGNKD-2: Folder rules add exclusions to existing job filters. Exclusions always win; this version has no rules that re-include excluded content.
- MAXIGNKD-3: Matching directories are skipped before their contents are scanned, so excluding a dependency tree saves scanning work as well as backup space.
- MAXIGNKD-4: Ignore rules only affect future selection. Previously backed-up content remains retained under MaxBackup's existing additive behavior.
- MAXIGNKD-5: An unreadable or invalid ignore file prevents file transfers for that job pass and produces a logged error, rather than silently backing up content the user intended to exclude.

## Requirements

- MAXIGNR-1: Discovery and scope
  - MAXIGNR-1.1: A `.maxbackupignore` file in the job source directory applies to that directory and its descendants.
  - MAXIGNR-1.2: A `.maxbackupignore` file in a visited descendant directory adds rules for that directory and its descendants.
  - MAXIGNR-1.3: Rules in one repository do not apply to sibling repositories.
  - MAXIGNR-1.4: Ignore files above the job source are not consulted.
  - MAXIGNR-1.5: Jobs without ignore files retain their existing include, exclude, and built-in exclusion behavior.
  - MAXIGNR-1.6: Ignore files are read anew for each job pass; edits take effect on the next scan without restarting the service.
  - MAXIGNR-1.7: Existing job patterns that select content relative to a parent directory retain their selection behavior. Ignore files outside the original job source are not read, and selected paths reentering that source still honor its rules.
- MAXIGNR-2: Text format
  - MAXIGNR-2.1: Each nonempty rule occupies one line; surrounding whitespace is trimmed.
  - MAXIGNR-2.2: Blank lines and lines whose first non-whitespace character is `#` are ignored. Comments must occupy their own lines.
  - MAXIGNR-2.3: UTF-8 files with or without a byte-order mark are accepted, with Windows or Unix line endings.
  - MAXIGNR-2.4: Patterns use `/` as the path separator and support the existing job filter wildcard syntax: `*` within one segment and `**` across directory levels.
  - MAXIGNR-2.5: Matching is case-insensitive, consistent with existing job filters on Windows.
  - MAXIGNR-2.6: Unsupported negation (`!`), backslash separators, drive-qualified paths, parent traversal, and empty path rules cause an error identifying the ignore file and line.
- MAXIGNR-3: Pattern behavior
  - MAXIGNR-3.1: A pattern without an internal slash matches a file or directory name at any depth under its owning folder; for example, `*.tmp` and `node_modules`.
  - MAXIGNR-3.2: A leading slash anchors a pattern to its owning folder; `/dist/` excludes that folder's immediate `dist` directory.
  - MAXIGNR-3.3: A slash-containing relative pattern is anchored to its owning folder; `assets/generated/` excludes that specific subtree.
  - MAXIGNR-3.4: A trailing slash restricts a rule to directories; `cache/` does not exclude a regular file named `cache`.
  - MAXIGNR-3.5: When a rule matches a directory, the entire directory is excluded without reading its descendants or their ignore files.
  - MAXIGNR-3.6: Parent exclusions remain effective when descendant ignore files are present.
  - MAXIGNR-3.7: An explicit `**` wildcard can match multiple directory levels, for example `**/generated/*.tmp`.
- MAXIGNR-4: Backup behavior and failure handling
  - MAXIGNR-4.1: Local copy and cloud upload jobs use the same folder exclusion rules.
  - MAXIGNR-4.2: A file is eligible only if selected by the existing job filters and not excluded by any applicable folder rule.
  - MAXIGNR-4.3: A `.maxbackupignore` file is itself eligible for backup under the same filters as other files.
  - MAXIGNR-4.4: Excluding content does not delete source files or existing backup copies.
  - MAXIGNR-4.5: An unreadable or invalid ignore file encountered during scanning logs an error and prevents copies or uploads for that job pass.
  - MAXIGNR-4.6: Preserve existing job cancellation handling; the scanner adds no cancellation checks or exception handling changes.
  - MAXIGNR-4.7: Ignore support requires no job configuration changes or automatic application of exclusions to existing repositories.

## Non-Goals

- Full Git ignore syntax, including negation, escape sequences, and character-class wildcards.
- Reading `.gitignore` or automatically excluding dependencies and build output by name.
- Removing retained backup content, changing retention policies, or changing installed services.
- A new CLI command, configuration toggle, or interactive ignore editor.

## Open Questions

None. The matching choices above are the assumptions for this implementation and can be refined after review.

## Change Log

Empty during initial drafting.
