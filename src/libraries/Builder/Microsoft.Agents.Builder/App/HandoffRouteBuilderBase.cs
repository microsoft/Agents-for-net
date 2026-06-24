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
    /// Provides the generic base builder for routing handoff invoke activities in an <see cref="AgentApplication"/>.
    /// </summary>
    /// <remarks>
    /// Derive from <see cref="HandoffRouteBuilderBase{TBuilder}"/> to create specialized handoff route builders
    /// while preserving fluent chaining on the concrete builder type. Routes built from this base match invoke
    /// activities named <c>handoff/action</c>.
    /// </remarks>
    /// <typeparam name="TBuilder">The concrete builder type returned from fluent members.</typeparam>
    public abstract class HandoffRouteBuilderBase<TBuilder> : RouteBuilderBase<TBuilder>
        where TBuilder : HandoffRouteBuilderBase<TBuilder>
    {
        protected HandoffRouteBuilderBase() : base()
        {
            _route.Flags |= RouteFlags.Invoke;
        }

        /// <summary>
        /// Configures the route to handle handoff actions using the specified handler.
        /// </summary>
        /// <param name="handler">The handler to invoke when a matching handoff activity is received.</param>
        /// <returns>The current builder instance.</returns>
        protected TBuilder WithHandlerCore(HandoffHandler handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            Task<bool> routeSelector(ITurnContext context, CancellationToken _) => Task.FromResult
                (
                    IsContextMatch(context, _route)
                    && context.Activity.IsType(ActivityTypes.Invoke)
                    && string.Equals(context.Activity?.Name, "handoff/action", System.StringComparison.OrdinalIgnoreCase)
                );

            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                string token = turnContext.Activity.Value?.GetType()?.GetProperty("Continuation")?.GetValue(turnContext.Activity.Value) as string ?? "";
                await handler(turnContext, turnState, token, cancellationToken).ConfigureAwait(false);
                await turnContext.SendActivityAsync(Activity.CreateInvokeResponseActivity(), cancellationToken).ConfigureAwait(false);
            }

            _route.Selector = routeSelector;
            _route.Handler = routeHandler;

            return (TBuilder)this;
        }

        /// <summary>
        /// Returns the current builder instance.
        /// </summary>
        /// <remarks>Handoff routes always handle invoke activities, so the value of <paramref name="isInvoke"/> is ignored.</remarks>
        /// <param name="isInvoke">Ignored.</param>
        /// <returns>The current builder instance.</returns>
        public override TBuilder AsInvoke(bool isInvoke = true)
        {
            return (TBuilder)this;
        }
    }
}