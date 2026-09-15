// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class A2AAccessTokenProvider(IMsalTokenClient msal) : IA2AAccessTokenProvider
{
    public async Task<string?> GetAccessTokenAsync(A2AAuthMode mode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(msal);

        return mode switch
        {
            A2AAuthMode.None => null,
            A2AAuthMode.Delegated => await msal.AcquireDelegatedTokenAsync(cancellationToken).ConfigureAwait(false),
            A2AAuthMode.App => await msal.AcquireApplicationTokenAsync(cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }
}
