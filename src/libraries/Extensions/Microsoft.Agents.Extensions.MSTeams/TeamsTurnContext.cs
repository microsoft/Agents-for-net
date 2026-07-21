// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
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
    public Microsoft.Teams.Api.Clients.ApiClient Client => _turnContext.Services.Get<Microsoft.Teams.Api.Clients.ApiClient>();

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
