// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Provides a concrete builder for routing message activities in an <see cref="AgentApplication"/>.
    /// </summary>
    /// <remarks>
    /// Use <see cref="MessageRouteBuilder"/> when you need a message route that uses the standard
    /// <see cref="RouteHandler"/> delegate. This type inherits the message-specific matching behavior from
    /// <see cref="MessageRouteBuilderBase{TBuilder}"/>, including filtering by message text, regular expression,
    /// channel, and agentic routing options.
    /// </remarks>
    public class MessageRouteBuilder : MessageRouteBuilderBase<MessageRouteBuilder>
    {
        /// <summary>
        /// Creates a new instance of the MessageRouteBuilder class for constructing route definitions.
        /// </summary>
        /// <returns>A MessageRouteBuilder instance that can be used to configure and build routes.</returns>
        public static MessageRouteBuilder Create()
        {
            return new MessageRouteBuilder();
        }

        /// <summary>
        /// Assigns the specified route handler to the current route and returns the updated builder instance.
        /// </summary>
        /// <param name="handler">The route handler to associate with the route. Cannot be null.</param>
        /// <returns>The current MessageRouteBuilder instance with the handler set, enabling method chaining.</returns>
        public MessageRouteBuilder WithHandler(RouteHandler<IMessageActivity> handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            Task typedHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                return handler(new TypedTurnContext<IMessageActivity>(turnContext), turnState, cancellationToken);
            }

            _route.Handler = typedHandler;
            return this;
        }

        public MessageRouteBuilder WithHandler(RouteHandler handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            _route.Handler = handler;
            return this;
        }
    }
}