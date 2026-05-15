// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Threading;

namespace Microsoft.Agents.Extensions.Slack;

public static class SlackEndpointExtensions
{
    /// <summary>
    /// Maps Slack endpoints.
    /// </summary>
    /// <param name="endpoints"></param>
    /// <param name="requireAuth">Defaults to true.  Use false to allow anonymous requests (recommended for Development only)</param>
    /// <returns></returns>
    public static IEndpointConventionBuilder MapSlackEndpoints<T>(this IEndpointRouteBuilder endpoints, bool requireAuth = true) where T : class, IAgent
    {
        var slackGroup = endpoints.MapGroup("");
        if (requireAuth)
        {
            slackGroup.RequireAuthorization();
        }
        else
        {
            slackGroup.AllowAnonymous();
        }

        slackGroup.MapPost("/api/actions", async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, T agent, CancellationToken cancellationToken) =>
        {
            await adapter.ProcessAsync(request, response, agent, cancellationToken).ConfigureAwait(false);
        });

        return slackGroup;
    }
}
