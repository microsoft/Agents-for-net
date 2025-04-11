// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.State;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Function for selecting whether a route handler should be triggered.
    /// </summary>
    /// <param name="turnContext">Context for the current turn of conversation with the user.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>True if the route handler should be triggered. Otherwise, False.</returns>
    public delegate Task<bool> RouteSelector(ITurnContext turnContext, CancellationToken cancellationToken);

    /// <summary>
    /// The common route handler. Function for handling an incoming request.
    /// </summary>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns></returns>
    public delegate Task RouteHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken);

    internal class Route
    {
        public Route(RouteSelector selector, bool isInvokeRoute = false)
        {
            Selector = selector;
            Handler = (_, _, _) => Task.CompletedTask;
            IsInvokeRoute = isInvokeRoute;
        }

        public Route(RouteHandler handler, bool isInvokeRoute = false)
        {
            Selector = (_, _) => Task.FromResult(true);
            Handler = handler;
            IsInvokeRoute = isInvokeRoute;
        }

        public Route(RouteSelector selector, RouteHandler handler, bool isInvokeRoute = false)
        {
            Selector = selector;
            Handler = handler;
            IsInvokeRoute = isInvokeRoute;
        }

        public RouteSelector Selector { get; private set; }

        public RouteHandler Handler { get; private set; }

        public bool IsInvokeRoute { get; private set; }
    }
}
