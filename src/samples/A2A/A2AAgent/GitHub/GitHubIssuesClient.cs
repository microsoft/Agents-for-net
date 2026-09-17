// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
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
            .Select(issue => $"- {GetRepositorySlug(issue.HtmlUrl, issue.Number)}#{issue.Number} {issue.Title}")
            .ToArray();

        return lines.Length == 0
            ? "No open GitHub issues are currently assigned to you."
            : string.Join(Environment.NewLine, lines);
    }

    private static string GetRepositorySlug(string issueHtmlUrl, int issueNumber)
    {
        if (!Uri.TryCreate(issueHtmlUrl, UriKind.Absolute, out Uri? uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        string[] segments = uri.GetComponents(UriComponents.Path, UriFormat.UriEscaped)
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length != 4
            || string.IsNullOrEmpty(segments[0])
            || string.IsNullOrEmpty(segments[1])
            || !string.Equals(segments[2], "issues", StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(segments[3], NumberStyles.None, CultureInfo.InvariantCulture, out int parsedIssueNumber)
            || parsedIssueNumber != issueNumber)
        {
            return string.Empty;
        }

        return $"{segments[0]}/{segments[1]}";
    }
}
