// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Extensions.Teams.TeamsTeams
{
    public class TeamsTeam
    {
        private readonly AgentApplication _app;
        private readonly ChannelId _channelId;

        internal TeamsTeam(AgentApplication app, ChannelId channelId)
        {
            _app = app;
            _channelId = channelId;
        }

        /// <summary>
        /// Handle any team update event.  Use <see cref="Microsoft.Teams.Api.Activities.ConversationUpdateActivity.EventType"/> to differentiate between 
        /// team update event types (e.g. archived, deleted, etc.) using:
        /// <code>
        /// var eventType = turnContext.Activity.GetChannelData&lt;Microsoft.Teams.Api.ChannelData>().EventType;
        /// </code>
        /// </summary>
        /// <param name="handler">The delegate that handles the team update event. This handler is called with information about the team.</param>
        /// <param name="rank">The priority rank for the route. Lower values indicate higher priority. The default is unspecified.</param>
        /// <param name="autoSignInHandlers">An array of OAuth handler identifiers to use for automatic sign-in during the team update process.
        /// Specify null if no automatic sign-in is required.</param>
        /// <param name="isAgenticOnly">true to invoke the handler only for agentic channels; otherwise, false.</param>
        /// <returns>The current TeamsTeam instance, allowing for method chaining.</returns>
        public TeamsTeam OnTeamEventReceived(TeamUpdateHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            _app.AddRoute(TeamUpdateRouteBuilder.Create()
                .WithChannelId(_channelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
                .WithHandler(handler)
                .WithOAuthHandlers(autoSignInHandlers)
                .Build());
            return this;
        }

        /// <summary>
        /// Registers a handler to be invoked when a team is archived.
        /// </summary>
        /// <remarks>Alternatively, the <see cref="TeamArchivedRouteAttribute"/> can be used to decorate a <see cref="TeamUpdateHandler"/> method for the same purpose.</remarks>
        /// <param name="handler">The delegate that handles the team archived event. This handler is called with information about the team.</param>
        /// <param name="rank">The priority rank for the route. Lower values indicate higher priority. The default is unspecified.</param>
        /// <param name="autoSignInHandlers">An array of OAuth handler identifiers to use for automatic sign-in during the team archived process.
        /// Specify null if no automatic sign-in is required.</param>
        /// <param name="isAgenticOnly">true to invoke the handler only for agentic channels; otherwise, false.</param>
        /// <returns>The current TeamsTeam instance, allowing for method chaining.</returns>
        public TeamsTeam OnArchived(TeamUpdateHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            _app.AddRoute(TeamUpdateRouteBuilder.Create()
                .ForTeamArchived()
                .WithChannelId(_channelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
                .WithHandler(handler)
                .WithOAuthHandlers(autoSignInHandlers)
                .Build());
            return this;
        }

        /// <summary>
        /// Registers a handler to be invoked when a team is unarchived.
        /// </summary>
        /// <remarks>Alternatively, the <see cref="TeamUnarchivedRouteAttribute"/> can be used to decorate a <see cref="TeamUpdateHandler"/> method for the same purpose.</remarks>
        /// <param name="handler">The delegate that handles the team unarchived event. This handler is called with information about the team.</param>
        /// <param name="rank">The priority rank for the route. Lower values indicate higher priority. The default is unspecified.</param>
        /// <param name="autoSignInHandlers">An array of OAuth handler identifiers to use for automatic sign-in during the team unarchived process.
        /// Specify null if no automatic sign-in is required.</param>
        /// <param name="isAgenticOnly">true to invoke the handler only for agentic channels; otherwise, false.</param>
        /// <returns>The current TeamsTeam instance, allowing for method chaining.</returns>
        public TeamsTeam OnUnarchived(TeamUpdateHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            _app.AddRoute(TeamUpdateRouteBuilder.Create()
                .ForTeamUnarchived()
                .WithChannelId(_channelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
                .WithHandler(handler)
                .WithOAuthHandlers(autoSignInHandlers)
                .Build());
            return this;
        }

        /// <summary>
        /// Registers a handler to be invoked when a team is renamed.
        /// </summary>
        /// <remarks>Alternatively, the <see cref="TeamRenamedRouteAttribute"/> can be used to decorate a <see cref="TeamUpdateHandler"/> method for the same purpose.</remarks>
        /// <param name="handler">The delegate that handles the team renamed event. This handler is called with information about the team.</param>
        /// <param name="rank">The priority rank for the route. Lower values indicate higher priority. The default is unspecified.</param>
        /// <param name="autoSignInHandlers">An array of OAuth handler identifiers to use for automatic sign-in during the team renamed process.
        /// Specify null if no automatic sign-in is required.</param>
        /// <param name="isAgenticOnly">true to invoke the handler only for agentic channels; otherwise, false.</param>
        /// <returns>The current TeamsTeam instance, allowing for method chaining.</returns>
        public TeamsTeam OnRenamed(TeamUpdateHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            _app.AddRoute(TeamUpdateRouteBuilder.Create()
                .ForTeamRenamed()
                .WithChannelId(_channelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
                .WithHandler(handler)
                .WithOAuthHandlers(autoSignInHandlers)
                .Build());
            return this;
        }

        /// <summary>
        /// Registers a handler to be invoked when a team is restored.
        /// </summary>
        /// <remarks>Alternatively, the <see cref="TeamRestoredRouteAttribute"/> can be used to decorate a <see cref="TeamUpdateHandler"/> method for the same purpose.</remarks>
        /// <param name="handler">The delegate that handles the team restored event. This handler is called with information about the team.</param>
        /// <param name="rank">The priority rank for the route. Lower values indicate higher priority. The default is unspecified.</param>
        /// <param name="autoSignInHandlers">An array of OAuth handler identifiers to use for automatic sign-in during the team restored process.
        /// Specify null if no automatic sign-in is required.</param>
        /// <param name="isAgenticOnly">true to invoke the handler only for agentic channels; otherwise, false.</param>
        /// <returns>The current TeamsTeam instance, allowing for method chaining.</returns>
        public TeamsTeam OnRestored(TeamUpdateHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            _app.AddRoute(TeamUpdateRouteBuilder.Create()
                .ForTeamRestored()
                .WithChannelId(_channelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
                .WithHandler(handler)
                .WithOAuthHandlers(autoSignInHandlers)
                .Build());
            return this;
        }

        /// <summary>
        /// Registers a handler to be invoked when a team is deleted.
        /// </summary>
        /// <remarks>Alternatively, the <see cref="TeamDeletedRouteAttribute"/> can be used to decorate a <see cref="TeamUpdateHandler"/> method for the same purpose.</remarks>
        /// <param name="handler">The delegate that handles the team deleted event. This handler is called with information about the team.</param>
        /// <param name="rank">The priority rank for the route. Lower values indicate higher priority. The default is unspecified.</param>
        /// <param name="autoSignInHandlers">An array of OAuth handler identifiers to use for automatic sign-in during the team deleted process.
        /// Specify null if no automatic sign-in is required.</param>
        /// <param name="isAgenticOnly">true to invoke the handler only for agentic channels; otherwise, false.</param>
        /// <returns>The current TeamsTeam instance, allowing for method chaining.</returns>
        public TeamsTeam OnDeleted(TeamUpdateHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            _app.AddRoute(TeamUpdateRouteBuilder.Create()
                .ForTeamDeleted()
                .WithChannelId(_channelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
                .WithHandler(handler)
                .WithOAuthHandlers(autoSignInHandlers)
                .Build());
            return this;
        }

        /// <summary>
        /// Registers a handler to be invoked when a team is hard deleted.
        /// </summary>
        /// <remarks>Alternatively, the <see cref="TeamHardDeletedRouteAttribute"/> can be used to decorate a <see cref="TeamUpdateHandler"/> method for the same purpose.</remarks>
        /// <param name="handler">The delegate that handles the team hard deleted event. This handler is called with information about the team.</param>
        /// <param name="rank">The priority rank for the route. Lower values indicate higher priority. The default is unspecified.</param>
        /// <param name="autoSignInHandlers">An array of OAuth handler identifiers to use for automatic sign-in during the team hard deleted process.
        /// Specify null if no automatic sign-in is required.</param>
        /// <param name="isAgenticOnly">true to invoke the handler only for agentic channels; otherwise, false.</param>
        /// <returns>The current TeamsTeam instance, allowing for method chaining.</returns>
        public TeamsTeam OnHardDeleted(TeamUpdateHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            _app.AddRoute(TeamUpdateRouteBuilder.Create()
                .ForTeamHardDeleted()
                .WithChannelId(_channelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
                .WithHandler(handler)
                .WithOAuthHandlers(autoSignInHandlers)
                .Build());
            return this;
        }
    }
}
