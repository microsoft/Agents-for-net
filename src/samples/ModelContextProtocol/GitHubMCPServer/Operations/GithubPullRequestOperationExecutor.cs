using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Agents.Mcp.Core.Handlers.Contracts.ClientMethods.Logging;
using Microsoft.Agents.Mcp.Core.Payloads;
using Microsoft.Agents.Mcp.Server.GitHubMCPServer.DataModel;
using Microsoft.Agents.Mcp.Server.Methods.Tools.ToolsCall.Handlers;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Mcp.Server.GitHubMCPServer.Operations
{


    #region Operation Executors

    public class GitHubCreatePullRequestOperationExecutor : McpToolExecutorBase<GitHubCreatePullRequestInput, GitHubCreatePullRequestOutput>
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public GitHubCreatePullRequestOperationExecutor(IConfiguration configuration, HttpClient? httpClient = null)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient = httpClient ?? new HttpClient();
        }

        public override string Id => "GitHub_CreatePullRequest";
        public override string Description => "Creates a new pull request in a GitHub repository";

        public override async Task<GitHubCreatePullRequestOutput> ExecuteAsync(McpRequest<GitHubCreatePullRequestInput> payload, IMcpContext context, CancellationToken ct)
        {
            var owner = payload.Parameters.Owner;
            var repo = payload.Parameters.Repo;
            var title = payload.Parameters.Title;
            var body = payload.Parameters.Body ?? string.Empty;
            var head = payload.Parameters.Head;
            var baseBranch = payload.Parameters.Base;
            var draft = payload.Parameters.Draft ?? false;
            var maintainerCanModify = payload.Parameters.MaintainerCanModify ?? false;

            // Log the operation
            await context.PostNotificationAsync(new McpLogNotification<string>(
                new NotificationParameters<string>()
                {
                    Level = "notice",
                    Logger = "echo",
                    Data = $"Creating pull request in {owner}/{repo}: {title}"
                }), ct);

            // Create request body
            var requestBody = new
            {
                title,
                body,
                head,
                @base = baseBranch,
                draft,
                maintainer_can_modify = maintainerCanModify
            };

            // Set up the request
            var token = await GetGitHubTokenFromContext(context);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MicrosoftAgentsTool", "1.0"));
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            // Serialize to JSON
            var jsonContent = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Make the API call
            var response = await _httpClient.PostAsync($"https://api.github.com/repos/{owner}/{repo}/pulls", content, ct);

            // Ensure success
            response.EnsureSuccessStatusCode();

            // Parse the response
            var responseContent = await response.Content.ReadAsStringAsync(ct);
            var pullRequest = JsonSerializer.Deserialize<GitHubPullRequest>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to deserialize GitHub API response");

            // Create the output
            var result = new GitHubCreatePullRequestOutput
            {
                PullRequest = pullRequest,
                Url = pullRequest.HtmlUrl,
                Number = pullRequest.Number
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
                throw new InvalidOperationException("GitHub Personal Access Token not found in configuration.");
            }

            return Task.FromResult(token);
        }
    }

    public class GitHubListPullRequestsOperationExecutor : McpToolExecutorBase<GitHubListPullRequestsInput, GitHubListPullRequestsOutput>
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public GitHubListPullRequestsOperationExecutor(IConfiguration configuration, HttpClient? httpClient = null)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient = httpClient ?? new HttpClient();
        }

        public override string Id => "GitHub_ListPullRequests";
        public override string Description => "Lists pull requests in a GitHub repository";

        public override async Task<GitHubListPullRequestsOutput> ExecuteAsync(McpRequest<GitHubListPullRequestsInput> payload, IMcpContext context, CancellationToken ct)
        {
            var owner = payload.Parameters.Owner;
            var repo = payload.Parameters.Repo;
            var state = payload.Parameters.State;
            var head = payload.Parameters.Head;
            var baseBranch = payload.Parameters.Base;
            var sort = payload.Parameters.Sort;
            var direction = payload.Parameters.Direction;
            var perPage = payload.Parameters.PerPage;
            var page = payload.Parameters.Page;

            // Log the operation
            await context.PostNotificationAsync(new McpLogNotification<string>(
                new NotificationParameters<string>()
                {
                    Level = "notice",
                    Logger = "echo",
                    Data = $"Listing pull requests for {owner}/{repo}"
                }), ct);

            // Set up the request
            var token = await GetGitHubTokenFromContext(context);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MicrosoftAgentsTool", "1.0"));
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            // Build the URL with parameters
            var url = new UriBuilder($"https://api.github.com/repos/{owner}/{repo}/pulls");
            var queryParams = new List<string>();

            if (!string.IsNullOrEmpty(state)) queryParams.Add($"state={state}");
            if (!string.IsNullOrEmpty(head)) queryParams.Add($"head={head}");
            if (!string.IsNullOrEmpty(baseBranch)) queryParams.Add($"base={baseBranch}");
            if (!string.IsNullOrEmpty(sort)) queryParams.Add($"sort={sort}");
            if (!string.IsNullOrEmpty(direction)) queryParams.Add($"direction={direction}");
            if (perPage.HasValue) queryParams.Add($"per_page={perPage}");
            if (page.HasValue) queryParams.Add($"page={page}");

            if (queryParams.Count > 0)
            {
                url.Query = string.Join("&", queryParams);
            }

            // Make the API call
            var response = await _httpClient.GetAsync(url.Uri, ct);

            // Ensure success
            response.EnsureSuccessStatusCode();

            // Parse the response
            var responseContent = await response.Content.ReadAsStringAsync(ct);
            var pullRequests = JsonSerializer.Deserialize<List<GitHubPullRequest>>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to deserialize GitHub API response");

            // Create the output
            var result = new GitHubListPullRequestsOutput
            {
                PullRequests = pullRequests
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
                throw new InvalidOperationException("GitHub Personal Access Token not found in configuration.");
            }

            return Task.FromResult(token);
        }
    }

    public class GitHubMergePullRequestOperationExecutor : McpToolExecutorBase<GitHubMergePullRequestInput, GitHubMergePullRequestOutput>
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public GitHubMergePullRequestOperationExecutor(IConfiguration configuration, HttpClient? httpClient = null)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient = httpClient ?? new HttpClient();
        }

        public override string Id => "GitHub_MergePullRequest";
        public override string Description => "Merges a pull request in a GitHub repository";

        public override async Task<GitHubMergePullRequestOutput> ExecuteAsync(McpRequest<GitHubMergePullRequestInput> payload, IMcpContext context, CancellationToken ct)
        {
            var owner = payload.Parameters.Owner;
            var repo = payload.Parameters.Repo;
            var pullNumber = payload.Parameters.PullNumber;
            var commitTitle = payload.Parameters.CommitTitle;
            var commitMessage = payload.Parameters.CommitMessage;
            var mergeMethod = payload.Parameters.MergeMethod;

            // Log the operation
            await context.PostNotificationAsync(new McpLogNotification<string>(
                new NotificationParameters<string>()
                {
                    Level = "notice",
                    Logger = "echo",
                    Data = $"Merging pull request {pullNumber} in {owner}/{repo}"
                }), ct);

            // Create request body
            var requestBody = new
            {
                commit_title = commitTitle,
                commit_message = commitMessage,
                merge_method = mergeMethod
            };

            // Set up the request
            var token = await GetGitHubTokenFromContext(context);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MicrosoftAgentsTool", "1.0"));
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            // Serialize to JSON
            var jsonContent = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Make the API call
            var response = await _httpClient.PutAsync($"https://api.github.com/repos/{owner}/{repo}/pulls/{pullNumber}/merge", content, ct);

            // Ensure success
            response.EnsureSuccessStatusCode();

            // Parse the response
            var responseContent = await response.Content.ReadAsStringAsync(ct);
            var mergeResult = JsonSerializer.Deserialize<GitHubMergeResult>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to deserialize GitHub API response");

            // Create the output
            var result = new GitHubMergePullRequestOutput
            {
                MergeResult = mergeResult
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
                throw new InvalidOperationException("GitHub Personal Access Token not found in configuration.");
            }

            return Task.FromResult(token);
        }
    }

    public class GitHubGetPullRequestOperationExecutor : McpToolExecutorBase<GitHubGetPullRequestInput, GitHubGetPullRequestOutput>
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public GitHubGetPullRequestOperationExecutor(IConfiguration configuration, HttpClient? httpClient = null)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient = httpClient ?? new HttpClient();
        }

        public override string Id => "GitHub_GetPullRequest";
        public override string Description => "Gets details of a specific pull request";

        public override async Task<GitHubGetPullRequestOutput> ExecuteAsync(McpRequest<GitHubGetPullRequestInput> payload, IMcpContext context, CancellationToken ct)
        {
            var owner = payload.Parameters.Owner;
            var repo = payload.Parameters.Repo;
            var pullNumber = payload.Parameters.PullNumber;

            // Log the operation
            await context.PostNotificationAsync(new McpLogNotification<string>(
                new NotificationParameters<string>()
                {
                    Level = "notice",
                    Logger = "echo",
                    Data = $"Getting pull request {pullNumber} from {owner}/{repo}"
                }), ct);

            // Set up the request
            var token = await GetGitHubTokenFromContext(context);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MicrosoftAgentsTool", "1.0"));
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            // Make the API call
            var response = await _httpClient.GetAsync($"https://api.github.com/repos/{owner}/{repo}/pulls/{pullNumber}", ct);

            // Ensure success
            response.EnsureSuccessStatusCode();

            // Parse the response
            var responseContent = await response.Content.ReadAsStringAsync(ct);
            var pullRequest = JsonSerializer.Deserialize<GitHubPullRequest>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to deserialize GitHub API response");

            // Create the output
            var result = new GitHubGetPullRequestOutput
            {
                PullRequest = pullRequest
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
                throw new InvalidOperationException("GitHub Personal Access Token not found in configuration.");
            }

            return Task.FromResult(token);
        }
    }
    #endregion

   }