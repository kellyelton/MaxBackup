---
description: Fetch and sync PR review comments from GitHub into local documents
---

Use the skill tool to load "pull-pr-review" and execute the workflow:

skill({ name: "pull-pr-review" })

If an argument is provided, use it as the PR number.
Otherwise, determine the PR from the current branch.

$ARGUMENTS
