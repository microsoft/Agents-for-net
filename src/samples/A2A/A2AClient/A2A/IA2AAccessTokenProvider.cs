// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2AClient.A2A;

internal interface IA2AAccessTokenProvider
{
    Task<string?> GetAccessTokenAsync(A2AAgentCardAuthentication? authentication, CancellationToken cancellationToken);
}
