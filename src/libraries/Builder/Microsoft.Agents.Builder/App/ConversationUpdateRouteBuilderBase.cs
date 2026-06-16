// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Provides the generic base builder for routing conversation update activities in an <see cref="AgentApplication"/>.
    /// </summary>
    /// <remarks>
    /// Derive from <see cref="ConversationUpdateRouteBuilderBase{TBuilder}"/> to create specialized conversation
    /// update route builders while preserving fluent chaining on the concrete builder type. This base class supplies
    /// the shared conversation update matching behavior, including update-event filters, custom selectors, channel
    /// constraints, and agentic routing support.
    /// </remarks>
    /// <typeparam name="TBuilder">The concrete builder type returned from fluent members.</typeparam>
    public abstract class ConversationUpdateRouteBuilderBase<TBuilder> : RouteBuilderBase<TBuilder>
        where TBuilder : ConversationUpdateRouteBuilderBase<TBuilder>
    {

        protected ConversationUpdateRouteBuilderBase() : base() { }

        /// <summary>
        /// Configures the route to match a specific <see cref="Microsoft.Agents.Builder.App.ConversationUpdateEvents"/>, such as members being added or removed.
        /// </summary>
        /// <remarks>Use this method to restrict the route to trigger only for a particular conversation
        /// update event. If the specified event is not recognized, the route will match any conversation update
        /// activity.</remarks>
        /// <param name="eventName">The name of the conversation update event to match. Common values include events for members being 
        /// added or removed. Cannot be null.</param>
        /// <returns>The current builder instance for method chaining.</returns>
        public TBuilder WithUpdateEvent(string eventName)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(eventName, nameof(eventName));

            if (_route.Selector != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"ConversationUpdateRouteBuilder.WithUpdateEvent({eventName})");
            }

            if (ConversationUpdateEvents.MembersAdded.Equals(eventName))
            {
                _route.Selector = (context, ct) => Task.FromResult
                    (
                        IsContextMatch(context, _route)
                        && context.Activity is IConversationUpdateActivity updateActivity
                        && updateActivity.MembersAdded?.Count > 0
                    );
            }
            else if (ConversationUpdateEvents.MembersRemoved.Equals(eventName))
            {
                _route.Selector = (context, ct) => Task.FromResult
                    (
                        IsContextMatch(context, _route)
                        && context.Activity is IConversationUpdateActivity updateActivity
                        && updateActivity.MembersRemoved?.Count > 0
                    );
            }
            else
            {
                _route.Selector = (context, ct) => Task.FromResult
                    (
                        IsContextMatch(context, _route)
                        && context.Activity is IConversationUpdateActivity
                    );
            }

            return (TBuilder)this;
        }

        /// <summary>
        /// Sets a custom route selector used to determine how incoming requests are matched to this route builder.
        /// </summary>
        /// <remarks>Use this method to customize the matching logic for routes. This allows for advanced
        /// routing scenarios where requests are selected based on custom rules or patterns.</remarks>
        /// <param name="selector">The route selector that defines the criteria for matching requests to the route. The supplied selector does
        /// not need to validate base route properties like ChannelId, Agentic, etc. An Activity type of "conversationUpdate" is enforced.</param>
        /// <returns>The current builder instance with the specified selector applied.</returns>
        public override TBuilder WithSelector(RouteSelector selector)
        {
            AssertionHelpers.ThrowIfNull(selector, nameof(selector));

            if (_route.Selector != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"ConversationUpdateRouteBuilder.WithSelector()");
            }

            async Task<bool> ensureConversationUpdate(ITurnContext context, CancellationToken cancellationToken)
            {
                return IsContextMatch(context, _route) && context.Activity.IsType(ActivityTypes.ConversationUpdate) && await selector(context, cancellationToken).ConfigureAwait(false);
            }

            _route.Selector = ensureConversationUpdate;
            return (TBuilder)this;
        }

        /// <summary>
        /// Returns the current builder instance.
        /// </summary>
        /// <remarks>Conversation updates cannot be configured as invoke routes, so the value of <paramref name="isInvoke"/> is ignored.</remarks>
        /// <param name="isInvoke">Ignored.</param>
        /// <returns>The current builder instance.</returns>
        public override TBuilder AsInvoke(bool isInvoke = true)
        {
            return (TBuilder)this;
        }
    }
}
