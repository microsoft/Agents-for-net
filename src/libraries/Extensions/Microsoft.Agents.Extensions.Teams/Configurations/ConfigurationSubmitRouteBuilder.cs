// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Configurations;

/// <summary>
/// Provides a builder for configuring routes that handle Teams config submit invocations.
/// </summary>
/// <remarks>
/// Use <see cref="ConfigurationSubmitRouteBuilder"/> to create and configure routes that respond to Activity Type of
/// <see cref="Microsoft.Agents.Core.Models.ActivityTypes.Invoke"/> with a name of
/// <see cref="Microsoft.Teams.Api.Activities.Invokes.Name.Configs.Submit"/>.
/// </remarks>
public class ConfigurationSubmitRouteBuilder : ConfigurationRouteBuilderBase<ConfigurationSubmitRouteBuilder>
{
    /// <summary>
    /// Initializes a new instance of <see cref="ConfigurationSubmitRouteBuilder"/>,
    /// pre-configured to match config submit invocations.
    /// </summary>
    public ConfigurationSubmitRouteBuilder() : base()
    {
        InvokeName = Microsoft.Teams.Api.Activities.Invokes.Name.Configs.Submit;
    }

    /// <summary>
    /// Creates a new instance of the <see cref="ConfigurationSubmitRouteBuilder"/> class.
    /// </summary>
    /// <returns>A new <see cref="ConfigurationSubmitRouteBuilder"/>.</returns>
    public static ConfigurationSubmitRouteBuilder Create()
    {
        return new ConfigurationSubmitRouteBuilder();
    }

    /// <summary>
    /// Configures the route to use the specified handler for processing config submit invocations.
    /// </summary>
    /// <param name="handler">An asynchronous delegate invoked when a config submit request is received.
    /// Receives the turn context, turn state, config data from the activity value,
    /// and a cancellation token. Must return a <see cref="Microsoft.Teams.Api.Config.ConfigResponse"/>.</param>
    /// <returns>The current <see cref="ConfigurationSubmitRouteBuilder"/> instance for method chaining.</returns>
    public ConfigurationSubmitRouteBuilder WithHandler(ConfigurationHandler handler)
    {
        _route.Handler = async (ctx, ts, ct) =>
        {
            var ttc = new TeamsTurnContext(ctx);
            var result = await handler(ttc, ts, ctx.Activity.Value, ct).ConfigureAwait(false);
            await TeamsAgentExtension.SetResponse(ttc, result).ConfigureAwait(false);
        };
        return this;
    }
}
