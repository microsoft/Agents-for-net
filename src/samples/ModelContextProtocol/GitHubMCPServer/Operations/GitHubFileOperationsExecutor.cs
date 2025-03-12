using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Agents.Mcp.Core.Handlers.Contracts.ClientMethods.Logging;
using Microsoft.Agents.Mcp.Core.Payloads;
using Microsoft.Agents.Mcp.Server.GitHubMCPServer.DataModel;
using Microsoft.Agents.Mcp.Server.Methods.Tools.ToolsCall.Handlers;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Mcp.Server.GitHubMCPServer.Operations
{
    public class GitHubFileOperationsExecutor : McpToolExecutorBase<GitHubFileOperationsInput, GitHubFileOperationsOutput>
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public GitHubFileOperationsExecutor(IConfiguration configuration, HttpClient? httpClient = null)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient = httpClient ?? new HttpClient();
        }

        public override string Id => "GitHub_FileOperations";
        public override string Description => "Handles GitHub file operations such as create, update, and retrieve.";

        public override async Task<GitHubFileOperationsOutput> ExecuteAsync(McpRequest<GitHubFileOperationsInput> payload, IMcpContext context, CancellationToken ct)
        {
            var owner = payload.Parameters.Owner;
            var repo = payload.Parameters.Repo;
            var path = payload.Parameters.Path;
            var operationType = payload.Parameters.OperationType;
            var branch = payload.Parameters.Branch;
            var content = payload.Parameters.Content;
            var message = payload.Parameters.Message;

            await context.PostNotificationAsync(new McpLogNotification<string>(
                new NotificationParameters<string>
                {
                    Level = "notice",
                    Logger = "echo",
                    Data = $"Performing {operationType} operation on {owner}/{repo}/{path}"
                }), ct);

            var token = await GetGitHubTokenFromContext(context);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MicrosoftAgentsTool", "1.0"));

            switch (operationType.ToLower())
            {
                case "get":
                    return new GitHubFileOperationsOutput { FileContent = await GetFileContents(owner, repo, path, branch, ct) };
                case "create":
                case "update":
                    return new GitHubFileOperationsOutput { Response = await CreateOrUpdateFile(owner, repo, path, content, message, branch, ct) };
                default:
                    throw new InvalidOperationException("Unsupported file operation type.");
            }
        }

        private async Task<string> GetFileContents(string owner, string repo, string path, string? branch, CancellationToken ct)
        {
            string url = $"https://api.github.com/repos/{owner}/{repo}/contents/{path}";
            if (!string.IsNullOrEmpty(branch))
            {
                url += $"?ref={branch}";
            }

            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var fileResponse = JsonSerializer.Deserialize<GitHubFileResponse>(json);
            return Encoding.UTF8.GetString(Convert.FromBase64String(fileResponse?.Content ?? ""));
        }

        private async Task<string> CreateOrUpdateFile(string owner, string repo, string path, string content, string message, string branch, CancellationToken ct)
        {
            string url = $"https://api.github.com/repos/{owner}/{repo}/contents/{path}";
            string encodedContent = Convert.ToBase64String(Encoding.UTF8.GetBytes(content));

            string? sha = null;
            try
            {
                var existingContent = await GetFileContents(owner, repo, path, branch, ct);
                if (!string.IsNullOrEmpty(existingContent))
                {
                    sha = existingContent; // This should be updated to extract SHA from response
                }
            }
            catch
            {
                // File doesn't exist, proceed with creation
            }

            var requestBody = new
            {
                message,
                content = encodedContent,
                branch,
                sha
            };

            var requestContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync(url, requestContent, ct);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync(ct);
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
