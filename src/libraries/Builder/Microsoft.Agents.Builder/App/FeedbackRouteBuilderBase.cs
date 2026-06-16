// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Provides the generic base builder for routing feedback loop invoke activities in an <see cref="AgentApplication"/>.
    /// </summary>
    /// <remarks>
    /// Derive from <see cref="FeedbackRouteBuilderBase{TBuilder}"/> to create specialized feedback route builders
    /// while preserving fluent chaining on the concrete builder type. Routes built from this base match invoke
    /// activities named <c>message/submitAction</c> whose <c>actionName</c> is <c>feedback</c>.
    /// </remarks>
    /// <typeparam name="TBuilder">The concrete builder type returned from fluent members.</typeparam>
    public abstract class FeedbackRouteBuilderBase<TBuilder> : RouteBuilderBase<TBuilder>
        where TBuilder : FeedbackRouteBuilderBase<TBuilder>
    {
        protected FeedbackRouteBuilderBase() : base()
        {
            _route.Flags |= RouteFlags.Invoke;
        }

        /// <summary>
        /// Returns the current builder instance.
        /// </summary>
        /// <remarks>Feedback routes always handle invoke activities, so the value of <paramref name="isInvoke"/> is ignored.</remarks>
        /// <param name="isInvoke">Ignored.</param>
        /// <returns>The current builder instance.</returns>
        public override TBuilder AsInvoke(bool isInvoke = true)
        {
            return (TBuilder)this;
        }
    }
}