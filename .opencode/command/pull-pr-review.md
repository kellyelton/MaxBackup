---
description: Fetch and sync PR review comments from GitHub into local documents
---

EXECUTE THE FOLLOWING WORKFLOW IMMEDIATELY. Do not ask clarifying questions - just start Step 1.

Arguments provided: $ARGUMENTS

## Step 1: Determine PR Number

If provided as argument, use it. Otherwise:
```
pwsh .agents/tools/pr-review.ps1 get-pr-number
```

If no PR found, inform user and STOP.

## Step 2: Get Repository Info

```
pwsh .agents/tools/pr-review.ps1 get-repo-info
```

## Step 3: Fetch All Review Comments

```
pwsh .agents/tools/pr-review.ps1 get-pr-comments {PR_NUMBER}
```

Extract from JSON: id, path, line, body, created_at

## Step 4: Create Directory

Create `docs/pr/{PR_NUMBER}/` if it doesn't exist.

## Step 5: Create Comment Documents

For each NEW comment (check by ID to avoid duplicates), create `docs/pr/{PR_NUMBER}/{N}-{slug}.md`:

```
# Review: {Short Title}

**File:** {path}
**Line:** {line}
**Comment ID:** {comment_id}
**Status:** [ ] Open

## Reviewer Comment
{body}

## Discussion
(pending)

## Resolution
**Decision:** (pending)
**Committed:** No
```

## Step 6: Create/Update Index

Create or update `docs/pr/{PR_NUMBER}/README.md` with a table of all comments.

## Step 7: Report Summary

Tell user: how many new comments found, how many skipped, files created.
