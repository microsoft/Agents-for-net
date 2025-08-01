// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Hosting.A2A.Protocol;

namespace Microsoft.Agents.Hosting.A2A
{
    public interface IA2AHttpAdapter : IAgentHttpAdapter
    {
        Task ProcessAgentCardAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string messagePrefix, CancellationToken cancellationToken = default);
    }
}
