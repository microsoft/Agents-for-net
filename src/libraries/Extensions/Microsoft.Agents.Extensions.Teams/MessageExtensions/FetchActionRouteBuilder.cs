// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Extensions.Teams.MessageExtensions;

/// <summary>
/// Provides a builder for configuring <c>composeExtension/fetchTask</c> Invokes in an AgentApplication.
/// </summary>
/// <remarks>
/// Use <see cref="FetchActionRouteBuilder"/> to create and configure routes that respond to Activity Type of
/// <see cref="Microsoft.Agents.Core.Models.ActivityTypes.Invoke"/> with a name of
/// <see cref="Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.FetchTask"/>,
/// optionally filtered by command ID via <see cref="WithCommand(string)"/>.
/// </remarks>
public class FetchActionRouteBuilder : CommandRouteBuilderBase<FetchActionRouteBuilder>
{
    public FetchActionRouteBuilder() : base()
    {
        InvokeName = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.FetchTask;
    }

    /// <summary>
    /// Creates a new instance of the <see cref="FetchActionRouteBuilder"/> class.
    /// </summary>
    /// <returns>A new <see cref="FetchActionRouteBuilder"/>.</returns>
    public static FetchActionRouteBuilder Create()
    {
        return new FetchActionRouteBuilder();
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
            var ttc = new TeamsTurnContext(ctx);
            var action = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.MessageExtensions.Action>(ttc.Activity.Value);
            var response = await handler(ttc, ts, action, ct).ConfigureAwait(false);
            await TeamsAgentExtension.SetResponse(ttc, response).ConfigureAwait(false);
        };
        return this;
    }
}
