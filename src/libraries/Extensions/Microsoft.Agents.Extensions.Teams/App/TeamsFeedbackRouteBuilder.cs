// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams.App;

namespace Microsoft.Agents.Extensions.Teams;

/// <summary>
/// Provides a fluent builder for Teams feedback loop routes.
/// </summary>
public class TeamsFeedbackRouteBuilder : FeedbackRouteBuilderBase<TeamsFeedbackRouteBuilder>
{

    /// <summary>
    /// Assigns the specified Teams feedback loop handler to the current route.
    /// </summary>
    /// <param name="handler">The Teams feedback loop handler to associate with the route.</param>
    /// <param name="proactive">The proactive messaging service used to create the Teams turn context.</param>
    /// <returns>The current builder instance.</returns>
    public TeamsFeedbackRouteBuilder WithHandler(TeamsFeedbackLoopHandler handler)
    {
        AssertionHelpers.ThrowIfNull(handler, nameof(handler));
        var routeHandler = HandlerUtils.WrapHandler(handler);
        return WithHandlerCore(routeHandler);
    }

    /// <summary>
    /// Applies Teams-specific defaults before the route is built.
    /// </summary>
    protected override void PreBuild()
    {
        _route.ChannelId = Channels.Msteams;
        base.PreBuild();
    }
}