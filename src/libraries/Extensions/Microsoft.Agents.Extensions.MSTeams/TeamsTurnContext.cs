// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.MSTeams;

public class TeamsTurnContext : TurnContextWrapper, ITeamsTurnContext
{
    public TeamsTurnContext(ITurnContext turnContext) : base(turnContext)
    {
    }

    /// <inheritdoc/>
    public new ITeamsActivity Activity =>
        _turnContext.Activity as ITeamsActivity ?? ProtocolJsonSerializer.ToObject<TeamsActivity>(_turnContext.Activity);

    /// <inheritdoc/>
    public Microsoft.Teams.Api.Clients.ApiClient Client => _turnContext.Services.Get<Microsoft.Teams.Api.Clients.ApiClient>();

    /// <inheritdoc/>
    public Task AddReactionAsync(string reactionType, string? activityId = null)
    {
        return Client.Conversations.Reactions.AddAsync(
            Activity.Conversation.Id,
            activityId ?? Activity.Id,
            new Microsoft.Teams.Api.Messages.ReactionType(reactionType));
    }

    /// <inheritdoc/>
    public Task DeleteReactionAsync(string reactionType, string? activityId = null)
    {
        return Client.Conversations.Reactions.DeleteAsync(
            Activity.Conversation.Id,
            activityId ?? Activity.Id,
            new Microsoft.Teams.Api.Messages.ReactionType(reactionType));
    }

    /// <inheritdoc/>
    public Task<ResourceResponse> ReplyAsync(string message, CancellationToken cancellationToken = default)
    {
        return ReplyAsync(new Activity { Type = ActivityTypes.Message, Text = message }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ResourceResponse> ReplyAsync(Activity activity, CancellationToken cancellationToken = default)
    {
        if (Activity.Id != null)
        {
            activity.PrependQuote(Activity.Id);
        }

        return SendActivityAsync(activity, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ResourceResponse> SendAsync(string conversationId, string text, CancellationToken cancellationToken = default)
    {
        var conversation = CreateProactiveConversation(conversationId);
        return Proactive.SendActivityAsync(
            Adapter,
            conversation,
            MessageFactory.Text(text),
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ResourceResponse> SendAsync(string conversationId, string activityId, string text, CancellationToken cancellationToken = default)
    {
        string threadedConversationId = ToThreadedConversationId(conversationId, activityId);
        var threadedConversation = CreateProactiveConversation(threadedConversationId);

        return await Proactive.SendActivityAsync(
            Adapter,
            threadedConversation,
            MessageFactory.Text(text),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<ResourceResponse> SendTargetedActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
    {
        return SendActivityAsync(activity.Clone().MakeTargetedActivity(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ResourceResponse[]> SendTargetedActivitiesAsync(IActivity[] activities, CancellationToken cancellationToken = default)
    {
        var clonedActivities = new List<IActivity>(activities.Length);
        foreach (var activity in activities)
        {
            clonedActivities.Add(activity.Clone().MakeTargetedActivity());
        }

        return SendActivitiesAsync([.. clonedActivities], cancellationToken);
    }

    /// <inheritdoc/>
    public GraphServiceClient GetGraphClient(string handlerName = null, string graphBaseUrl = "https://graph.microsoft.com/v1.0")
    {
        return GraphClientFactory.CreateUserGraphClient(GetUserAuthorization(), this, handlerName, graphBaseUrl);
    }

    /// <inheritdoc/>
    public GraphServiceClient GetAppGraphClient(string graphBaseUrl = "https://graph.microsoft.com/v1.0")
    {
        var tokenProvider = GetConnections().GetTokenProvider(Identity, Activity);
        return GraphClientFactory.CreateAppGraphClient(tokenProvider, graphBaseUrl);
    }

    /// <inheritdoc/>
    public GraphServiceClient GetAppGraphClientForConnection(string connectionName, string graphBaseUrl = "https://graph.microsoft.com/v1.0")
    {
        AssertionHelpers.ThrowIfNullOrEmpty(connectionName, nameof(connectionName));
        var tokenProvider = GetConnections().GetConnection(connectionName);
        return GraphClientFactory.CreateAppGraphClient(tokenProvider, graphBaseUrl);
    }

    private UserAuthorization GetUserAuthorization()
    {
        var userAuthorization = _turnContext.Services.Get<UserAuthorization>();
        if (userAuthorization == null)
        {
            throw new InvalidOperationException(
                "UserAuthorization is not configured on the AgentApplication. A delegated (user) Graph client requires configured user authorization.");
        }

        return userAuthorization;
    }

    private Conversation CreateProactiveConversation(string conversationId)
    {
        AssertionHelpers.ThrowIfNullOrWhiteSpace(conversationId, nameof(conversationId));

        var reference = new ConversationReference(
            user: Activity.From,
            agent: Activity.Recipient,
            channelId: Microsoft.Agents.Core.Models.Channels.Msteams,
            serviceUrl: Activity.ServiceUrl,
            conversation: new ConversationAccount(id: conversationId));
        return new Conversation(Identity, reference);
    }

    private IConnections GetConnections()
    {
        var connections = _turnContext.Services.Get<IConnections>();
        if (connections == null)
        {
            throw new InvalidOperationException(
                "IConnections is not configured on the AgentApplication. An app-only Graph client requires a configured token connection.");
        }

        return connections;
    }

    private static string ToThreadedConversationId(string conversationId, string activityId)
    {
        AssertionHelpers.ThrowIfNullOrWhiteSpace(conversationId, nameof(conversationId));
        if (string.IsNullOrEmpty(activityId) || !ulong.TryParse(activityId, out ulong parsedActivityId) || parsedActivityId == 0)
        {
            throw new ArgumentException(
                $"Invalid activityId \"{activityId}\": must be a non-zero numeric value.",
                nameof(activityId));
        }

        string baseConversationId = conversationId.Split(';')[0];
        return $"{baseConversationId};messageid={activityId}";
    }

}
