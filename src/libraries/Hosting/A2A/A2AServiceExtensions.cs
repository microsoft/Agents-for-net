// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
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

    /// <summary>
    /// This adds HTTP endpoints for all AgentApplications defined in the calling assembly.  Each AgentApplication must have been added using <see cref="AddAgent{TAgent}(IHostApplicationBuilder)"/>.
    /// </summary>
    /// <param name="endpoints"></param>
    /// <param name="requireAuth"></param>
    /// <param name="defaultPath"></param>
    /// <exception cref="InvalidOperationException"/>
    public static IEndpointConventionBuilder MapA2AApplicationEndpoints(
        this IEndpointRouteBuilder endpoints,
        bool requireAuth = true,
        [StringSyntax("Route")] string defaultPath = "/a2a")
    {
        if (string.IsNullOrEmpty(defaultPath))
        {
            defaultPath = "/a2a";
        }

        var a2aGroup = endpoints.MapGroup("");
        if (requireAuth)
        {
            a2aGroup.RequireAuthorization();
        }
        else
        {
            a2aGroup.AllowAnonymous();
        }

        var allAgents = Assembly.GetCallingAssembly().GetTypes().Where(t => typeof(AgentApplication).IsAssignableFrom(t)).ToList();
        if (allAgents.Count == 0)
        {
            // This is to handle declaring an AgentApplication in an AddTransient lambda.
            var inlineAgent = endpoints.ServiceProvider.GetService<IAgent>()
                ?? throw new InvalidOperationException("No AgentApplications were found in the calling assembly. Ensure that at least one AgentApplication is defined.");
            allAgents.Add(inlineAgent.GetType());
        }

        foreach (var agent in allAgents)
        {
            var interfaces = agent.GetCustomAttributes<AgentInterfaceAttribute>(true)?.ToList();
            if (interfaces?.Count == 0)
            {
                if (allAgents.Count == 1)
                {
                    // If there is only one AgentApplication, we can default
                    interfaces = new List<AgentInterfaceAttribute>()
                        {
                            new(A2AAgentTransportProtocol.JsonRpc, defaultPath)
                        };
                }
                else
                {
                    throw new InvalidOperationException($"No AgentInterfaceAttribute was found on Agent '{agent.FullName}'. When multiple AgentApplications are defined, each must have at least one AgentInterfaceAttribute.");
                }
            }

            foreach (var agentInterface in interfaces)
            {
                if (agentInterface.Protocol != A2AAgentTransportProtocol.JsonRpc && agentInterface.Protocol != A2AAgentTransportProtocol.HttpJson)
                {
                    continue;
                }

                if (agentInterface.Protocol == A2AAgentTransportProtocol.JsonRpc)
                {
                    a2aGroup.MapJsonRpcMethods(agentInterface.Path);
                    a2aGroup.MapGet($"{agentInterface.Path}/.well-known/agent-card.json", (HttpRequest request, HttpResponse response, IA2AHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
                    {
                        return adapter.ProcessAgentCardAsync(request, response, agent, agentInterface.Path, cancellationToken);
                    });
                }
                else if (agentInterface.Protocol == A2AAgentTransportProtocol.HttpJson)
                {
                    a2aGroup.MapHttpMethods(agentInterface.Path);
                }
            }

            a2aGroup.MapGet(".well-known/agent-card.json", (HttpRequest request, HttpResponse response, IA2AHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
            {
                return adapter.ProcessAgentCardAsync(request, response, agent, defaultPath, cancellationToken);
            });
        }

        return a2aGroup;
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

        var a2aGroup = endpoints.MapGroup("");
        if (requireAuth)
        {
            a2aGroup.RequireAuthorization();
        }
        else
        {
            a2aGroup.AllowAnonymous();
        }

        return a2aGroup.MapJsonRpcMethods(path);
    }

    private static RouteGroupBuilder MapJsonRpcMethods(this RouteGroupBuilder routeGroup, string prefixPath = "")
    {
        routeGroup.MapPost(
            prefixPath,
            async (HttpRequest request, HttpResponse response, IA2AHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
            {
                return await adapter.ProcessJsonRpcAsync(request, response, agent, cancellationToken);
            })
            .WithMetadata(new AcceptsMetadata(["application/json"]))
            .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status200OK, contentTypes: ["text/event-stream"]))
            .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status202Accepted));

        return routeGroup;
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

        var routeGroup = endpoints.MapGroup("");
        if (requireAuth)
        {
            routeGroup.RequireAuthorization();
        }
        else
        {
            routeGroup.AllowAnonymous();
        }

        return routeGroup.MapHttpMethods(path);
    }

    private static RouteGroupBuilder MapHttpMethods(this RouteGroupBuilder routeGroup, string prefixPath = "/a2a")
    {
        // /v1/card endpoint - Agent discovery
        routeGroup.MapGet($"{prefixPath}/v1/card", async (HttpRequest request, HttpResponse response, IA2AHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
            await adapter.ProcessAgentCardAsync(request, response, agent, prefixPath, cancellationToken));

        // /v1/tasks/{id} endpoint
        routeGroup.MapGet($"{prefixPath}/v1/tasks/{{id}}", (HttpRequest request, HttpResponse response, IA2AHttpAdapter adapter, IAgent agent, string id, [FromQuery] int? historyLength, [FromQuery] string? metadata, CancellationToken cancellationToken) =>
            adapter.GetTaskAsync(request, response, agent, id, historyLength, metadata, cancellationToken));

        // /v1/tasks/{id}:cancel endpoint
        routeGroup.MapPost($"{prefixPath}/v1/tasks/{{id}}:cancel", (HttpRequest request, HttpResponse response, IA2AHttpAdapter adapter, IAgent agent, string id, CancellationToken cancellationToken) =>
            adapter.CancelTaskAsync(request, response, agent, id, cancellationToken));

        // /v1/tasks/{id}:subscribe endpoint
        routeGroup.MapGet($"{prefixPath}/v1/tasks/{{id}}:subscribe", (HttpRequest request, HttpResponse response, IA2AHttpAdapter adapter, IAgent agent, string id, CancellationToken cancellationToken) =>
            adapter.SubscribeToTask(request, response, agent, id, cancellationToken));

        // /v1/tasks/{id}/pushNotificationConfigs endpoint - POST
        routeGroup.MapPost($"{prefixPath}/v1/tasks/{{id}}/pushNotificationConfigs", (HttpRequest request, HttpResponse response, IA2AHttpAdapter adapter, IAgent agent, string id, [FromBody] PushNotificationConfig pushNotificationConfig, CancellationToken cancellationToken) =>
            adapter.SetPushNotificationAsync(request, response, agent, id, pushNotificationConfig, cancellationToken));

        // /v1/tasks/{id}/pushNotificationConfigs endpoint - GET
        routeGroup.MapGet($"{prefixPath}/v1/tasks/{{id}}/pushNotificationConfigs/{{notificationConfigId?}}", (HttpRequest request, HttpResponse response, IA2AHttpAdapter adapter, IAgent agent, string id, string? notificationConfigId, CancellationToken cancellationToken) =>
            adapter.GetPushNotificationAsync(request, response, agent, id, notificationConfigId, cancellationToken));

        // /v1/message:send endpoint
        routeGroup.MapPost($"{prefixPath}/v1/message:send", (HttpRequest request, HttpResponse response, IA2AHttpAdapter adapter, IAgent agent, [FromBody] MessageSendParams sendParams, CancellationToken cancellationToken) =>
            adapter.SendMessageAsync(request, response, agent, sendParams, cancellationToken));

        // /v1/message:stream endpoint
        routeGroup.MapPost($"{prefixPath}/v1/message:stream", (HttpRequest request, HttpResponse response, IA2AHttpAdapter adapter, IAgent agent, [FromBody] MessageSendParams sendParams, CancellationToken cancellationToken) =>
            adapter.SendMessageStream(request, response, agent, sendParams, cancellationToken));

        return routeGroup;
    }
}
