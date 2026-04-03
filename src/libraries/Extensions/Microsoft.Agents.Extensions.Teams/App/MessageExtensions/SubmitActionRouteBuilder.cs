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
    /// extensions. The handler receives the deserialized Action.Data from the incoming activity, allowing for type-safe
    /// processing of the action's data payload.</remarks>
    /// <typeparam name="TData">The type of the <c>data</c> argument extracted from the submit action payload and passed to the handler.</typeparam>
    /// <param name="handler">An asynchronous delegate that processes the submit action, receiving the context, timestamp, deserialized data
    /// of type TData, and a cancellation token.</param>
    /// <returns>The current instance of SubmitActionRouteBuilder, enabling method chaining.</returns>
    public SubmitActionRouteBuilder WithHandler<TData>(SubmitActionHandler<TData> handler)
    {
        _route.Handler = async (ctx, ts, ct) =>
        {
            var action = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.MessageExtensions.Action>(ctx.Activity.Value);
            var result = await handler(ctx, ts, ProtocolJsonSerializer.ToObject<TData>(action.Data), ct).ConfigureAwait(false);
            await TeamsAgentExtension.SetResponse(ctx, result).ConfigureAwait(false);
        };
        return this;
    }

    /// <summary>
    /// Configures the route to use the specified handler for processing submit actions.
    /// </summary>
    /// <remarks>Unlike <see cref="WithHandler{TData}(SubmitActionHandler{TData})"/>, this method does not perform deserialization of the action's data payload.</remarks>
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
