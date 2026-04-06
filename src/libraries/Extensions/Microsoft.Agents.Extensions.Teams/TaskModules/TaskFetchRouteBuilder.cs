// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Extensions.Teams.TaskModules;

/// <summary>
/// Provides a builder for configuring fetch routes in an AgentApplication.
/// </summary>
/// <remarks>
/// Use <see cref="TaskFetchRouteBuilder"/> to create and configure routes that respond to Activity Type of
/// <see cref="Microsoft.Agents.Core.Models.ActivityTypes.Invoke"/> with a name of
/// <see cref="Microsoft.Teams.Api.Activities.Invokes.Name.Tasks.Fetch"/>,
/// optionally filtered by a task data key value via <see cref="WithValue(string)"/>.
/// </remarks>
public class TaskFetchRouteBuilder : KeyValueRouteBuilderBase<TaskFetchRouteBuilder>
{
    public TaskFetchRouteBuilder() : base()
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
    /// <returns>The current instance of TaskFetchRouteBuilder, enabling method chaining.</returns>
    public TaskFetchRouteBuilder WithHandler(TaskFetchHandler handler)
    {
        _route.Handler = async (ctx, ts, ct) =>
        {
            var value = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.TaskModules.Request>(ctx.Activity.Value);
            var response = await handler(ctx, ts, value, ct).ConfigureAwait(false);
            await TeamsAgentExtension.SetResponse(ctx, response).ConfigureAwait(false);
        };
        return this;
    }
}
