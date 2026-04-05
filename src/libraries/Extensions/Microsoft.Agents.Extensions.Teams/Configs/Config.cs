// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System.Threading;

namespace Microsoft.Agents.Extensions.Teams.Configs;

public class Config
{
    private readonly AgentApplication _app;
    private readonly ChannelId _channelId;

    internal Config(AgentApplication app, ChannelId channelId)
    {
        _app = app;
        _channelId = channelId;
    }

    /// <summary>
    /// Handles config fetch events for Microsoft Teams.
    /// </summary>
    /// <param name="handler">Function to call when the event is triggered.</param>
    /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
    /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
    /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
    /// <returns>The AgentExtension instance for chaining purposes.</returns>
    public Config OnConfigFetch(ConfigHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
    {
        _app.AddRoute(InvokeRouteBuilder.Create()
            .WithName(Microsoft.Teams.Api.Activities.Invokes.Name.Configs.Fetch)
            .WithChannelId(_channelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
            .WithHandler(async (ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken) =>
            {
                Microsoft.Teams.Api.Config.ConfigResponse result = await handler(turnContext, turnState, turnContext.Activity.Value, cancellationToken).ConfigureAwait(false);
                await TeamsAgentExtension.SetResponse(turnContext, result).ConfigureAwait(false);
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
    public Config OnConfigSubmit(ConfigHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
    {
        _app.AddRoute(InvokeRouteBuilder.Create()
            .WithName(Microsoft.Teams.Api.Activities.Invokes.Name.Configs.Submit)
            .WithChannelId(_channelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
            .WithHandler(async (ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken) =>
            {
                Microsoft.Teams.Api.Config.ConfigResponse result = await handler(turnContext, turnState, turnContext.Activity.Value, cancellationToken).ConfigureAwait(false);
                await TeamsAgentExtension.SetResponse(turnContext, result).ConfigureAwait(false);
            })
            .WithOAuthHandlers(autoSignInHandlers)
            .Build());

        return this;
    }

}
