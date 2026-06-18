// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.App
{
    /// <summary>
    /// Provides a fluent builder for Teams message reactions added routes.
    /// </summary>
    public class TeamsReactionsAddedRouteBuilder : RouteBuilderBase<TeamsReactionsAddedRouteBuilder>
    {
        /// <summary>
        /// Creates a new instance of the <see cref="TeamsReactionsAddedRouteBuilder"/> class.
        /// </summary>
        /// <returns>A new <see cref="TeamsReactionsAddedRouteBuilder"/>.</returns>
        public static TeamsReactionsAddedRouteBuilder Create()
        {
            return new TeamsReactionsAddedRouteBuilder();
        }

        /// <summary>
        /// Assigns the specified Teams route handler to the current route.
        /// </summary>
        /// <param name="handler">The Teams-aware handler to associate with the route.</param>
        /// <returns>The current builder instance.</returns>
        public TeamsReactionsAddedRouteBuilder WithHandler(TeamsRouteHandler handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            _route.Selector = (ITurnContext context, CancellationToken _) => Task.FromResult
            (
                IsContextMatch(context, _route)
                && context.Activity.IsType(ActivityTypes.MessageReaction)
                && context.Activity?.ReactionsAdded != null
                && context.Activity.ReactionsAdded.Count > 0
            );

            _route.Handler = HandlerUtils.WrapHandler(handler);

            return this;
        }

        /// <inheritdoc/>
        public override TeamsReactionsAddedRouteBuilder AsInvoke(bool isInvoke = true)
        {
            return this;
        }

        /// <summary>
        /// Applies Teams-specific defaults before the route is built.
        /// </summary>
        protected override void PreBuild()
        {
            _route.ChannelId = Channels.Msteams;
            base.PreBuild();
        }
    }
}
