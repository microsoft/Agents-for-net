// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Configs;

/// <summary>
/// Provides a builder for configuring routes that handle Teams config fetch invocations.
/// </summary>
/// <remarks>
/// Use <see cref="ConfigFetchRouteBuilder"/> to create and configure routes that respond to
/// config fetch requests from Microsoft Teams.
/// <code>
/// var route = ConfigFetchRouteBuilder.Create()
///     .WithHandler(async (context, state, configData, ct) =>
///     {
///         return new Microsoft.Teams.Api.Config.ConfigResponse { /* ... */ };
///     })
///     .Build();
///
/// app.AddRoute(route);
/// </code>
/// </remarks>
public class ConfigFetchRouteBuilder : ConfigRouteBuilderBase<ConfigFetchRouteBuilder>
{
    /// <summary>
    /// Initializes a new instance of <see cref="ConfigFetchRouteBuilder"/>,
    /// pre-configured to match config fetch invocations.
    /// </summary>
    public ConfigFetchRouteBuilder() : base()
    {
        InvokeName = Microsoft.Teams.Api.Activities.Invokes.Name.Configs.Fetch;
    }

    /// <summary>
    /// Configures the route to use the specified handler for processing config fetch invocations.
    /// </summary>
    /// <param name="handler">An asynchronous delegate invoked when a config fetch request is received.
    /// Receives the turn context, turn state, config data from the activity value,
    /// and a cancellation token. Must return a <see cref="Microsoft.Teams.Api.Config.ConfigResponse"/>.</param>
    /// <returns>The current <see cref="ConfigFetchRouteBuilder"/> instance for method chaining.</returns>
    public ConfigFetchRouteBuilder WithHandler(ConfigHandler handler)
    {
        _route.Handler = async (ctx, ts, ct) =>
        {
            var result = await handler(ctx, ts, ctx.Activity.Value, ct).ConfigureAwait(false);
            await TeamsAgentExtension.SetResponse(ctx, result).ConfigureAwait(false);
        };
        return this;
    }
}
