// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Errors;
using Microsoft.Teams.Api;
using System;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.MessageExtensions;

/// <summary>
/// Provides a builder for configuring query link routes in an AgentApplication.
/// </summary>
/// <remarks>
/// Use <see cref="QueryLinkRouteBuilder"/> to create and configure routes that respond to Activity Type of
/// <see cref="Microsoft.Agents.Core.Models.ActivityTypes.Invoke"/> with a name of
/// <see cref="Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.QueryLink"/>.
/// <code>
/// var route = QueryLinkRouteBuilder.Create()
///     .WithHandler(async (context, state, url, ct) =>
///     {
///         // Handle link unfurling for the URL
///     })
///     .Build();
///
/// app.AddRoute(route);
/// </code>
/// </remarks>
public class QueryLinkRouteBuilder : RouteBuilderBase<QueryLinkRouteBuilder>
{
    public QueryLinkRouteBuilder() : base()
    {
        _route.Flags |= RouteFlags.Invoke;
    }

    /// <summary>
    /// Configures the route to use the specified asynchronous handler for processing query link.
    /// </summary>
    /// <param name="handler">An asynchronous delegate that processes the query link.</param>
    /// <returns>The current instance of QueryLinkRouteBuilder, enabling method chaining.</returns>
    public QueryLinkRouteBuilder WithHandler(QueryLinkHandler handler)
    {
        _route.Handler = async (ctx, ts, ct) =>
        {
            AppBasedQueryLink? value = ProtocolJsonSerializer.ToObject<AppBasedQueryLink>(ctx.Activity.Value);
            var response = await handler(ctx, ts, value?.Url, ct).ConfigureAwait(false);
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
    /// <returns>The current instance of <see cref="QueryLinkRouteBuilder"/> with Invoke routing enabled.</returns>
    public override QueryLinkRouteBuilder AsInvoke(bool isInvoke = true)
    {
        return this;
    }

    protected override void PreBuild()
    {
        if (_route.Handler == null)
        {
            throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteBuilderMissingProperty, null, typeof(QueryLinkRouteBuilder).Name, "Handler");
        }

        _route.ChannelId ??= Channels.Msteams;

        _route.Selector ??= (ctx, ct) =>
            {
                return Task.FromResult(
                    IsContextMatch(ctx, _route)
                    && ctx.Activity.IsType(ActivityTypes.Invoke)
                    && string.Equals(ctx.Activity.Name, Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.QueryLink)
                );
            };
    }
}
