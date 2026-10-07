// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using A2A.AspNetCore;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Extensions.A2A.Errors;
using Microsoft.Agents.Extensions.A2A.Pipeline;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Extensions.A2A.ProtocolExtensions.InTaskAuthorization;

[assembly: Microsoft.Agents.Builder.AgentServiceRegistrationAttribute(
    typeof(Microsoft.Agents.Extensions.A2A.Integration.A2AServiceRegistrar))]

namespace Microsoft.Agents.Extensions.A2A;

/// <summary>
/// Provides service-registration and endpoint-mapping extensions for A2A agents.
/// </summary>
public static class A2AServiceExtensions
{
    internal static A2AEndpointDispatch Dispatch { get; } = CreateDispatch();

    /// <summary>
    /// Registers the internal A2A endpoint-processing services explicitly.
    /// </summary>
    /// <remarks>
    /// <c>AddAgentCore</c> registers these services automatically. Custom hosts that do not call
    /// <c>AddAgentCore</c> can call this method directly.
    /// </remarks>
    /// <param name="services">The service collection to receive the A2A adapter services.</param>
    public static void AddA2AAdapter(this IServiceCollection services)
    {
        services.TryAddSingleton(sp => ActivatorUtilities.CreateInstance<A2AAdapter>(sp));
    }

    /// <summary>
    /// This adds HTTP endpoints for all AgentApplications defined in the calling assembly. Each
    /// AgentApplication must have been registered with <c>AddAgent&lt;TAgent&gt;</c>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder to configure.</param>
    /// <param name="requireAuth">Whether mapped endpoints require authorization. When <see langword="null"/>, uses the configured agent authorization policy.</param>
    /// <param name="defaultPath">The default A2A endpoint path for a single agent without an interface attribute.</param>
    /// <returns>The mapped endpoint group for additional configuration.</returns>
    /// <exception cref="InvalidOperationException">The calling assembly and service provider contain no <see cref="AgentApplication"/>, or an agent in a multi-agent application lacks an <see cref="AgentInterfaceAttribute"/>.</exception>
    public static IEndpointConventionBuilder MapA2AApplicationEndpoints(
        this IEndpointRouteBuilder endpoints,
        bool? requireAuth = null,
        [StringSyntax("Route")] string defaultPath = "/a2a")
    {
        requireAuth ??= endpoints.IsAgentAuthorizationConfigured();
        if (string.IsNullOrEmpty(defaultPath))
        {
            defaultPath = "/a2a";
        }

        var a2aGroup = endpoints.MapGroup("");
        if (requireAuth.Value)
        {
            a2aGroup.RequireAuthorization();
        }
        else
        {
            a2aGroup.AllowAnonymous();
        }

        var allAgents = ResolveAgentTypes(Assembly.GetCallingAssembly(), endpoints.ServiceProvider);

        foreach (var agent in allAgents)
        {
            var interfaces = ResolveAgentInterfaces(agent, allAgents.Count, defaultPath);

            foreach (var agentInterface in interfaces)
            {
                if (agentInterface.Protocol != A2AAgentTransportProtocol.JsonRpc && agentInterface.Protocol != A2AAgentTransportProtocol.HttpJson)
                {
                    continue;
                }

                if (agentInterface.Protocol == A2AAgentTransportProtocol.JsonRpc)
                {
                    a2aGroup.MapJsonRpcMethods(agentInterface.Path);
                    a2aGroup.MapGet($"{agentInterface.Path}/.well-known/agent-card.json", (HttpRequest request, HttpResponse response, [FromServices] A2AAdapter adapter, [FromServices] IAgent agent, CancellationToken cancellationToken) =>
                    {
                        return adapter.ProcessAgentCardAsync(request, response, agent, agentInterface.Path, cancellationToken);
                    });
                }
                else if (agentInterface.Protocol == A2AAgentTransportProtocol.HttpJson)
                {
                    a2aGroup.MapHttpMethods(agentInterface.Path);
                }
            }

        }

        a2aGroup.MapGet(".well-known/agent-card.json", (HttpRequest request, HttpResponse response, [FromServices] A2AAdapter adapter, [FromServices] IAgent agent, CancellationToken cancellationToken) =>
        {
            return adapter.ProcessAgentCardAsync(request, response, agent, defaultPath, cancellationToken);
        });

        return a2aGroup;
    }

    internal static List<Type> ResolveAgentTypes(Assembly callingAssembly, IServiceProvider serviceProvider)
    {
        var agents = callingAssembly.GetTypes()
            .Where(type => typeof(AgentApplication).IsAssignableFrom(type))
            .ToList();

        if (agents.Count == 0)
        {
            // This is to handle declaring an AgentApplication in an AddTransient lambda.
            var inlineAgent = serviceProvider.GetService<IAgent>()
                ?? throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(
                    ErrorHelper.AgentApplicationNotFound,
                    null);
            agents.Add(inlineAgent.GetType());
        }

        return agents;
    }

    internal static List<AgentInterfaceAttribute> ResolveAgentInterfaces(
        Type agent,
        int agentCount,
        string defaultPath)
    {
        var interfaces = agent.GetCustomAttributes<AgentInterfaceAttribute>(true).ToList();
        if (interfaces.Count == 0 && agentCount == 1)
        {
            interfaces.Add(new AgentInterfaceAttribute(A2AAgentTransportProtocol.JsonRpc, defaultPath));
        }
        else if (interfaces.Count == 0)
        {
            throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(
                ErrorHelper.AgentInterfaceMissing,
                null,
                agent.FullName);
        }

        return interfaces;
    }


    /// <summary>
    /// Maps A2A endpoints for TAgent type.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder to configure.</param>
    /// <param name="requireAuth">Whether endpoints require authorization. Defaults to <see langword="true"/>.</param>
    /// <param name="path">The route pattern. Defaults to <c>/a2a</c>.</param>
    /// <returns>An endpoint convention builder for further configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty.</exception>
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
        var operationGroup = routeGroup.MapGroup("");
        operationGroup.AddEndpointFilter(UseRequestContextAsync);
        operationGroup.MapA2A(
            A2AAdapter.RequestHandlerProxy,
            prefixPath,
            Dispatch.Registry,
            Dispatch.JsonRpcBindings)
            .WithMetadata(new AcceptsMetadata(["application/json"]))
            .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status200OK, contentTypes: ["text/event-stream"]))
            .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status202Accepted));

        return routeGroup;
    }

    private static async ValueTask<object?> UseRequestContextAsync(
        EndpointFilterInvocationContext invocationContext,
        EndpointFilterDelegate next)
    {
        var adapter = invocationContext.HttpContext.RequestServices.GetRequiredService<A2AAdapter>();
        var agent = invocationContext.HttpContext.RequestServices.GetRequiredService<IAgent>();
        using var scope = adapter.BeginRequest(invocationContext.HttpContext, agent);
        return await next(invocationContext).ConfigureAwait(false);
    }

    private static A2AEndpointDispatch CreateDispatch()
    {
        var operationBuilder = new A2ACustomOperationRegistryBuilder();
        var authResponse = InTaskAuthorizationOperation.AddOperation(operationBuilder);
        var registry = operationBuilder.Build();

        var jsonRpcBuilder = new A2AJsonRpcCustomOperationBuilder();
        InTaskAuthorizationOperation.AddJsonRpcBinding(jsonRpcBuilder, authResponse);

        var httpBuilder = new A2AHttpCustomOperationBuilder();
        InTaskAuthorizationOperation.AddHttpBinding(httpBuilder, authResponse);

        return new A2AEndpointDispatch(
            registry,
            jsonRpcBuilder.Build(registry),
            httpBuilder.Build(registry));
    }

    /// <summary>
    /// Enables HTTP A2A endpoints for the specified path.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder to configure.</param>
    /// <param name="requireAuth">Whether endpoints require authorization. Defaults to <see langword="false"/>.</param>
    /// <param name="path">The base path for the HTTP A2A endpoints.</param>
    /// <returns>An endpoint convention builder for further configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty.</exception>
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
        routeGroup.MapGet(
            $"{prefixPath}/card",
            async (HttpRequest request, HttpResponse response, [FromServices] A2AAdapter adapter, [FromServices] IAgent agent, CancellationToken cancellationToken) =>
                await adapter.ProcessAgentCardAsync(request, response, agent, prefixPath, cancellationToken));
        var operationGroup = routeGroup.MapGroup("");
        operationGroup.AddEndpointFilter(UseRequestContextAsync);
        operationGroup.MapHttpA2A(
            A2AAdapter.RequestHandlerProxy,
            prefixPath,
            Dispatch.Registry,
            Dispatch.HttpBindings);

        return routeGroup;
    }
}

internal sealed record A2AEndpointDispatch(
    A2ACustomOperationRegistry Registry,
    A2AJsonRpcCustomOperationBindings JsonRpcBindings,
    A2AHttpCustomOperationBindings HttpBindings);
