using System.ComponentModel;

namespace Microsoft.Agents.Mcp.Server.GitHubMCPServer.DataModel
{
    // Create repository input model (matching the existing one in your code)
    public struct GitHubCreateRepoInput
    {
        [Description("Repository name")]
        public required string Name { get; init; }

        [Description("Repository description")]
        public string? Description { get; init; }

        [Description("Whether the repository should be private")]
        public bool? Private { get; init; }

        [Description("Initialize with README.md")]
        public bool? AutoInit { get; init; }
    }

    // Create repository output model (matching the existing one in your code)
    public struct GitHubCreateRepoOutput
    {
        public required string RepoUrl { get; init; }
        public required string Owner { get; init; }
        public required string RepoName { get; init; }
        public required bool IsPrivate { get; init; }
    }
    // Search repositories input model
    public struct GitHubSearchRepositoriesInput
    {
        [Description("Search query (see GitHub search syntax)")]
        public required string Query { get; init; }

        [Description("Page number for pagination (default: 1)")]
        public int? Page { get; init; }

        [Description("Number of results per page (default: 30, max: 100)")]
        public int? PerPage { get; init; }
    }

    // Search repositories output model
    public struct GitHubSearchRepositoriesOutput
    {
        public required int TotalCount { get; init; }
        public required bool IncompleteResults { get; init; }
        public required GitHubRepositoryItem[] Items { get; init; }
    }

    // Repository item in search results
    public struct GitHubRepositoryItem
    {
        public required string Name { get; init; }
        public required string FullName { get; init; }
        public required string HtmlUrl { get; init; }
        public required string Description { get; init; }
        public required bool Private { get; init; }
        public required GitHubOwnerItem Owner { get; init; }
    }

    // Owner item in search results
    public struct GitHubOwnerItem
    {
        public required string Login { get; init; }
        public required string HtmlUrl { get; init; }
    }

    // Fork repository input model
    public struct GitHubForkRepositoryInput
    {
        [Description("Repository owner (username or organization)")]
        public required string Owner { get; init; }

        [Description("Repository name")]
        public required string Repo { get; init; }

        [Description("Optional: organization to fork to (defaults to your personal account)")]
        public string? Organization { get; init; }
    }

    // Fork repository output model
    public struct GitHubForkRepositoryOutput
    {
        public required string RepoUrl { get; init; }
        public required string Owner { get; init; }
        public required string RepoName { get; init; }
        public required bool IsPrivate { get; init; }
    }
}
