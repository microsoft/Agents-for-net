// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2AClient;

internal interface IGitHubDeviceFlowTokenClient
{
    Task<GitHubDeviceFlowAccessToken> AcquireTokenAsync(
        A2AAgentCardAuthentication authentication,
        CancellationToken cancellationToken);
}

internal sealed record GitHubDeviceFlowAccessToken(string AccessToken, TimeSpan? ExpiresIn);
