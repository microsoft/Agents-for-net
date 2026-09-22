// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2AClient;

internal interface IOAuthTokenClient
{
    Task<OAuthAccessToken> AcquireTokenAsync(
        A2AAgentCardAuthentication authentication,
        OAuthCredentialProviderOptions provider,
        OAuthClientRegistration registration,
        CancellationToken cancellationToken);

    Task<OAuthAccessToken> RefreshTokenAsync(
        A2AAgentCardAuthentication authentication,
        OAuthCredentialProviderOptions provider,
        OAuthClientRegistration registration,
        string refreshToken,
        CancellationToken cancellationToken);
}
