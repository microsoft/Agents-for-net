// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using System;

namespace Microsoft.Agents.Builder.App.Builders
{
    public class RouteBuilder : RouteBuilderBase<RouteBuilder>
    {
        /// <summary>
        /// Sets the route selector used to determine how incoming requests are matched to this route builder.
        /// </summary>
        /// <remarks>Use this method to customize the matching logic for routes. This allows for advanced
        /// routing scenarios where requests are selected based on custom rules or patterns.</remarks>
        /// <param name="selector">The route selector that defines the criteria for matching requests to the route.</param>
        /// <returns>The current instance of <see cref="RouteBuilder"/> with the specified selector applied.</returns>
        public RouteBuilder WithSelector(RouteSelector selector)
        {
            _route.Selector = selector;
            return this;
        }
    }

    /// <summary>
    /// Provides a fluent builder for configuring and constructing a Route instance with custom selection logic,
    /// handlers, and routing options.
    /// </summary>
    public class RouteBuilderBase<TBuilder> where TBuilder : RouteBuilderBase<TBuilder>
    {
        protected readonly Route _route = new();

        public RouteBuilderBase() { }

        /// <summary>
        /// Creates a new instance of the RouteBuilder class for constructing route definitions.
        /// </summary>
        /// <returns>A RouteBuilder instance that can be used to configure and build routes.</returns>
        public static TBuilder Create()
        {
            var builder = Activator.CreateInstance<TBuilder>();
            return builder;
        }

        /// <summary>
        /// Assigns the specified route handler to the current route and returns the updated builder instance.
        /// </summary>
        /// <param name="handler">The route handler to associate with the route. Cannot be null.</param>
        /// <returns>The current RouteBuilder instance with the handler set, enabling method chaining.</returns>
        public TBuilder WithHander(RouteHandler handler)
        {
            _route.Handler = handler;
            return (TBuilder)this;
        }

        /// <summary>
        /// Configures the route to use the specified OAuth authentication handlers.
        /// </summary>
        /// <remarks>Use this method to specify which OAuth authentication handlers should be applied to
        /// the route. This is useful when multiple authentication schemes are available and you want to restrict the
        /// route to specific handlers.</remarks>
        /// <param name="handlers">An array of handler names to be used for OAuth authentication. If null, no handlers will be configured.</param>
        /// <returns>The current <see cref="RouteBuilder"/> instance with the OAuth handlers configured.</returns>
        public TBuilder WithOAuthHandlers(string[] handlers)
        {
            _route.OAuthHandlers = context => handlers ?? [];
            return (TBuilder)this;
        }

        /// <summary>
        /// Configures OAuth handler functions for the route using the specified delegate.
        /// </summary>
        /// <remarks>Use this method to specify custom OAuth authentication handlers for the route. The
        /// provided delegate will be invoked for each request to determine which handlers should be applied based on
        /// the context.</remarks>
        /// <param name="handlers">A delegate that takes an <see cref="ITurnContext"/> and returns an array of OAuth handler names to be used
        /// for authentication. If <paramref name="handlers"/> is null, no OAuth handlers will be configured.</param>
        /// <returns>The current <see cref="RouteBuilder"/> instance with the OAuth handlers configured.</returns>
        public TBuilder WithOAuthHandlers(Func<ITurnContext, string[]> handlers)
        {
            _route.OAuthHandlers = handlers ?? (Func<ITurnContext, string[]>)(context => []);
            return (TBuilder)this;
        }

        /// <summary>
        /// Flags the route for Invoke handling.
        /// </summary>
        /// <returns>The current <see cref="RouteBuilder"/> instance with the invocation flag updated.</returns>
        public TBuilder AsInvoke(bool isInvoke = true)
        {
            if (isInvoke)
            {
                _route.Flags |= RouteFlags.Invoke;
            }
            else
            {
                _route.Flags &= ~RouteFlags.Invoke;
            }
            return (TBuilder)this;
        }

        /// <summary>
        /// Configures the route to operate in agentic mode, enabling behaviors associated with agentic processing.
        /// </summary>
        /// <remarks>Agentic mode may alter how the route processes requests, enabling features or
        /// behaviors specific to agentic workflows. Use this method when agentic processing is required for the
        /// route.</remarks>
        /// <param name="isAgentic">A value indicating whether agentic mode should be enabled. Set to <see langword="true"/> to enable agentic
        /// mode; otherwise, set to <see langword="false"/>.</param>
        /// <returns>The current <see cref="RouteBuilder"/> instance with the agentic mode configuration applied.</returns>
        public TBuilder AsAgentic(bool isAgentic = true)
        {
            if (isAgentic)
            {
                _route.Flags |= RouteFlags.Agentic;
            }
            else
            {
                _route.Flags &= ~RouteFlags.Agentic;
            }
            return (TBuilder)this;
        }

        /// <summary>
        /// Marks the current route as non-terminal and returns the updated builder instance.
        /// </summary>
        /// <remarks>A non-terminal route allows further route matching beyond this point. Use this method
        /// when the route should not be considered a final endpoint.</remarks>
        /// <returns>The current <see cref="RouteBuilder"/> instance with the non-terminal flag set.</returns>
        public TBuilder AsNonTerminal()
        {
            _route.Flags |= RouteFlags.NonTerminal;
            return (TBuilder)this;
        }

        /// <summary>
        /// Sets the order rank for the route and returns the current builder instance for further configuration.
        /// </summary>
        /// <remarks>Use this method to specify the relative priority of the route when multiple routes
        /// are evaluated. Lower rank values typically indicate higher priority.</remarks>
        /// <param name="rank">The rank value to assign to the route. Must be a non-negative number representing the route's priority in
        /// ordering.</param>
        /// <returns>The current <see cref="RouteBuilder"/> instance with the updated order rank.</returns>
        public TBuilder WithOrderRank(ushort rank)
        {
            _route.Rank = rank;
            return (TBuilder)this;
        }

        /// <summary>
        /// Builds and returns the configured route instance after validating required components.
        /// </summary>
        /// <remarks>Throws an exception if the route's selector or handler is not set. Ensure that both
        /// components are configured before calling this method. The return <c>Route</c> can be used in <see cref="AgentApplication.AddRoute(Route)"/>.</remarks>
        /// <returns>The constructed <see cref="Route"/> instance representing the current route configuration.</returns>
        public Route Build()
        {
            AssertionHelpers.ThrowIfNull(_route.Selector, nameof(_route.Selector));
            AssertionHelpers.ThrowIfNull(_route.Handler, nameof(_route.Handler));

            return _route;
        }
    }
}
