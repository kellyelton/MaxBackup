# PR Review Helper Tools
# Usage: pwsh .agents/tools/pr-review.ps1 <command> [args]

param(
    [Parameter(Position=0)]
    [string]$Command,
    
    [Parameter(Position=1)]
    [string]$Arg1,
    
    [Parameter(Position=2)]
    [string]$Arg2
)

function Get-RepoInfo {
    $info = gh repo view --json owner,name | ConvertFrom-Json
    return @{
        Owner = $info.owner
        Name = $info.name
    }
}

function Get-PRNumber {
    param([string]$Branch)
    
    if (-not $Branch) {
        $Branch = git branch --show-current
    }
    
    $pr = gh pr list --head $Branch --json number | ConvertFrom-Json
    if ($pr.Count -gt 0) {
        return $pr[0].number
    }
    return $null
}

function Get-PRComments {
    param([int]$PRNumber)
    
    $repo = Get-RepoInfo
    $comments = gh api "repos/$($repo.Owner)/$($repo.Name)/pulls/$PRNumber/comments" | ConvertFrom-Json
    return $comments
}

function Get-CommentDetails {
    param(
        [int]$PRNumber,
        [int]$CommentId
    )
    
    $repo = Get-RepoInfo
    $comment = gh api "repos/$($repo.Owner)/$($repo.Name)/pulls/comments/$CommentId" | ConvertFrom-Json
    return $comment
}

function Get-ThreadId {
    param(
        [int]$PRNumber,
        [int]$CommentId
    )
    
    $repo = Get-RepoInfo
    
    $query = @"
query {
  repository(owner: "$($repo.Owner)", name: "$($repo.Name)") {
    pullRequest(number: $PRNumber) {
      reviewThreads(first: 100) {
        nodes {
          id
          isResolved
          comments(first: 1) {
            nodes {
              databaseId
            }
          }
        }
      }
    }
  }
}
"@
    
    $result = gh api graphql -f query="$query" | ConvertFrom-Json
    $threads = $result.data.repository.pullRequest.reviewThreads.nodes
    
    foreach ($thread in $threads) {
        if ($thread.comments.nodes[0].databaseId -eq $CommentId) {
            return $thread.id
        }
    }
    return $null
}

function Resolve-ReviewThread {
    param([string]$ThreadId)
    
    $mutation = @"
mutation {
  resolveReviewThread(input: { threadId: "$ThreadId" }) {
    thread {
      isResolved
    }
  }
}
"@
    
    $result = gh api graphql -f query="$mutation" | ConvertFrom-Json
    return $result.data.resolveReviewThread.thread.isResolved
}

# Main command dispatcher
switch ($Command) {
    "get-pr-number" {
        $prNum = Get-PRNumber -Branch $Arg1
        if ($prNum) {
            Write-Output $prNum
        } else {
            Write-Error "No PR found for current branch"
            exit 1
        }
    }
    
    "get-repo-info" {
        $repo = Get-RepoInfo
        Write-Output "$($repo.Owner)/$($repo.Name)"
    }
    
    "get-pr-comments" {
        if (-not $Arg1) {
            Write-Error "Usage: pr-review.ps1 get-pr-comments <PR_NUMBER>"
            exit 1
        }
        $comments = Get-PRComments -PRNumber ([int]$Arg1)
        $comments | ConvertTo-Json -Depth 10
    }
    
    "get-comment" {
        if (-not $Arg1 -or -not $Arg2) {
            Write-Error "Usage: pr-review.ps1 get-comment <PR_NUMBER> <GITHUB_COMMENT_ID>"
            Write-Error "Note: GITHUB_COMMENT_ID is the ID from inside the file (e.g., 2683822801), NOT the local file number"
            exit 1
        }
        $comment = Get-CommentDetails -PRNumber ([int]$Arg1) -CommentId ([int]$Arg2)
        $comment | ConvertTo-Json -Depth 10
    }
    
    "get-thread-id" {
        if (-not $Arg1 -or -not $Arg2) {
            Write-Error "Usage: pr-review.ps1 get-thread-id <PR_NUMBER> <GITHUB_COMMENT_ID>"
            Write-Error "Note: GITHUB_COMMENT_ID is the ID from inside the file (e.g., 2683822801), NOT the local file number"
            exit 1
        }
        $threadId = Get-ThreadId -PRNumber ([int]$Arg1) -CommentId ([int]$Arg2)
        if ($threadId) {
            Write-Output $threadId
        } else {
            Write-Error "Thread not found for comment $Arg2"
            exit 1
        }
    }
    
    "resolve-thread" {
        if (-not $Arg1) {
            Write-Error "Usage: pr-review.ps1 resolve-thread <THREAD_ID>"
            exit 1
        }
        $resolved = Resolve-ReviewThread -ThreadId $Arg1
        if ($resolved) {
            Write-Output "Thread resolved successfully"
        } else {
            Write-Error "Failed to resolve thread"
            exit 1
        }
    }
    
    default {
        Write-Output @"
PR Review Helper Tools

IMPORTANT: COMMENT_ID is the GitHub API comment ID (e.g., 2683822801), 
NOT the local file number (e.g., 3). Find the GitHub Comment ID inside 
the local file header: **Comment ID:** 2683822801

Commands:
  get-pr-number                    Get PR number for current branch
  get-repo-info                    Get owner/repo info
  get-pr-comments <PR>             Get all review comments for a PR
  get-comment <PR> <COMMENT_ID>    Get details for a specific comment
  get-thread-id <PR> <COMMENT_ID>  Get the thread ID for a comment (needed for resolving)
  resolve-thread <THREAD_ID>       Mark a review thread as resolved

Examples:
  pwsh .agents/tools/pr-review.ps1 get-pr-number
  pwsh .agents/tools/pr-review.ps1 get-pr-comments 21
  pwsh .agents/tools/pr-review.ps1 get-comment 21 2683822801
  pwsh .agents/tools/pr-review.ps1 get-thread-id 21 2683822801
  pwsh .agents/tools/pr-review.ps1 resolve-thread "PRRT_kwDONdef123"
"@
    }
}
