// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace A2AAgent;

internal sealed class GitHubIssuesClient(HttpClient httpClient) : IGitHubIssuesClient
{
    private const string FirstIssuesRequestUri = "issues?filter=assigned&state=open&per_page=20";
    // Bound pagination so one agent turn performs at most three GitHub API requests.
    private const int MaxIssuePages = 3;
    private const int MaxRenderedIssues = 5;
    private const string GitHubAcceptMediaType = "application/vnd.github+json";
    private const string GitHubApiVersion = "2022-11-28";
    private static readonly ProductInfoHeaderValue UserAgent = new("MicrosoftAgentsA2ASample", "1.0");

    public async Task<string> GetAssignedIssuesSummaryAsync(string accessToken, CancellationToken cancellationToken)
    {
        var filtered = new List<GitHubIssueDto>(MaxRenderedIssues);
        string? requestUri = FirstIssuesRequestUri;

        for (int page = 0; page < MaxIssuePages && requestUri is not null; page++)
        {
            using var request = CreateRequest(requestUri, accessToken);
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            GitHubIssueDto[] issues = await JsonSerializer.DeserializeAsync<GitHubIssueDto[]>(
                stream,
                cancellationToken: cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("GitHub issues response was empty.");

            filtered.AddRange(issues
                .Where(issue => issue.PullRequest.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
                .Take(MaxRenderedIssues - filtered.Count));

            requestUri = filtered.Count >= MaxRenderedIssues
                ? null
                : GetNextPageUri(response.Headers, httpClient.BaseAddress);
        }

        if (filtered.Count == 0)
        {
            return "No open GitHub issues are currently assigned to you.";
        }

        string[] lines = filtered
            .Select(issue => $"- {GetRepositorySlug(issue.RepositoryUrl)}#{issue.Number} {issue.Title}")
            .ToArray();

        return string.Join(Environment.NewLine, lines);
    }

    private static HttpRequestMessage CreateRequest(string requestUri, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.ParseAdd(GitHubAcceptMediaType);
        request.Headers.UserAgent.Add(UserAgent);
        request.Headers.Add("X-GitHub-Api-Version", GitHubApiVersion);
        return request;
    }

    private static string? GetNextPageUri(HttpResponseHeaders headers, Uri? baseAddress)
    {
        if (!headers.TryGetValues("Link", out IEnumerable<string>? values))
        {
            return null;
        }

        foreach (string value in values)
        {
            foreach (string link in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = link.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2
                    || !parts.Skip(1).Any(part =>
                        string.Equals(part, "rel=\"next\"", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(part, "rel=next", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                string target = parts[0];
                if (target.Length < 3
                    || target[0] != '<'
                    || target[^1] != '>'
                    || baseAddress is null
                    || !Uri.TryCreate(baseAddress, target[1..^1], out Uri? nextPage)
                    || !IsSafeNextPageUri(baseAddress, nextPage))
                {
                    return null;
                }

                return nextPage.AbsoluteUri;
            }
        }

        return null;
    }

    private static bool IsSafeNextPageUri(Uri baseAddress, Uri nextPage)
        => nextPage.IsAbsoluteUri
            && string.Equals(nextPage.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && nextPage.IsDefaultPort
            && string.IsNullOrEmpty(nextPage.UserInfo)
            && string.Equals(baseAddress.Scheme, nextPage.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(baseAddress.IdnHost, nextPage.IdnHost, StringComparison.OrdinalIgnoreCase)
            && baseAddress.Port == nextPage.Port;

    private static string GetRepositorySlug(string repositoryUrl)
    {
        if (!Uri.TryCreate(repositoryUrl, UriKind.Absolute, out Uri? uri))
        {
            return repositoryUrl;
        }

        string path = uri.AbsolutePath.Trim('/');
        return path.StartsWith("repos/", StringComparison.OrdinalIgnoreCase)
            ? path.Substring("repos/".Length)
            : path;
    }
}
