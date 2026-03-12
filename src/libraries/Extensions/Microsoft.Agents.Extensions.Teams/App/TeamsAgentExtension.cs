// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.App.Builders;
using Microsoft.Agents.Extensions.Teams.App.Meetings;
using Microsoft.Agents.Extensions.Teams.App.MessageExtensions;
using Microsoft.Agents.Extensions.Teams.App.TaskModules;
using Microsoft.Teams.Api;
using Microsoft.Teams.Api.Config;
using Microsoft.Teams.Api.O365;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.App;

/// <summary>
/// AgentExtension for Microsoft Teams.
/// </summary>
public class TeamsAgentExtension : AgentExtension
{
    /// <summary>
    /// Creates a new TeamsAgentExtension instance.
    /// To leverage this extension, call <see cref="AgentApplication.RegisterExtension(IAgentExtension)"/> with an instance of this class.
    /// Use the callback method to register routes for handling Teams-specific events.
    /// </summary>
    /// <param name="agentApplication">The agent application to leverage for route registration.</param>
    /// <param name="options">Options for configuring TaskModules.</param>
    public TeamsAgentExtension(AgentApplication agentApplication, TaskModulesOptions? options = null)
    {
        ChannelId = Channels.Msteams;

        AgentApplication = agentApplication;

        Meetings = new Meeting(agentApplication);
        MessageExtensions = new MessageExtension(agentApplication);
        TaskModules = new TaskModule(agentApplication, options);

        agentApplication.OnBeforeTurn((turnContext, turnState, cancellationToken) =>
        {
            if (turnContext.Activity.ChannelId == Channels.Msteams)
            {
                // Set the TeamsApiClient in the turn context for use in handlers.
                turnContext.SetTeamsApiClient(agentApplication, cancellationToken);

                // Explicit conversation of Activity.ChannelData to Teams' ChannelData for improved performance
                turnContext.Activity.ChannelData = ProtocolJsonSerializer.ToObject<ChannelData>(turnContext.Activity.ChannelData);
            }
            return Task.FromResult(true);
        });
    }

    /// <summary>
    /// Fluent interface for accessing Meetings' specific features.
    /// </summary>
    public Meeting Meetings { get; }

    /// <summary>
    /// Fluent interface for accessing Message Extensions' specific features.
    /// </summary>
    public MessageExtension MessageExtensions { get; }

    /// <summary>
    /// Fluent interface for accessing Task Modules' specific features.
    /// </summary>
    public TaskModule TaskModules { get; }

#if !NETSTANDARD
    internal AgentApplication AgentApplication { get; init;}
#else
    internal AgentApplication AgentApplication { get; set;}
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
        AgentApplication.AddRoute(TeamsConversationUpdateRouteBuilder.Create()
            .WithUpdateEvent(conversationUpdateEvent)
            .WithChannelId(ChannelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
            .WithHandler(handler)
            .WithOAuthHandlers(autoSignInHandlers)
            .Build());
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
        AgentApplication.AddRoute(TypeRouteBuilder.Create()
            .WithType(ActivityTypes.MessageUpdate)
            .WithChannelId(ChannelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
            .WithSelector((turnContext, cancellationToken) =>
            {
                ChannelData ChannelData = turnContext.Activity.GetChannelData<ChannelData>();
                return Task.FromResult(string.Equals(ChannelData?.EventType, "editMessage", StringComparison.OrdinalIgnoreCase));
            })
            .WithHandler(handler)
            .WithOAuthHandlers(autoSignInHandlers)
            .Build());
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
        AgentApplication.AddRoute(TypeRouteBuilder.Create()
            .WithType(ActivityTypes.MessageUpdate)
            .WithChannelId(ChannelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
            .WithSelector((turnContext, cancellationToken) =>
            {
                ChannelData ChannelData = turnContext.Activity.GetChannelData<ChannelData>();
                return Task.FromResult(string.Equals(ChannelData?.EventType, "undeleteMessage", StringComparison.OrdinalIgnoreCase));
            })
            .WithHandler(handler)
            .WithOAuthHandlers(autoSignInHandlers)
            .Build());
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
        AgentApplication.AddRoute(TypeRouteBuilder.Create()
            .WithType(ActivityTypes.MessageDelete)
            .WithChannelId(ChannelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
            .WithSelector((turnContext, cancellationToken) =>
            {
                ChannelData channelData = turnContext.Activity.GetChannelData<ChannelData>();
                return Task.FromResult(string.Equals(channelData?.EventType, "softDeleteMessage", StringComparison.OrdinalIgnoreCase));
            })
            .WithHandler(handler)
            .WithOAuthHandlers(autoSignInHandlers)
            .Build());
        return this;
    }

    /// <summary>
    /// Handles read receipt events for messages sent by the agent in personal scope.
    /// </summary>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
    /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
    /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
    /// <returns>The AgentExtension instance for chaining purposes.</returns>
    public TeamsAgentExtension OnTeamsReadReceipt(ReadReceiptHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
    {
        AgentApplication.AddRoute(EventRouteBuilder.Create()
            .WithName(Microsoft.Teams.Api.Activities.Events.Name.ReadReceipt)
            .WithChannelId(ChannelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
            .WithHandler(async (ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken) =>
            {
                JsonElement readReceiptInfo = (JsonElement)turnContext.Activity.Value;
                await handler(turnContext, turnState, readReceiptInfo, cancellationToken);
            })
            .WithOAuthHandlers(autoSignInHandlers)
            .Build());
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
        AgentApplication.AddRoute(InvokeRouteBuilder.Create()
            .WithName(Microsoft.Teams.Api.Activities.Invokes.Name.Configs.Fetch)
            .WithChannelId(ChannelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
            .WithHandler(async (ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken) =>
            {
                ConfigResponse result = await handler(turnContext, turnState, turnContext.Activity.Value, cancellationToken);
                await SetResponse(turnContext, result);
            })
            .WithOAuthHandlers(autoSignInHandlers)
            .Build());

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
        AgentApplication.AddRoute(InvokeRouteBuilder.Create()
            .WithName(Microsoft.Teams.Api.Activities.Invokes.Name.Configs.Submit)
            .WithChannelId(ChannelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
            .WithHandler(async (ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken) =>
            {
                ConfigResponse result = await handler(turnContext, turnState, turnContext.Activity.Value, cancellationToken);
                await SetResponse(turnContext, result);
            })
            .WithOAuthHandlers(autoSignInHandlers)
            .Build());

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
        AgentApplication.AddRoute(InvokeRouteBuilder.Create()
            .WithName(Microsoft.Teams.Api.Activities.Invokes.Name.FileConsent)
            .WithChannelId(ChannelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
            .WithSelector((turnContext, cancellationToken) =>
            {
                FileConsentCardResponse fileConsentCardResponse = ProtocolJsonSerializer.ToObject<FileConsentCardResponse>(turnContext.Activity.Value);
                return Task.FromResult(fileConsentCardResponse != null && string.Equals(fileConsentCardResponse.Action, fileConsentAction));
            })
            .WithHandler(async (turnContext, turnState, cancellationToken) =>
            {
                FileConsentCardResponse fileConsentCardResponse = ProtocolJsonSerializer.ToObject<FileConsentCardResponse>(turnContext.Activity.Value);
                if (string.Equals(fileConsentCardResponse.Action, fileConsentAction))
                {
                    await handler(turnContext, turnState, fileConsentCardResponse, cancellationToken);
                    await SetResponse(turnContext);
                }
            })
            .WithOAuthHandlers(autoSignInHandlers)
            .Build());

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
    public TeamsAgentExtension OnO365ConnectorCardAction(O365ConnectorCardActionHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
    {
        AgentApplication.AddRoute(InvokeRouteBuilder.Create()
            .WithName(Microsoft.Teams.Api.Activities.Invokes.Name.ExecuteAction)
            .WithChannelId(ChannelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
            .WithHandler(async (turnContext, turnState, cancellationToken) =>
            {
                ConnectorCardActionQuery query = ProtocolJsonSerializer.ToObject<ConnectorCardActionQuery>(turnContext.Activity.Value) ?? new();
                await handler(turnContext, turnState, query, cancellationToken);
                await SetResponse(turnContext);
            })
            .WithOAuthHandlers(autoSignInHandlers)
            .Build());

        return this;
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
