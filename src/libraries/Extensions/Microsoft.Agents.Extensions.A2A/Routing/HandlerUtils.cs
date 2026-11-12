// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Extensions.A2A;
using Microsoft.Agents.Extensions.A2A.Pipeline;

namespace Microsoft.Agents.Extensions.A2A.Routing;

internal static class HandlerUtils
{
    public static IA2ATurnContext WrapTurnContext(ITurnContext context)
        => context as IA2ATurnContext ?? new A2ATurnContext(context);

    public static RouteHandler WrapHandler(A2ARouteHandler handler)
    {
        return async (ctx, turnState, cancellationToken) =>
        {
            var a2aContext = WrapTurnContext(ctx);
            await handler(a2aContext, turnState, cancellationToken);
        };
    }
}
