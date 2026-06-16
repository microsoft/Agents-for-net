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
    /// Provides a concrete builder for routing feedback loop invoke activities in an <see cref="AgentApplication"/>.
    /// </summary>
    /// <remarks>
    /// Use <see cref="FeedbackRouteBuilder"/> when you need a feedback route that uses the standard
    /// <see cref="FeedbackLoopHandler"/> delegate. This type inherits the shared feedback loop selection behavior
    /// from <see cref="FeedbackRouteBuilderBase{TBuilder}"/>.
    /// </remarks>
    public class FeedbackRouteBuilder : FeedbackRouteBuilderBase<FeedbackRouteBuilder>
    {

        /// <summary>
        /// Creates a new instance of the FeedbackRouteBuilder class for constructing route definitions.
        /// </summary>
        /// <returns>A FeedbackRouteBuilder instance that can be used to configure and build routes.</returns>
        public static FeedbackRouteBuilder Create()
        {
            return new FeedbackRouteBuilder();
        }

        /// <summary>
        /// Configures the route to handle feedback actions using the specified feedback loop handler.
        /// </summary>
        /// <remarks>This method sets up the route to invoke the provided handler when an incoming
        /// activity represents a feedback action (i.e., an invoke activity with the name "message/submitAction" and an
        /// actionName of "feedback"). Use this method to define custom logic for handling feedback submissions in the
        /// feedback loop.</remarks>
        /// <param name="handler">A delegate that processes feedback data when a feedback action is received. Cannot be null.</param>
        /// <returns>The current <see cref="Microsoft.Agents.Builder.App.FeedbackRouteBuilder"/> instance for method chaining.</returns>
        public FeedbackRouteBuilder WithHandler(FeedbackLoopHandler handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            Task<bool> routeSelector(ITurnContext context, CancellationToken _)
            {
                if (context.Activity is not IInvokeActivity invokeActivity)
                {
                    return Task.FromResult(false);
                }

                var jsonObject = ProtocolJsonSerializer.ToObject<JsonObject>(invokeActivity.Value);
                string? actionName = jsonObject != null && jsonObject.ContainsKey("actionName") ? jsonObject["actionName"].ToString() : string.Empty;
                return Task.FromResult
                (
                    IsContextMatch(context, _route)
                    && invokeActivity.Name == "message/submitAction"
                    && actionName == "feedback"
                );
            }

            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                if (turnContext.Activity is not IInvokeActivity invokeActivity)
                {
                    return;
                }

                FeedbackData feedbackLoopData = ProtocolJsonSerializer.ToObject<FeedbackData>(invokeActivity.Value)!;
                feedbackLoopData.ReplyToId = invokeActivity.ReplyToId;

                await handler(turnContext, turnState, feedbackLoopData, cancellationToken);
                await turnContext.SendActivityAsync(Activity.CreateInvokeResponseActivity(), cancellationToken);
            }

            _route.Selector = routeSelector;
            _route.Handler = routeHandler;

            return this;
        }

        /// <summary>
        /// Returns the current route builder instance configured for Invoke routing. This method ensures that the route
        /// remains set as an Invoke route.
        /// </summary>
        /// <remarks>This override prevents changing the route configuration from Invoke routing,
        /// maintaining consistency with the route's initial setup.</remarks>
        /// <param name="isInvoke">Ignored</param>
        /// <returns>The current instance of <see cref="Microsoft.Agents.Builder.App.FeedbackRouteBuilder"/> with Invoke routing enabled.</returns>
        public override FeedbackRouteBuilder AsInvoke(bool isInvoke = true)
        {
            return this;
        }
    }
}