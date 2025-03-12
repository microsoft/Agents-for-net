using System;

namespace Microsoft.Agents.Mcp.Server.GitHubMCPServer.DataModel
{
    public class GitHubListCommitsInput
    {
        public string Owner { get; set; } = string.Empty;
        public string Repo { get; set; } = string.Empty;
        public int? Page { get; set; }
        public int? PerPage { get; set; }
        public string? Sha { get; set; }
    }

    public class GitHubListCommitsOutput
    {
        public GitHubCommitResponse[] Commits { get; set; } = Array.Empty<GitHubCommitResponse>();
    }

    public class GitHubCommitResponse
    {
        public string Sha { get; set; } = string.Empty;
        public GitHubCommitDetails Commit { get; set; } = new();
    }

    public class GitHubCommitDetails
    {
        public GitHubCommitAuthor Author { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }

    public class GitHubCommitAuthor
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
    }
}
