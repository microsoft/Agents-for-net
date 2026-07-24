// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.MSTeams.Errors;
using System;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.MSTeams.MessageExtensions;

/// <summary>
/// Provides a builder for configuring query URL setting routes in an AgentApplication.
/// </summary>
/// <remarks>
/// Use <see cref="QuerySettingUrlRouteBuilder"/> to create and configure routes that respond to Activity Type of
/// <see cref="Microsoft.Agents.Core.Models.ActivityTypes.Invoke"/> with a name of
/// <see cref="Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.QuerySettingUrl"/>.
/// </remarks>
public class QuerySettingUrlRouteBuilder : RouteBuilderBase<QuerySettingUrlRouteBuilder>
{
    /// <summary>
    /// Creates a new instance of the QuerySettingUrlRouteBuilder class for constructing route definitions.
    /// </summary>
    /// <returns>A QuerySettingUrlRouteBuilder instance that can be used to configure and build routes.</returns>
    public static QuerySettingUrlRouteBuilder Create()
    {
        var builder = Activator.CreateInstance<QuerySettingUrlRouteBuilder>();
        return builder;
    }

    public QuerySettingUrlRouteBuilder() : base()
    {
        _route.Flags |= RouteFlags.Invoke;
    }

    /// <summary>
    /// Configures the route to use the specified asynchronous handler for processing query URL settings.
    /// </summary>
    /// <param name="handler">An asynchronous delegate that processes the query URL settings.</param>
    /// <returns>The current instance of QuerySettingUrlRouteBuilder, enabling method chaining.</returns>
    public QuerySettingUrlRouteBuilder WithHandler(QuerySettingUrlHandler handler)
    {
        _route.Handler = async (ctx, ts, ct) =>
        {
            var response = await handler(new TeamsTurnContext(ctx), ts, ct).ConfigureAwait(false);
            await TeamsAgentExtension.SetResponse(ctx, response).ConfigureAwait(false);
        };
        return this;
    }

    /// <summary>
    /// Returns the current route builder instance configured for Invoke routing. This method ensures that the route
    /// remains set as an Invoke route.
    /// </summary>
    /// <remarks>This override prevents changing the route configuration from Invoke routing,
    /// maintaining consistency with the route's initial setup.</remarks>
    /// <param name="isInvoke">A value indicating whether the route should be treated as an Invoke route. The parameter is ignored, as the
    /// route is always configured for Invoke routing.</param>
    /// <returns>The current instance of <see cref="QuerySettingUrlRouteBuilder"/> with Invoke routing enabled.</returns>
    public override QuerySettingUrlRouteBuilder AsInvoke(bool isInvoke = true)
    {
        return this;
    }

    protected override void PreBuild()
    {
        if (_route.Handler == null)
        {
            throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteBuilderMissingProperty, null, typeof(QuerySettingUrlRouteBuilder).Name, "Handler");
        }

        _route.ChannelId ??= Microsoft.Agents.Core.Models.Channels.Msteams;

        _route.Selector ??= (ctx, ct) =>
            {
                return Task.FromResult(
                    IsContextMatch(ctx, _route)
                    && ctx.Activity.IsType(ActivityTypes.Invoke)
                    && string.Equals(ctx.Activity.Name, Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.QuerySettingUrl)
                );
            };
    }
}
