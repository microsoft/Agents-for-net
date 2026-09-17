// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace A2AAgent;

/// <summary>
/// Reads issue information for the signed-in GitHub user.
/// </summary>
public interface IGitHubIssuesClient
{
    Task<string> GetAssignedIssuesSummaryAsync(string accessToken, CancellationToken cancellationToken);
}
