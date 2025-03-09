﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Models;
using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Extensions.Teams.App.Meetings;
using Microsoft.Agents.Extensions.Teams.App.MessageExtensions;
using Microsoft.Agents.Extensions.Teams.App.TaskModules;

namespace Microsoft.Agents.Extensions.Teams.App
{
    /// <summary>
    /// AgentApplication class for routing and processing incoming requests.
    /// </summary>
    public class TeamsAgentExtension : IAgentExtension
    {
        private static readonly string CONFIG_FETCH_INVOKE_NAME = "config/fetch";
        private static readonly string CONFIG_SUBMIT_INVOKE_NAME = "config/submit";

        //TODO:  Teams AgentApplication isn't handling:
        //  InputFiles to TurnState.Temp (BeforeTurn now?)
        //  AI.run

        /// <summary>
        /// Creates a new AgentApplication instance.
        /// </summary>
        /// <param name="agentApplication"></param>
        /// <param name="options"></param>
        public TeamsAgentExtension(AgentApplication agentApplication, TaskModulesOptions? options = null)
        {
            AgentApplication = agentApplication;

            Meetings = new MeetingsFeature(agentApplication);
            MessageExtensions = new MessageExtensionsFeature(agentApplication);
            TaskModules = new TaskModulesFeature(agentApplication, options);

            Options = options;
        }

        public TaskModulesOptions Options { get; }

        /// <summary>
        /// Fluent interface for accessing Meetings' specific features.
        /// </summary>
        public MeetingsFeature Meetings { get; }

        /// <summary>
        /// Fluent interface for accessing Message Extensions' specific features.
        /// </summary>
        public MessageExtensionsFeature MessageExtensions { get; }

        /// <summary>
        /// Fluent interface for accessing Task Modules' specific features.
        /// </summary>
        public TaskModulesFeature TaskModules { get; }

        protected AgentApplication AgentApplication { get; init;}

        /// <summary>
        /// Handles conversation update events.
        /// </summary>
        /// <param name="conversationUpdateEvent">Name of the conversation update event to handle, can use <see cref="ConversationUpdateEvents"/>.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The AgentApplication instance for chaining purposes.</returns>
        public TeamsAgentExtension OnConversationUpdate(string conversationUpdateEvent, RouteHandler handler)
        {
            ArgumentNullException.ThrowIfNull(conversationUpdateEvent);
            ArgumentNullException.ThrowIfNull(handler);
            RouteSelectorAsync routeSelector;
            switch (conversationUpdateEvent)
            {
                case TeamsConversationUpdateEvents.ChannelCreated:
                case TeamsConversationUpdateEvents.ChannelDeleted:
                case TeamsConversationUpdateEvents.ChannelRenamed:
                case TeamsConversationUpdateEvents.ChannelRestored:
                    {
                        routeSelector = (context, _) => Task.FromResult
                        (
                            string.Equals(context.Activity?.ChannelId, Channels.Msteams)
                            && string.Equals(context.Activity?.Type, ActivityTypes.ConversationUpdate, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(context.Activity?.GetChannelData<TeamsChannelData>()?.EventType, conversationUpdateEvent)
                            && context.Activity?.GetChannelData<TeamsChannelData>()?.Channel != null
                            && context.Activity?.GetChannelData<TeamsChannelData>()?.Team != null
                        );
                        break;
                    }
                case TeamsConversationUpdateEvents.MembersAdded:
                    {
                        routeSelector = (context, _) => Task.FromResult
                        (
                            string.Equals(context.Activity?.Type, ActivityTypes.ConversationUpdate, StringComparison.OrdinalIgnoreCase)
                            && context.Activity?.MembersAdded != null
                            && context.Activity.MembersAdded.Count > 0
                        );
                        break;
                    }
                case TeamsConversationUpdateEvents.MembersRemoved:
                    {
                        routeSelector = (context, _) => Task.FromResult
                        (
                            string.Equals(context.Activity?.Type, ActivityTypes.ConversationUpdate, StringComparison.OrdinalIgnoreCase)
                            && context.Activity?.MembersRemoved != null
                            && context.Activity.MembersRemoved.Count > 0
                        );
                        break;
                    }
                case TeamsConversationUpdateEvents.TeamRenamed:
                case TeamsConversationUpdateEvents.TeamDeleted:
                case TeamsConversationUpdateEvents.TeamHardDeleted:
                case TeamsConversationUpdateEvents.TeamArchived:
                case TeamsConversationUpdateEvents.TeamUnarchived:
                case TeamsConversationUpdateEvents.TeamRestored:
                    {
                        routeSelector = (context, _) => Task.FromResult
                        (
                            string.Equals(context.Activity?.ChannelId, Channels.Msteams)
                            && string.Equals(context.Activity?.Type, ActivityTypes.ConversationUpdate, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(context.Activity?.GetChannelData<TeamsChannelData>()?.EventType, conversationUpdateEvent)
                            && context.Activity?.GetChannelData<TeamsChannelData>()?.Team != null
                        );
                        break;
                    }
                default:
                    {
                        routeSelector = (context, _) => Task.FromResult
                        (
                            string.Equals(context.Activity?.ChannelId, Channels.Msteams)
                            && string.Equals(context.Activity?.Type, ActivityTypes.ConversationUpdate, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(context.Activity?.GetChannelData<TeamsChannelData>()?.EventType, conversationUpdateEvent)
                        );
                        break;
                    }
            }
            AgentApplication.AddRoute(routeSelector, handler, isInvokeRoute: false);
            return this;
        }

        /// <summary>
        /// Handles message edit events.
        /// </summary>
        /// <param name="handler">Function to call when the event is triggered.</param>
        /// <returns>The AgentApplication instance for chaining purposes.</returns>
        public TeamsAgentExtension OnMessageEdit(RouteHandler handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            RouteSelectorAsync routeSelector = (turnContext, cancellationToken) =>
            {
                TeamsChannelData teamsChannelData;
                return Task.FromResult(
                    string.Equals(turnContext.Activity.Type, ActivityTypes.MessageUpdate, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(turnContext.Activity.ChannelId, Channels.Msteams)
                    && (teamsChannelData = turnContext.Activity.GetChannelData<TeamsChannelData>()) != null
                    && string.Equals(teamsChannelData.EventType, "editMessage"));
            };
            AgentApplication.AddRoute(routeSelector, handler, isInvokeRoute: false);
            return this;
        }

        /// <summary>
        /// Handles message undo soft delete events.
        /// </summary>
        /// <param name="handler">Function to call when the event is triggered.</param>
        /// <returns>The AgentApplication instance for chaining purposes.</returns>
        public TeamsAgentExtension OnMessageUndelete(RouteHandler handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            RouteSelectorAsync routeSelector = (turnContext, cancellationToken) =>
            {
                TeamsChannelData teamsChannelData;
                return Task.FromResult(
                    string.Equals(turnContext.Activity.Type, ActivityTypes.MessageUpdate, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(turnContext.Activity.ChannelId, Channels.Msteams)
                    && (teamsChannelData = turnContext.Activity.GetChannelData<TeamsChannelData>()) != null
                    && string.Equals(teamsChannelData.EventType, "undeleteMessage"));
            };
            AgentApplication.AddRoute(routeSelector, handler, isInvokeRoute: false);
            return this;
        }

        /// <summary>
        /// Handles message soft delete events.
        /// </summary>
        /// <param name="handler">Function to call when the event is triggered.</param>
        /// <returns>The AgentApplication instance for chaining purposes.</returns>
        public TeamsAgentExtension OnMessageDelete(RouteHandler handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            RouteSelectorAsync routeSelector = (turnContext, cancellationToken) =>
            {
                TeamsChannelData teamsChannelData;
                return Task.FromResult(
                    string.Equals(turnContext.Activity.Type, ActivityTypes.MessageDelete, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(turnContext.Activity.ChannelId, Channels.Msteams)
                    && (teamsChannelData = turnContext.Activity.GetChannelData<TeamsChannelData>()) != null
                    && string.Equals(teamsChannelData.EventType, "softDeleteMessage"));
            };
            AgentApplication.AddRoute(routeSelector, handler, isInvokeRoute: false);
            return this;
        }

        /// <summary>
        /// Handles read receipt events for messages sent by the bot in personal scope.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The AgentApplication instance for chaining purposes.</returns>
        public TeamsAgentExtension OnTeamsReadReceipt(ReadReceiptHandler handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            RouteSelectorAsync routeSelector = (context, _) => Task.FromResult
            (
                string.Equals(context.Activity?.Type, ActivityTypes.Event, StringComparison.OrdinalIgnoreCase)
                && string.Equals(context.Activity?.ChannelId, Channels.Msteams)
                && string.Equals(context.Activity?.Name, "application/vnd.microsoft.readReceipt")
            );
            RouteHandler routeHandler = async (ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken) =>
            {
                ReadReceiptInfo readReceiptInfo = ProtocolJsonSerializer.ToObject<ReadReceiptInfo>(turnContext.Activity.Value) ?? new();
                await handler(turnContext, turnState, readReceiptInfo, cancellationToken);
            };
            AgentApplication.AddRoute(routeSelector, routeHandler, isInvokeRoute: false);
            return this;
        }

        /// <summary>
        /// Handles config fetch events for Microsoft Teams.
        /// </summary>
        /// <param name="handler">Function to call when the event is triggered.</param>
        /// <returns>The AgentApplication instance for chaining purposes.</returns>
        public TeamsAgentExtension OnConfigFetch(ConfigHandlerAsync handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            RouteSelectorAsync routeSelector = (turnContext, cancellationToken) => Task.FromResult(
                string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                && string.Equals(turnContext.Activity.Name, CONFIG_FETCH_INVOKE_NAME)
                && string.Equals(turnContext.Activity.ChannelId, Channels.Msteams));
            RouteHandler routeHandler = async (ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken) =>
            {
                ConfigResponseBase result = await handler(turnContext, turnState, turnContext.Activity.Value, cancellationToken);

                // Check to see if an invoke response has already been added
                if (!turnContext.StackState.Has(ChannelAdapter.InvokeResponseKey))
                {
                    Activity activity = ActivityUtilities.CreateInvokeResponseActivity(result);
                    await turnContext.SendActivityAsync(activity, cancellationToken);
                }
            };
            AgentApplication.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
            return this;
        }

        /// <summary>
        /// Handles config submit events for Microsoft Teams.
        /// </summary>
        /// <param name="handler">Function to call when the event is triggered.</param>
        /// <returns>The AgentApplication instance for chaining purposes.</returns>
        public TeamsAgentExtension OnConfigSubmit(ConfigHandlerAsync handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            RouteSelectorAsync routeSelector = (turnContext, cancellationToken) => Task.FromResult(
                string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                && string.Equals(turnContext.Activity.Name, CONFIG_SUBMIT_INVOKE_NAME)
                && string.Equals(turnContext.Activity.ChannelId, Channels.Msteams));
            RouteHandler routeHandler = async (ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken) =>
            {
                ConfigResponseBase result = await handler(turnContext, turnState, turnContext.Activity.Value, cancellationToken);

                // Check to see if an invoke response has already been added
                if (!turnContext.StackState.Has(ChannelAdapter.InvokeResponseKey))
                {
                    Activity activity = ActivityUtilities.CreateInvokeResponseActivity(result);
                    await turnContext.SendActivityAsync(activity, cancellationToken);
                }
            };
            AgentApplication.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
            return this;
        }

        /// <summary>
        /// Handles when a file consent card is accepted by the user.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The AgentApplication instance for chaining purposes.</returns>
        public TeamsAgentExtension OnFileConsentAccept(FileConsentHandler handler)
            => OnFileConsent(handler, "accept");

        /// <summary>
        /// Handles when a file consent card is declined by the user.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The AgentApplication instance for chaining purposes.</returns>
        public TeamsAgentExtension OnFileConsentDecline(FileConsentHandler handler)
            => OnFileConsent(handler, "decline");

        private TeamsAgentExtension OnFileConsent(FileConsentHandler handler, string fileConsentAction)
        {
            ArgumentNullException.ThrowIfNull(handler);
            RouteSelectorAsync routeSelector = (context, _) =>
            {
                FileConsentCardResponse? fileConsentCardResponse;
                return Task.FromResult
                (
                    string.Equals(context.Activity?.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(context.Activity?.Name, "fileConsent/invoke")
                    && (fileConsentCardResponse = ProtocolJsonSerializer.ToObject<FileConsentCardResponse>(context.Activity!.Value)) != null
                    && string.Equals(fileConsentCardResponse.Action, fileConsentAction)
                );
            };
            RouteHandler routeHandler = async (turnContext, turnState, cancellationToken) =>
            {
                FileConsentCardResponse fileConsentCardResponse = ProtocolJsonSerializer.ToObject<FileConsentCardResponse>(turnContext.Activity.Value) ?? new();
                await handler(turnContext, turnState, fileConsentCardResponse, cancellationToken);

                // Check to see if an invoke response has already been added
                if (!turnContext.StackState.Has(ChannelAdapter.InvokeResponseKey))
                {
                    Activity activity = ActivityUtilities.CreateInvokeResponseActivity();
                    await turnContext.SendActivityAsync(activity, cancellationToken);
                }
            };
            AgentApplication.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
            return this;
        }

        /// <summary>
        /// Handles O365 Connector Card Action activities.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The AgentApplication instance for chaining purposes.</returns>
        public AgentApplication OnO365ConnectorCardAction(O365ConnectorCardActionHandler handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            RouteSelectorAsync routeSelector = (context, _) => Task.FromResult
            (
                string.Equals(context.Activity?.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                && string.Equals(context.Activity?.Name, "actionableMessage/executeAction")
            );
            RouteHandler routeHandler = async (turnContext, turnState, cancellationToken) =>
            {
                O365ConnectorCardActionQuery query = ProtocolJsonSerializer.ToObject<O365ConnectorCardActionQuery>(turnContext.Activity.Value) ?? new();
                await handler(turnContext, turnState, query, cancellationToken);

                // Check to see if an invoke response has already been added
                if (!turnContext.StackState.Has(ChannelAdapter.InvokeResponseKey))
                {
                    Activity activity = ActivityUtilities.CreateInvokeResponseActivity();
                    await turnContext.SendActivityAsync(activity, cancellationToken);
                }
            };
            AgentApplication.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
            return AgentApplication;
        }

        /// <summary>
        /// Registers a handler for feedback loop events when a user clicks the thumbsup or thumbsdown button on a response sent from the AI module.
        /// <see cref="AIOptions{TState}.EnableFeedbackLoop"/> must be set to true.
        /// </summary>
        /// <param name="handler">Function to cal lwhen the route is triggered</param>
        /// <returns></returns>
        public TeamsAgentExtension OnFeedbackLoop(FeedbackLoopHandler handler)
        {
            ArgumentNullException.ThrowIfNull(handler);

            RouteSelectorAsync routeSelector = (context, _) =>
            {
                var jsonObject = ProtocolJsonSerializer.ToObject<JsonObject>(context.Activity.Value);
                string? actionName = jsonObject.ContainsKey("actionName") ? jsonObject["actionName"].ToString() : string.Empty;
                return Task.FromResult
                (
                    context.Activity.Type == ActivityTypes.Invoke
                    && context.Activity.Name == "message/submitAction"
                    && actionName == "feedback"
                );
            };

            RouteHandler routeHandler = async (turnContext, turnState, cancellationToken) =>
            {
                FeedbackLoopData feedbackLoopData = ProtocolJsonSerializer.ToObject<FeedbackLoopData>(turnContext.Activity.Value)!;
                feedbackLoopData.ReplyToId = turnContext.Activity.ReplyToId;

                await handler(turnContext, turnState, feedbackLoopData, cancellationToken);

                // Check to see if an invoke response has already been added
                if (!turnContext.StackState.Has(ChannelAdapter.InvokeResponseKey))
                {
                    Activity activity = ActivityUtilities.CreateInvokeResponseActivity();
                    await turnContext.SendActivityAsync(activity, cancellationToken);
                }
            };

            AgentApplication.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
            return this;
        }
    }
}
