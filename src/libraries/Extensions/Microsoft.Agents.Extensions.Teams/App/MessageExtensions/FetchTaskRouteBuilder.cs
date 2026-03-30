// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.App.MessageExtensions;

/// <summary>
/// Provides a builder for configuring fetch task routes in an AgentApplication.
/// </summary>
public class FetchTaskRouteBuilder : CommandRouteBuilderBase<FetchTaskRouteBuilder>
{
    public FetchTaskRouteBuilder() : base()
    {
        InvokeName = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.FetchTask;
    }

    /// <summary>
    /// Configures the route to use the specified asynchronous handler for processing fetch tasks.
    /// </summary>
    /// <remarks>Use this method to specify custom logic for handling fetch tasks in Teams message
    /// extensions. The handler receives the deserialized data from the incoming activity, allowing for type-safe
    /// processing of the action's payload.</remarks>
    /// <param name="handler">An asynchronous delegate that processes the fetch task</param>
    /// <returns>The current instance of FetchTaskRouteBuilder, enabling method chaining.</returns>
    public FetchTaskRouteBuilder WithHandler(FetchTaskHandler handler)
    {
        _route.Handler = async (ctx, ts, ct) =>
        {
            var response = await handler(ctx, ts, ct).ConfigureAwait(false);
            await TeamsAgentExtension.SetResponse(ctx, response).ConfigureAwait(false);
        };
        return this;
    }
}
