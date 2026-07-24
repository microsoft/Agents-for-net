// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using Microsoft.Agents.Core;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Provides a concrete builder for routing event activities in an <see cref="AgentApplication"/>.
    /// </summary>
    /// <remarks>
    /// Use <see cref="EventRouteBuilder"/> when you need an event route that uses the standard
    /// <see cref="RouteHandler"/> delegate. This type inherits the shared event matching behavior from
    /// <see cref="EventRouteBuilderBase{TBuilder}"/>, including event-name filters, custom selectors, channel
    /// constraints, and agentic routing support.
    /// </remarks>
    public class EventRouteBuilder : EventRouteBuilderBase<EventRouteBuilder>
    {
        /// <summary>
        /// Creates a new instance of the <see cref="EventRouteBuilder"/> class.
        /// </summary>
        /// <returns>A new <see cref="EventRouteBuilder"/>.</returns>
        public static EventRouteBuilder Create()
        {
            return new EventRouteBuilder();
        }

        /// <summary>
        /// Assigns the specified route handler to the current route and returns the updated builder instance.
        /// </summary>
        /// <param name="handler">The route handler to associate with the route. Cannot be null.</param>
        /// <returns>The current <see cref="EventRouteBuilder"/> instance for method chaining.</returns>
        public EventRouteBuilder WithHandler(RouteHandler handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            _route.Handler = handler;
            return this;
        }
    }
}
