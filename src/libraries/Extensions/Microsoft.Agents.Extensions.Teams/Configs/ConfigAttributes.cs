// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using System;
using System.Reflection;

namespace Microsoft.Agents.Extensions.Teams.Configs;

/// <summary>
/// Attribute to define a route that handles Teams config fetch invocations.
/// The decorated method must match the <see cref="ConfigHandler"/> delegate signature —
/// the third parameter must be <see langword="object"/> and the return type must be
/// <c>Task&lt;Microsoft.Teams.Api.Config.ConfigResponse&gt;</c>.
/// </summary>
/// <remarks>
/// <code>
/// [ConfigFetchRoute]
/// public Task&lt;Microsoft.Teams.Api.Config.ConfigResponse&gt; OnConfigFetchAsync(
///     ITurnContext turnContext,
///     ITurnState turnState,
///     object configData,
///     CancellationToken cancellationToken)
/// {
///     return Task.FromResult(new Microsoft.Teams.Api.Config.ConfigResponse { /* ... */ });
/// }
/// </code>
/// Alternatively, <see cref="Config.OnConfigFetch"/> can be used to register the handler via the fluent API.
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class ConfigFetchRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    /// <inheritdoc />
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<ConfigHandler>(app, method);
        var builder = ConfigFetchRouteBuilder.Create()
            .WithHandler(handler)
            .AsAgentic(isAgenticOnly)
            .WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams config submit invocations.
/// The decorated method must match the <see cref="ConfigHandler"/> delegate signature —
/// the third parameter must be <see langword="object"/> and the return type must be
/// <c>Task&lt;Microsoft.Teams.Api.Config.ConfigResponse&gt;</c>.
/// </summary>
/// <remarks>
/// <code>
/// [ConfigSubmitRoute]
/// public Task&lt;Microsoft.Teams.Api.Config.ConfigResponse&gt; OnConfigSubmitAsync(
///     ITurnContext turnContext,
///     ITurnState turnState,
///     object configData,
///     CancellationToken cancellationToken)
/// {
///     return Task.FromResult(new Microsoft.Teams.Api.Config.ConfigResponse { /* ... */ });
/// }
/// </code>
/// Alternatively, <see cref="Config.OnConfigSubmit"/> can be used to register the handler via the fluent API.
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class ConfigSubmitRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    /// <inheritdoc />
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<ConfigHandler>(app, method);
        var builder = ConfigSubmitRouteBuilder.Create()
            .WithHandler(handler)
            .AsAgentic(isAgenticOnly)
            .WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}
