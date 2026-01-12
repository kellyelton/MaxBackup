---
description: Process and resolve a single PR review comment with user discussion
---

Use the skill tool to load "resolve-review" and execute the workflow:

skill({ name: "resolve-review" })

Arguments:
- $1 = PR number (optional, defaults to current branch's PR)
- $2 = Comment number or file path (optional, defaults to first open comment)

CRITICAL REMINDER: This workflow resolves exactly ONE comment. After resolution, STOP immediately. Do not proceed to other comments.

$ARGUMENTS
