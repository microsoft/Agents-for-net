// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Octokit;

namespace A2AAgent;

internal interface IGitHubClientFactory
{
    IGitHubClient Create(string accessToken);
}
