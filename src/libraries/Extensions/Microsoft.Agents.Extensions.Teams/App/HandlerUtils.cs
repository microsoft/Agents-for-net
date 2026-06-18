// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Proactive;
using System;
using System.Linq;
using System.Reflection;


namespace Microsoft.Agents.Extensions.Teams.App;

internal static class HandlerUtils
{

    public static RouteHandler WrapHandler(TeamsRouteHandler handler)
    {
        return async (ctx, turnState, cancellationToken) =>
        {
            var ttc = new TeamsTurnContext(ctx);
            await handler(ttc, turnState, cancellationToken);
        };
    }

    public static HandoffHandler WrapHandler(TeamsHandoffHandler handler)
    {
        return async (ctx, turnState, continuation, cancellationToken) =>
        {
            var ttc = new TeamsTurnContext(ctx);
            await handler(ttc, turnState, continuation, cancellationToken);
        };
    }

    public static FeedbackLoopHandler WrapHandler(TeamsFeedbackLoopHandler handler)
    {
        return async (ctx, turnState, feedbackData, cancellationToken) =>
        {
            var ttc = new TeamsTurnContext(ctx);
            await handler(ttc, turnState, feedbackData, cancellationToken);
        };
    }
}