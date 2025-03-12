using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Agents.Mcp.Core.Handlers.Contracts.ClientMethods.Logging;
using Microsoft.Agents.Mcp.Core.Payloads;
using Microsoft.Agents.Mcp.Server.GitHubMCPServer.DataModel;
using Microsoft.Agents.Mcp.Server.Methods.Tools.ToolsCall.Handlers;
using Microsoft.Extensions.Configuration;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Mcp.Server.GitHubMCPServer.Operations
{
    public class GitHubBranchOperationExecutor : McpToolExecutorBase<GitHubBranchInput, GitHubBranchOutput>
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public GitHubBranchOperationExecutor(IConfiguration configuration, HttpClient? httpClient = null)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient = httpClient ?? new HttpClient();
        }

        public override string Id => "GitHub_BranchOperations";
        public override string Description => "Handles GitHub branch operations such as fetching branch SHA, creating new branches, and updating branch references.";

        public override async Task<GitHubBranchOutput> ExecuteAsync(McpRequest<GitHubBranchInput> payload, IMcpContext context, CancellationToken ct)
        {
            var owner = payload.Parameters.Owner;
            var repo = payload.Parameters.Repo;
            var branch = payload.Parameters.Branch;
            var baseBranch = payload.Parameters.BaseBranch;
            var operation = payload.Parameters.Operation; // "getSHA", "createBranch", "updateBranch"

            await context.PostNotificationAsync(new McpLogNotification<string>(
                new NotificationParameters<string>
                {
                    Level = "notice",
                    Logger = "GitHub",
                    Data = $"Executing {operation} operation for branch {branch} in repository {owner}/{repo}"
                }), ct);

            var token = await GetGitHubTokenFromContext(context);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MicrosoftAgentsTool", "1.0"));

            switch (operation)
            {
                case "getSHA":
                    return new GitHubBranchOutput { Sha = await GetBranchSHA(owner, repo, branch, ct) };

                case "createBranch":
                    var sha = await GetBranchSHA(owner, repo, baseBranch, ct);
                    await CreateBranch(owner, repo, branch, sha, ct);
                    return new GitHubBranchOutput { Message = $"Branch {branch} created successfully from {baseBranch}" };

                case "updateBranch":
                    await UpdateBranchReference(owner, repo, branch, payload.Parameters.NewSha, ct);
                    return new GitHubBranchOutput { Message = $"Branch {branch} updated successfully" };

                default:
                    throw new InvalidOperationException("Invalid GitHub branch operation.");
            }
        }

        private async Task<string> GetBranchSHA(string owner, string repo, string branch, CancellationToken ct)
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/git/ref/heads/{branch}";
            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<GitHubBranchResponse>(responseContent)
                ?? throw new InvalidOperationException("Failed to deserialize GitHub branch response");

            return data.Object.Sha;
        }

        private async Task CreateBranch(string owner, string repo, string newBranch, string baseSha, CancellationToken ct)
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/git/refs";
            var payload = new { @ref = $"refs/heads/{newBranch}", sha = baseSha };
            var jsonContent = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, jsonContent, ct);
            response.EnsureSuccessStatusCode();
        }

        private async Task UpdateBranchReference(string owner, string repo, string branch, string newSha, CancellationToken ct)
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/git/refs/heads/{branch}";
            var payload = new { sha = newSha, force = true };
            var jsonContent = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PatchAsync(url, jsonContent, ct);
            response.EnsureSuccessStatusCode();
        }

        private Task<string> GetGitHubTokenFromContext(IMcpContext context)
        {
            var token = _configuration["GitHub:PersonalAccessToken"]
                ?? _configuration["GitHub:PAT"]
                ?? _configuration.GetSection("GitHub").GetValue<string>("PersonalAccessToken");

            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException("GitHub Personal Access Token not found in configuration.");
            }

            return Task.FromResult(token);
        }
    }

    public class GitHubBranchInput
    {
        public string Owner { get; set; } = string.Empty;
        public string Repo { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public string BaseBranch { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty; // getSHA, createBranch, updateBranch
        public string? NewSha { get; set; }
    }

    public class GitHubBranchOutput
    {
        public string? Sha { get; set; }
        public string? Message { get; set; }
    }

    public class GitHubBranchResponse
    {
        public GitHubObject Object { get; set; } = new GitHubObject();
    }

    public class GitHubObject
    {
        public string Sha { get; set; } = string.Empty;
    }
}
