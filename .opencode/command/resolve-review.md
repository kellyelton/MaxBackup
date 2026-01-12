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

Present clearly: what the reviewer is asking, the affected code, options:
- **Accept** - Implement the suggested change
- **Reject** - Decline with explanation (MUST leave a reply comment)
- **Modify** - Implement a variation of the suggestion
- **Defer** - Acknowledge but postpone to future work

**CRITICAL:** Discuss with user BEFORE any code changes.

## Step 6: Iterate Until Decision

Update local document's Discussion section. Continue until clear resolution.

Record the decision type (accept/reject/modify/defer) and rationale.

## Step 7: Handle Based on Decision

### If ACCEPTED or MODIFIED:
1. Make code changes
2. Verify with lsp_diagnostics
3. Reply to comment explaining what was done:
   ```
   pwsh .agents/tools/pr-review.ps1 reply {PR_NUMBER} {GITHUB_COMMENT_ID} "Fixed in this PR - [brief description of change]"
   ```

### If REJECTED:
**MANDATORY:** Always reply explaining why the suggestion was declined:
```
pwsh .agents/tools/pr-review.ps1 reply {PR_NUMBER} {GITHUB_COMMENT_ID} "Declining this suggestion because [reason]. [Optional: alternative approach or future consideration]"
```

Common rejection reasons to include:
- Performance/complexity tradeoff not worth it for this use case
- Existing pattern in codebase differs intentionally
- Out of scope for this PR, tracked separately
- Technical constraint prevents this approach

### If DEFERRED:
Reply acknowledging the feedback:
```
pwsh .agents/tools/pr-review.ps1 reply {PR_NUMBER} {GITHUB_COMMENT_ID} "Good point - deferring to [issue/future PR]. [Brief reason for deferral]"
```

## Step 8: Build and Test (If Code Changed)

Skip this step if no code changes were made (reject/defer).

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

If code was changed:
```
git add <files> docs/pr/{PR_NUMBER}/
git commit -m "fix: resolve PR review - [brief description]"
```

If only rejected/deferred (no code changes):
```
git add docs/pr/{PR_NUMBER}/
git commit -m "docs: resolve PR review comment #[local_number] - [accept/reject/defer]"
```

## Step 11: Update Local Tracking

Update `docs/pr/{PR_NUMBER}/README.md`: change `[ ]` to `[x]` for this comment.

## Step 12: STOP

**WORKFLOW COMPLETE.** Do NOT proceed to other comments.
