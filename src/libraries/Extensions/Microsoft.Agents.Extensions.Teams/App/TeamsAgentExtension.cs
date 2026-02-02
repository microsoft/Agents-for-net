// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.App.MessageExtensions;
using Microsoft.Teams.Api;
using Microsoft.Teams.Api.Config;
using Microsoft.Teams.Api.O365;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static Microsoft.Teams.Api.Activities.ConversationUpdateActivity;

namespace Microsoft.Agents.Extensions.Teams.App
{
    /// <summary>
    /// AgentExtension for Microsoft Teams.
    /// </summary>
    public class TeamsAgentExtension : AgentExtension
    {
        //private static readonly string CONFIG_FETCH_INVOKE_NAME = "config/fetch";
        //private static readonly string CONFIG_SUBMIT_INVOKE_NAME = "config/submit";

        /// <summary>
        /// Creates a new TeamsAgentExtension instance.
        /// To leverage this extension, call <see cref="AgentApplication.RegisterExtension(IAgentExtension)"/> with an instance of this class.
        /// Use the callback method to register routes for handling Teams-specific events.
        /// </summary>
        /// <param name="agentApplication">The agent application to leverage for route registration.</param>
        /// <param name="options">Options for configuring TaskModules.</param>
        public TeamsAgentExtension(AgentApplication agentApplication)
        {
            ChannelId = Channels.Msteams;

            AgentApplication = agentApplication;
            MessageExtensions = new MessageExtension(agentApplication);
        }

        /// <summary>
        /// Fluent interface for accessing Message Extensions' specific features.
        /// </summary>
        public MessageExtension MessageExtensions { get; }

#if !NETSTANDARD
        protected AgentApplication AgentApplication { get; init;}
#else
        protected AgentApplication AgentApplication { get; set;}
#endif

        /// <summary>
        /// Handles conversation update events.
        /// </summary>
        /// <param name="conversationUpdateEvent">Name of the conversation update event to handle, can use <see cref="ConversationUpdateEvents"/>.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The AgentExtension instance for chaining purposes.</returns>
        public TeamsAgentExtension OnConversationUpdate(string conversationUpdateEvent, RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            AssertionHelpers.ThrowIfNull(conversationUpdateEvent, nameof(conversationUpdateEvent));
            
            RouteSelector routeSelector;
            if (conversationUpdateEvent == ConversationUpdateEvents.MembersAdded)
            {
                routeSelector = (context, _) => Task.FromResult
                (
                    string.Equals(context.Activity?.Type, ActivityTypes.ConversationUpdate, StringComparison.OrdinalIgnoreCase)
                    && context.Activity?.MembersAdded != null
                    && context.Activity.MembersAdded.Count > 0
                );
            }
            else if (conversationUpdateEvent == ConversationUpdateEvents.MembersRemoved)
            {
                routeSelector = (context, _) => Task.FromResult
                (
                    string.Equals(context.Activity?.Type, ActivityTypes.ConversationUpdate, StringComparison.OrdinalIgnoreCase)
                    && context.Activity?.MembersRemoved != null
                    && context.Activity.MembersRemoved.Count > 0
                );
            }
            else if (
                   conversationUpdateEvent == EventType.ChannelCreated
                || conversationUpdateEvent == EventType.ChannelDeleted
                || conversationUpdateEvent == EventType.ChannelRenamed
                || conversationUpdateEvent == EventType.ChannelRestored
                || conversationUpdateEvent == EventType.ChannelShared
                || conversationUpdateEvent == EventType.ChannelUnShared)
            {
                routeSelector = (context, _) => Task.FromResult
                (
                    string.Equals(context.Activity?.Type, ActivityTypes.ConversationUpdate, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(context.Activity?.GetChannelData<ChannelData>()?.EventType, conversationUpdateEvent)
                    && context.Activity?.GetChannelData<ChannelData>()?.Channel != null
                    && context.Activity?.GetChannelData<ChannelData>()?.Team != null
                );
            }
            else if (
                   conversationUpdateEvent == EventType.TeamRenamed
                || conversationUpdateEvent == EventType.TeamDeleted
                || conversationUpdateEvent == EventType.TeamHardDeleted
                || conversationUpdateEvent == EventType.TeamArchived
                || conversationUpdateEvent == EventType.TeamUnarchived
                || conversationUpdateEvent == EventType.TeamRestored)
            {
                routeSelector = (context, _) => Task.FromResult
                (
                    string.Equals(context.Activity?.Type, ActivityTypes.ConversationUpdate, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(context.Activity?.GetChannelData<ChannelData>()?.EventType, conversationUpdateEvent)
                    && context.Activity?.GetChannelData<ChannelData>()?.Team != null
                );
            }
            else
            {
                routeSelector = (context, _) => Task.FromResult
                (
                    string.Equals(context.Activity?.Type, ActivityTypes.ConversationUpdate, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(context.Activity?.GetChannelData<ChannelData>()?.EventType, conversationUpdateEvent)
                );
            }

            AddRoute(AgentApplication, routeSelector, handler, isInvokeRoute: false, rank, autoSignInHandlers, isAgenticOnly);
            return this;
        }

        /// <summary>
        /// Handles message edit events.
        /// </summary>
        /// <param name="handler">Function to call when the event is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The AgentExtension instance for chaining purposes.</returns>
        public TeamsAgentExtension OnMessageEdit(RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            static Task<bool> routeSelector(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                ChannelData ChannelData;
                return Task.FromResult(
                    string.Equals(turnContext.Activity.Type, ActivityTypes.MessageUpdate, StringComparison.OrdinalIgnoreCase)
                    && (ChannelData = turnContext.Activity.GetChannelData<ChannelData>()) != null
                    && string.Equals(ChannelData.EventType, "editMessage"));
            }

            AddRoute(AgentApplication, routeSelector, handler, isInvokeRoute: false, rank, autoSignInHandlers, isAgenticOnly);
            return this;
        }

        /// <summary>
        /// Handles message undo soft delete events.
        /// </summary>
        /// <param name="handler">Function to call when the event is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The AgentExtension instance for chaining purposes.</returns>
        public TeamsAgentExtension OnMessageUndelete(RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            static Task<bool> routeSelector(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                ChannelData ChannelData;
                return Task.FromResult(
                    string.Equals(turnContext.Activity.Type, ActivityTypes.MessageUpdate, StringComparison.OrdinalIgnoreCase)
                    && (ChannelData = turnContext.Activity.GetChannelData<ChannelData>()) != null
                    && string.Equals(ChannelData.EventType, "undeleteMessage"));
            }

            AddRoute(AgentApplication, routeSelector, handler, isInvokeRoute: false, rank, autoSignInHandlers, isAgenticOnly);
            return this;
        }

        /// <summary>
        /// Handles message soft delete events.
        /// </summary>
        /// <param name="handler">Function to call when the event is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The AgentExtension instance for chaining purposes.</returns>
        public TeamsAgentExtension OnMessageDelete(RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            static Task<bool> routeSelector(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                ChannelData ChannelData;
                return Task.FromResult(
                    string.Equals(turnContext.Activity.Type, ActivityTypes.MessageDelete, StringComparison.OrdinalIgnoreCase)
                    && (ChannelData = turnContext.Activity.GetChannelData<ChannelData>()) != null
                    && string.Equals(ChannelData.EventType, "softDeleteMessage"));
            }

            AddRoute(AgentApplication, routeSelector, handler, isInvokeRoute: false, rank, autoSignInHandlers, isAgenticOnly);
            return this;
        }

        /// <summary>
        /// Handles read receipt events for messages sent by the bot in personal scope.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The AgentExtension instance for chaining purposes.</returns>
        public TeamsAgentExtension OnTeamsReadReceipt(ReadReceiptHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            Task<bool> routeSelector(ITurnContext context, CancellationToken _) => Task.FromResult
            (
                string.Equals(context.Activity?.Type, ActivityTypes.Event, StringComparison.OrdinalIgnoreCase)
                && string.Equals(context.Activity?.Name, "application/vnd.microsoft.readReceipt")
            );

            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                JsonElement readReceiptInfo = (JsonElement)turnContext.Activity.Value;
                await handler(turnContext, turnState, readReceiptInfo, cancellationToken);
            }

            AddRoute(AgentApplication, routeSelector, routeHandler, isInvokeRoute: false, rank, autoSignInHandlers, isAgenticOnly);
            return this;
        }

        /// <summary>
        /// Handles config fetch events for Microsoft Teams.
        /// </summary>
        /// <param name="handler">Function to call when the event is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The AgentExtension instance for chaining purposes.</returns>
        public TeamsAgentExtension OnConfigFetch(ConfigHandlerAsync handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            Task<bool> routeSelector(ITurnContext turnContext, CancellationToken cancellationToken) => Task.FromResult(
                string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                && string.Equals(turnContext.Activity.Name, Microsoft.Teams.Api.Activities.Invokes.Name.Configs.Fetch));

            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                ConfigResponse result = await handler(turnContext, turnState, turnContext.Activity.Value, cancellationToken);
                await SetResponse(turnContext, result);
            }

            AddRoute(AgentApplication, routeSelector, routeHandler, isInvokeRoute: true, rank, autoSignInHandlers, isAgenticOnly);
            return this;
        }

        /// <summary>
        /// Handles config submit events for Microsoft Teams.
        /// </summary>
        /// <param name="handler">Function to call when the event is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The AgentExtension instance for chaining purposes.</returns>
        public TeamsAgentExtension OnConfigSubmit(ConfigHandlerAsync handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            Task<bool> routeSelector(ITurnContext turnContext, CancellationToken cancellationToken) => Task.FromResult(
                string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                && string.Equals(turnContext.Activity.Name, Microsoft.Teams.Api.Activities.Invokes.Name.Configs.Submit));

            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                ConfigResponse result = await handler(turnContext, turnState, turnContext.Activity.Value, cancellationToken);
                await SetResponse(turnContext, result);
            }

            AddRoute(AgentApplication, routeSelector, routeHandler, isInvokeRoute: true, rank, autoSignInHandlers, isAgenticOnly);
            return this;
        }

        /// <summary>
        /// Handles when a file consent card is accepted by the user.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The AgentExtension instance for chaining purposes.</returns>
        public TeamsAgentExtension OnFileConsentAccept(FileConsentHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
            => OnFileConsent(handler, "accept", rank, autoSignInHandlers, isAgenticOnly);

        /// <summary>
        /// Handles when a file consent card is declined by the user.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The AgentExtension instance for chaining purposes.</returns>
        public TeamsAgentExtension OnFileConsentDecline(FileConsentHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
            => OnFileConsent(handler, "decline", rank, autoSignInHandlers, isAgenticOnly);

        private TeamsAgentExtension OnFileConsent(FileConsentHandler handler, string fileConsentAction, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            Task<bool> routeSelector(ITurnContext context, CancellationToken _)
            {
                FileConsentCardResponse? fileConsentCardResponse;
                return Task.FromResult
                (
                    string.Equals(context.Activity?.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(context.Activity?.Name, "fileConsent/invoke")
                    && (fileConsentCardResponse = ProtocolJsonSerializer.ToObject<FileConsentCardResponse>(context.Activity!.Value)) != null
                    && string.Equals(fileConsentCardResponse.Action, fileConsentAction)
                );
            }

            RouteHandler routeHandler = async (turnContext, turnState, cancellationToken) =>
            {
                FileConsentCardResponse fileConsentCardResponse = ProtocolJsonSerializer.ToObject<FileConsentCardResponse>(turnContext.Activity.Value);
                await handler(turnContext, turnState, fileConsentCardResponse, cancellationToken);
                await SetResponse(turnContext);
            };

            AddRoute(AgentApplication, routeSelector, routeHandler, isInvokeRoute: true, rank, autoSignInHandlers, isAgenticOnly);
            return this;
        }

        /// <summary>
        /// Handles O365 Connector Card Action activities.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The AgentExtension instance for chaining purposes.</returns>
        public AgentApplication OnO365ConnectorCardAction(O365ConnectorCardActionHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            Task<bool> routeSelector(ITurnContext context, CancellationToken _) => Task.FromResult
            (
                string.Equals(context.Activity?.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                && string.Equals(context.Activity?.Name, "actionableMessage/executeAction")
            );

            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                ConnectorCardActionQuery query = ProtocolJsonSerializer.ToObject<ConnectorCardActionQuery>(turnContext.Activity.Value) ?? new();
                await handler(turnContext, turnState, query, cancellationToken);
                await SetResponse(turnContext);
            }

            AddRoute(AgentApplication, routeSelector, routeHandler, isInvokeRoute: true, rank, autoSignInHandlers, isAgenticOnly);
            return AgentApplication;
        }

        internal static Task SetResponse(ITurnContext context, object result = null)
        {
            if (!context.StackState.Has(ChannelAdapter.InvokeResponseKey))
            {
                var activity = Activity.CreateInvokeResponseActivity(result);
                return context.SendActivityAsync(activity, CancellationToken.None);
            }

            return Task.CompletedTask;
        }
    }
}
