// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Extensions.Teams.MessageExtensions;

/// <summary>
/// Provides a builder for configuring <c>composeExtension/fetchTask</c> Invokes in an AgentApplication.
/// </summary>
public class FetchActionRouteBuilder : CommandRouteBuilderBase<FetchActionRouteBuilder>
{
    public FetchActionRouteBuilder() : base()
    {
        InvokeName = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.FetchTask;
    }

    /// <summary>
    /// Configures the route to use the specified asynchronous handler for processing fetch tasks.
    /// </summary>
    /// <remarks>Use this method to specify custom logic for handling fetch tasks in Teams message
    /// extensions. The handler receives the deserialized data from the incoming activity, allowing for type-safe
    /// processing of the action's payload.</remarks>
    /// <param name="handler">An asynchronous delegate that processes the fetch action</param>
    /// <returns>The current instance of FetchActionRouteBuilder, enabling method chaining.</returns>
    public FetchActionRouteBuilder WithHandler(FetchActionHandler handler)
    {
        _route.Handler = async (ctx, ts, ct) =>
        {
            var action = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.MessageExtensions.Action>(ctx.Activity.Value);
            var response = await handler(ctx, ts, action, ct).ConfigureAwait(false);
            await TeamsAgentExtension.SetResponse(ctx, response).ConfigureAwait(false);
        };
        return this;
    }
}
