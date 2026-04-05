// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.Consent;

public class FileConsent
{
    private readonly AgentApplication _app;
    private readonly ChannelId _channelId;

    internal FileConsent(AgentApplication app, ChannelId channelId)
    {
        _app = app;
        _channelId = channelId;
    }

    /// <summary>
    /// Handles when a file consent card is accepted by the user.
    /// </summary>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
    /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
    /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
    /// <returns>The AgentExtension instance for chaining purposes.</returns>
    public FileConsent OnFileConsentAccept(FileConsentHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        => OnFileConsent(handler, "accept", rank, autoSignInHandlers, isAgenticOnly);

    /// <summary>
    /// Handles when a file consent card is declined by the user.
    /// </summary>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
    /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
    /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
    /// <returns>The AgentExtension instance for chaining purposes.</returns>
    public FileConsent OnFileConsentDecline(FileConsentHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        => OnFileConsent(handler, "decline", rank, autoSignInHandlers, isAgenticOnly);

    private FileConsent OnFileConsent(FileConsentHandler handler, string fileConsentAction, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
    {
        _app.AddRoute(InvokeRouteBuilder.Create()
            .WithName(Microsoft.Teams.Api.Activities.Invokes.Name.FileConsent)
            .WithChannelId(_channelId).WithOrderRank(rank).AsAgentic(isAgenticOnly)
            .WithSelector((turnContext, cancellationToken) =>
            {
                Microsoft.Teams.Api.FileConsentCardResponse fileConsentCardResponse = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.FileConsentCardResponse>(turnContext.Activity.Value);
                return Task.FromResult(fileConsentCardResponse != null && string.Equals(fileConsentCardResponse.Action, fileConsentAction));
            })
            .WithHandler(async (turnContext, turnState, cancellationToken) =>
            {
                Microsoft.Teams.Api.FileConsentCardResponse fileConsentCardResponse = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.FileConsentCardResponse>(turnContext.Activity.Value);
                if (string.Equals(fileConsentCardResponse.Action, fileConsentAction))
                {
                    await handler(turnContext, turnState, fileConsentCardResponse, cancellationToken).ConfigureAwait(false);
                    await TeamsAgentExtension.SetResponse(turnContext).ConfigureAwait(false);
                }
            })
            .WithOAuthHandlers(autoSignInHandlers)
            .Build());

        return this;
    }
}