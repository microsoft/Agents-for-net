using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Agents.Mcp.Core.Handlers.Contracts.ClientMethods.Logging;
using Microsoft.Agents.Mcp.Core.Payloads;
using Microsoft.Agents.Mcp.Server.Methods.Tools.ToolsCall.Handlers;
using Microsoft.Extensions.Configuration;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System;

namespace Microsoft.Agents.Mcp.Server.GitHubMCPServer.Operations;

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

public struct GitHubCreateRepoOutput
{
    public required string RepoUrl { get; init; }
    public required string Owner { get; init; }
    public required string RepoName { get; init; }
    public required bool IsPrivate { get; init; }
}

public class GitHubCreateRepoOperationExecutor : McpToolExecutorBase<GitHubCreateRepoInput, GitHubCreateRepoOutput>
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public GitHubCreateRepoOperationExecutor(IConfiguration configuration, HttpClient? httpClient = null)
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