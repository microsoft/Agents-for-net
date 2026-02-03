// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Builder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
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
        services.AddSingleton<A2AAdapter>();
        services.AddSingleton<IA2AHttpAdapter>(sp => sp.GetService<A2AAdapter>());
    }

    /*
    /// <summary>
    /// Maps A2A endpoints for IAgent.
    /// </summary>
    /// <param name="endpoints"></param>
    /// <param name="requireAuth">Defaults to true.  Use false to allow anonymous requests (recommended for Development only)</param>
    /// <param name="path">Indicate the route patter, defaults to "/a2a"</param>
    /// <returns>An endpoint convention builder for further configuration.</returns>
    public static IEndpointConventionBuilder MapA2AJsonRpc(this IEndpointRouteBuilder endpoints, bool requireAuth = true, [StringSyntax("Route")] string path = "/a2a")
    {
        return endpoints.MapA2AJsonRpc<IAgent>(requireAuth, pattern);
    }
    */

    /// <summary>
    /// Maps A2A endpoints for TAgent type.
    /// </summary>
    /// <param name="endpoints"></param>
    /// <param name="requireAuth">Defaults to true.  Use false to allow anonymous requests (recommended for Development only)</param>
    /// <param name="path">Indicate the route patter, defaults to "/a2a"</param>
    /// <returns>An endpoint convention builder for further configuration.</returns>
    public static IEndpointConventionBuilder MapA2AJsonRpc(this IEndpointRouteBuilder endpoints, bool requireAuth = true, [StringSyntax("Route")] string path = "/a2a")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrEmpty(path);

        var a2aGroup = endpoints.MapGroup(path);
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
    /// <param name="path">The base path where the A2A agent is hosted.</param>
    /// <returns>An endpoint convention builder for further configuration.</returns>
    public static IEndpointConventionBuilder MapWellKnownAgentCard(this IEndpointRouteBuilder endpoints, bool requireAuth = false, [StringSyntax("Route")] string path = "/a2a")
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var routeGroup = endpoints.MapGroup(path ?? string.Empty);
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
            return adapter.ProcessAgentCardAsync(request, response, agent, path, cancellationToken);
        });

        // This is for the TCK which hits root
        endpoints.MapGet(".well-known/agent-card.json", (HttpRequest request, HttpResponse response, IA2AHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
        {
            return adapter.ProcessAgentCardAsync(request, response, agent, path, cancellationToken);
        });

        return routeGroup;
    }

    /// <summary>
    /// Enables HTTP A2A endpoints for the specified path.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder to configure.</param>
    /// <param name="requireAuth"></param>
    /// <param name="path">The base path for the HTTP A2A endpoints.</param>
    /// <returns>An endpoint convention builder for further configuration.</returns>
    public static IEndpointConventionBuilder MapA2AHttp(this IEndpointRouteBuilder endpoints, bool requireAuth = false, [StringSyntax("Route")] string path = "/a2a")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrEmpty(path);

        var routeGroup = endpoints.MapGroup(path);
        if (requireAuth)
        {
            routeGroup.RequireAuthorization();
        }
        else
        {
            routeGroup.AllowAnonymous();
        }

        // /v1/card endpoint - Agent discovery
        routeGroup.MapGet("/v1/card", async (HttpRequest request, HttpResponse response, IA2AHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
            await adapter.ProcessAgentCardAsync(request, response, agent, path, cancellationToken));

        // /v1/tasks/{id} endpoint
        routeGroup.MapGet("/v1/tasks/{id}", (HttpRequest request, HttpResponse response, IA2AHttpAdapter adapter, IAgent agent, string id, [FromQuery] int? historyLength, [FromQuery] string? metadata, CancellationToken cancellationToken) =>
            adapter.GetTaskAsync(request, response, agent, id, historyLength, metadata, cancellationToken));

        // /v1/tasks/{id}:cancel endpoint
        routeGroup.MapPost("/v1/tasks/{id}:cancel", (HttpRequest request, HttpResponse response, IA2AHttpAdapter adapter, IAgent agent, string id, CancellationToken cancellationToken) =>
            adapter.CancelTaskAsync(request, response, agent, id, cancellationToken));

        // /v1/tasks/{id}:subscribe endpoint
        routeGroup.MapGet("/v1/tasks/{id}:subscribe", (HttpRequest request, HttpResponse response, IA2AHttpAdapter adapter, IAgent agent, string id, CancellationToken cancellationToken) => 
            adapter.SubscribeToTask(request, response, agent, id, cancellationToken));

        // /v1/tasks/{id}/pushNotificationConfigs endpoint - POST
        routeGroup.MapPost("/v1/tasks/{id}/pushNotificationConfigs", (HttpRequest request, HttpResponse response, IA2AHttpAdapter adapter, IAgent agent, string id, [FromBody] PushNotificationConfig pushNotificationConfig, CancellationToken cancellationToken) =>
            adapter.SetPushNotificationAsync(request, response, agent, id, pushNotificationConfig, cancellationToken));

        // /v1/tasks/{id}/pushNotificationConfigs endpoint - GET
        routeGroup.MapGet("/v1/tasks/{id}/pushNotificationConfigs/{notificationConfigId?}", (HttpRequest request, HttpResponse response, IA2AHttpAdapter adapter, IAgent agent, string id, string? notificationConfigId, CancellationToken cancellationToken) =>
            adapter.GetPushNotificationAsync(request, response, agent, id, notificationConfigId, cancellationToken));

        // /v1/message:send endpoint
        routeGroup.MapPost("/v1/message:send", (HttpRequest request, HttpResponse response, IA2AHttpAdapter adapter, IAgent agent, [FromBody] MessageSendParams sendParams, CancellationToken cancellationToken) =>
            adapter.SendMessageAsync(request, response, agent, sendParams, cancellationToken));

        // /v1/message:stream endpoint
        routeGroup.MapPost("/v1/message:stream", (HttpRequest request, HttpResponse response, IA2AHttpAdapter adapter, IAgent agent, [FromBody] MessageSendParams sendParams, CancellationToken cancellationToken) =>
            adapter.SendMessageStream(request, response, agent, sendParams, cancellationToken));

        return routeGroup;
    }

}
