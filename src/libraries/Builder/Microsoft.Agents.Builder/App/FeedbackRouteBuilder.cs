// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// RouteBuilder for routing Feedback Loop activities in an AgentApplication.
    /// </summary>
    /// <remarks>
    /// Use <see cref="Microsoft.Agents.Builder.App.FeedbackRouteBuilder"/> to create and configure routes that respond to activities of type 'invoke' with
    /// name "message/submitAction". This builder allows matching event activities by name or regular expression, and supports
    /// channelId and agentic routing scenarios. Instances are created via the <see cref="Microsoft.Agents.Builder.App.FeedbackRouteBuilder.Create"/> method
    /// and further configured using <see cref="Microsoft.Agents.Builder.App.FeedbackRouteBuilder.WithHandler(Microsoft.Agents.Builder.App.FeedbackLoopHandler)"/>.<br/><br/>
    /// Example usage:<br/><br/>
    /// <code>
    /// var route = FeedbackRouteBuilder.Create()
    ///    .WithHandler(async (context, state, feedbackData, ct) => Task.FromResult(context.SendActivityAsync("Feedback action received", cancellationToken: ct)))
    ///    .Build();
    ///    
    /// app.AddRoute(route);
    /// </code>
    /// </remarks>
    public class FeedbackRouteBuilder : FeedbackRouteBuilderBase<FeedbackRouteBuilder>
    {
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
            return WithHandlerCore(handler);
        }
    }
}
