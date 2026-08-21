// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.MSTeams.Messages;

/// <summary>
/// Provides a builder for configuring routes that handle Teams read receipt events.
/// </summary>
/// <remarks>
/// Use <see cref="ReadReceiptRouteBuilder"/> to create and configure routes that respond to Activity Type of <see cref="Microsoft.Agents.Core.Models.ActivityTypes.Event"/> with a name of
/// <see cref="Microsoft.Teams.Api.Activities.Events.Name.ReadReceipt"/> event activities sent when a user reads a message the agent sent in personal scope.
/// </remarks>
public class ReadReceiptRouteBuilder : RouteBuilderBase<ReadReceiptRouteBuilder>
{
    /// <summary>
    /// Creates a new instance of the ReadReceiptRouteBuilder class for constructing route definitions.
    /// </summary>
    /// <returns>A ReadReceiptRouteBuilder instance that can be used to configure and build routes.</returns>
    public static ReadReceiptRouteBuilder Create()
    {
        var builder = Activator.CreateInstance<ReadReceiptRouteBuilder>();
        return builder;
    }

    /// <summary>
    /// Read receipt routes are event activities, not invoke routes.
    /// </summary>
    /// <param name="isInvoke">Ignored.</param>
    /// <returns>The current builder instance.</returns>
    public override ReadReceiptRouteBuilder AsInvoke(bool isInvoke = true) => this;

    /// <summary>
    /// Configures the route to use the specified handler for processing read receipt events.
    /// The handler receives the deserialized <see cref="JsonElement"/> payload from the activity value.
    /// </summary>
    /// <param name="handler">An asynchronous delegate that processes the read receipt event.</param>
    /// <returns>The current <see cref="ReadReceiptRouteBuilder"/> instance for method chaining.</returns>
    public ReadReceiptRouteBuilder WithHandler(ReadReceiptHandler handler)
    {
        _route.Handler = async (ctx, ts, ct) =>
        {
            var data = (JsonElement)ctx.Activity.Value;
            await handler(new TeamsTurnContext(ctx), ts, data, ct).ConfigureAwait(false);
        };
        return this;
    }

    /// <inheritdoc />
    protected override void PreBuild()
    {
        _route.ChannelId ??= Microsoft.Agents.Core.Models.Channels.Msteams;
        _route.Selector = (context, ct) => Task.FromResult(
            IsContextMatch(context, _route)
            && context.Activity.IsType(ActivityTypes.Event)
            && context.Activity.Name != null
            && string.Equals(
                Microsoft.Teams.Api.Activities.Events.Name.ReadReceipt,
                context.Activity.Name, StringComparison.OrdinalIgnoreCase)
        );
    }
}
