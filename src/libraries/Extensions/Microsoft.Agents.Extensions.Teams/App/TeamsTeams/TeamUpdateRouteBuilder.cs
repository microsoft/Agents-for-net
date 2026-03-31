// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Microsoft.Teams.Api;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Microsoft.Teams.Api.Activities.ConversationUpdateActivity;

namespace Microsoft.Agents.Extensions.Teams.App.TeamsTeams;

/// <summary>
/// RouteBuilder for routing Teams ConversationUpdate activities in an AgentApplication.
/// </summary>
/// <remarks>Use <see cref="TeamUpdateRouteBuilder"/> to create and configure routes that respond to conversation
/// update events. This builder allows matching update events, ordering, oauth, and agentic routing scenarios.  This
/// builder defaults to the <c>Channels.MsTeams</c> channelId unless otherwise specified. Example usage:
/// <code>
/// var route = TeamUpdateRouteBuilder.Create()
///    .ForTeamArchived()
///    .ForTeamUnarchived()
///    .WithHandler(async (context, state, team, cancellationToken) => { /* handler logic */ })
///    .Build();
///    
/// app.AddRoute(route);
/// </code>
/// </remarks>
public partial class TeamUpdateRouteBuilder : RouteBuilderBase<TeamUpdateRouteBuilder>
{
    private readonly IList<string> _teamEvents = [];

    /// <summary>
    /// Match on team archived events.
    /// </summary>
    /// <returns>The current instance of the <see cref="TeamUpdateRouteBuilder"/>, enabling method chaining.</returns>
    public TeamUpdateRouteBuilder ForTeamArchived()
    {
        _teamEvents.Add(EventType.TeamArchived);
        return this;
    }

    /// <summary>
    /// Match on team unarchived events.
    /// </summary>
    /// <returns>The current instance of the TeamUpdateRouteBuilder, enabling method chaining.</returns>
    public TeamUpdateRouteBuilder ForTeamUnarchived()
    {
        _teamEvents.Add(EventType.TeamUnarchived);
        return this;
    }

    /// <summary>
    /// Match on team deleted events.
    /// </summary>
    /// <returns>The current instance of the <see cref="TeamUpdateRouteBuilder"/>, enabling method chaining.</returns>
    public TeamUpdateRouteBuilder ForTeamDeleted()
    {
        _teamEvents.Add(EventType.TeamDeleted);
        return this;
    }

    /// <summary>
    /// Match on team hard deleted events.
    /// </summary>
    /// <returns>The current instance of the <see cref="TeamUpdateRouteBuilder"/>, enabling method chaining.</returns>
    public TeamUpdateRouteBuilder ForTeamHardDeleted()
    {
        _teamEvents.Add(EventType.TeamHardDeleted);
        return this;
    }

    /// <summary>
    /// Match on team renamed events.
    /// </summary>
    /// <returns>The current instance of the <see cref="TeamUpdateRouteBuilder"/>, enabling method chaining.</returns>
    public TeamUpdateRouteBuilder ForTeamRenamed()
    {
        _teamEvents.Add(EventType.TeamRenamed);
        return this;
    }

    /// <summary>
    /// Match on team restored events.
    /// </summary>
    /// <returns>The current instance of the <see cref="TeamUpdateRouteBuilder"/>, enabling method chaining.</returns>
    public TeamUpdateRouteBuilder ForTeamRestored()
    {
        _teamEvents.Add(EventType.TeamRestored);
        return this;
    }

    /// <summary>
    /// Configures the route to use the specified handler for team update events.
    /// </summary>
    /// <param name="handler">The handler to process team update events.</param>
    /// <returns>The current instance of the TeamUpdateRouteBuilder, enabling method chaining.</returns>
    public TeamUpdateRouteBuilder WithHandler(TeamUpdateHandler handler)
    {
        _route.Handler = (ctx, ts, ct) => handler(ctx, ts, ctx.Activity.GetChannelData<ChannelData>().Team, ct);
        return this;
    }

    protected override void PreBuild()
    {
        _route.ChannelId ??= Channels.Msteams;
        _route.Selector ??= (context, _) =>
        {
            var teamChannelData = context.Activity.GetChannelData<ChannelData>();
            return Task.FromResult
            (
                IsContextMatch(context, _route)
                && context.Activity.IsType(ActivityTypes.ConversationUpdate)
                && (_teamEvents.Count > 0 ? _teamEvents.Contains(teamChannelData?.EventType) : AnyTeamEvent().IsMatch(teamChannelData?.EventType ?? string.Empty))
                && teamChannelData?.Team != null
            );
        };
    }

    /// <summary>
    /// Returns the current event route builder instance. For event routes, the invoke flag is ignored to
    /// prevent misconfiguration.
    /// </summary>
    /// <remarks>Team updates cannot be configured as invoke routes. This method always returns the
    /// current instance, regardless of the value of <paramref name="isInvoke"/>.</remarks>
    /// <param name="isInvoke">Ignored</param>
    /// <returns>The current instance of <see cref="TeamUpdateRouteBuilder"/>.</returns>
    public new TeamUpdateRouteBuilder AsInvoke(bool isInvoke = true)
    {
        return this;
    }

    [GeneratedRegex("team.*")]
    private static partial Regex AnyTeamEvent();
}
