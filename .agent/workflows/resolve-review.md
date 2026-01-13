---
description: Process and resolve PR review comments from GitHub
---

# Resolve Review Workflow

This workflow processes PR review comments and creates individual documents for iteration.

> [!CAUTION]
> **STRICT SCOPE: ONE COMMENT PER CONVERSATION**
> This workflow is designed to resolve exactly **ONE** review comment per conversation. 
> 1. Start the workflow.
> 2. Resolve **ONE** comment (fully, including resolution on GitHub).
> 3. **STOP IMMEDIATELY.**
> 4. Do not look for or mention other comments. 
> 5. The user must initiate a fresh conversation/workflow call for the next comment.

## Fetching Comments

// turbo
1. Get PR number from user or determine from current branch:
```
gh pr list --head $(git branch --show-current) --json number --jq '.[0].number'
```

// turbo
2. Fetch all review comments:
```
gh api repos/{owner}/{repo}/pulls/{PR_NUMBER}/comments
```

3. Parse the comments and create `docs/pr/{PR_NUMBER}/` directory

4. For each comment, create a markdown file `docs/pr/{PR_NUMBER}/{N}-{slug}.md`. These documents serve as the **discussion board** for each issue.

```markdown
# Review: {Short Title}

**File:** `{path}`
**Line:** {line}
**Comment ID:** {id}
**Status:** [ ] Open | [ ] Accepted | [ ] Rejected | [ ] Deferred

## Copilot's Comment

{body}

## Discussion

<!-- Agent and user discuss the issue here -->

## Resolution

**Decision:** (pending)
**Approach:** (to be determined)
**Committed:** No

---
*PR {PR_NUMBER} | Created: {date}*
```

5. Create an index file `docs/pr/{PR_NUMBER}/README.md` linking all comments

6. Notify user that comments are ready for review

# Resolve Review Workflow: Individual Comment

**CRITICAL: Every comment MUST be discussed and agreed upon with the user BEFORE any code is modified.**

> [!IMPORTANT]
> **REMINDER:** You are here to resolve **ONLY ONE** comment. Once the resolution step is complete, you must STOP and end the task.

1. **Selection:**
   - **MANDATORY:** Open the index file `docs/pr/{PR_NUMBER}/README.md` to see the current state of all comments.
   - If a specific comment is specified, open its document (e.g., `docs/pr/21/1-platform-check.md`).
   - **Automatic Selection:** If nothing is specified, scan the index table for the first row with `[ ]` (Open). Select that comment to start work.

2. **Read Comment Context:** Use the `gh` CLI to get the full comment details and thread ID if not already clear.
   ```powershell
   gh api repos/{owner}/{repo}/pulls/comments/{comment_id}
   ```

3. **Discussion:** Present the issue and options to the user.
4. **Iterate** in the document/chat until a resolution is decided.
5. **Update local document:** Record the decision in the "Discussion" and "Resolution" sections.
6. **IMPLEMENTING CHANGES:** Only if accepted, perform the implementation.
7. **Build and test.**
8. **MARK AS RESOLVED ON GITHUB:** (CRITICAL) Use the `gh` API to resolve the thread. You must find the GraphQL `threadId` first.
   
   **Finding Thread ID:**
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

   **Resolving Thread:**
   ```powershell
   gh api graphql -f query='
   mutation($id: ID!) {
     resolveReviewThread(input: { threadId: $id }) {
       thread { isResolved }
     }
   }' -f id="{THREAD_ID}"
   ```

9. **REPLY TO THE COMMENT ON GITHUB:** Always leave a reply explaining the resolution.

   > [!CAUTION]
   > **GitHub Auto-linking Warning:** NEVER use `#` followed by a number in replies (e.g., `#4`, `#21`).
   > GitHub auto-converts `#N` into links to issue/PR N, causing confusion.
   > - **BAD:** "Fixed as part of comment #4" → Links to unrelated issue
   > - **GOOD:** "Fixed as part of the duplicate regex comment" → No auto-link
   > Always reference other comments by DESCRIPTION, not number.

   **If ACCEPTED/MODIFIED:**
   ```powershell
   pwsh .agents/tools/pr-review.ps1 reply {PR_NUMBER} {COMMENT_ID} "Fixed - [brief description of what was changed]"
   ```

   **If REJECTED (MANDATORY - always explain why):**
   ```powershell
   pwsh .agents/tools/pr-review.ps1 reply {PR_NUMBER} {COMMENT_ID} "Declining this suggestion because [reason]."
   ```

   **If DEFERRED:**
   ```powershell
   pwsh .agents/tools/pr-review.ps1 reply {PR_NUMBER} {COMMENT_ID} "Good point - deferring to a future PR. [Brief reason]"
   ```

10. **STAGING AND COMMITTING CHANGES:** Stage and commit both code changes and updated `docs/pr/` tracking documents.
    
    > [!WARNING]
    > Do NOT use `#{N}` in commit messages - it creates GitHub auto-links.

    If code was changed:
    ```powershell
    git add <modified_files> docs/pr/{PR_NUMBER}/
    git commit -m "fix: resolve PR review - {description}"
    ```

    If only rejected/deferred (no code changes):
    ```powershell
    git add docs/pr/{PR_NUMBER}/
    git commit -m "docs: resolve PR review comment - {description}"
    ```

11. **DONE:** This completes the workflow for this comment. 
    - Update the individual document status to "Accepted" and "Committed: Yes".
    - **MANDATORY:** Check off the comment in the `docs/pr/{PR_NUMBER}/README.md` index table by changing `[ ]` to `[x]`.
    - **STOP:** Do not proceed to another comment in this conversation.
