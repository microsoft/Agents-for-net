// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Provides a concrete builder for routing activities by activity type in an <see cref="AgentApplication"/>.
    /// </summary>
    /// <remarks>
    /// Use <see cref="TypeRouteBuilder"/> when you need a type-based route that uses the standard
    /// <see cref="RouteHandler"/> delegate. This type inherits the shared activity-type matching behavior from
    /// <see cref="TypeRouteBuilderBase{TBuilder}"/>, including activity type filters, custom selectors, channel
    /// constraints, and agentic routing support.
    /// </remarks>
    public class TypeRouteBuilder : TypeRouteBuilderBase<TypeRouteBuilder>
    {
        /// <summary>
        /// Creates a new instance of the TypeRouteBuilder class for constructing route definitions.
        /// </summary>
        /// <returns>A TypeRouteBuilder instance that can be used to configure and build routes.</returns>
        public static TypeRouteBuilder Create()
        {
            return new TypeRouteBuilder();
        }

        /// <summary>
        /// Assigns the specified route handler to the current route and returns the updated builder instance.
        /// </summary>
        /// <param name="handler">The route handler to associate with the route. Cannot be null.</param>
        /// <returns>The current <see cref="TypeRouteBuilder"/> instance for method chaining.</returns>
        public TypeRouteBuilder WithHandler(RouteHandler handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            _route.Handler = handler;
            return (TypeRouteBuilder)this;
        }

        public TypeRouteBuilder WithHandler<T>(RouteHandler<T> handler) where T : IActivity
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            _route.Handler = (ct, ts, ctok) => handler(new TypedTurnContext<T>(ct), ts, ctok);
            return this;
        }
    }
}