// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Samples.A2AClient;
using Microsoft.Agents.Samples.A2AClient.OAuth;

namespace Microsoft.Agents.Samples.A2AClient.OAuth.Providers;

internal interface IOAuthCredentialProvider
{
    string Id { get; }

    OAuthProviderMatch? Match(A2AAgentCardAuthentication authentication);

    Task<OAuthCredentialBinding> BindAsync(
        Uri agentOrigin,
        A2AAgentCardAuthentication authentication,
        OAuthProviderMatch match,
        CancellationToken cancellationToken);
}

internal interface IOAuthCredentialProviderResolver
{
    Task<OAuthCredentialBinding> ResolveAsync(
        Uri agentOrigin,
        A2AAgentCardAuthentication authentication,
        CancellationToken cancellationToken);
}
