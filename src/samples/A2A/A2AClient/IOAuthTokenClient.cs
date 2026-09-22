// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2AClient;

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
