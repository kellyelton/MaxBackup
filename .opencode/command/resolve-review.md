---
description: Process and resolve a single PR review comment from GitHub with user discussion
---

EXECUTE THE FOLLOWING WORKFLOW IMMEDIATELY. Do not ask clarifying questions - just start Step 1.

Arguments provided: $ARGUMENTS

**STRICT SCOPE: Resolve exactly ONE comment, then STOP.**

## Step 1: Determine PR Number

If provided in arguments, use it. Otherwise:
```
pwsh .agents/tools/pr-review.ps1 get-pr-number
```

## Step 2: Select Comment File

Read `docs/pr/{PR_NUMBER}/README.md` to see the comment index table.

Find the first row with `[ ]` (Open) status, or use specific local number if provided in arguments.

**IMPORTANT:** The local file number (e.g., `3` in `3-progress-throttling.md`) is NOT the GitHub Comment ID!

## Step 3: Get GitHub Comment ID

Read the selected comment file (e.g., `docs/pr/21/3-progress-throttling.md`).

Look for the **Comment ID** field in the file header. Example:
```
**Comment ID:** 2683822801
```

This is the actual GitHub API comment ID you need for API calls.

**Example mapping:**
- Local file: `docs/pr/21/3-progress-throttling.md` (local number = 3)
- GitHub Comment ID inside file: `2683822801` (use THIS for API calls)

## Step 4: Get Full Comment Details from GitHub

Use the **GitHub Comment ID** (NOT the local file number):
```
pwsh .agents/tools/pr-review.ps1 get-comment {PR_NUMBER} {GITHUB_COMMENT_ID}
```

Example:
```
pwsh .agents/tools/pr-review.ps1 get-comment 21 2683822801
```

## Step 5: Present to User

Present clearly: what the reviewer is asking, the affected code, options (accept/reject/defer/modify).

**CRITICAL:** Discuss with user BEFORE any code changes.

## Step 6: Iterate Until Decision

Update local document's Discussion section. Continue until clear resolution.

## Step 7: Implement Changes (If Accepted)

Make code changes, verify with lsp_diagnostics.

## Step 8: Build and Test

```
pwsh -Command "dotnet build MaxBackup.sln --verbosity minimal"
pwsh -Command "dotnet test Max.IntegrationTests --verbosity minimal"
```

## Step 9: Mark Resolved on GitHub

Get thread ID using the **GitHub Comment ID**, then resolve:
```
pwsh .agents/tools/pr-review.ps1 get-thread-id {PR_NUMBER} {GITHUB_COMMENT_ID}
pwsh .agents/tools/pr-review.ps1 resolve-thread {THREAD_ID}
```

Example:
```
pwsh .agents/tools/pr-review.ps1 get-thread-id 21 2683822801
pwsh .agents/tools/pr-review.ps1 resolve-thread "PRRT_kwDONdef..."
```

## Step 10: Commit

```
git add <files> docs/pr/{PR_NUMBER}/
git commit -m "fix: resolve PR review comment"
```

## Step 11: Update Local Tracking

Update `docs/pr/{PR_NUMBER}/README.md`: change `[ ]` to `[x]` for this comment.

## Step 12: STOP

**WORKFLOW COMPLETE.** Do NOT proceed to other comments.
