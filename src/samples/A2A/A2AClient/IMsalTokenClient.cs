// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2AClient;

internal interface IMsalTokenClient
{
    Task<string> AcquireDelegatedTokenAsync(A2AAgentCardAuthentication authentication, CancellationToken cancellationToken);

    Task<string> AcquireApplicationTokenAsync(A2AAgentCardAuthentication authentication, CancellationToken cancellationToken);

    Task<string> AcquireDelegatedTokenAsync(CancellationToken cancellationToken);

    Task<string> AcquireApplicationTokenAsync(CancellationToken cancellationToken);
}
