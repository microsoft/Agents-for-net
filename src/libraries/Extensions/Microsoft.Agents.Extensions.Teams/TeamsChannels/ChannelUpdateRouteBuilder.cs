// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Microsoft.Teams.Api;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Microsoft.Teams.Api.Activities.ConversationUpdateActivity;

namespace Microsoft.Agents.Extensions.Teams.TeamsChannels;

/// <summary>
/// RouteBuilder for routing Channel ConversationUpdate activities in an AgentApplication.
/// </summary>
/// <remarks>Use <see cref="ChannelUpdateRouteBuilder"/> to create and configure routes that respond to Activity Type of
/// <see cref="Microsoft.Agents.Core.Models.ActivityTypes.ConversationUpdate"/> with
/// <see cref="Microsoft.Teams.Api.ChannelData.EventType"/> matching channel events.
/// This builder allows matching specific event types via <see cref="ForChannelCreated()"/>, <see cref="ForChannelDeleted()"/>, etc.,
/// and supports ordering, oauth, and agentic routing scenarios.
/// This builder defaults to the <c>Channels.MsTeams</c> channelId unless otherwise specified.
/// </remarks>
public partial class ChannelUpdateRouteBuilder : RouteBuilderBase<ChannelUpdateRouteBuilder>
{
    private readonly IList<string> _channelEvents = [];

    /// <summary>
    /// Match on channel created events.
    /// </summary>
    /// <returns>The current instance of the <see cref="ChannelUpdateRouteBuilder"/>, enabling method chaining.</returns>
    public ChannelUpdateRouteBuilder ForChannelCreated()
    {
        _channelEvents.Add(EventType.ChannelCreated);
        return this;
    }

    /// <summary>
    /// Match on channel deleted events.
    /// </summary>
    /// <returns>The current instance of the ChannelUpdateRouteBuilder, enabling method chaining.</returns>
    public ChannelUpdateRouteBuilder ForChannelDeleted()
    {
        _channelEvents.Add(EventType.ChannelDeleted);
        return this;
    }

    /// <summary>
    /// Match on channel renamed events.
    /// </summary>
    /// <returns>The current instance of the <see cref="ChannelUpdateRouteBuilder"/>, enabling method chaining.</returns>
    public ChannelUpdateRouteBuilder ForChannelRenamed()
    {
        _channelEvents.Add(EventType.ChannelRenamed);
        return this;
    }

    /// <summary>
    /// Match on channel restored events.
    /// </summary>
    /// <returns>The current instance of the <see cref="ChannelUpdateRouteBuilder"/>, enabling method chaining.</returns>
    public ChannelUpdateRouteBuilder ForChannelRestored()
    {
        _channelEvents.Add(EventType.ChannelRestored);
        return this;
    }

    /// <summary>
    /// Match on channel shared events.
    /// </summary>
    /// <returns>The current instance of the <see cref="ChannelUpdateRouteBuilder"/>, enabling method chaining.</returns>
    public ChannelUpdateRouteBuilder ForChannelShared()
    {
        _channelEvents.Add(EventType.ChannelShared);
        return this;
    }

    /// <summary>
    /// Match on channel unshared events.
    /// </summary>
    /// <returns>The current instance of the <see cref="ChannelUpdateRouteBuilder"/>, enabling method chaining.</returns>
    public ChannelUpdateRouteBuilder ForChannelUnshared()
    {
        _channelEvents.Add(EventType.ChannelUnShared);
        return this;
    }

    /// <summary>
    /// Match on channel member added events.
    /// </summary>
    /// <returns>The current instance of the <see cref="ChannelUpdateRouteBuilder"/>, enabling method chaining.</returns>
    public ChannelUpdateRouteBuilder ForChannelMemberAdded()
    {
        _channelEvents.Add(EventType.ChannelMemberAdded);
        return this;
    }

    /// <summary>
    /// Match on channel member removed events.
    /// </summary>
    /// <returns>The current instance of the <see cref="ChannelUpdateRouteBuilder"/>, enabling method chaining.</returns>
    public ChannelUpdateRouteBuilder ForChannelMemberRemoved()
    {
        _channelEvents.Add(EventType.ChannelMemberRemoved);
        return this;
    }

    /// <summary>
    /// Configures the route to use the specified handler for channel update events.
    /// </summary>
    /// <param name="handler">The handler to process channel update events.</param>
    /// <returns>The current instance of the ChannelUpdateRouteBuilder, enabling method chaining.</returns>
    public ChannelUpdateRouteBuilder WithHandler(ChannelUpdateHandler handler)
    {
        _route.Handler = (ctx, ts, ct) => handler(ctx, ts, ctx.Activity.GetChannelData<ChannelData>().Channel, ct);
        return this;
    }

    /// <inheritdoc />
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
                && (_channelEvents.Count > 0 ? _channelEvents.Contains(teamChannelData?.EventType) : AnyChannelEvent().IsMatch(teamChannelData?.EventType ?? string.Empty))
                && teamChannelData?.Channel != null
            );
        };
    }

    /// <summary>
    /// Returns the current event route builder instance. For event routes, the invoke flag is ignored to
    /// prevent misconfiguration.
    /// </summary>
    /// <remarks>Channel updates cannot be configured as invoke routes. This method always returns the
    /// current instance, regardless of the value of <paramref name="isInvoke"/>.</remarks>
    /// <param name="isInvoke">Ignored</param>
    /// <returns>The current instance of <see cref="ChannelUpdateRouteBuilder"/>.</returns>
    public override ChannelUpdateRouteBuilder AsInvoke(bool isInvoke = true)
    {
        return this;
    }

    [GeneratedRegex("channel.*")]
    private static partial Regex AnyChannelEvent();
}
