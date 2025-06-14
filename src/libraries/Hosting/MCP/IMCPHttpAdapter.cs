// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Agents.Builder;

namespace Microsoft.Agents.Hosting.MCP
{
    public interface IMCPHttpAdapter
    {
        /// <summary>
        /// This method can be called from inside a POST method on any controller implementation.
        /// </summary>
        /// <param name="httpRequest">The HTTP request object, typically in a POST handler by a controller.</param>
        /// <param name="httpResponse">The HTTP response object.</param>
        /// <param name="agent">The Agent implementation.</param>
        /// <param name="routePattern"></param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        Task ProcessAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string routePattern, CancellationToken cancellationToken = default);
    }
}
