// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Configs;
using Microsoft.Agents.Extensions.Teams.Consent;
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
    /// Creates a new TeamsAgentExtension instance.
    /// To leverage this extension, call <see cref="AgentApplication.RegisterExtension(IAgentExtension)"/> with an instance of this class.
    /// Use the callback method to register routes for handling Teams-specific events.
    /// <code>
    /// public class MyAgentApplication : AgentApplication
    /// {
    ///    public MyAgentApplication(AgentApplicationOptions options) : base(options)
    ///    {
    ///       RegisterExtension(new TeamsAgentExtension(this), teams =>
    ///       {
    ///          teams.Channels
    ///             .OnCreated(async (turnContext, turnState, channelInfo, cancellationToken) =>
    ///                {
    ///                   // Handle channel created event
    ///                })
    ///             .OnDeleted(async (turnContext, turnState, channelInfo, cancellationToken) =>
    ///                {
    ///                   // Handle channel deleted event
    ///                });
    ///                
    ///          teams.Meetings
    ///             .OnStart(async (turnContext, turnState, meetingInfo, cancellationToken) =>
    ///                {
    ///                   // Handle meeting started event
    ///                })
    ///             .OnEnd(async (turnContext, turnState, meetingInfo, cancellationToken) =>
    ///                {
    ///                   // Handle meeting ended event
    ///                });
    ///       });
    ///    }
    /// }
    /// </code>
    /// </summary>
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
        Config = new Config(agentApplication, ChannelId);

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
    /// Teams Config features.
    /// </summary>
    public Config Config { get; }

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
