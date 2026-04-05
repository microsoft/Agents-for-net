// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Errors;
using System;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.Consent;

/// <summary>
/// Base class for route builders that match Teams file consent invoke activities by a fixed action.
/// </summary>
/// <typeparam name="TBuilder">The concrete builder type, used to enable fluent method chaining.</typeparam>
public abstract class FileConsentRouteBuilderBase<TBuilder> : RouteBuilderBase<TBuilder>
    where TBuilder : FileConsentRouteBuilderBase<TBuilder>
{
    /// <summary>
    /// The file consent action string (<c>"accept"</c> or <c>"decline"</c>) this builder matches.
    /// Subclass constructors must set this property.
    /// </summary>
    protected string Action { get; set; }

    protected FileConsentRouteBuilderBase() : base()
    {
        _route.Flags |= RouteFlags.Invoke;
    }

    /// <summary>
    /// File consent routes are always invoke routes; this override is a no-op.
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
            if (!IsContextMatch(context, _route)
                || !context.Activity.IsType(ActivityTypes.Invoke)
                || !string.Equals(context.Activity.Name, Microsoft.Teams.Api.Activities.Invokes.Name.FileConsent, StringComparison.OrdinalIgnoreCase)
                || context.Activity.Value == null)
            {
                return Task.FromResult(false);
            }

            var response = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.FileConsentCardResponse>(context.Activity.Value);
            return Task.FromResult(response != null && string.Equals(response.Action, Action, StringComparison.OrdinalIgnoreCase));
        };
    }
}
