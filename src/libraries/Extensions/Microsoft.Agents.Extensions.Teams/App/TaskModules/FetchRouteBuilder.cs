// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Extensions.Teams.App.TaskModules;

/// <summary>
/// Provides a builder for configuring fetch routes in an AgentApplication.
/// </summary>
public class FetchRouteBuilder : KeyValueRouteBuilderBase<FetchRouteBuilder>
{
    public FetchRouteBuilder() : base()
    {
        InvokeName = Microsoft.Teams.Api.Activities.Invokes.Name.Tasks.Fetch;
    }

    /// <summary>
    /// Configures the route to use the specified asynchronous handler for processing fetch requests.
    /// </summary>
    /// <remarks>Use this method to specify custom logic for handling fetch requests in Teams task modules. The 
    /// handler receives the deserialized data from the incoming activity, allowing for type-safe processing of 
    /// the fetch request's payload.</remarks>
    /// <param name="handler">An asynchronous delegate that processes the fetch request.</param>
    /// <returns>The current instance of FetchRouteBuilder, enabling method chaining.</returns>
    public FetchRouteBuilder WithHandler(FetchHandler handler)
    {
        _route.Handler = async (ctx, ts, ct) =>
        {
            var value = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.TaskModules.Request>(ctx.Activity.Value);
            var response = await handler(ctx, ts, value, ct).ConfigureAwait(false);
            await TeamsAgentExtension.SetResponse(ctx, response).ConfigureAwait(false);
        };
        return this;
    }

    /// <summary>
    /// Configures the route to use the specified asynchronous handler for processing fetch requests,
    /// with <c>Request.Data</c> deserialized to <typeparamref name="TData"/>.
    /// </summary>
    /// <typeparam name="TData">The type to deserialize <c>Request.Data</c> into and pass to the handler.</typeparam>
    /// <param name="handler">An asynchronous delegate that processes the fetch request, receiving the deserialized data payload.</param>
    /// <returns>The current instance of FetchRouteBuilder, enabling method chaining.</returns>
    public FetchRouteBuilder WithHandler<TData>(FetchHandler<TData> handler)
    {
        _route.Handler = async (ctx, ts, ct) =>
        {
            var elements = ProtocolJsonSerializer.ToJsonElements(ctx.Activity.Value);
            var data = elements.TryGetValue("data", out var dataElement)
                ? ProtocolJsonSerializer.ToObject<TData>(dataElement)
                : default;
            var response = await handler(ctx, ts, data, ct).ConfigureAwait(false);
            await TeamsAgentExtension.SetResponse(ctx, response).ConfigureAwait(false);
        };
        return this;
    }
}
