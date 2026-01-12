---
name: pull-pr-review
description: Fetch and sync PR review comments from GitHub into local documents for iterative resolution
license: MIT
compatibility: opencode
metadata:
  workflow: github-pr
  audience: developers
---

# Pull PR Review Workflow

Fetches code review comments from a GitHub PR and creates/updates local documents for iterative resolution.

## When to Use

Use this skill when:
- You need to fetch PR review comments from GitHub
- You want to create local tracking documents for review comments
- You're starting a review resolution cycle

## Workflow Steps

### Step 1: Determine PR Number

If a PR number is provided as an argument, use it. Otherwise, get it from the current branch:

```powershell
gh pr list --head $(git branch --show-current) --json number --jq '.[0].number'
```

If no PR is found, inform the user and stop.

### Step 2: Get Repository Info

```powershell
gh repo view --json owner,name --jq '"\(.owner.login)/\(.name)"'
```

### Step 3: Fetch All Review Comments

```powershell
gh api repos/{owner}/{repo}/pulls/{PR_NUMBER}/comments
```

Parse the JSON response to extract:
- `id` - Comment ID (for deduplication)
- `path` - File path
- `line` or `original_line` - Line number
- `body` - Comment content
- `created_at` - Review date

### Step 4: Create Directory Structure

Create `docs/pr/{PR_NUMBER}/` if it doesn't exist.

### Step 5: Determine Next Comment Number

Scan existing files matching pattern `{N}-*.md` in the directory:
- Find the highest N
- Start at 1 if none exist
- This allows multiple review rounds without overwriting

### Step 6: Create Comment Documents

For each NEW comment (check by comment ID to avoid duplicates):

1. Generate a slug from the file path or first few words of comment
2. Create file: `docs/pr/{PR_NUMBER}/{N}-{slug}.md`
3. Increment N for each new comment

Use this format:

```markdown
# Review: {Short Title}

**File:** `{path}`
**Line:** {line}
**Comment ID:** {comment_id}
**Review Date:** {created_at}
**Status:** [ ] Open

## Copilot's Comment

{body}

## Discussion

<!-- Discussion goes here -->

## Resolution

**Decision:** (pending)
**Approach:** (to be determined)
**Committed:** No

---
*PR #{number} | Fetched: {current_date}*
```

### Step 7: Update or Create Index

Create or update `docs/pr/{PR_NUMBER}/README.md`:

```markdown
# PR #{PR_NUMBER} Review Comments

| # | File | Status | Comment |
|---|------|--------|---------|
| 1 | `path/to/file.cs` | [ ] | Short description... |
| 2 | `path/to/other.cs` | [ ] | Short description... |

---
*Last synced: {date}*
```

When updating:
- Preserve existing entries with their status
- Append new comments to the table
- Do NOT modify existing status checkboxes

### Step 8: Report Summary

Report to the user:
- How many new comments found
- How many already existed (skipped)
- List of new files created

## Important Notes

- `docs/pr/` is gitignored - these are local working documents
- Each developer can have their own resolution notes
- Comments are de-duplicated by their GitHub comment ID
- Use `/resolve-review` workflow to work through individual comments
