﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Microsoft.Agents.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Hosting.A2A.JsonRpc;

namespace Microsoft.Agents.Hosting.A2A;

public static class A2AServiceExtensions
{
    /// <summary>
    /// Registers the A2AAdapter
    /// </summary>
    /// <remarks>This is required for A2A request handling.</remarks>
    /// <param name="services"></param>
    public static void AddA2AAdapter(this IServiceCollection services)
    {
        services.AddAsyncAdapterSupport();

        services.AddSingleton<A2AAdapter>();
        services.AddSingleton<IA2AHttpAdapter>(sp => sp.GetService<A2AAdapter>());
    }

    /// <summary>
    /// Maps A2A endpoints.
    /// </summary>
    /// <param name="endpoints"></param>
    /// <param name="requireAuth">Defaults to true.  Use false to allow anonymous requests (recommended for Development only)</param>
    /// <param name="pattern">Indicate the route patter, defaults to "/a2a"</param>
    /// <returns></returns>
    public static IEndpointConventionBuilder MapA2A(this IEndpointRouteBuilder endpoints, bool requireAuth = true, [StringSyntax("Route")] string pattern = "/a2a")
    {
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
                await adapter.ProcessAsync(request, response, agent, cancellationToken);
            })
            .WithMetadata(new AcceptsMetadata(["application/json"]))
            .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status200OK, contentTypes: ["text/event-stream"]))
            .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status202Accepted));

        // AgentCard
        a2aGroup.MapGet("/.well-known/agent-card.json", async (HttpRequest request, HttpResponse response, IA2AHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
        {
            System.Diagnostics.Trace.WriteLine("/.well-known/agent.json");
            await adapter.ProcessAgentCardAsync(request, response, agent, pattern, cancellationToken);
        })
            .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status200OK, typeof(JsonRpcError), contentTypes: ["application/json"]));

        // Temporary because the TCK is hitting host root with the older document name
        endpoints.MapGet("/.well-known/agent.json", async (HttpRequest request, HttpResponse response, IA2AHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
        {
            System.Diagnostics.Trace.WriteLine("/.well-known/agent.json");
            await adapter.ProcessAgentCardAsync(request, response, agent, pattern, cancellationToken);
        })
            .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status200OK, typeof(JsonRpcError), contentTypes: ["application/json"]));

        return a2aGroup;
    }
}
