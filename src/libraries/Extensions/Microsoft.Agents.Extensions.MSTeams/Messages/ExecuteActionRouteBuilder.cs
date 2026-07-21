// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.MSTeams.Messages;

/// <summary>
/// Provides a builder for configuring routes that handle Teams O365 Connector Card Action invoke activities.
/// </summary>
/// <remarks>
/// Use <see cref="ExecuteActionRouteBuilder"/> to create and configure routes that respond to Activity Type of <see cref="ActivityTypes.Invoke"/> with a name of
/// <see cref="Microsoft.Teams.Api.Activities.Invokes.Name.ExecuteAction"/> triggered by O365 Connector Card action buttons.
/// </remarks>
public class ExecuteActionRouteBuilder : RouteBuilderBase<ExecuteActionRouteBuilder>
{
    /// <summary>
    /// Creates a new instance of the ExecuteActionRouteBuilder class for constructing route definitions.
    /// </summary>
    /// <returns>An ExecuteActionRouteBuilder instance that can be used to configure and build routes.</returns>
    public static ExecuteActionRouteBuilder Create()
    {
        var builder = Activator.CreateInstance<ExecuteActionRouteBuilder>();
        return builder;
    }

    /// <summary>
    /// Configures the route to use the specified handler for processing O365 Connector Card Action invokes.
    /// The handler receives the deserialized <see cref="Microsoft.Teams.Api.O365.ConnectorCardActionQuery"/> from the activity value.
    /// </summary>
    /// <param name="handler">An asynchronous delegate that processes the O365 Connector Card Action invoke.</param>
    /// <returns>The current <see cref="ExecuteActionRouteBuilder"/> instance for method chaining.</returns>
    public ExecuteActionRouteBuilder WithHandler(ExecuteActionRouteHandler handler)
    {
        _route.Handler = async (ctx, ts, ct) =>
        {
            Microsoft.Teams.Api.O365.ConnectorCardActionQuery query =
                ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.O365.ConnectorCardActionQuery>(ctx.Activity.Value) ?? new();
            await handler(new TeamsTurnContext(ctx), ts, query, ct).ConfigureAwait(false);
            await TeamsAgentExtension.SetResponse(ctx).ConfigureAwait(false);
        };
        return this;
    }

    /// <inheritdoc />
    protected override void PreBuild()
    {
        _route.ChannelId ??= Microsoft.Agents.Core.Models.Channels.Msteams;
        _route.Selector = (context, ct) => Task.FromResult(
            IsContextMatch(context, _route)
            && context.Activity.IsType(ActivityTypes.Invoke)
            && context.Activity.Name != null
            && string.Equals(
                Microsoft.Teams.Api.Activities.Invokes.Name.ExecuteAction,
                context.Activity.Name, StringComparison.OrdinalIgnoreCase)
        );
    }
}
