extern alias A2AAgentSample;

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using A2AAgentSample::A2AAgent;
using Moq;
using Octokit;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class GitHubIssuesClientTests
{
    [Fact]
    public async Task GetAssignedIssuesSummaryAsync_UsesAssignedOpenIssueQuery_AndFiltersPullRequests()
    {
        var issues = new Mock<IIssuesClient>(MockBehavior.Strict);
        IssueRequest? capturedRequest = null;
        ApiOptions? capturedOptions = null;
        issues
            .Setup(client => client.GetAllForCurrent(It.IsAny<IssueRequest>(), It.IsAny<ApiOptions>()))
            .Callback<IssueRequest, ApiOptions>((request, options) =>
            {
                capturedRequest = request;
                capturedOptions = options;
            })
            .ReturnsAsync([
                CreateIssue(17, "Harden scopes", "https://github.com/octo/repo/issues/17"),
                CreatePullRequest(18, "PR should disappear", "https://github.com/octo/repo/pull/18"),
            ]);
        CapturingGitHubClientFactory factory = CreateFactory(issues.Object);

        string summary = await new GitHubIssuesClient(factory)
            .GetAssignedIssuesSummaryAsync("github-token", CancellationToken.None);

        Assert.Equal(1, factory.CreateCount);
        Assert.Equal("github-token", factory.AccessToken);
        Assert.NotNull(capturedRequest);
        Assert.Equal(IssueFilter.Assigned, capturedRequest!.Filter);
        Assert.Equal(ItemStateFilter.Open, capturedRequest.State);
        Assert.NotNull(capturedOptions);
        Assert.Equal(20, capturedOptions!.PageSize);
        Assert.Equal(3, capturedOptions.PageCount);
        Assert.Equal(1, capturedOptions.StartPage);
        Assert.Contains("octo/repo#17 Harden scopes", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("PR should disappear", summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAssignedIssuesSummaryAsync_StopsAfterFiveRenderedIssues()
    {
        var issues = new Mock<IIssuesClient>(MockBehavior.Strict);
        issues
            .Setup(client => client.GetAllForCurrent(It.IsAny<IssueRequest>(), It.IsAny<ApiOptions>()))
            .ReturnsAsync([
                CreateIssue(1, "Issue 1", "https://github.com/octo/repo/issues/1"),
                CreateIssue(2, "Issue 2", "https://github.com/octo/repo/issues/2"),
                CreateIssue(3, "Issue 3", "https://github.com/octo/repo/issues/3"),
                CreateIssue(4, "Issue 4", "https://github.com/octo/repo/issues/4"),
                CreateIssue(5, "Issue 5", "https://github.com/octo/repo/issues/5"),
                CreateIssue(6, "Issue 6", "https://github.com/octo/repo/issues/6"),
            ]);

        string summary = await new GitHubIssuesClient(CreateFactory(issues.Object))
            .GetAssignedIssuesSummaryAsync("github-token", CancellationToken.None);

        Assert.Contains("octo/repo#5 Issue 5", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("octo/repo#6 Issue 6", summary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://example.com/octo/repo/issues/17")]
    [InlineData("https://github.com/octo/repo/discussions/17")]
    [InlineData("https://github.com/octo/repo/issues/18")]
    public async Task GetAssignedIssuesSummaryAsync_InvalidHtmlUrl_UsesSafeFallback(string htmlUrl)
    {
        var issues = new Mock<IIssuesClient>(MockBehavior.Strict);
        issues
            .Setup(client => client.GetAllForCurrent(It.IsAny<IssueRequest>(), It.IsAny<ApiOptions>()))
            .ReturnsAsync([
                CreateIssue(17, "Harden scopes", htmlUrl),
            ]);

        string summary = await new GitHubIssuesClient(CreateFactory(issues.Object))
            .GetAssignedIssuesSummaryAsync("github-token", CancellationToken.None);

        Assert.Equal("- #17 Harden scopes", summary);
    }

    [Fact]
    public async Task GetAssignedIssuesSummaryAsync_GitHubHostMatch_IsCaseInsensitive()
    {
        var issues = new Mock<IIssuesClient>(MockBehavior.Strict);
        issues
            .Setup(client => client.GetAllForCurrent(It.IsAny<IssueRequest>(), It.IsAny<ApiOptions>()))
            .ReturnsAsync([
                CreateIssue(17, "Harden scopes", "https://GitHub.com/octo/repo/issues/17"),
            ]);

        string summary = await new GitHubIssuesClient(CreateFactory(issues.Object))
            .GetAssignedIssuesSummaryAsync("github-token", CancellationToken.None);

        Assert.Equal("- octo/repo#17 Harden scopes", summary);
    }

    [Fact]
    public async Task GetAssignedIssuesSummaryAsync_NoIssuesAfterFiltering_ReturnsExistingEmptyMessage()
    {
        var issues = new Mock<IIssuesClient>(MockBehavior.Strict);
        issues
            .Setup(client => client.GetAllForCurrent(It.IsAny<IssueRequest>(), It.IsAny<ApiOptions>()))
            .ReturnsAsync([
                CreatePullRequest(18, "PR should disappear", "https://github.com/octo/repo/pull/18"),
            ]);

        string summary = await new GitHubIssuesClient(CreateFactory(issues.Object))
            .GetAssignedIssuesSummaryAsync("github-token", CancellationToken.None);

        Assert.Equal("No open GitHub issues are currently assigned to you.", summary);
    }

    [Fact]
    public async Task GetAssignedIssuesSummaryAsync_PreCanceledToken_ThrowsWithoutCallingGitHub()
    {
        var issues = new Mock<IIssuesClient>(MockBehavior.Strict);
        CapturingGitHubClientFactory factory = CreateFactory(issues.Object);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new GitHubIssuesClient(factory).GetAssignedIssuesSummaryAsync("github-token", cancellation.Token));

        Assert.Equal(0, factory.CreateCount);
    }

    [Fact]
    public async Task GetAssignedIssuesSummaryAsync_ProviderFailure_IsNotSwallowed()
    {
        var issues = new Mock<IIssuesClient>(MockBehavior.Strict);
        issues
            .Setup(client => client.GetAllForCurrent(It.IsAny<IssueRequest>(), It.IsAny<ApiOptions>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new GitHubIssuesClient(CreateFactory(issues.Object))
                .GetAssignedIssuesSummaryAsync("github-token", CancellationToken.None));

        Assert.Equal("boom", exception.Message);
    }

    private static CapturingGitHubClientFactory CreateFactory(IIssuesClient issues)
    {
        var client = new Mock<IGitHubClient>(MockBehavior.Strict);
        client.SetupGet(mock => mock.Issue)
            .Returns(issues);

        return new CapturingGitHubClientFactory(client.Object);
    }

    private static Issue CreateIssue(int number, string title, string htmlUrl)
        => new(
            url: $"https://api.github.com/repos/octo/repo/issues/{number}",
            htmlUrl: htmlUrl,
            commentsUrl: $"https://api.github.com/repos/octo/repo/issues/{number}/comments",
            eventsUrl: $"https://api.github.com/repos/octo/repo/issues/{number}/events",
            number: number,
            state: ItemState.Open,
            title: title,
            body: string.Empty,
            closedBy: null!,
            user: null!,
            labels: [],
            assignee: null!,
            assignees: [],
            milestone: null!,
            comments: 0,
            pullRequest: null,
            closedAt: null,
            createdAt: DateTimeOffset.UnixEpoch,
            updatedAt: DateTimeOffset.UnixEpoch,
            id: number,
            nodeId: $"ISSUE_{number}",
            locked: false,
            repository: new Repository(),
            reactions: new ReactionSummary(),
            activeLockReason: null,
            stateReason: null);

    private static Issue CreatePullRequest(int number, string title, string htmlUrl)
        => new(
            url: $"https://api.github.com/repos/octo/repo/issues/{number}",
            htmlUrl: htmlUrl,
            commentsUrl: $"https://api.github.com/repos/octo/repo/issues/{number}/comments",
            eventsUrl: $"https://api.github.com/repos/octo/repo/issues/{number}/events",
            number: number,
            state: ItemState.Open,
            title: title,
            body: string.Empty,
            closedBy: null!,
            user: null!,
            labels: [],
            assignee: null!,
            assignees: [],
            milestone: null!,
            comments: 0,
            pullRequest: new PullRequest(number),
            closedAt: null,
            createdAt: DateTimeOffset.UnixEpoch,
            updatedAt: DateTimeOffset.UnixEpoch,
            id: number,
            nodeId: $"ISSUE_{number}",
            locked: false,
            repository: new Repository(),
            reactions: new ReactionSummary(),
            activeLockReason: null,
            stateReason: null);

    private sealed class CapturingGitHubClientFactory(IGitHubClient client) : IGitHubClientFactory
    {
        public string? AccessToken { get; private set; }

        public int CreateCount { get; private set; }

        public IGitHubClient Create(string accessToken)
        {
            CreateCount++;
            AccessToken = accessToken;
            return client;
        }
    }
}
