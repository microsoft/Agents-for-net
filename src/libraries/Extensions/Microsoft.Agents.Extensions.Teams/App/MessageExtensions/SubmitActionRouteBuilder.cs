// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Extensions.Teams.App.MessageExtensions;

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
    /// Configures the route to use the specified asynchronous handler for processing submit actions.
    /// </summary>
    /// <remarks>Use this method to specify custom logic for handling submit actions in Teams message
    /// extensions. The handler receives the deserialized data from the incoming activity, allowing for type-safe
    /// processing of the action's payload.</remarks>
    /// <typeparam name="TData">The type of data extracted from the submit action payload and passed to the handler.</typeparam>
    /// <param name="handler">An asynchronous delegate that processes the submit action, receiving the context, timestamp, deserialized data
    /// of type TData, and a cancellation token.</param>
    /// <returns>The current instance of SubmitActionRouteBuilder, enabling method chaining.</returns>
    public SubmitActionRouteBuilder WithHandler<TData>(SubmitActionHandler<TData> handler)
    {
        _route.Handler = async (ctx, ts, ct) =>
        {
            var value = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.MessageExtensions.Action>(ctx.Activity.Value);
            var result = await handler(ctx, ts, ProtocolJsonSerializer.ToObject<TData>(value.Data), ct).ConfigureAwait(false);
            await TeamsAgentExtension.SetResponse(ctx, result).ConfigureAwait(false);
        };
        return this;
    }
}
