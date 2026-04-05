// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Extensions.Teams.MessageExtensions;

/// <summary>
/// Provides a builder for configuring submit action routes in an AgentApplication.
/// </summary>
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
