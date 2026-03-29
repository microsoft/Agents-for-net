// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Extensions.Teams.App.MessageExtensions;

/// <summary>
/// Provides a builder for configuring query routes in an AgentApplication.
/// </summary>
public class QueryRouteBuilder : CommandRouteBuilderBase<QueryRouteBuilder>
{
    public QueryRouteBuilder() : base()
    {
        InvokeName = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.Query;
    }

    /// <summary>
    /// Configures the route to use the specified asynchronous handler for processing query.
    /// </summary>
    /// <remarks>Use this method to specify custom logic for handling queries in Teams message
    /// extensions. The handler receives the deserialized data from the incoming activity, allowing for type-safe
    /// processing of the query's payload.</remarks>
    /// <param name="handler">An asynchronous delegate that processes the query, receiving the context, timestamp, deserialized data
    /// of type Microsoft.Teams.Api.MessageExtensions.Query, and a cancellation token.</param>
    /// <returns>The current instance of QueryRouteBuilder, enabling method chaining.</returns>
    public QueryRouteBuilder WithHandler(QueryHandlerAsync handler)
    {
        _route.Handler = async (ctx, ts, ct) =>
        {
            var value = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.MessageExtensions.Query>(ctx.Activity.Value);
            var result = await handler(ctx, ts, value, ct).ConfigureAwait(false);
            await TeamsAgentExtension.SetResponse(ctx, new Microsoft.Teams.Api.MessageExtensions.Response()
            {
                ComposeExtension = result
            }).ConfigureAwait(false);
        };
        return this;
    }
}
