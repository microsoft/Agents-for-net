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
/// Provides a builder for configuring settings routes in an AgentApplication.
/// </summary>
/// <remarks>
/// Use <see cref="SettingRouteBuilder"/> to create and configure routes that respond to Activity Type of
/// <see cref="Microsoft.Agents.Core.Models.ActivityTypes.Invoke"/> with a name of
/// <see cref="Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.Setting"/>.
/// </remarks>
public class SettingRouteBuilder : RouteBuilderBase<SettingRouteBuilder>
{
    /// <summary>
    /// Creates a new instance of the SettingRouteBuilder class for constructing route definitions.
    /// </summary>
    /// <returns>A SettingRouteBuilder instance that can be used to configure and build routes.</returns>
    public static SettingRouteBuilder Create()
    {
        var builder = Activator.CreateInstance<SettingRouteBuilder>();
        return builder;
    }

    public SettingRouteBuilder() : base()
    {
        _route.Flags |= RouteFlags.Invoke;
    }

    /// <summary>
    /// Configures the route to use the specified asynchronous handler for processing configure settings.
    /// </summary>
    /// <remarks>Use this method to specify custom logic for handling configure settings in Teams message
    /// extensions. The handler receives the deserialized data from the incoming activity, allowing for type-safe
    /// processing of the action's payload.</remarks>
    /// <returns>The current instance of ConfigureSettingsRouteBuilder, enabling method chaining.</returns>
    public SettingRouteBuilder WithHandler(SettingHandler handler)
    {
        _route.Handler = async (ctx, ts, ct) =>
        {
            var value = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.MessageExtensions.Query>(ctx.Activity.Value);
            var response = await handler(new TeamsTurnContext(ctx), ts, value, ct).ConfigureAwait(false);
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
    /// <returns>The current instance of <see cref="SettingRouteBuilder"/> with Invoke routing enabled.</returns>
    public override SettingRouteBuilder AsInvoke(bool isInvoke = true)
    {
        return this;
    }

    protected override void PreBuild()
    {
        if (_route.Handler == null)
        {
            throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteBuilderMissingProperty, null, typeof(SettingRouteBuilder).Name, "Handler");
        }

        _route.ChannelId ??= Channels.Msteams;

        _route.Selector ??= (ctx, ct) =>
            {
                return Task.FromResult(
                    IsContextMatch(ctx, _route)
                    && ctx.Activity.IsType(ActivityTypes.Invoke)
                    && string.Equals(ctx.Activity.Name, Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.Setting)
                );
            };
    }
}
