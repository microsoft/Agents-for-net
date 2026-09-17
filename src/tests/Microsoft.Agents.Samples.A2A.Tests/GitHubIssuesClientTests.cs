extern alias A2AAgentSample;

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using A2AAgentSample::A2AAgent;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class GitHubIssuesClientTests
{
    [Fact]
    public async Task GetAssignedIssuesSummaryAsync_UsesAssignedOpenIssuesEndpoint_Headers_AndFiltersPullRequests()
    {
        var handler = new SequenceJsonHandler([
            new ResponsePage(
                """
                [
                  { "html_url": "https://github.com/octo/repo/issues/17", "number": 17, "repository_url": "https://api.github.com/repos/octo/repo", "title": "Harden scopes" },
                  { "html_url": "https://github.com/octo/repo/pull/18", "number": 18, "repository_url": "https://api.github.com/repos/octo/repo", "title": "PR should disappear", "pull_request": { "url": "https://api.github.com/repos/octo/repo/pulls/18" } }
                ]
                """)
        ]);
        using var client = CreateClient(handler);

        var sut = new GitHubIssuesClient(client);
        string summary = await sut.GetAssignedIssuesSummaryAsync("github-token", CancellationToken.None);

        RequestRecord request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/issues?filter=assigned&state=open&per_page=20", request.PathAndQuery);
        Assert.Equal("Bearer", request.AuthorizationScheme);
        Assert.Equal("github-token", request.AuthorizationParameter);
        Assert.Equal("application/vnd.github+json", request.Accept);
        Assert.Equal("2022-11-28", request.ApiVersion);
        Assert.Equal("MicrosoftAgentsA2ASample/1.0", request.UserAgent);
        Assert.Contains("octo/repo#17 Harden scopes", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("PR should disappear", summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAssignedIssuesSummaryAsync_FirstPageContainsOnlyPullRequests_FollowsNextPageForIssue()
    {
        var handler = new SequenceJsonHandler([
            new ResponsePage(
                """[{ "number": 1, "repository_url": "https://api.github.com/repos/octo/repo", "title": "First PR", "pull_request": { "url": "https://api.github.com/repos/octo/repo/pulls/1" } }]""",
                "https://api.github.com/issues?filter=assigned&state=open&per_page=20&page=2"),
            new ResponsePage(
                """[{ "number": 2, "repository_url": "https://api.github.com/repos/octo/repo", "title": "Second-page issue" }]"""),
        ]);
        using var client = CreateClient(handler, new Uri("https://API.GITHUB.COM/"));

        string summary = await new GitHubIssuesClient(client)
            .GetAssignedIssuesSummaryAsync("github-token", CancellationToken.None);

        Assert.Equal(
            [
                "/issues?filter=assigned&state=open&per_page=20",
                "/issues?filter=assigned&state=open&per_page=20&page=2",
            ],
            handler.Requests.Select(request => request.PathAndQuery));
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal("api.github.com", request.RequestUri.Host);
            Assert.Equal("Bearer", request.AuthorizationScheme);
        });
        Assert.Contains("octo/repo#2 Second-page issue", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("First PR", summary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://example.com/issues?page=2")]
    [InlineData("http://api.github.com/issues?page=2")]
    [InlineData("https://api.github.com:444/issues?page=2")]
    [InlineData("https://user@api.github.com/issues?page=2")]
    public async Task GetAssignedIssuesSummaryAsync_UnsafeNextLink_DoesNotSendBearerRequest(string nextLink)
    {
        var handler = new SequenceJsonHandler([
            new ResponsePage(CreatePullRequestsJson(1), nextLink),
            new ResponsePage(CreateIssuesJson(2, 1)),
        ]);
        using var client = CreateClient(handler);

        string summary = await new GitHubIssuesClient(client)
            .GetAssignedIssuesSummaryAsync("github-token", CancellationToken.None);

        Assert.DoesNotContain(handler.Requests, request =>
            request.AuthorizationScheme is not null
            && !string.Equals(
                "https://api.github.com",
                request.RequestUri.GetLeftPart(UriPartial.Authority),
                StringComparison.OrdinalIgnoreCase));
        RequestRecord request = Assert.Single(handler.Requests);
        Assert.Equal("api.github.com", request.RequestUri.Host);
        Assert.Equal("Bearer", request.AuthorizationScheme);
        Assert.Equal("No open GitHub issues are currently assigned to you.", summary);
    }

    [Fact]
    public async Task GetAssignedIssuesSummaryAsync_MalformedNextLink_DoesNotRequestAnotherPage()
    {
        var handler = new SequenceJsonHandler([
            new ResponsePage(CreatePullRequestsJson(1), "https://[api.github.com/issues?page=2"),
            new ResponsePage(CreateIssuesJson(2, 1)),
        ]);
        using var client = CreateClient(handler);

        string summary = await new GitHubIssuesClient(client)
            .GetAssignedIssuesSummaryAsync("github-token", CancellationToken.None);

        Assert.Single(handler.Requests);
        Assert.Equal("No open GitHub issues are currently assigned to you.", summary);
    }

    [Fact]
    public async Task GetAssignedIssuesSummaryAsync_StopsAfterCollectingFiveIssues()
    {
        var handler = new SequenceJsonHandler([
            new ResponsePage(
                CreateIssuesJson(1, 4),
                "https://api.github.com/issues?filter=assigned&state=open&per_page=20&page=2"),
            new ResponsePage(
                CreateIssuesJson(5, 2),
                "https://api.github.com/issues?filter=assigned&state=open&per_page=20&page=3"),
        ]);
        using var client = CreateClient(handler);

        string summary = await new GitHubIssuesClient(client)
            .GetAssignedIssuesSummaryAsync("github-token", CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("octo/repo#5 Issue 5", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("octo/repo#6 Issue 6", summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAssignedIssuesSummaryAsync_NoNextLink_DoesNotRequestAnotherPage()
    {
        var handler = new SequenceJsonHandler([
            new ResponsePage(
                """[{ "number": 1, "repository_url": "https://api.github.com/repos/octo/repo", "title": "Only PR", "pull_request": { "url": "https://api.github.com/repos/octo/repo/pulls/1" } }]"""),
        ]);
        using var client = CreateClient(handler);

        string summary = await new GitHubIssuesClient(client)
            .GetAssignedIssuesSummaryAsync("github-token", CancellationToken.None);

        Assert.Single(handler.Requests);
        Assert.Equal("No open GitHub issues are currently assigned to you.", summary);
    }

    [Fact]
    public async Task GetAssignedIssuesSummaryAsync_StopsAtThreePageCap()
    {
        var handler = new SequenceJsonHandler([
            new ResponsePage(
                CreatePullRequestsJson(1),
                "https://api.github.com/issues?filter=assigned&state=open&per_page=20&page=2"),
            new ResponsePage(
                CreatePullRequestsJson(2),
                "https://api.github.com/issues?filter=assigned&state=open&per_page=20&page=3"),
            new ResponsePage(
                CreatePullRequestsJson(3),
                "https://api.github.com/issues?filter=assigned&state=open&per_page=20&page=4"),
        ]);
        using var client = CreateClient(handler);

        string summary = await new GitHubIssuesClient(client)
            .GetAssignedIssuesSummaryAsync("github-token", CancellationToken.None);

        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal("No open GitHub issues are currently assigned to you.", summary);
    }

    private static HttpClient CreateClient(HttpMessageHandler handler, Uri? baseAddress = null)
        => new(handler) { BaseAddress = baseAddress ?? new Uri("https://api.github.com/") };

    private static string CreateIssuesJson(int firstNumber, int count)
        => "["
            + string.Join(
                ",",
                Enumerable.Range(firstNumber, count).Select(number =>
                    $$"""{ "number": {{number}}, "repository_url": "https://api.github.com/repos/octo/repo", "title": "Issue {{number}}" }"""))
            + "]";

    private static string CreatePullRequestsJson(int number)
        => $$"""[{ "number": {{number}}, "repository_url": "https://api.github.com/repos/octo/repo", "title": "PR {{number}}", "pull_request": { "url": "https://api.github.com/repos/octo/repo/pulls/{{number}}" } }]""";

    private sealed class SequenceJsonHandler(IReadOnlyList<ResponsePage> pages) : HttpMessageHandler
    {
        private int _pageIndex;

        public List<RequestRecord> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RequestRecord(
                request.Method,
                request.RequestUri!,
                request.RequestUri!.PathAndQuery,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                request.Headers.Accept.SingleOrDefault()?.MediaType,
                request.Headers.UserAgent.ToString(),
                request.Headers.TryGetValues("X-GitHub-Api-Version", out IEnumerable<string>? versions)
                    ? versions.Single()
                    : null));

            ResponsePage page = pages[_pageIndex++];
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(page.Json, Encoding.UTF8, "application/json")
            };
            if (page.NextLink is not null)
            {
                response.Headers.TryAddWithoutValidation("Link", $"<{page.NextLink}>; rel=\"next\"");
            }

            return Task.FromResult(response);
        }
    }

    private sealed record ResponsePage(string Json, string? NextLink = null);

    private sealed record RequestRecord(
        HttpMethod Method,
        Uri RequestUri,
        string PathAndQuery,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        string? Accept,
        string UserAgent,
        string? ApiVersion);
}
