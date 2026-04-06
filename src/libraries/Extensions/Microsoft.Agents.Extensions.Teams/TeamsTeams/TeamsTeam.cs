// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Extensions.Teams.TeamsTeams
{
    /// <summary>
    /// Provides fluent-style registration of handlers for Microsoft Teams team lifecycle events,
    /// such as archived, unarchived, renamed, restored, deleted, and hard-deleted.
    /// </summary>
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
        /// Registers a handler to be invoked for any team update event.
        /// Use <see cref="Microsoft.Teams.Api.Activities.ConversationUpdateActivity.EventType"/> to differentiate between
        /// team update event types (e.g. archived, deleted, etc.) using:
        /// <code>
        /// var eventType = turnContext.Activity.GetChannelData&lt;Microsoft.Teams.Api.ChannelData>().EventType;
        /// </code>
        /// </summary>
        /// <remarks>Alternatively, the <see cref="TeamUpdateRouteAttribute"/> can be used to decorate a <see cref="TeamUpdateHandler"/> method for the same purpose.</remarks>
        /// <param name="handler">The delegate that handles the team update event. This handler is called with information about the team.</param>
        /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
        /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
        /// <returns>The current TeamsTeam instance, allowing for method chaining.</returns>
        public TeamsTeam OnTeamEventReceived(TeamUpdateHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
        {
            _app.AddRoute(TeamUpdateRouteBuilder.Create()
                .WithChannelId(_channelId).WithOrderRank(rank)
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
        /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
        /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
        /// <returns>The current TeamsTeam instance, allowing for method chaining.</returns>
        public TeamsTeam OnArchived(TeamUpdateHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
        {
            _app.AddRoute(TeamUpdateRouteBuilder.Create()
                .ForTeamArchived()
                .WithChannelId(_channelId).WithOrderRank(rank)
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
        /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
        /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
        /// <returns>The current TeamsTeam instance, allowing for method chaining.</returns>
        public TeamsTeam OnUnarchived(TeamUpdateHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
        {
            _app.AddRoute(TeamUpdateRouteBuilder.Create()
                .ForTeamUnarchived()
                .WithChannelId(_channelId).WithOrderRank(rank)
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
        /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
        /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
        /// <returns>The current TeamsTeam instance, allowing for method chaining.</returns>
        public TeamsTeam OnRenamed(TeamUpdateHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
        {
            _app.AddRoute(TeamUpdateRouteBuilder.Create()
                .ForTeamRenamed()
                .WithChannelId(_channelId).WithOrderRank(rank)
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
        /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
        /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
        /// <returns>The current TeamsTeam instance, allowing for method chaining.</returns>
        public TeamsTeam OnRestored(TeamUpdateHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
        {
            _app.AddRoute(TeamUpdateRouteBuilder.Create()
                .ForTeamRestored()
                .WithChannelId(_channelId).WithOrderRank(rank)
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
        /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
        /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
        /// <returns>The current TeamsTeam instance, allowing for method chaining.</returns>
        public TeamsTeam OnDeleted(TeamUpdateHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
        {
            _app.AddRoute(TeamUpdateRouteBuilder.Create()
                .ForTeamDeleted()
                .WithChannelId(_channelId).WithOrderRank(rank)
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
        /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
        /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
        /// <returns>The current TeamsTeam instance, allowing for method chaining.</returns>
        public TeamsTeam OnHardDeleted(TeamUpdateHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
        {
            _app.AddRoute(TeamUpdateRouteBuilder.Create()
                .ForTeamHardDeleted()
                .WithChannelId(_channelId).WithOrderRank(rank)
                .WithHandler(handler)
                .WithOAuthHandlers(autoSignInHandlers)
                .Build());
            return this;
        }
    }
}
