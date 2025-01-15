// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Agents.BotBuilder;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    /// <summary>
    /// Interface to express the relationship between an MVC API controller and a Bot Builder Adapter.
    /// This interface can be used for dependency injection.
    /// </summary>
    public interface IBotHttpAdapter
    {
        /// <summary>
        /// This method can be called from inside a POST method on any controller implementation.
        /// </summary>
        /// <param name="httpRequest">The HTTP request object, typically in a POST handler by a controller.</param>
        /// <param name="httpResponse">The HTTP response object.</param>
        /// <param name="bot">The bot implementation.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        Task ProcessAsync(HttpRequest httpRequest, HttpResponse httpResponse, IBot bot, CancellationToken cancellationToken = default(CancellationToken));
    }
}
