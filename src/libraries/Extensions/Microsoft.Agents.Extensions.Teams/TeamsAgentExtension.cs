// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Configurations;
using Microsoft.Agents.Extensions.Teams.FileConsents;
using Microsoft.Agents.Extensions.Teams.Meetings;
using Microsoft.Agents.Extensions.Teams.MessageExtensions;
using Microsoft.Agents.Extensions.Teams.Messages;
using Microsoft.Agents.Extensions.Teams.TaskModules;
using Microsoft.Agents.Extensions.Teams.TeamsChannels;
using Microsoft.Agents.Extensions.Teams.TeamsTeams;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams;

/// <summary>
/// AgentExtension for Microsoft Teams.
/// </summary>
public class TeamsAgentExtension : AgentExtension
{
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
        Configuration = new Configuration(agentApplication, ChannelId);

        agentApplication.OnBeforeTurn((turnContext, turnState, cancellationToken) =>
        {
            if (turnContext.Activity.ChannelId == ChannelId)
            {
                // Set the TeamsApiClient in the turn context for use in handlers.
                turnContext.SetTeamsApiClient(agentApplication, cancellationToken);

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
    /// Teams Configuration features.
    /// </summary>
    public Configuration Configuration { get; }

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
