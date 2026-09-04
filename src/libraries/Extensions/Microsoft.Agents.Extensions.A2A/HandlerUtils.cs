// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;

namespace Microsoft.Agents.Extensions.A2A;

internal static class HandlerUtils
{
    public static RouteHandler WrapHandler(A2ARouteHandler handler)
    {
        return async (ctx, turnState, cancellationToken) =>
        {
            var a2aContext = new A2ATurnContext(ctx);
            await handler(a2aContext, turnState, cancellationToken);
        };
    }
}
