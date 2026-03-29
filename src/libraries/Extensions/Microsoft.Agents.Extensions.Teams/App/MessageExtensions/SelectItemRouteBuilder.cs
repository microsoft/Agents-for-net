// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Errors;
using System;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.App.MessageExtensions;

/// <summary>
/// Provides a builder for configuring select item routes in an AgentApplication.
/// </summary>
public class SelectItemRouteBuilder : RouteBuilderBase<SelectItemRouteBuilder>
{
    public SelectItemRouteBuilder() : base()
    {
        _route.Flags |= RouteFlags.Invoke;
    }

    /// <summary>
    /// Configures the route to use the specified asynchronous handler for processing select item.
    /// </summary>
    /// <remarks>Use this method to specify custom logic for handling select item actions in Teams message
    /// extensions. The handler receives the deserialized data from the incoming activity, allowing for type-safe
    /// processing of the action's payload.</remarks>
    /// <typeparam name="TData">The type of data extracted from the select item action payload and passed to the handler.</typeparam>
    /// <param name="handler">An asynchronous delegate that processes the select item, receiving the context, timestamp, deserialized data
    /// of type TData, and a cancellation token.</param>
    /// <returns>The current instance of SelectItemRouteBuilder, enabling method chaining.</returns>
    public SelectItemRouteBuilder WithHandler<TData>(SelectItemHandlerAsync<TData> handler)
    {
        _route.Handler = async (ctx, ts, ct) =>
        {
            var value = ProtocolJsonSerializer.ToObject<TData>(ctx.Activity.Value);
            var result = await handler(ctx, ts, value, ct).ConfigureAwait(false);
            await TeamsAgentExtension.SetResponse(ctx, new Microsoft.Teams.Api.MessageExtensions.Response()
            {
                ComposeExtension = result
            }).ConfigureAwait(false);
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
    /// <returns>The current instance of <see cref="SelectItemRouteBuilder"/> with Invoke routing enabled.</returns>
    public override SelectItemRouteBuilder AsInvoke(bool isInvoke = true)
    {
        return this;
    }

    protected override void PreBuild()
    {
        if (_route.Handler == null)
        {
            throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteBuilderMissingProperty, null, typeof(SelectItemRouteBuilder).Name, "Handler");
        }

        _route.ChannelId ??= Channels.Msteams;

        _route.Selector ??= (ctx, ct) =>
            {
                return Task.FromResult(
                    IsContextMatch(ctx, _route)
                    && ctx.Activity.IsType(ActivityTypes.Invoke) 
                    && string.Equals(ctx.Activity.Name, Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.SelectItem)
                );
            };
    }
}
    