// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Samples.A2AClient.OAuth;

namespace Microsoft.Agents.Samples.A2AClient.OAuth.Tokens;

internal interface IOAuthTokenClient
{
    Task<OAuthAccessToken> AcquireTokenAsync(
        OAuthCredentialBinding binding,
        CancellationToken cancellationToken);

    Task<OAuthAccessToken> RefreshTokenAsync(
        OAuthCredentialBinding binding,
        string refreshToken,
        CancellationToken cancellationToken);
}
