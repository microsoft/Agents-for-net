// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Builder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Microsoft.Agents.Hosting.AspNetCore.A2A;

public static class A2AServiceExtensions
{
    /// <summary>
    /// Registers the A2AAdapter
    /// </summary>
    /// <remarks>This is required for A2A request handling.</remarks>
    /// <param name="services"></param>
    public static void AddA2AAdapter(this IServiceCollection services)
    {
        // !!! A2AAdapter still needs this?
        services.AddAsyncAdapterSupport();

        services.AddSingleton<A2AAdapter>();
        services.AddSingleton<IA2AHttpAdapter>(sp => sp.GetService<A2AAdapter>());
    }

    /*
    /// <summary>
    /// Maps A2A endpoints for IAgent.
    /// </summary>
    /// <param name="endpoints"></param>
    /// <param name="requireAuth">Defaults to true.  Use false to allow anonymous requests (recommended for Development only)</param>
    /// <param name="pattern">Indicate the route patter, defaults to "/a2a"</param>
    /// <returns>An endpoint convention builder for further configuration.</returns>
    public static IEndpointConventionBuilder MapA2AJsonRpc(this IEndpointRouteBuilder endpoints, bool requireAuth = true, [StringSyntax("Route")] string pattern = "/a2a")
    {
        return endpoints.MapA2AJsonRpc<IAgent>(requireAuth, pattern);
    }
    */

    /// <summary>
    /// Maps A2A endpoints for TAgent type.
    /// </summary>
    /// <param name="endpoints"></param>
    /// <param name="requireAuth">Defaults to true.  Use false to allow anonymous requests (recommended for Development only)</param>
    /// <param name="pattern">Indicate the route patter, defaults to "/a2a"</param>
    /// <returns>An endpoint convention builder for further configuration.</returns>
    public static IEndpointConventionBuilder MapA2AJsonRpc(this IEndpointRouteBuilder endpoints, bool requireAuth = true, [StringSyntax("Route")] string pattern = "/a2a")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrEmpty(pattern);

        var a2aGroup = endpoints.MapGroup(pattern);
        if (requireAuth)
        {
            a2aGroup.RequireAuthorization();
        }
        else
        {
            a2aGroup.AllowAnonymous();
        }

        // JSONRPC
        a2aGroup.MapPost(
            "",
            async (HttpRequest request, HttpResponse response, IA2AHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
            {
                return await adapter.ProcessJsonRpcAsync(request, response, agent, cancellationToken);
            })
            .WithMetadata(new AcceptsMetadata(["application/json"]))
            .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status200OK, contentTypes: ["text/event-stream"]))
            .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status202Accepted));

        return a2aGroup;
    }

    /// <summary>
    /// Enables the well-known agent card endpoint for agent discovery.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder to configure.</param>
    /// <param name="requireAuth"></param>
    /// <param name="adapter"></param>
    /// <param name="agent"></param>
    /// <param name="pattern">The base path where the A2A agent is hosted.</param>
    /// <returns>An endpoint convention builder for further configuration.</returns>
    public static IEndpointConventionBuilder MapWellKnownAgentCard(this IEndpointRouteBuilder endpoints, bool requireAuth = false, [StringSyntax("Route")] string pattern = "/a2a")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrEmpty(pattern);

        var routeGroup = endpoints.MapGroup(pattern);
        if (requireAuth)
        {
            routeGroup.RequireAuthorization();
        }
        else
        {
            routeGroup.AllowAnonymous();
        }

        routeGroup.MapGet(".well-known/agent-card.json", (HttpRequest request, HttpResponse response, IA2AHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
        {
            return adapter.ProcessAgentCardAsync(request, response, agent, pattern, cancellationToken);
        });

        return routeGroup;
    }
}
