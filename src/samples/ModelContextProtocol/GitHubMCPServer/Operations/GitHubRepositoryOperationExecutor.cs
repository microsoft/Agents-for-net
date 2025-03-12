using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Agents.Mcp.Core.Handlers.Contracts.ClientMethods.Logging;
using Microsoft.Agents.Mcp.Core.Payloads;
using Microsoft.Agents.Mcp.Server.GitHubMCPServer.DataModel;
using Microsoft.Agents.Mcp.Server.Methods.Tools.ToolsCall.Handlers;
using Microsoft.Extensions.Configuration;
using System;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Mcp.Server.GitHubMCPServer.Operations;



public class GitHubSearchRepositoriesOperationExecutor : McpToolExecutorBase<GitHubSearchRepositoriesInput, GitHubSearchRepositoriesOutput>
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public GitHubSearchRepositoriesOperationExecutor(IConfiguration configuration, HttpClient? httpClient = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _httpClient = httpClient ?? new HttpClient();
    }

    public override string Id => "GitHub_SearchRepositories";
    public override string Description => "Searches for GitHub repositories based on the provided query";

    public override async Task<GitHubSearchRepositoriesOutput> ExecuteAsync(McpRequest<GitHubSearchRepositoriesInput> payload, IMcpContext context, CancellationToken ct)
    {
        var query = payload.Parameters.Query;
        var page = payload.Parameters.Page ?? 1;
        var perPage = payload.Parameters.PerPage ?? 30;

        // Log the operation
        await context.PostNotificationAsync(new McpLogNotification<string>(
            new NotificationParameters<string>()
            {
                Level = "notice",
                Logger = "echo",
                Data = $"Searching GitHub repositories with query: {query}"
            }), ct);

        // Set up the request
        var token = await GetGitHubTokenFromContext(context);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MicrosoftAgentsTool", "1.0"));

        // Build the query URL with parameters
        var encodedQuery = Uri.EscapeDataString(query);
        var url = $"https://api.github.com/search/repositories?q={encodedQuery}&page={page}&per_page={perPage}";

        // Make the API call
        var response = await _httpClient.GetAsync(url, ct);

        // Ensure success
        response.EnsureSuccessStatusCode();

        // Parse the response
        var responseContent = await response.Content.ReadAsStringAsync(ct);
        var searchResponse = JsonSerializer.Deserialize<GitHubSearchResponse>(responseContent)
            ?? throw new InvalidOperationException("Failed to deserialize GitHub API search response");

        // Map to our output type
        var items = searchResponse.Items.Select(item => new GitHubRepositoryItem
        {
            Name = item.Name,
            FullName = item.FullName,
            HtmlUrl = item.HtmlUrl,
            Description = item.Description ?? string.Empty,
            Private = item.Private,
            Owner = new GitHubOwnerItem
            {
                Login = item.Owner.Login,
                HtmlUrl = item.Owner.HtmlUrl
            }
        }).ToArray();

        return new GitHubSearchRepositoriesOutput
        {
            TotalCount = searchResponse.TotalCount,
            IncompleteResults = searchResponse.IncompleteResults,
            Items = items
        };
    }

    private Task<string> GetGitHubTokenFromContext(IMcpContext context)
    {
        // Get the token from the configuration (reused from CreateRepo executor)
        var token = _configuration["GitHub:PersonalAccessToken"]
            ?? _configuration["GitHub:PAT"]
            ?? _configuration.GetSection("GitHub").GetValue<string>("PersonalAccessToken");

        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException("GitHub Personal Access Token not found in configuration. " +
                "Please ensure it is set in appsettings.json or other configuration sources under GitHub:PersonalAccessToken.");
        }

        return Task.FromResult(token);
    }

    // Classes to deserialize GitHub API search response
    private class GitHubSearchResponse
    {
        public int TotalCount { get; set; }
        public bool IncompleteResults { get; set; }
        public GitHubRepositoryResponseItem[] Items { get; set; } = Array.Empty<GitHubRepositoryResponseItem>();
    }

    private class GitHubRepositoryResponseItem
    {
        public string Name { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string HtmlUrl { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool Private { get; set; }
        public GitHubOwnerResponse Owner { get; set; } = new();
    }

    private class GitHubOwnerResponse
    {
        public string Login { get; set; } = string.Empty;
        public string HtmlUrl { get; set; } = string.Empty;
    }
}

public class GitHubRepositoryOperationExecutor : McpToolExecutorBase<GitHubCreateRepoInput, GitHubCreateRepoOutput>
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public GitHubRepositoryOperationExecutor(IConfiguration configuration, HttpClient? httpClient = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _httpClient = httpClient ?? new HttpClient();
    }

    public override string Id => "GitHub_CreateRepository";
    public override string Description => "Creates a new GitHub repository";

    public override async Task<GitHubCreateRepoOutput> ExecuteAsync(McpRequest<GitHubCreateRepoInput> payload, IMcpContext context, CancellationToken ct)
    {
        var name = payload.Parameters.Name;
        var description = payload.Parameters.Description;
        var isPrivate = payload.Parameters.Private ?? false;
        var autoInit = payload.Parameters.AutoInit ?? true;

        // Log the operation
        await context.PostNotificationAsync(new McpLogNotification<string>(
            new NotificationParameters<string>()
            {
                Level = "notice",
                Logger = "echo",
                Data = $"Creating GitHub repository: {name}"
            }), ct);

        // Create request body
        var requestBody = new
        {
            name,
            description,
            @private = isPrivate,
            auto_init = autoInit
        };

        // Serialize to JSON
        var jsonContent = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        // Get token from context (assuming it's stored in context)
        var token = await GetGitHubTokenFromContext(context);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MicrosoftAgentsTool", "1.0"));

        // Make the API call
        var response = await _httpClient.PostAsync("https://api.github.com/user/repos", content, ct);

        // Ensure success
        response.EnsureSuccessStatusCode();

        // Parse the response
        var responseContent = await response.Content.ReadAsStringAsync(ct);
        var repoData = JsonSerializer.Deserialize<GitHubRepositoryResponse>(responseContent)
            ?? throw new InvalidOperationException("Failed to deserialize GitHub API response");

        // Create the output
        var result = new GitHubCreateRepoOutput()
        {
            RepoUrl = repoData.HtmlUrl,
            Owner = repoData.Owner.Login,
            RepoName = repoData.Name,
            IsPrivate = repoData.Private
        };

        return result;
    }

    private Task<string> GetGitHubTokenFromContext(IMcpContext context)
    {
        // Get the token from the configuration
        var token = _configuration["GitHub:PersonalAccessToken"]
            ?? _configuration["GitHub:PAT"]
            ?? _configuration.GetSection("GitHub").GetValue<string>("PersonalAccessToken");

        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException("GitHub Personal Access Token not found in configuration. " +
                "Please ensure it is set in appsettings.json or other configuration sources under GitHub:PersonalAccessToken.");
        }

        return Task.FromResult(token);
    }

    // Classes to deserialize GitHub API response
    private class GitHubRepositoryResponse
    {
        public string Name { get; set; } = string.Empty;
        public string HtmlUrl { get; set; } = string.Empty;
        public bool Private { get; set; }
        public GitHubOwner Owner { get; set; } = new();
    }

    private class GitHubOwner
    {
        public string Login { get; set; } = string.Empty;
    }
}

public class GitHubForkRepositoryOperationExecutor : McpToolExecutorBase<GitHubForkRepositoryInput, GitHubForkRepositoryOutput>
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public GitHubForkRepositoryOperationExecutor(IConfiguration configuration, HttpClient? httpClient = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _httpClient = httpClient ?? new HttpClient();
    }

    public override string Id => "GitHub_ForkRepository";
    public override string Description => "Forks a GitHub repository";

    public override async Task<GitHubForkRepositoryOutput> ExecuteAsync(McpRequest<GitHubForkRepositoryInput> payload, IMcpContext context, CancellationToken ct)
    {
        var owner = payload.Parameters.Owner;
        var repo = payload.Parameters.Repo;
        var organization = payload.Parameters.Organization ?? string.Empty;

        // Log the operation
        await context.PostNotificationAsync(new McpLogNotification<string>(
            new NotificationParameters<string>()
            {
                Level = "notice",
                Logger = "echo",
                Data = $"Forking GitHub repository: {owner}/{repo}"
            }), ct);

        // Create request body if organization is specified
        object? requestBody = null;
        if (!string.IsNullOrEmpty(organization))
        {
            requestBody = new { organization };
        }

        // Set up the request
        var token = await GetGitHubTokenFromContext(context);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MicrosoftAgentsTool", "1.0"));

        // Prepare content if needed
        StringContent? content = null;
        if (requestBody != null)
        {
            var jsonContent = JsonSerializer.Serialize(requestBody);
            content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        }

        // Build the URL
        var url = $"https://api.github.com/repos/{owner}/{repo}/forks";

        // Make the API call
        var response = await (content != null
            ? _httpClient.PostAsync(url, content, ct)
            : _httpClient.PostAsync(url, new StringContent("", Encoding.UTF8, "application/json"), ct));

        // Ensure success
        response.EnsureSuccessStatusCode();

        // Parse the response
        var responseContent = await response.Content.ReadAsStringAsync(ct);
        var repoData = JsonSerializer.Deserialize<GitHubRepositoryResponse>(responseContent)
            ?? throw new InvalidOperationException("Failed to deserialize GitHub API fork response");

        // Create the output
        var result = new GitHubForkRepositoryOutput()
        {
            RepoUrl = repoData.HtmlUrl,
            Owner = repoData.Owner.Login,
            RepoName = repoData.Name,
            IsPrivate = repoData.Private
        };

        return result;
    }

    private Task<string> GetGitHubTokenFromContext(IMcpContext context)
    {
        // Get the token from the configuration (reused from CreateRepo executor)
        var token = _configuration["GitHub:PersonalAccessToken"]
            ?? _configuration["GitHub:PAT"]
            ?? _configuration.GetSection("GitHub").GetValue<string>("PersonalAccessToken");

        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException("GitHub Personal Access Token not found in configuration. " +
                "Please ensure it is set in appsettings.json or other configuration sources under GitHub:PersonalAccessToken.");
        }

        return Task.FromResult(token);
    }

    // Classes to deserialize GitHub API response (reused from CreateRepo executor)
    private class GitHubRepositoryResponse
    {
        public string Name { get; set; } = string.Empty;
        public string HtmlUrl { get; set; } = string.Empty;
        public bool Private { get; set; }
        public GitHubOwner Owner { get; set; } = new();
    }

    private class GitHubOwner
    {
        public string Login { get; set; } = string.Empty;
    }
}