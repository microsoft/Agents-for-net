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
        public Route(RouteSelector selector, bool isInvokeRoute = false, bool isAgenticRoute = false) : this(selector, (_, _, _) => Task.CompletedTask, isAgenticRoute, isInvokeRoute)
        {
        }

        public Route(RouteHandler handler, bool isInvokeRoute = false, bool isAgenticRoute = false) : this((_, _) => Task.FromResult(true), handler, isInvokeRoute, isAgenticRoute, null)
        {
        }

        public Route(RouteSelector selector, RouteHandler handler, bool isInvokeRoute = false, bool isAgenticRoute = false, params string[] autoSignInHandler)
        {
            Selector = selector;
            Handler = handler;
            IsInvokeRoute = isInvokeRoute;
            IsAgenticRoute = isAgenticRoute;
            AutoSignInHandler = autoSignInHandler;
        }

        public RouteSelector Selector { get; private set; }

        public RouteHandler Handler { get; private set; }

        public bool IsAgenticRoute { get; private set; }

        public bool IsInvokeRoute { get; private set; }

        public string[] AutoSignInHandler { get; private set; }
    }
}
