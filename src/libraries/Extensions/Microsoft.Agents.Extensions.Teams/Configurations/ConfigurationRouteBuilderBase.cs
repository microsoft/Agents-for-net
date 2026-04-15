// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams.Errors;
using System;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.Configurations;

/// <summary>
/// Base class for route builders that match Teams config invoke activities by a fixed invoke name.
/// </summary>
/// <typeparam name="TBuilder">The concrete builder type, used to enable fluent method chaining.</typeparam>
public abstract class ConfigurationRouteBuilderBase<TBuilder> : RouteBuilderBase<TBuilder>
    where TBuilder : ConfigurationRouteBuilderBase<TBuilder>
{
    /// <summary>
    /// The Teams invoke activity name this builder matches (e.g. <c>"config/fetch"</c> or <c>"config/submit"</c>).
    /// Subclass constructors must set this property.
    /// </summary>
    protected string InvokeName { get; set; }

    /// <summary>
    /// Initializes a new instance of <see cref="ConfigurationRouteBuilderBase{TBuilder}"/>,
    /// pre-configured as an Invoke route.
    /// </summary>
    protected ConfigurationRouteBuilderBase() : base()
    {
        _route.Flags |= RouteFlags.Invoke;
    }

    /// <summary>
    /// Config routes are always invoke routes; this override is a no-op.
    /// </summary>
    public override TBuilder AsInvoke(bool isInvoke = true) => (TBuilder)this;

    /// <inheritdoc />
    protected override void PreBuild()
    {
        if (_route.Handler == null)
        {
            throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(
                ErrorHelper.RouteBuilderMissingProperty, null, typeof(TBuilder).Name, "Handler");
        }

        _route.ChannelId ??= Channels.Msteams;

        _route.Selector = (context, ct) =>
        {
            return Task.FromResult(
                IsContextMatch(context, _route)
                && context.Activity.IsType(ActivityTypes.Invoke)
                && string.Equals(context.Activity.Name, InvokeName, StringComparison.OrdinalIgnoreCase));
        };
    }
}
