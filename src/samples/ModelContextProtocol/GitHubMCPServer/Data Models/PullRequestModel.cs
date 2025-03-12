using System;
using System.Collections.Generic;
using System.ComponentModel;


namespace Microsoft.Agents.Mcp.Server.GitHubMCPServer.DataModel
{
    #region Data Models

    public class GitHubPullRequestFile
    {
        public string Sha { get; set; } = string.Empty;
        public string Filename { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // 'added', 'removed', 'modified', 'renamed', 'copied', 'changed', 'unchanged'
        public int Additions { get; set; }
        public int Deletions { get; set; }
        public int Changes { get; set; }
        public string BlobUrl { get; set; } = string.Empty;
        public string RawUrl { get; set; } = string.Empty;
        public string ContentsUrl { get; set; } = string.Empty;
        public string? Patch { get; set; }
    }

    public class GitHubStatusCheck
    {
        public string Url { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty; // 'error', 'failure', 'pending', 'success'
        public string? Description { get; set; }
        public string? TargetUrl { get; set; }
        public string Context { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
    }

    public class GitHubCombinedStatus
    {
        public string State { get; set; } = string.Empty; // 'error', 'failure', 'pending', 'success'
        public IEnumerable<GitHubStatusCheck> Statuses { get; set; } = Array.Empty<GitHubStatusCheck>();
        public string Sha { get; set; } = string.Empty;
        public int TotalCount { get; set; }
    }

    public class GitHubPullRequestComment
    {
        public string Url { get; set; } = string.Empty;
        public int Id { get; set; }
        public string NodeId { get; set; } = string.Empty;
        public int? PullRequestReviewId { get; set; }
        public string DiffHunk { get; set; } = string.Empty;
        public string? Path { get; set; }
        public int? Position { get; set; }
        public int? OriginalPosition { get; set; }
        public string CommitId { get; set; } = string.Empty;
        public string OriginalCommitId { get; set; } = string.Empty;
        public GitHubUser User { get; set; } = new GitHubUser();
        public string Body { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
        public string HtmlUrl { get; set; } = string.Empty;
        public string PullRequestUrl { get; set; } = string.Empty;
        public string AuthorAssociation { get; set; } = string.Empty;
        public GitHubLinks Links { get; set; } = new GitHubLinks();
    }

    public class GitHubLinks
    {
        public GitHubLink Self { get; set; } = new GitHubLink();
        public GitHubLink Html { get; set; } = new GitHubLink();
        public GitHubLink PullRequest { get; set; } = new GitHubLink();
    }

    public class GitHubLink
    {
        public string Href { get; set; } = string.Empty;
    }

    public class GitHubPullRequestReview
    {
        public int Id { get; set; }
        public string NodeId { get; set; } = string.Empty;
        public GitHubUser User { get; set; } = new GitHubUser();
        public string? Body { get; set; }
        public string State { get; set; } = string.Empty; // 'APPROVED', 'CHANGES_REQUESTED', 'COMMENTED', 'DISMISSED', 'PENDING'
        public string HtmlUrl { get; set; } = string.Empty;
        public string PullRequestUrl { get; set; } = string.Empty;
        public string CommitId { get; set; } = string.Empty;
        public string? SubmittedAt { get; set; }
        public string AuthorAssociation { get; set; } = string.Empty;
    }

    public class GitHubUser
    {
        public string Login { get; set; } = string.Empty;
        public int Id { get; set; }
        public string NodeId { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public string GravatarId { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string HtmlUrl { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool SiteAdmin { get; set; }
    }

    public class GitHubRef
    {
        public string Label { get; set; } = string.Empty;
        public string Ref { get; set; } = string.Empty;
        public string Sha { get; set; } = string.Empty;
        public GitHubUser User { get; set; } = new GitHubUser();
        public GitHubRepository Repo { get; set; } = new GitHubRepository();
    }

    public class GitHubRepository
    {
        public int Id { get; set; }
        public string NodeId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public GitHubUser Owner { get; set; } = new GitHubUser();
        public bool Private { get; set; }
        public string HtmlUrl { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool Fork { get; set; }
        public string Url { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
        public string PushedAt { get; set; } = string.Empty;
        public string DefaultBranch { get; set; } = string.Empty;
    }

    public class GitHubPullRequest
    {
        public int Id { get; set; }
        public string NodeId { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string HtmlUrl { get; set; } = string.Empty;
        public int Number { get; set; }
        public string State { get; set; } = string.Empty;
        public bool Locked { get; set; }
        public string Title { get; set; } = string.Empty;
        public GitHubUser User { get; set; } = new GitHubUser();
        public string? Body { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
        public string? ClosedAt { get; set; }
        public string? MergedAt { get; set; }
        public string? MergeCommitSha { get; set; }
        public GitHubUser? Assignee { get; set; }
        public List<GitHubUser> Assignees { get; set; } = new List<GitHubUser>();
        public List<GitHubUser> RequestedReviewers { get; set; } = new List<GitHubUser>();
        public GitHubRef Head { get; set; } = new GitHubRef();
        public GitHubRef Base { get; set; } = new GitHubRef();
        public bool Draft { get; set; }
        public bool Merged { get; set; }
        public bool? Mergeable { get; set; }
        public bool? Rebaseable { get; set; }
        public string MergeableState { get; set; } = string.Empty;
        public int Comments { get; set; }
        public int ReviewComments { get; set; }
        public int Commits { get; set; }
        public int Additions { get; set; }
        public int Deletions { get; set; }
        public int ChangedFiles { get; set; }
    }
    public class GitHubMergeResult
    {
        public bool Merged { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Sha { get; set; } = string.Empty;
        public GitHubUser? MergedBy { get; set; }
        public string? MergeCommitSha { get; set; }
        public string? NodeId { get; set; }
        public string? Url { get; set; }
        public string? HtmlUrl { get; set; }
    }


    #endregion


    #region Input Models

    public class GitHubCreatePullRequestInput
    {
        
        [Description("Repository owner (username or organization)")]
        public string Owner { get; set; } = string.Empty;

        [Description("Repository name")]
        public string Repo { get; set; } = string.Empty;

        [Description("Pull request title")]
        public string Title { get; set; } = string.Empty;

        [Description("Pull request body/description")]
        public string? Body { get; set; }

        [Description("The name of the branch where your changes are implemented")]
        public string Head { get; set; } = string.Empty;

        [Description("The name of the branch you want the changes pulled into")]
        public string Base { get; set; } = string.Empty;

        [Description("Whether to create the pull request as a draft")]
        public bool? Draft { get; set; }

        [Description("Whether maintainers can modify the pull request")]
        public bool? MaintainerCanModify { get; set; }
    }

    public class GitHubGetPullRequestInput
    {
        [Description("Repository owner (username or organization)")]
        public string Owner { get; set; } = string.Empty;

        [Description("Repository name")]
        public string Repo { get; set; } = string.Empty;

        [Description("Pull request number")]
        public int PullNumber { get; set; }
    }

    public class GitHubGetPullRequestReviewsInput
    {
        [Description("Repository owner (username or organization)")]
        public string Owner { get; set; } = string.Empty;

        [Description("Repository name")]
        public string Repo { get; set; } = string.Empty;

        [Description("Pull request number")]
        public int PullNumber { get; set; }
    }

    public class GitHubListPullRequestsInput
    {
        [Description("Repository owner (username or organization)")]
        public string Owner { get; set; } = string.Empty;

        [Description("Repository name")]
        public string Repo { get; set; } = string.Empty;

        [Description("State of the pull request (e.g., open, closed, all)")]
        public string? State { get; set; }

        [Description("Filter pulls by head user/branch name")]
        public string? Head { get; set; }

        [Description("Filter pulls by base branch name")]
        public string? Base { get; set; }

        [Description("Sort by created, updated, popularity, long-running")]
        public string? Sort { get; set; }

        [Description("Sort direction (asc or desc)")]
        public string? Direction { get; set; }

        [Description("Number of results per page (max 100)")]
        public int? PerPage { get; set; }

        [Description("Page number of the results to fetch")]
        public int? Page { get; set; }
    }
    public class GitHubMergePullRequestInput
    {
        [Description("Repository owner (username or organization)")]
        public string Owner { get; set; } = string.Empty;

        [Description("Repository name")]
        public string Repo { get; set; } = string.Empty;

        [Description("Pull request number")]
        public int PullNumber { get; set; }

        [Description("Title for the merge commit message")]
        public string? CommitTitle { get; set; }

        [Description("Extra detail to append to the automatic commit message")]
        public string? CommitMessage { get; set; }

        [Description("Merge method to use (merge, squash, rebase)")]
        public string? MergeMethod { get; set; }
    }


    #endregion

    #region Output Models

    public class GitHubCreatePullRequestOutput
    {
        public GitHubPullRequest PullRequest { get; set; } = new GitHubPullRequest();
        public string Url { get; set; } = string.Empty;
        public int Number { get; set; }
    }

    public class GitHubGetPullRequestOutput
    {
        public GitHubPullRequest PullRequest { get; set; } = new GitHubPullRequest();
    }

    public class GitHubListPullRequestsOutput
    {
        public List<GitHubPullRequest> PullRequests { get; set; } = new List<GitHubPullRequest>();
    }

    public class GitHubCreatePullRequestReviewOutput
    {
        public GitHubPullRequestReview Review { get; set; } = new GitHubPullRequestReview();
    }

    public class GitHubMergePullRequestOutput
    {
        public GitHubMergeResult MergeResult { get; set; } = new GitHubMergeResult();
    }

    public class GitHubGetPullRequestFilesOutput
    {
        public List<GitHubPullRequestFile> Files { get; set; } = new List<GitHubPullRequestFile>();
    }

    public class GitHubGetPullRequestStatusOutput
    {
        public GitHubCombinedStatus Status { get; set; } = new GitHubCombinedStatus();
    }

    public class GitHubUpdatePullRequestBranchOutput
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class GitHubGetPullRequestCommentsOutput
    {
        public List<GitHubPullRequestComment> Comments { get; set; } = new List<GitHubPullRequestComment>();
    }

    public class GitHubGetPullRequestReviewsOutput
    {
        public List<GitHubPullRequestReview> Reviews { get; set; } = new List<GitHubPullRequestReview>();
    }
    
    #endregion

}