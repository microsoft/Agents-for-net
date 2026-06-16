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

        public HandoffRouteBuilder WithHandler(HandoffHandler handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            Task<bool> routeSelector(ITurnContext context, CancellationToken _) => Task.FromResult
                (
                    IsContextMatch(context, _route)
                    && context.Activity is IInvokeActivity invokeActivity
                    && string.Equals(invokeActivity.Name, "handoff/action", System.StringComparison.OrdinalIgnoreCase)
                );

            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                if (turnContext.Activity is not IInvokeActivity invokeActivity)
                {
                    return;
                }

                string token = invokeActivity.Value?.GetType()?.GetProperty("Continuation")?.GetValue(invokeActivity.Value) as string ?? "";
                await handler(turnContext, turnState, token, cancellationToken);
                await turnContext.SendActivityAsync(Activity.CreateInvokeResponseActivity(), cancellationToken);
            }

            _route.Selector = routeSelector;
            _route.Handler = routeHandler;

            return this;
        }
    }
}