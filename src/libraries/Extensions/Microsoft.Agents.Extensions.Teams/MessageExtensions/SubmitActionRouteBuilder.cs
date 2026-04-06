// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Extensions.Teams.MessageExtensions;

/// <summary>
/// Provides a builder for configuring submit action routes in an AgentApplication.
/// </summary>
/// <remarks>
/// Use <see cref="SubmitActionRouteBuilder"/> to create and configure routes that respond to Activity Type of
/// <see cref="Microsoft.Agents.Core.Models.ActivityTypes.Invoke"/> with a name of
/// <see cref="Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.SubmitAction"/>,
/// optionally filtered by command ID via <see cref="WithCommand(string)"/>.
/// <code>
/// var route = SubmitActionRouteBuilder.Create()
///     .WithCommand("actionCmd")
///     .WithHandler(async (context, state, action, ct) =>
///     {
///         // Handle submit action
///     })
///     .Build();
///
/// app.AddRoute(route);
/// </code>
/// </remarks>
public class SubmitActionRouteBuilder : CommandRouteBuilderBase<SubmitActionRouteBuilder>
{
    public SubmitActionRouteBuilder() : base()
    {
        InvokeName = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.SubmitAction;
    }

    /// <summary>
    /// Configures the route to use the specified handler for processing submit actions.
    /// </summary>
    /// <param name="handler">The delegate that processes the submit action.</param>
    /// <returns>The current instance of the SubmitActionRouteBuilder, enabling method chaining.</returns>
    public SubmitActionRouteBuilder WithHandler(SubmitActionHandler handler)
    {
        _route.Handler = async (ctx, ts, ct) =>
        {
            var action = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.MessageExtensions.Action>(ctx.Activity.Value);
            var result = await handler(ctx, ts, action, ct).ConfigureAwait(false);
            await TeamsAgentExtension.SetResponse(ctx, result).ConfigureAwait(false);
        };
        return this;
    }
}
