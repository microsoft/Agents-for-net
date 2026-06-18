// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Config;
using Microsoft.Agents.Extensions.Teams.FileConsents;
using Microsoft.Agents.Extensions.Teams.Meetings;
using Microsoft.Agents.Extensions.Teams.MessageExtensions;
using Microsoft.Agents.Extensions.Teams.Messages;
using Microsoft.Agents.Extensions.Teams.TaskModules;
using Microsoft.Agents.Extensions.Teams.TeamsChannels;
using Microsoft.Agents.Extensions.Teams.TeamsTeams;
using Microsoft.Graph;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams;

/// <summary>
/// AgentExtension for Microsoft Teams.
/// </summary>
public class TeamsAgentExtension : AgentExtension
{
    private readonly AgentApplication _agentApplication;

    /// <summary>
    /// Creates a new <see cref="TeamsAgentExtension"/> instance.
    /// </summary>
    /// <remarks>
    /// The preferred way to enable the Teams extension is via the <see cref="TeamsExtensionAttribute"/> on a
    /// <c>partial</c> <see cref="AgentApplication"/> subclass, which causes a source generator to expose a
    /// <c>Teams</c> property of this type automatically.
    /// Use this constructor directly only when manually calling
    /// <see cref="AgentApplication.RegisterExtension(IAgentExtension)"/>.
    /// </remarks>
    /// <param name="agentApplication">The AgentApplication for this extension.</param>
    public TeamsAgentExtension(AgentApplication agentApplication)
    {
        ChannelId = Core.Models.Channels.Msteams;

        Meetings = new Meeting(agentApplication, ChannelId);
        MessageExtensions = new MessageExtension(agentApplication, ChannelId);
        TaskModules = new TaskModule(agentApplication, ChannelId);
        Channels = new TeamsChannel(agentApplication, ChannelId);
        Teams = new TeamsTeam(agentApplication, ChannelId);
        FileConsent = new FileConsent(agentApplication, ChannelId);
        Messages = new Message(agentApplication, ChannelId);
        Config = new TeamsConfig(agentApplication, ChannelId);

        _agentApplication = agentApplication;
        _agentApplication.OnBeforeTurn((turnContext, turnState, cancellationToken) =>
        {
            if (turnContext.Activity.ChannelId == ChannelId)
            {
                // Set the TeamsApiClient in the turn context for use in handlers.
                turnContext.SetTeamsApiClient(_agentApplication, cancellationToken);

                // Explicit conversion of Activity.ChannelData to Teams' ChannelData for improved performance
                turnContext.Activity.ChannelData = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.ChannelData>(turnContext.Activity.ChannelData);
            }
            return Task.FromResult(true);
        });
    }

    /// <summary>
    /// Teams Meetings features.
    /// </summary>
    public Meeting Meetings { get; }

    /// <summary>
    /// Teams Message Extensions features.
    /// </summary>
    public MessageExtension MessageExtensions { get; }

    /// <summary>
    /// Teams Task Modules features.
    /// </summary>
    public TaskModule TaskModules { get; }

    /// <summary>
    /// Teams Channel features.
    /// </summary>
    public TeamsChannel Channels { get; }

    /// <summary>
    /// Teams Team features.
    /// </summary>
    public TeamsTeam Teams { get; }

    /// <summary>
    /// Teams File Consent features.
    /// </summary>
    public FileConsent FileConsent { get; }

    /// <summary>
    /// Message features.
    /// </summary>
    public Message Messages { get; }

    /// <summary>
    /// Teams config features.
    /// </summary>
    public TeamsConfig Config { get; }

    /// <summary>
    /// Gets the Teams API client for the specified turn context.
    /// </summary>
    /// <param name="turnContext">The turn context.</param>
    /// <returns>The Teams API client.</returns>
    public Microsoft.Teams.Api.Clients.ApiClient GetTeamsClient(ITurnContext turnContext)
    {
        return turnContext.GetTeamsApiClient();
    }

    /// <summary>
    /// Creates a new instance of the GraphServiceClient for accessing Microsoft Graph APIs using the current turn
    /// context.
    /// </summary>
    /// <remarks>Use this method to obtain a GraphServiceClient that is pre-authenticated for the user or bot
    /// associated with the current turn. The returned client can be used to make requests to Microsoft Graph on behalf
    /// of the user.  This requires that UserAuthorization is properly configured and the user is signed in.</remarks>
    /// <param name="turnContext">The turn context containing information about the current conversation and user. Cannot be null.</param>
    /// <param name="handlerName">The name of the handler to use for token acquisition. If null, the default handler is used.</param>
    /// <param name="graphBaseUrl">The base URL for the Microsoft Graph API. Defaults to "https://graph.microsoft.com/v1.0".</param>
    /// <returns>A GraphServiceClient instance configured with authentication for the current turn context.</returns>
    public GraphServiceClient GetGraph(ITurnContext turnContext, string handlerName = null, string graphBaseUrl = "https://graph.microsoft.com/v1.0")
    {
        return new GraphServiceClient(new TokenProvider(_agentApplication, turnContext, handlerName), graphBaseUrl);
    }

    internal static Task SetResponse(ITurnContext context, object result = null, int status = 200)
    {
        if (!context.StackState.Has(ChannelAdapter.InvokeResponseKey))
        {
            var activity = Activity.CreateInvokeResponseActivity(result, status);
            return context.SendActivityAsync(activity, CancellationToken.None);
        }

        return Task.CompletedTask;
    }
}

class TokenProvider(AgentApplication agentApplication, ITurnContext turnContext, string handlerName) : IAuthenticationProvider
{
    public async Task AuthenticateRequestAsync(RequestInformation request, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
    {
        var token = await agentApplication.UserAuthorization.GetTurnTokenAsync(turnContext, handlerName, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (token != null)
        {
            request.Headers["Authorization"] = [$"Bearer {token}"];
        }
    }
}
