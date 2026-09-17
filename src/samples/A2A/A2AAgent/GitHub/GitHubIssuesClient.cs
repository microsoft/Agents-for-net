// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octokit;

namespace A2AAgent;

internal sealed class GitHubIssuesClient(IGitHubClientFactory gitHubClients) : IGitHubIssuesClient
{
    private const int MaxRenderedIssues = 5;

    public async Task<string> GetAssignedIssuesSummaryAsync(string accessToken, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IGitHubClient client = gitHubClients.Create(accessToken);
        var request = new IssueRequest
        {
            Filter = IssueFilter.Assigned,
            State = ItemStateFilter.Open,
        };
        var options = new ApiOptions
        {
            PageSize = 20,
            PageCount = 3,
            StartPage = 1,
        };

        IReadOnlyList<Issue> issues = await client.Issue
            .GetAllForCurrent(request, options)
            .ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        string[] lines = issues
            .Where(issue => issue.PullRequest is null)
            .Take(MaxRenderedIssues)
            .Select(issue => $"- {GetRepositorySlug(issue.HtmlUrl)}#{issue.Number} {issue.Title}")
            .ToArray();

        return lines.Length == 0
            ? "No open GitHub issues are currently assigned to you."
            : string.Join(Environment.NewLine, lines);
    }

    private static string GetRepositorySlug(string repositoryUrl)
    {
        if (!Uri.TryCreate(repositoryUrl, UriKind.Absolute, out Uri? uri))
        {
            return repositoryUrl;
        }

        string[] segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length >= 3
            && string.Equals(segments[0], "repos", StringComparison.OrdinalIgnoreCase))
        {
            return $"{segments[1]}/{segments[2]}";
        }

        if (segments.Length >= 2)
        {
            return $"{segments[0]}/{segments[1]}";
        }

        string path = uri.AbsolutePath.Trim('/');
        return path.StartsWith("repos/", StringComparison.OrdinalIgnoreCase)
            ? path.Substring("repos/".Length)
            : path;
    }
}
