// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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
            return WithHandlerCore(handler);
        }
    }
}