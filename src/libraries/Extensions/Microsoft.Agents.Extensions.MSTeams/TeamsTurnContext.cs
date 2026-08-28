// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Graph;
using System;
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
    public Microsoft.Teams.Apps.Clients.ApiClient Client => _turnContext.Services.Get<Microsoft.Teams.Apps.Clients.ApiClient>();

    /// <inheritdoc/>
    public override Task<ResourceResponse> SendActivityAsync(
        string text,
        string speak = null,
        string inputHint = InputHints.AcceptingInput,
        CancellationToken cancellationToken = default)
    {
        AssertionHelpers.ThrowIfNullOrWhiteSpace(text, nameof(text));

        return SendActivityAsync(new Activity
        {
            Type = ActivityTypes.Message,
            Text = text,
            Speak = speak,
            InputHint = inputHint
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public override Task<ResourceResponse> SendActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
    {
        ApplyPromptPreview(activity);

        return base.SendActivityAsync(activity, cancellationToken);
    }

    /// <inheritdoc/>
    public override Task<ResourceResponse[]> SendActivitiesAsync(IActivity[] activities, CancellationToken cancellationToken = default)
    {
        AssertionHelpers.ThrowIfNull(activities, nameof(activities));

        foreach (var activity in activities)
        {
            ApplyPromptPreview(activity);
        }

        return base.SendActivitiesAsync(activities, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ResourceResponse> SendTargetedActivityAsync(
        IActivity activity,
        ChannelAccount recipient,
        CancellationToken cancellationToken = default)
    {
        AssertionHelpers.ThrowIfNull(activity, nameof(activity));

        return SendActivityAsync(activity.Clone().WithTargetedRecipient(recipient), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ResourceResponse> SendTargetedActivityAsync(
        IActivity activity,
        string recipientId,
        CancellationToken cancellationToken = default)
    {
        return SendTargetedActivityAsync(
            activity,
            new ChannelAccount(recipientId, role: RoleTypes.User),
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ResourceResponse> SendTargetedActivityAsync(
        string text,
        ChannelAccount recipient,
        CancellationToken cancellationToken = default)
    {
        AssertionHelpers.ThrowIfNullOrWhiteSpace(text, nameof(text));

        return SendTargetedActivityAsync(
            new Activity
            {
                Type = ActivityTypes.Message,
                Text = text,
                InputHint = InputHints.AcceptingInput
            },
            recipient,
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ResourceResponse> SendTargetedActivityAsync(
        string text,
        string recipientId,
        CancellationToken cancellationToken = default)
    {
        return SendTargetedActivityAsync(
            text,
            new ChannelAccount(recipientId, role: RoleTypes.User),
            cancellationToken);
    }

    private void ApplyPromptPreview(IActivity activity)
    {
        if (activity?.Type == ActivityTypes.Message
            && Activity.IsRecipientTargeted()
            && !string.IsNullOrWhiteSpace(Activity.Id))
        {
            PromptPreviewActivityNormalizer.Apply(activity, Activity.Id);
        }
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
}
