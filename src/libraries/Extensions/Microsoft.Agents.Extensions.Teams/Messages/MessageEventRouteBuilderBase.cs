// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using System;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.Messages;

/// <summary>
/// Base class for route builders that match Teams message activities by activity type and channel data event type.
/// </summary>
/// <typeparam name="TBuilder">The concrete builder type, used to enable fluent method chaining.</typeparam>
public abstract class MessageEventRouteBuilderBase<TBuilder> : RouteBuilderBase<TBuilder>
    where TBuilder : MessageEventRouteBuilderBase<TBuilder>
{
    /// <summary>
    /// The activity type (e.g. <c>messageUpdate</c> or <c>messageDelete</c>) that this builder matches.
    /// Subclass constructors must set this property.
    /// </summary>
    protected string ActivityTypeName { get; set; }

    /// <summary>
    /// The Teams channel-data <c>eventType</c> value (e.g. <c>"editMessage"</c>) that this builder matches.
    /// Subclass constructors must set this property.
    /// </summary>
    protected string EventTypeName { get; set; }

    /// <summary>
    /// Message event routes cannot be configured as invoke routes.
    /// </summary>
    /// <param name="isInvoke">Ignored.</param>
    /// <returns>The current builder instance.</returns>
    public override TBuilder AsInvoke(bool isInvoke = true) => (TBuilder)this;

    /// <inheritdoc />
    protected override void PreBuild()
    {
        _route.ChannelId ??= Channels.Msteams;
        _route.Selector = (context, ct) => Task.FromResult(
            IsContextMatch(context, _route)
            && context.Activity.IsType(ActivityTypeName)
            && string.Equals(
                context.Activity.GetChannelData<Microsoft.Teams.Api.ChannelData>()?.EventType,
                EventTypeName,
                StringComparison.OrdinalIgnoreCase)
        );
    }
}
