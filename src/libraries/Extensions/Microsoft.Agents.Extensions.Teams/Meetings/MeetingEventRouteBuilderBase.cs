// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using System;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.Meetings;

/// <summary>
/// Base class for route builders that match Teams meeting event activities by a fixed event name.
/// </summary>
/// <typeparam name="TBuilder">The concrete builder type, used to enable fluent method chaining.</typeparam>
public abstract class MeetingEventRouteBuilderBase<TBuilder> : RouteBuilderBase<TBuilder>
    where TBuilder : MeetingEventRouteBuilderBase<TBuilder>
{
    /// <summary>
    /// The Teams event activity name that this builder matches.
    /// Subclass constructors must set this property.
    /// </summary>
    protected string EventName { get; set; }

    /// <summary>
    /// For meeting event routes, the invoke flag is ignored to prevent misconfiguration.
    /// </summary>
    /// <remarks>Meeting events cannot be configured as invoke routes. This method always returns the
    /// current instance, regardless of the value of <paramref name="isInvoke"/>.</remarks>
    /// <param name="isInvoke">Ignored.</param>
    /// <returns>The current builder instance.</returns>
    public override TBuilder AsInvoke(bool isInvoke = true) => (TBuilder)this;

    /// <inheritdoc />
    protected override void PreBuild()
    {
        _route.Selector = (context, ct) => Task.FromResult(
            IsContextMatch(context, _route)
            && context.Activity.IsType(ActivityTypes.Event)
            && context.Activity.Name != null
            && EventName.Equals(context.Activity.Name, StringComparison.OrdinalIgnoreCase)
        );
    }
}
