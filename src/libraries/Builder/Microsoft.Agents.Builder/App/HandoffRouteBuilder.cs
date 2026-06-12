// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Provides a concrete builder for routing handoff invoke activities in an <see cref="AgentApplication"/>.
    /// </summary>
    /// <remarks>
    /// Use <see cref="HandoffRouteBuilder"/> when you need a handoff route that uses the standard
    /// <see cref="HandoffHandler"/> delegate. This type inherits the shared handoff selection behavior from
    /// <see cref="HandoffRouteBuilderBase{TBuilder}"/>.
    /// </remarks>
    public class HandoffRouteBuilder : HandoffRouteBuilderBase<HandoffRouteBuilder>
    {

        /// <summary>
        /// Creates a new instance of the HandoffRouteBuilder class for constructing route definitions.
        /// </summary>
        /// <returns>A HandOffRouteBuilder instance that can be used to configure and build routes.</returns>
        public static HandoffRouteBuilder Create()
        {
            return new HandoffRouteBuilder();
        }

        /// <summary>
        /// Configures the route to handle handoff actions using the specified handler.
        /// </summary>
        /// <param name="handler">The handler to invoke when a matching handoff activity is received.</param>
        /// <returns>The current <see cref="HandoffRouteBuilder"/> instance for method chaining.</returns>
        public HandoffRouteBuilder WithHandler(HandoffHandler handler)
        {
            return WithHandlerCore(handler);
        }
    }
}