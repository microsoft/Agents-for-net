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
    public class GitHubListCommitsOperationExecutor : McpToolExecutorBase<GitHubListCommitsInput, GitHubListCommitsOutput>
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public GitHubListCommitsOperationExecutor(IConfiguration configuration, HttpClient? httpClient = null)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient = httpClient ?? new HttpClient();
        }

        public override string Id => "GitHub_ListCommits";
        public override string Description => "Lists commits for a GitHub repository.";

        public override async Task<GitHubListCommitsOutput> ExecuteAsync(McpRequest<GitHubListCommitsInput> payload, IMcpContext context, CancellationToken ct)
        {
            var owner = payload.Parameters.Owner;
            var repo = payload.Parameters.Repo;
            var page = payload.Parameters.Page ?? 1;
            var perPage = payload.Parameters.PerPage ?? 30;
            var sha = payload.Parameters.Sha ?? string.Empty;

            await context.PostNotificationAsync(new McpLogNotification<string>(
                new NotificationParameters<string>
                {
                    Level = "notice",
                    Logger = "echo",
                    Data = $"Fetching commits for repository {owner}/{repo}"
                }), ct);

            var token = await GetGitHubTokenFromContext(context);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MicrosoftAgentsTool", "1.0"));

            var url = $"https://api.github.com/repos/{owner}/{repo}/commits?page={page}&per_page={perPage}";
            if (!string.IsNullOrEmpty(sha))
            {
                url += $"&sha={sha}";
            }

            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(ct);
            var commits = JsonSerializer.Deserialize<GitHubCommitResponse[]>(responseContent)
                ?? throw new InvalidOperationException("Failed to deserialize GitHub API response");

            return new GitHubListCommitsOutput { Commits = commits };
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

   
}
