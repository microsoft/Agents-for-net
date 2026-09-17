// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using System;

namespace Microsoft.Agents.Extensions.Slack;

internal static class HandlerUtils
{
    public static RouteHandler WrapHandler(SlackRouteHandler handler)
    {
        return async (ctx, turnState, cancellationToken) =>
        {
            var stc = new SlackTurnContext(ctx);
            await handler(stc, turnState, cancellationToken);
        };
    }

    public static FeedbackLoopHandler WrapHandler(SlackFeedbackLoopHandler handler)
    {
        return async (ctx, turnState, feedbackData, cancellationToken) =>
        {
            var stc = new SlackTurnContext(ctx);
            await handler(stc, turnState, feedbackData, cancellationToken);
        };
    }

    public static RouteHandler WrapHandler(TypedRouteHandler<ISlackActivity> handler)
    {
        return async (ctx, turnState, cancellationToken) =>
        {
            var stc = new SlackTurnContext(ctx);
            await handler(stc, turnState, cancellationToken);
        };
    }

    /// <summary>
    /// Resolves a delegate created from a decorated method to a <see cref="Microsoft.Agents.Builder.App.RouteHandler"/>. A
    /// <see cref="Microsoft.Agents.Extensions.Slack.SlackRouteHandler"/> (Slack-specific context) or a
    /// <see cref="Microsoft.Agents.Builder.App.TypedRouteHandler{T}"/> of <see cref="Microsoft.Agents.Extensions.Slack.ISlackActivity"/> is wrapped; a native
    /// <see cref="Microsoft.Agents.Builder.App.RouteHandler"/> is used as-is.
    /// </summary>
    public static RouteHandler ResolveRouteHandler(Delegate handler)
    {
        return handler switch
        {
            SlackRouteHandler slackHandler => WrapHandler(slackHandler),
            TypedRouteHandler<ISlackActivity> typedHandler => WrapHandler(typedHandler),
            _ => (RouteHandler)handler,
        };
    }

    /// <summary>
    /// Resolves a delegate created from a decorated method to a <see cref="Microsoft.Agents.Builder.App.FeedbackLoopHandler"/>. A
    /// <see cref="Microsoft.Agents.Extensions.Slack.SlackFeedbackLoopHandler"/> (Slack-specific context) is wrapped; a native
    /// <see cref="Microsoft.Agents.Builder.App.FeedbackLoopHandler"/> is used as-is.
    /// </summary>
    public static FeedbackLoopHandler ResolveFeedbackLoopHandler(Delegate handler)
    {
        return handler is SlackFeedbackLoopHandler slackHandler ? WrapHandler(slackHandler) : (FeedbackLoopHandler)handler;
    }
}
