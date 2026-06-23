// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.MSTeams.App
{
    /// <summary>
    /// Provides a fluent builder for Teams message reactions removed routes.
    /// </summary>
    public class TeamsReactionsRemovedRouteBuilder : RouteBuilderBase<TeamsReactionsRemovedRouteBuilder>
    {
        /// <summary>
        /// Creates a new instance of the <see cref="TeamsReactionsRemovedRouteBuilder"/> class.
        /// </summary>
        /// <returns>A new <see cref="TeamsReactionsRemovedRouteBuilder"/>.</returns>
        public static TeamsReactionsRemovedRouteBuilder Create()
        {
            return new TeamsReactionsRemovedRouteBuilder();
        }

        /// <summary>
        /// Assigns the specified Teams route handler to the current route.
        /// </summary>
        /// <param name="handler">The Teams-aware handler to associate with the route.</param>
        /// <returns>The current builder instance.</returns>
        public TeamsReactionsRemovedRouteBuilder WithHandler(TeamsRouteHandler handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            _route.Selector = (ITurnContext context, CancellationToken _) => Task.FromResult
            (
                IsContextMatch(context, _route)
                && context.Activity.IsType(ActivityTypes.MessageReaction)
                && context.Activity?.ReactionsRemoved != null
                && context.Activity.ReactionsRemoved.Count > 0
            );

            _route.Handler = HandlerUtils.WrapHandler(handler);

            return this;
        }

        /// <inheritdoc/>
        public override TeamsReactionsRemovedRouteBuilder AsInvoke(bool isInvoke = true)
        {
            return this;
        }

        /// <summary>
        /// Applies Teams-specific defaults before the route is built.
        /// </summary>
        protected override void PreBuild()
        {
            _route.ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams;
            base.PreBuild();
        }
    }
}
