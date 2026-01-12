---
name: resolve-review
description: Process and resolve a single PR review comment from GitHub with user discussion
license: MIT
compatibility: opencode
metadata:
  workflow: github-pr
  audience: developers
---

# Resolve Review Workflow

Process and resolve PR review comments with user collaboration.

## CRITICAL RULES

> **LOCAL WORKFLOW ONLY:** Documents in `docs/pr/` are for AI-User discussion and tracking. They **MUST NOT** be committed or pushed to the repository.

> **STRICT SCOPE: ONE COMMENT PER CONVERSATION**
> 1. Start the workflow
> 2. Resolve **ONE** comment (fully, including resolution on GitHub)
> 3. **STOP IMMEDIATELY**
> 4. Do not look for or mention other comments
> 5. User must initiate a fresh conversation for the next comment

## When to Use

Use this skill when:
- You need to resolve a specific PR review comment
- You want to discuss a review comment with the user before implementing
- You're working through the review comments one at a time

## Workflow Steps

### Step 1: Determine PR Number

If provided, use the given PR number. Otherwise, get from current branch:

```powershell
gh pr list --head $(git branch --show-current) --json number --jq '.[0].number'
```

### Step 2: Get Repository Info

```powershell
gh repo view --json owner,name --jq '"\(.owner.login)/\(.name)"'
```

### Step 3: Select Comment

**MANDATORY:** Open `docs/pr/{PR_NUMBER}/README.md` to see all comments.

Selection logic:
- If a specific comment number is given (e.g., `/resolve-review 21 3`), use that
- If a specific file is given (e.g., `docs/pr/21/3-platform-check.md`), use that
- **Automatic selection:** Scan the index table for the first row with `[ ]` (Open). Select that comment.

### Step 4: Read Comment Context

Get full comment details from GitHub:

```powershell
gh api repos/{owner}/{repo}/pulls/comments/{comment_id}
```

### Step 5: Present to User for Discussion

Present the issue clearly:
- What the reviewer is asking
- The affected code location
- Options for resolution (accept, reject, defer, modify)

**CRITICAL:** Every comment MUST be discussed and agreed upon with the user BEFORE any code is modified.

### Step 6: Iterate Until Decision

Update the local document's "Discussion" section with:
- Points raised
- Options considered
- Final decision

Continue discussing until a clear resolution is decided.

### Step 7: Update Local Document

Record the decision in the document:
- Update "Discussion" section with conversation summary
- Update "Resolution" section with:
  - **Decision:** Accepted / Rejected / Deferred
  - **Approach:** What will be done
  - **Committed:** No (will update after commit)

### Step 8: Implement Changes (If Accepted)

Only if the decision is to accept the review comment:
1. Make the necessary code changes
2. Follow existing codebase patterns
3. Verify with `lsp_diagnostics`

### Step 9: Build and Test

```powershell
dotnet build MaxBackup.sln --verbosity minimal
dotnet test Max.IntegrationTests --verbosity minimal
```

### Step 10: Mark as Resolved on GitHub

**CRITICAL:** Use the GitHub GraphQL API to resolve the review thread.

**Find Thread ID:**
```powershell
gh api graphql -F owner="{owner}" -F repo="{repo}" -F prNumber={PR_NUMBER} -f query='
query($owner: String!, $repo: String!, $prNumber: Int!) {
  repository(owner: $owner, name: $repo) {
    pullRequest(number: $prNumber) {
      reviewThreads(first: 50) {
        nodes { id isResolved comments(first: 1) { nodes { databaseId } } }
      }
    }
  }
}' -q '.data.repository.pullRequest.reviewThreads.nodes[] | select(.comments.nodes[].databaseId == {comment_id}) | .id'
```

**Resolve Thread:**
```powershell
gh api graphql -f query='
mutation($id: ID!) {
  resolveReviewThread(input: { threadId: $id }) {
    thread { isResolved }
  }
}' -f id="{THREAD_ID}"
```

### Step 11: Commit Code Changes Only

**DO NOT** stage `docs/pr/` files. Only commit code changes:

```powershell
git add <modified_code_files>
git commit -m "fix: resolve PR review comment #{N} - {description}"
```

### Step 12: Update Local Tracking

1. Update the individual document:
   - Status: `[x] Accepted` (or appropriate status)
   - Committed: Yes

2. **MANDATORY:** Update `docs/pr/{PR_NUMBER}/README.md`:
   - Change `[ ]` to `[x]` for this comment in the index table

### Step 13: STOP

**This completes the workflow for this ONE comment.**

Do NOT:
- Proceed to another comment
- Look for other open comments
- Suggest working on other comments

The user must explicitly start a new conversation for the next comment.

## Important Notes

- Local documents (`docs/pr/`) are for tracking only - never commit them
- Always discuss with user before making code changes
- One comment per conversation - strict scope
- Mark resolved on GitHub so the PR shows progress
