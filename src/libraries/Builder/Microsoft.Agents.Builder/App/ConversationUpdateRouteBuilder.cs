// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Provides a concrete builder for routing conversation update activities in an <see cref="AgentApplication"/>.
    /// </summary>
    /// <remarks>
    /// Use <see cref="ConversationUpdateRouteBuilder"/> when you need a conversation update route that uses the
    /// standard <see cref="RouteHandler"/> delegate. This type inherits the shared conversation update matching
    /// behavior from <see cref="ConversationUpdateRouteBuilderBase{TBuilder}"/>, including update-event filters,
    /// custom selectors, channel constraints, and agentic routing support.
    /// </remarks>
    public class ConversationUpdateRouteBuilder : ConversationUpdateRouteBuilderBase<ConversationUpdateRouteBuilder>
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ConversationUpdateRouteBuilder"/> class.
        /// </summary>
        /// <returns>A new <see cref="ConversationUpdateRouteBuilder"/>.</returns>
        public static ConversationUpdateRouteBuilder Create()
        {
            return new ConversationUpdateRouteBuilder();
        }

        /// <summary>
        /// Assigns the specified route handler to the current route and returns the updated builder instance.
        /// </summary>
        /// <param name="handler">The route handler to associate with the route. Cannot be null.</param>
        /// <returns>The current <see cref="ConversationUpdateRouteBuilder"/> instance for method chaining.</returns>
        public ConversationUpdateRouteBuilder WithHandler(RouteHandler handler)
        {
            _route.Handler = handler;
            return this;
        }

        public ConversationUpdateRouteBuilder WithHandler(RouteHandler<IConversationUpdateActivity> handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            Task typedHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                return handler(new TypedTurnContext<IConversationUpdateActivity>(turnContext), turnState, cancellationToken);
            }

            _route.Handler = typedHandler;
            return this;
        }
    }
}
