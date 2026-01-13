---
description: Fetch and sync PR review comments from GitHub into local documents
---

# Pull PR Review Workflow

Fetches code review comments from a GitHub PR and creates/updates local documents for iterative resolution.

## Usage

Run `/pull-pr-review` or `/pull-pr-review 21` (with PR number)

## Workflow Steps

// turbo
1. Get PR number (from argument or current branch):
```powershell
# If no PR number provided, get from current branch
gh pr list --head $(git branch --show-current) --json number --jq '.[0].number'
```

// turbo
2. Fetch all review comments from GitHub:
```powershell
gh api repos/{owner}/{repo}/pulls/{PR_NUMBER}/comments
```

3. Create directory `docs/pr/{PR_NUMBER}/` if it doesn't exist

4. Determine next comment number:
   - Scan existing files matching pattern `{N}-*.md`
   - Find the highest N, or start at 1 if none exist
   - This allows multiple review rounds without overwriting

5. For each NEW comment (check by comment ID to avoid duplicates):
   - Create file: `docs/pr/{PR_NUMBER}/{N}-{slug}.md`
   - Slug derived from file path or first few words of comment
   - Increment N for each new comment

6. Document format:
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
*PR #{number} | Fetched: {date}*
```

7. Update or create `README.md` index:
   - Preserve existing entries with their status
   - Append new comments to the table
   - Group by review date/phase if multiple syncs

8. Report summary:
   - How many new comments found
   - How many already existed (skipped)
   - List of new files created

## Handling Multiple Review Rounds

When running pull-pr-review again after a new review:

1. Existing documents are NOT modified
2. New comments get sequential numbers after existing ones
3. README.md is updated with new entries appended
4. Comments are de-duplicated by their GitHub comment ID

## Notes

- Use `/resolve-review` workflow to work through individual comments
