// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Octokit;

namespace A2AAgent;

internal sealed class GitHubClientFactory : IGitHubClientFactory
{
    private static readonly ProductHeaderValue s_productHeader = new("agents-sdk-net-a2a-sample");

    public IGitHubClient Create(string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        var client = new GitHubClient(s_productHeader)
        {
            Credentials = new Credentials(accessToken),
        };

        return client;
    }
}
