// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Microsoft.Teams.Apps;
using Microsoft.Teams.Apps.Schema;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.MSTeams.Channels;

/// <summary>
/// RouteBuilder for routing Channel ConversationUpdate activities in an AgentApplication.
/// </summary>
/// <remarks>Use <see cref="ChannelUpdateRouteBuilder"/> to create and configure routes that respond to Activity Type of
/// <see cref="Microsoft.Agents.Core.Models.ActivityTypes.ConversationUpdate"/> with
/// <see cref="Microsoft.Teams.Apps.Schema.TeamsChannelData.EventType"/> matching channel events.
/// This builder allows matching specific event types via <see cref="ForChannelCreated()"/>, <see cref="ForChannelDeleted()"/>, etc.,
/// and supports ordering, oauth, and agentic routing scenarios.
/// This builder defaults to the <c>Microsoft.Agents.Core.Models.Channels.Msteams</c> channelId unless otherwise specified.
/// </remarks>
public partial class ChannelUpdateRouteBuilder : RouteBuilderBase<ChannelUpdateRouteBuilder>
{
    private readonly IList<string> _channelEvents = [];

    /// <summary>
    /// Creates a new instance of the ChannelUpdateRouteBuilder class for constructing route definitions.
    /// </summary>
    /// <returns>A ChannelUpdateRouteBuilder instance that can be used to configure and build routes.</returns>
    public static ChannelUpdateRouteBuilder Create()
    {
        var builder = Activator.CreateInstance<ChannelUpdateRouteBuilder>();
        return builder;
    }

    /// <summary>
    /// Match on channel created events.
    /// </summary>
    /// <returns>The current instance of the <see cref="ChannelUpdateRouteBuilder"/>, enabling method chaining.</returns>
    public ChannelUpdateRouteBuilder ForChannelCreated()
    {
        _channelEvents.Add(ConversationEventType.ChannelCreated);
        return this;
    }

    /// <summary>
    /// Match on channel deleted events.
    /// </summary>
    /// <returns>The current instance of the ChannelUpdateRouteBuilder, enabling method chaining.</returns>
    public ChannelUpdateRouteBuilder ForChannelDeleted()
    {
        _channelEvents.Add(ConversationEventType.ChannelDeleted);
        return this;
    }

    /// <summary>
    /// Match on channel renamed events.
    /// </summary>
    /// <returns>The current instance of the <see cref="ChannelUpdateRouteBuilder"/>, enabling method chaining.</returns>
    public ChannelUpdateRouteBuilder ForChannelRenamed()
    {
        _channelEvents.Add(ConversationEventType.ChannelRenamed);
        return this;
    }

    /// <summary>
    /// Match on channel restored events.
    /// </summary>
    /// <returns>The current instance of the <see cref="ChannelUpdateRouteBuilder"/>, enabling method chaining.</returns>
    public ChannelUpdateRouteBuilder ForChannelRestored()
    {
        _channelEvents.Add(ConversationEventType.ChannelRestored);
        return this;
    }

    /// <summary>
    /// Match on channel shared events.
    /// </summary>
    /// <returns>The current instance of the <see cref="ChannelUpdateRouteBuilder"/>, enabling method chaining.</returns>
    public ChannelUpdateRouteBuilder ForChannelShared()
    {
        _channelEvents.Add(ConversationEventType.ChannelShared);
        return this;
    }

    /// <summary>
    /// Match on channel unshared events.
    /// </summary>
    /// <returns>The current instance of the <see cref="ChannelUpdateRouteBuilder"/>, enabling method chaining.</returns>
    public ChannelUpdateRouteBuilder ForChannelUnshared()
    {
        _channelEvents.Add(ConversationEventType.ChannelUnShared);
        return this;
    }

    /// <summary>
    /// Match on channel member added events.
    /// </summary>
    /// <returns>The current instance of the <see cref="ChannelUpdateRouteBuilder"/>, enabling method chaining.</returns>
    public ChannelUpdateRouteBuilder ForChannelMemberAdded()
    {
        _channelEvents.Add(ConversationEventType.ChannelMemberAdded);
        return this;
    }

    /// <summary>
    /// Match on channel member removed events.
    /// </summary>
    /// <returns>The current instance of the <see cref="ChannelUpdateRouteBuilder"/>, enabling method chaining.</returns>
    public ChannelUpdateRouteBuilder ForChannelMemberRemoved()
    {
        _channelEvents.Add(ConversationEventType.ChannelMemberRemoved);
        return this;
    }

    /// <summary>
    /// Configures the route to use the specified handler for channel update events.
    /// </summary>
    /// <param name="handler">The handler to process channel update events.</param>
    /// <returns>The current instance of the ChannelUpdateRouteBuilder, enabling method chaining.</returns>
    public ChannelUpdateRouteBuilder WithHandler(ChannelUpdateHandler handler)
    {
        _route.Handler = (ctx, ts, ct) => handler(new TeamsTurnContext(ctx), ts, ctx.Activity.GetChannelData<TeamsChannelData>().Channel, ct);
        return this;
    }

    /// <inheritdoc />
    protected override void PreBuild()
    {
        _route.ChannelId ??= Microsoft.Agents.Core.Models.Channels.Msteams;
        _route.Selector ??= (context, _) =>
        {
            var teamChannelData = context.Activity.GetChannelData<TeamsChannelData>();
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
