// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2AClient;

internal interface IGitHubDeviceFlowTokenClient
{
    Task<string> AcquireTokenAsync(A2AAgentCardAuthentication authentication, CancellationToken cancellationToken);
}
