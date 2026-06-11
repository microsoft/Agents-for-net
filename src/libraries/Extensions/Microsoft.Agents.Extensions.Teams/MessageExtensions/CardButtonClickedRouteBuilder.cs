// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Errors;
using System;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.MessageExtensions;

/// <summary>
/// Provides a builder for configuring card button click routes in an AgentApplication.
/// </summary>
/// <remarks>
/// Use <see cref="CardButtonClickedRouteBuilder"/> to create and configure routes that respond to Activity Type of
/// <see cref="Microsoft.Agents.Core.Models.ActivityTypes.Invoke"/> with a name of
/// <see cref="Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.CardButtonClicked"/>.
/// </remarks>
public class CardButtonClickedRouteBuilder : RouteBuilderBase<CardButtonClickedRouteBuilder>
{
    public CardButtonClickedRouteBuilder() : base()
    {
        _route.Flags |= RouteFlags.Invoke;
    }

    /// <summary>
    /// Configures the route to use the specified asynchronous handler for processing card button click actions with deserialized
    /// data of type TData.
    /// </summary>
    /// <remarks>Use this method to specify custom logic for handling card button click actions in Teams message
    /// extensions. The handler receives the deserialized data from the incoming activity, allowing for type-safe
    /// processing of the action's payload.</remarks>
    /// <returns>The current instance of CardButtonClickedRouteBuilder, enabling method chaining.</returns>
    public CardButtonClickedRouteBuilder WithHandler<TData>(CardButtonClickedHandler<TData> handler)
    {
        _route.Handler = async (ctx, ts, ct) =>
        {
            var cardData = ProtocolJsonSerializer.ToObject<TData>(ctx.Activity.Value);
            await handler(ctx, ts, cardData, ct).ConfigureAwait(false);
            await TeamsAgentExtension.SetResponse(ctx, null).ConfigureAwait(false);
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
    /// <returns>The current instance of <see cref="CardButtonClickedRouteBuilder"/> with Invoke routing enabled.</returns>
    public override CardButtonClickedRouteBuilder AsInvoke(bool isInvoke = true)
    {
        return this;
    }

    protected override void PreBuild()
    {
        if (_route.Handler == null)
        {
            throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteBuilderMissingProperty, null, typeof(CardButtonClickedRouteBuilder).Name, "Handler");
        }

        _route.ChannelId ??= Channels.Msteams;

        _route.Selector ??= (ctx, ct) =>
            {
                return Task.FromResult(
                    IsContextMatch(ctx, _route)
                    && ctx.Activity.IsType(ActivityTypes.Invoke)
                    && string.Equals(ctx.Activity.Name, Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.CardButtonClicked)
                );
            };
    }
}
