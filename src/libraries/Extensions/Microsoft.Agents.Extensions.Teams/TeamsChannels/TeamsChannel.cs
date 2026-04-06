// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Extensions.Teams.TeamsChannels
{
    /// <summary>
    /// Represents a Teams channel and provides methods to register handlers for channel events.
    /// </summary>
    public class TeamsChannel
    {
        private readonly AgentApplication _app;
        private readonly ChannelId _channelId;

        internal TeamsChannel(AgentApplication app, ChannelId channelId)
        {
            _app = app;
            _channelId = channelId;
        }

        /// <summary>
        /// Registers a handler to be invoked for any channel update event.
        /// Use <see cref="Microsoft.Teams.Api.Activities.ConversationUpdateActivity.EventType"/> to differentiate between
        /// channel update event types (e.g. created, deleted, etc.) using:
        /// <code>
        /// var eventType = turnContext.Activity.GetChannelData&lt;Microsoft.Teams.Api.ChannelData>().EventType;
        /// </code>
        /// </summary>
        /// <remarks>Alternatively, the <see cref="ChannelUpdateRouteAttribute"/> can be used to decorate a <see cref="ChannelUpdateHandler"/> method for the same purpose.</remarks>
        /// <param name="handler">The delegate that handles any channel update event.</param>
        /// <param name="rank">The priority rank for the route. Lower values indicate higher priority. The default is unspecified.</param>
        /// <param name="autoSignInHandlers">An array of OAuth handler identifiers to use for automatic sign-in. Specify null if no automatic sign-in is required.</param>
        /// <param name="isAgenticOnly">true to invoke the handler only for agentic channels; otherwise, false.</param>
        /// <returns>The current TeamsChannel instance, allowing for method chaining.</returns>
        public TeamsChannel OnChannelEventReceived(ChannelUpdateHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            _app.AddRoute(ChannelUpdateRouteBuilder.Create()
                .WithChannelId(_channelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
                .WithHandler(handler)
                .WithOAuthHandlers(autoSignInHandlers)
                .Build());
            return this;
        }

        /// <summary>
        /// Registers a handler to be invoked when a new Teams channel is created.
        /// </summary>
        /// <remarks>Alternatively, the <see cref="ChannelCreatedRouteAttribute"/> can be used to decorate a <see cref="ChannelUpdateHandler"/> method for the same purpose.</remarks>
        /// <param name="handler">The delegate that handles the channel creation event. This handler is called with information about the channel.</param>
        /// <param name="rank">The priority rank for the route. Lower values indicate higher priority. The default is unspecified.</param>
        /// <param name="autoSignInHandlers">An array of OAuth handler identifiers to use for automatic sign-in during the channel creation process.
        /// Specify null if no automatic sign-in is required.</param>
        /// <param name="isAgenticOnly">true to invoke the handler only for agentic channels; otherwise, false.</param>
        /// <returns>The current TeamsChannel instance, allowing for method chaining.</returns>
        public TeamsChannel OnCreated(ChannelUpdateHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            _app.AddRoute(ChannelUpdateRouteBuilder.Create()
                .ForChannelCreated()
                .WithChannelId(_channelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
                .WithHandler(handler)
                .WithOAuthHandlers(autoSignInHandlers)
                .Build());
            return this;
        }

        /// <summary>
        /// Registers a handler to be invoked when a Teams channel is deleted.
        /// </summary>
        /// <remarks>Alternatively, the <see cref="ChannelDeletedRouteAttribute"/> can be used to decorate a <see cref="ChannelUpdateHandler"/> method for the same purpose.</remarks>
        /// <param name="handler">The delegate that handles the channel deletion event. This handler is called with information about the channel.</param>
        /// <param name="rank">The priority rank for the route. Lower values indicate higher priority. The default is unspecified.</param>
        /// <param name="autoSignInHandlers">An array of OAuth handler identifiers to use for automatic sign-in during the channel deletion process.
        /// Specify null if no automatic sign-in is required.</param>
        /// <param name="isAgenticOnly">true to invoke the handler only for agentic channels; otherwise, false.</param>
        /// <returns>The current TeamsChannel instance, allowing for method chaining.</returns>
        public TeamsChannel OnDeleted(ChannelUpdateHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            _app.AddRoute(ChannelUpdateRouteBuilder.Create()
                .ForChannelDeleted()
                .WithChannelId(_channelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
                .WithHandler(handler)
                .WithOAuthHandlers(autoSignInHandlers)
                .Build());
            return this;
        }

        /// <summary>
        /// Registers a handler to be invoked when a Teams channel is renamed.
        /// </summary>
        /// <remarks>Alternatively, the <see cref="ChannelRenamedRouteAttribute"/> can be used to decorate a <see cref="ChannelUpdateHandler"/> method for the same purpose.</remarks>
        /// <param name="handler">The delegate that handles the channel renamed event. This handler is called with information about the channel.</param>
        /// <param name="rank">The priority rank for the route. Lower values indicate higher priority. The default is unspecified.</param>
        /// <param name="autoSignInHandlers">An array of OAuth handler identifiers to use for automatic sign-in during the channel renamed process.
        /// Specify null if no automatic sign-in is required.</param>
        /// <param name="isAgenticOnly">true to invoke the handler only for agentic channels; otherwise, false.</param>
        /// <returns>The current TeamsChannel instance, allowing for method chaining.</returns>
        public TeamsChannel OnRenamed(ChannelUpdateHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            _app.AddRoute(ChannelUpdateRouteBuilder.Create()
                .ForChannelRenamed()
                .WithChannelId(_channelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
                .WithHandler(handler)
                .WithOAuthHandlers(autoSignInHandlers)
                .Build());
            return this;
        }

        /// <summary>
        /// Registers a handler to be invoked when a Teams channel is shared.
        /// </summary>
        /// <remarks>Alternatively, the <see cref="ChannelSharedRouteAttribute"/> can be used to decorate a <see cref="ChannelUpdateHandler"/> method for the same purpose.</remarks>
        /// <param name="handler">The delegate that handles the channel shared event. This handler is called with information about the channel.</param>
        /// <param name="rank">The priority rank for the route. Lower values indicate higher priority. The default is unspecified.</param>
        /// <param name="autoSignInHandlers">An array of OAuth handler identifiers to use for automatic sign-in during the channel shared process.
        /// Specify null if no automatic sign-in is required.</param>
        /// <param name="isAgenticOnly">true to invoke the handler only for agentic channels; otherwise, false.</param>
        /// <returns>The current TeamsChannel instance, allowing for method chaining.</returns>
        public TeamsChannel OnShared(ChannelUpdateHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            _app.AddRoute(ChannelUpdateRouteBuilder.Create()
                .ForChannelShared()
                .WithChannelId(_channelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
                .WithHandler(handler)
                .WithOAuthHandlers(autoSignInHandlers)
                .Build());
            return this;
        }

        /// <summary>
        /// Registers a handler to be invoked when a Teams channel is unshared.
        /// </summary>
        /// <remarks>Alternatively, the <see cref="ChannelUnsharedRouteAttribute"/> can be used to decorate a <see cref="ChannelUpdateHandler"/> method for the same purpose.</remarks>
        /// <param name="handler">The delegate that handles the channel unshared event. This handler is called with information about the channel.</param>
        /// <param name="rank">The priority rank for the route. Lower values indicate higher priority. The default is unspecified.</param>
        /// <param name="autoSignInHandlers">An array of OAuth handler identifiers to use for automatic sign-in during the channel unshared process.
        /// Specify null if no automatic sign-in is required.</param>
        /// <param name="isAgenticOnly">true to invoke the handler only for agentic channels; otherwise, false.</param>
        /// <returns>The current TeamsChannel instance, allowing for method chaining.</returns>
        public TeamsChannel OnUnshared(ChannelUpdateHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            _app.AddRoute(ChannelUpdateRouteBuilder.Create()
                .ForChannelUnshared()
                .WithChannelId(_channelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
                .WithHandler(handler)
                .WithOAuthHandlers(autoSignInHandlers)
                .Build());
            return this;
        }

        /// <summary>
        /// Registers a handler to be invoked when a Teams channel is restored.
        /// </summary>
        /// <remarks>Alternatively, the <see cref="ChannelRestoredRouteAttribute"/> can be used to decorate a <see cref="ChannelUpdateHandler"/> method for the same purpose.</remarks>
        /// <param name="handler">The delegate that handles the channel restored event. This handler is called with information about the channel.</param>
        /// <param name="rank">The priority rank for the route. Lower values indicate higher priority. The default is unspecified.</param>
        /// <param name="autoSignInHandlers">An array of OAuth handler identifiers to use for automatic sign-in during the channel restored process.
        /// Specify null if no automatic sign-in is required.</param>
        /// <param name="isAgenticOnly">true to invoke the handler only for agentic channels; otherwise, false.</param>
        /// <returns>The current TeamsChannel instance, allowing for method chaining.</returns>
        public TeamsChannel OnRestored(ChannelUpdateHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            _app.AddRoute(ChannelUpdateRouteBuilder.Create()
                .ForChannelRestored()
                .WithChannelId(_channelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
                .WithHandler(handler)
                .WithOAuthHandlers(autoSignInHandlers)
                .Build());
            return this;
        }

        /// <summary>
        /// Registers a handler to be invoked when a Teams channel member is added.
        /// </summary>
        /// <remarks>Alternatively, the <see cref="ChannelMemberAddedRouteAttribute"/> can be used to decorate a <see cref="ChannelUpdateHandler"/> method for the same purpose.</remarks>
        /// <param name="handler">The delegate that handles the channel member added event. This handler is called with information about the channel.</param>
        /// <param name="rank">The priority rank for the route. Lower values indicate higher priority. The default is unspecified.</param>
        /// <param name="autoSignInHandlers">An array of OAuth handler identifiers to use for automatic sign-in during the channel member added process.
        /// Specify null if no automatic sign-in is required.</param>
        /// <param name="isAgenticOnly">true to invoke the handler only for agentic channels; otherwise, false.</param>
        /// <returns>The current TeamsChannel instance, allowing for method chaining.</returns>
        public TeamsChannel OnMemberAdded(ChannelUpdateHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            _app.AddRoute(ChannelUpdateRouteBuilder.Create()
                .ForChannelMemberAdded()
                .WithChannelId(_channelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
                .WithHandler(handler)
                .WithOAuthHandlers(autoSignInHandlers)
                .Build());
            return this;
        }

        /// <summary>
        /// Registers a handler to be invoked when a Teams channel member is removed.
        /// </summary>
        /// <remarks>Alternatively, the <see cref="ChannelMemberRemovedRouteAttribute"/> can be used to decorate a <see cref="ChannelUpdateHandler"/> method for the same purpose.</remarks>
        /// <param name="handler">The delegate that handles the channel member removed event. This handler is called with information about the channel.</param>
        /// <param name="rank">The priority rank for the route. Lower values indicate higher priority. The default is unspecified.</param>
        /// <param name="autoSignInHandlers">An array of OAuth handler identifiers to use for automatic sign-in during the channel member removed process.
        /// Specify null if no automatic sign-in is required.</param>
        /// <param name="isAgenticOnly">true to invoke the handler only for agentic channels; otherwise, false.</param>
        /// <returns>The current TeamsChannel instance, allowing for method chaining.</returns>
        public TeamsChannel OnMemberRemoved(ChannelUpdateHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            _app.AddRoute(ChannelUpdateRouteBuilder.Create()
                .ForChannelMemberRemoved()
                .WithChannelId(_channelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
                .WithHandler(handler)
                .WithOAuthHandlers(autoSignInHandlers)
                .Build());
            return this;
        }
    }
}
