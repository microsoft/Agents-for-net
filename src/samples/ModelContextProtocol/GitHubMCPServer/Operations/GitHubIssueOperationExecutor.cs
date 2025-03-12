using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Agents.Mcp.Core.Handlers.Contracts.ClientMethods.Logging;
using Microsoft.Agents.Mcp.Core.Payloads;
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
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Microsoft.Agents.Mcp.Server.GitHubMCPServer.Operations
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

    #region Get Issue
    public class GitHubIssueOperationExecutor : McpToolExecutorBase<GitHubGetIssueInput, GitHubIssueOutput>
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public GitHubIssueOperationExecutor(IConfiguration configuration, HttpClient? httpClient = null)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient = httpClient ?? new HttpClient();
        }

        public override string Id => "GitHub_GetIssue";
        public override string Description => "Gets a GitHub issue by number";

        public override async Task<GitHubIssueOutput> ExecuteAsync(McpRequest<GitHubGetIssueInput> payload, IMcpContext context, CancellationToken ct)
        {
            var owner = payload.Parameters.Owner;
            var repo = payload.Parameters.Repo;
            var issueNumber = payload.Parameters.IssueNumber;

            // Log the operation
            await context.PostNotificationAsync(new McpLogNotification<string>(
                new NotificationParameters<string>()
                {
                    Level = "notice",
                    Logger = "echo",
                    Data = $"Getting GitHub issue: {owner}/{repo}#{issueNumber}"
                }), ct);

            // Configure the HTTP client
            var token = await GetGitHubTokenFromContext(context);
            ConfigureHttpClient(token);

            // Make the API call
            var url = $"https://api.github.com/repos/{owner}/{repo}/issues/{issueNumber}";
            var response = await _httpClient.GetAsync(url, ct);

            // Ensure success
            response.EnsureSuccessStatusCode();

            // Parse the response
            var responseContent = await response.Content.ReadAsStringAsync(ct);
            var issueData = JsonSerializer.Deserialize<GitHubIssueResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to deserialize GitHub API response");

            // Create the output
            var result = MapIssueResponseToOutput(issueData);
          
            return result;
        }

        private void ConfigureHttpClient(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MicrosoftAgentsTool", "1.0"));
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
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

        private GitHubIssueOutput MapIssueResponseToOutput(GitHubIssueResponse issue)
        {
            return new GitHubIssueOutput
            {
                Number = issue.Number,
                Title = issue.Title,
                Body = issue.Body,
                State = issue.State,
                HtmlUrl = issue.HtmlUrl,
                Creator = issue.User.Login,
                Assignees = issue.Assignees?.Select(a => a.Login).ToList(),
                Labels = issue.Labels?.Select(l => l.Name).ToList(),
                CreatedAt = issue.CreatedAt,
                UpdatedAt = issue.UpdatedAt
            };
        }
    }
    #endregion

    #region Create Issue
    public class GitHubCreateIssueOperationExecutor : McpToolExecutorBase<GitHubCreateIssueInput, GitHubIssueOutput>
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public GitHubCreateIssueOperationExecutor(IConfiguration configuration, HttpClient? httpClient = null)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient = httpClient ?? new HttpClient();
        }

        public override string Id => "GitHub_CreateIssue";
        public override string Description => "Creates a new GitHub issue";

        public override async Task<GitHubIssueOutput> ExecuteAsync(McpRequest<GitHubCreateIssueInput> payload, IMcpContext context, CancellationToken ct)
        {
            var owner = payload.Parameters.Owner;
            var repo = payload.Parameters.Repo;
            var title = payload.Parameters.Title;
            var body = payload.Parameters.Body;
            var assignees = payload.Parameters.Assignees;
            var milestone = payload.Parameters.Milestone;
            var labels = payload.Parameters.Labels;

            // Log the operation
            await context.PostNotificationAsync(new McpLogNotification<string>(
                new NotificationParameters<string>()
                {
                    Level = "notice",
                    Logger = "echo",
                    Data = $"Creating GitHub issue in {owner}/{repo}: {title}"
                }), ct);

            // Create request body
            var requestBody = new
            {
                title,
                body,
                assignees,
                milestone,
                labels
            };

            // Serialize to JSON
            var jsonContent = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Configure the HTTP client
            var token = await GetGitHubTokenFromContext(context);
            ConfigureHttpClient(token);

            // Make the API call
            var url = $"https://api.github.com/repos/{owner}/{repo}/issues";
            var response = await _httpClient.PostAsync(url, content, ct);

            // Ensure success
            response.EnsureSuccessStatusCode();

            // Parse the response
            var responseContent = await response.Content.ReadAsStringAsync(ct);
            var issueData = JsonSerializer.Deserialize<GitHubIssueResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to deserialize GitHub API response");

            // Create the output
            var result = MapIssueResponseToOutput(issueData);
            return result;
        }

        private void ConfigureHttpClient(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MicrosoftAgentsTool", "1.0"));
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
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

        private GitHubIssueOutput MapIssueResponseToOutput(GitHubIssueResponse issue)
        {
            return new GitHubIssueOutput
            {
                Number = issue.Number,
                Title = issue.Title,
                Body = issue.Body,
                State = issue.State,
                HtmlUrl = issue.HtmlUrl,
                Creator = issue.User.Login,
                Assignees = issue.Assignees?.Select(a => a.Login).ToList(),
                Labels = issue.Labels?.Select(l => l.Name).ToList(),
                CreatedAt = issue.CreatedAt,
                UpdatedAt = issue.UpdatedAt
            };
        }
    }
    #endregion

    #region Add Issue Comment
    public class GitHubAddIssueCommentOperationExecutor : McpToolExecutorBase<GitHubAddIssueCommentInput, GitHubIssueCommentOutput>
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public GitHubAddIssueCommentOperationExecutor(IConfiguration configuration, HttpClient? httpClient = null)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient = httpClient ?? new HttpClient();
        }

        public override string Id => "GitHub_AddIssueComment";
        public override string Description => "Adds a comment to a GitHub issue";

        public override async Task<GitHubIssueCommentOutput> ExecuteAsync(McpRequest<GitHubAddIssueCommentInput> payload, IMcpContext context, CancellationToken ct)
        {
            var owner = payload.Parameters.Owner;
            var repo = payload.Parameters.Repo;
            var issueNumber = payload.Parameters.IssueNumber;
            var body = payload.Parameters.Body;

            // Log the operation
            await context.PostNotificationAsync(new McpLogNotification<string>(
                new NotificationParameters<string>()
                {
                    Level = "notice",
                    Logger = "echo",
                    Data = $"Adding comment to GitHub issue: {owner}/{repo}#{issueNumber}"
                }), ct);

            // Create request body
            var requestBody = new { body };

            // Serialize to JSON
            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Configure the HTTP client
            var token = await GetGitHubTokenFromContext(context);
            ConfigureHttpClient(token);

            // Make the API call
            var url = $"https://api.github.com/repos/{owner}/{repo}/issues/{issueNumber}/comments";
            var response = await _httpClient.PostAsync(url, content, ct);

            // Ensure success
            response.EnsureSuccessStatusCode();

            // Parse the response
            var responseContent = await response.Content.ReadAsStringAsync(ct);
            var commentData = JsonSerializer.Deserialize<GitHubCommentResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to deserialize GitHub API response");

            // Create the output
            var result = new GitHubIssueCommentOutput
            {
                Id = commentData.Id,
                Body = commentData.Body,
                Creator = commentData.User.Login,
                HtmlUrl = commentData.HtmlUrl,
                CreatedAt = commentData.CreatedAt,
                UpdatedAt = commentData.UpdatedAt
            };

            return result;
        }

        private void ConfigureHttpClient(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MicrosoftAgentsTool", "1.0"));
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
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
    }
    #endregion

    #region List Issues
    public class GitHubListIssuesOperationExecutor : McpToolExecutorBase<GitHubListIssuesInput, GitHubListIssuesOutput>
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public GitHubListIssuesOperationExecutor(IConfiguration configuration, HttpClient? httpClient = null)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient = httpClient ?? new HttpClient();
        }

        public override string Id => "GitHub_ListIssues";
        public override string Description => "Lists GitHub issues in a repository";

        public override async Task<GitHubListIssuesOutput> ExecuteAsync(McpRequest<GitHubListIssuesInput> payload, IMcpContext context, CancellationToken ct)
        {
            var owner = payload.Parameters.Owner;
            var repo = payload.Parameters.Repo;
            var direction = payload.Parameters.Direction;
            var labels = payload.Parameters.Labels;
            var page = payload.Parameters.Page;
            var perPage = payload.Parameters.PerPage;
            var since = payload.Parameters.Since;
            var sort = payload.Parameters.Sort;
            var state = payload.Parameters.State;

            // Log the operation
            await context.PostNotificationAsync(new McpLogNotification<string>(
                new NotificationParameters<string>()
                {
                    Level = "notice",
                    Logger = "echo",
                    Data = $"Listing GitHub issues for {owner}/{repo}"
                }), ct);

            // Configure the HTTP client
            var token = await GetGitHubTokenFromContext(context);
            ConfigureHttpClient(token);

            // Build URL with parameters
            var url = BuildUrl($"https://api.github.com/repos/{owner}/{repo}/issues", new Dictionary<string, string>
            {
                ["direction"] = direction,
                ["labels"] = labels != null ? string.Join(",", labels) : null,
                ["page"] = page?.ToString(),
                ["per_page"] = perPage?.ToString(),
                ["since"] = since,
                ["sort"] = sort,
                ["state"] = state
            });

            // Make the API call
            var response = await _httpClient.GetAsync(url, ct);

            // Ensure success
            response.EnsureSuccessStatusCode();

            // Parse the response
            var responseContent = await response.Content.ReadAsStringAsync(ct);
            var issuesData = JsonSerializer.Deserialize<List<GitHubIssueResponse>>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to deserialize GitHub API response");

            // Create the output
            var result = new GitHubListIssuesOutput
            {
                Issues = issuesData.Select(MapIssueResponseToOutput).ToList(),
                TotalCount = issuesData.Count
            };

            return result;
        }


        private string BuildUrl(string baseUrl, Dictionary<string, string> parameters)
        {
            var uriBuilder = new UriBuilder(baseUrl);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);

            foreach (var param in parameters.Where(p => !string.IsNullOrEmpty(p.Value)))
            {
                query[param.Key] = param.Value;
            }

            uriBuilder.Query = query.ToString();
            return uriBuilder.ToString();
        }

        private void ConfigureHttpClient(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MicrosoftAgentsTool", "1.0"));
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
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

        private GitHubIssueOutput MapIssueResponseToOutput(GitHubIssueResponse issue)
        {
            return new GitHubIssueOutput
            {
                Number = issue.Number,
                Title = issue.Title,
                Body = issue.Body,
                State = issue.State,
                HtmlUrl = issue.HtmlUrl,
                Creator = issue.User.Login,
                Assignees = issue.Assignees?.Select(a => a.Login).ToList(),
                Labels = issue.Labels?.Select(l => l.Name).ToList(),
                CreatedAt = issue.CreatedAt,
                UpdatedAt = issue.UpdatedAt
            };
        }
    }
    #endregion

    #region Update Issue
    public class GitHubUpdateIssueOperationExecutor : McpToolExecutorBase<GitHubUpdateIssueInput, GitHubIssueOutput>
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public GitHubUpdateIssueOperationExecutor(IConfiguration configuration, HttpClient? httpClient = null)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient = httpClient ?? new HttpClient();
        }

        public override string Id => "GitHub_UpdateIssue";
        public override string Description => "Updates a GitHub issue";

        public override async Task<GitHubIssueOutput> ExecuteAsync(McpRequest<GitHubUpdateIssueInput> payload, IMcpContext context, CancellationToken ct)
        {
            var owner = payload.Parameters.Owner;
            var repo = payload.Parameters.Repo;
            var issueNumber = payload.Parameters.IssueNumber;
            var title = payload.Parameters.Title;
            var body = payload.Parameters.Body;
            var assignees = payload.Parameters.Assignees;
            var milestone = payload.Parameters.Milestone;
            var labels = payload.Parameters.Labels;
            var state = payload.Parameters.State;

            // Log the operation
            await context.PostNotificationAsync(new McpLogNotification<string>(
                new NotificationParameters<string>()
                {
                    Level = "notice",
                    Logger = "echo",
                    Data = $"Updating GitHub issue: {owner}/{repo}#{issueNumber}"
                }), ct);

            // Create request body
            var requestBody = new
            {
                title,
                body,
                assignees,
                milestone,
                labels,
                state
            };

            // Serialize to JSON
            var jsonContent = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Configure the HTTP client
            var token = await GetGitHubTokenFromContext(context);
            ConfigureHttpClient(token);

            // Make the API call
            var url = $"https://api.github.com/repos/{owner}/{repo}/issues/{issueNumber}";
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
            {
                Content = content
            };
            var response = await _httpClient.SendAsync(request, ct);

            // Ensure success
            response.EnsureSuccessStatusCode();

            // Parse the response
            var responseContent = await response.Content.ReadAsStringAsync(ct);
            var issueData = JsonSerializer.Deserialize<GitHubIssueResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to deserialize GitHub API response");

            // Create the output
            var result = MapIssueResponseToOutput(issueData);
            return result;
        }

        private void ConfigureHttpClient(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MicrosoftAgentsTool", "1.0"));
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
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

        private GitHubIssueOutput MapIssueResponseToOutput(GitHubIssueResponse issue)
        {
            return new GitHubIssueOutput
            {
                Number = issue.Number,
                Title = issue.Title,
                Body = issue.Body,
                State = issue.State,
                HtmlUrl = issue.HtmlUrl,
                Creator = issue.User.Login,
                Assignees = issue.Assignees?.Select(a => a.Login).ToList(),
                Labels = issue.Labels?.Select(l => l.Name).ToList(),
                CreatedAt = issue.CreatedAt,
                UpdatedAt = issue.UpdatedAt
            };
        }
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