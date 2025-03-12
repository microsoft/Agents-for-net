using System.Collections.Generic;
using System.ComponentModel;
using System;

namespace Microsoft.Agents.Mcp.Server.GitHubMCPServer.DataModel
{

    #region Input/Output Structs

    public struct GitHubGetIssueInput
    {
        [Description("Repository owner")]
        public required string Owner { get; init; }

        [Description("Repository name")]
        public required string Repo { get; init; }

        [Description("Issue number")]
        public required int IssueNumber { get; init; }
    }

    public struct GitHubCreateIssueInput
    {
        [Description("Repository owner")]
        public required string Owner { get; init; }

        [Description("Repository name")]
        public required string Repo { get; init; }

        [Description("Issue title")]
        public required string Title { get; init; }

        [Description("Issue body")]
        public string? Body { get; init; }

        [Description("GitHub usernames to assign to this issue")]
        public List<string>? Assignees { get; init; }

        [Description("Milestone ID to associate with this issue")]
        public int? Milestone { get; init; }

        [Description("Labels to associate with this issue")]
        public List<string>? Labels { get; init; }
    }

    public struct GitHubAddIssueCommentInput
    {
        [Description("Repository owner")]
        public required string Owner { get; init; }

        [Description("Repository name")]
        public required string Repo { get; init; }

        [Description("Issue number")]
        public required int IssueNumber { get; init; }

        [Description("Comment body")]
        public required string Body { get; init; }
    }

    public struct GitHubListIssuesInput
    {
        [Description("Repository owner")]
        public required string Owner { get; init; }

        [Description("Repository name")]
        public required string Repo { get; init; }

        [Description("Sort direction: asc or desc")]
        public string? Direction { get; init; }

        [Description("Comma-separated list of labels")]
        public List<string>? Labels { get; init; }

        [Description("Page number")]
        public int? Page { get; init; }

        [Description("Results per page")]
        public int? PerPage { get; init; }

        [Description("Only issues updated after this time (ISO 8601 format)")]
        public string? Since { get; init; }

        [Description("Sort field: created, updated, or comments")]
        public string? Sort { get; init; }

        [Description("Issue state: open, closed, or all")]
        public string? State { get; init; }
    }

    public struct GitHubUpdateIssueInput
    {
        [Description("Repository owner")]
        public required string Owner { get; init; }

        [Description("Repository name")]
        public required string Repo { get; init; }

        [Description("Issue number")]
        public required int IssueNumber { get; init; }

        [Description("Issue title")]
        public string? Title { get; init; }

        [Description("Issue body")]
        public string? Body { get; init; }

        [Description("GitHub usernames to assign to this issue")]
        public List<string>? Assignees { get; init; }

        [Description("Milestone ID to associate with this issue")]
        public int? Milestone { get; init; }

        [Description("Labels to associate with this issue")]
        public List<string>? Labels { get; init; }

        [Description("Issue state: open or closed")]
        public string? State { get; init; }
    }

    public struct GitHubIssueOutput
    {
        public required int Number { get; init; }
        public required string Title { get; init; }
        public string? Body { get; init; }
        public required string State { get; init; }
        public required string HtmlUrl { get; init; }
        public required string Creator { get; init; }
        public List<string>? Assignees { get; init; }
        public List<string>? Labels { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }

    public struct GitHubIssueCommentOutput
    {
        public required int Id { get; init; }
        public required string Body { get; init; }
        public required string Creator { get; init; }
        public required string HtmlUrl { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }

    public struct GitHubListIssuesOutput
    {
        public required List<GitHubIssueOutput> Issues { get; init; }
        public required int TotalCount { get; init; }
    }
    #endregion
    #region Response Models
    // Classes to deserialize GitHub API responses
    public class GitHubIssueResponse
    {
        public int Number { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Body { get; set; }
        public string State { get; set; } = string.Empty;
        public string HtmlUrl { get; set; } = string.Empty;
        public GitHubUserResponse User { get; set; } = new();
        public List<GitHubUserResponse>? Assignees { get; set; }
        public List<GitHubLabelResponse>? Labels { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class GitHubCommentResponse
    {
        public int Id { get; set; }
        public string Body { get; set; } = string.Empty;
        public string HtmlUrl { get; set; } = string.Empty;
        public GitHubUserResponse User { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class GitHubUserResponse
    {
        public string Login { get; set; } = string.Empty;
        public int Id { get; set; }
    }

    public class GitHubLabelResponse
    {
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
    }
    #endregion
}