// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Base route builder for routing feedback loop activities in an AgentApplication.
    /// </summary>
    /// <typeparam name="TBuilder">The concrete builder type.</typeparam>
    public abstract class FeedbackRouteBuilderBase<TBuilder> : RouteBuilderBase<TBuilder>
        where TBuilder : FeedbackRouteBuilderBase<TBuilder>
    {
        protected FeedbackRouteBuilderBase() : base()
        {
            _route.Flags |= RouteFlags.Invoke;
        }

        protected TBuilder WithHandlerCore(FeedbackLoopHandler handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            Task<bool> routeSelector(ITurnContext context, CancellationToken _)
            {
                var jsonObject = ProtocolJsonSerializer.ToObject<JsonObject>(context.Activity.Value);
                string? actionName = jsonObject != null && jsonObject.ContainsKey("actionName") ? jsonObject["actionName"].ToString() : string.Empty;
                return Task.FromResult
                (
                    IsContextMatch(context, _route)
                    && context.Activity.Type == ActivityTypes.Invoke
                    && context.Activity.Name == "message/submitAction"
                    && actionName == "feedback"
                );
            }

            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                FeedbackData feedbackLoopData = ProtocolJsonSerializer.ToObject<FeedbackData>(turnContext.Activity.Value)!;
                feedbackLoopData.ReplyToId = turnContext.Activity.ReplyToId;

                await handler(turnContext, turnState, feedbackLoopData, cancellationToken).ConfigureAwait(false);
                await turnContext.SendActivityAsync(Activity.CreateInvokeResponseActivity(), cancellationToken).ConfigureAwait(false);
            }

            _route.Selector = routeSelector;
            _route.Handler = routeHandler;

            return (TBuilder)this;
        }

        /// <summary>
        /// Returns the current route builder instance configured for Invoke routing.
        /// </summary>
        /// <param name="isInvoke">Ignored</param>
        /// <returns>The current builder instance.</returns>
        public override TBuilder AsInvoke(bool isInvoke = true)
        {
            return (TBuilder)this;
        }
    }
}
