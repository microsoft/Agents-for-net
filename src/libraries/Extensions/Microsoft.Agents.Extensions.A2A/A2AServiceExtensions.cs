// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using A2A.AspNetCore;
using A2A.Grpc.AspNetCore;
using Grpc.AspNetCore.Server;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Extensions.A2A.Errors;
using Microsoft.Agents.Extensions.A2A.Hosting;
using Microsoft.Agents.Extensions.A2A.Pipeline;
using Microsoft.Agents.Extensions.A2A.ProtocolExtensions;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

[assembly: Microsoft.Agents.Builder.AgentServiceRegistrationAttribute(
    typeof(Microsoft.Agents.Extensions.A2A.Integration.A2AServiceRegistrar))]

namespace Microsoft.Agents.Extensions.A2A;

/// <summary>
/// Provides service-registration and endpoint-mapping extensions for A2A agents.
/// </summary>
public static partial class A2AServiceExtensions
{
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
        AddProtocolExtensionRegistry(services);
        if (services.Any(service => service.ServiceType == typeof(A2AAdapter)))
        {
            return;
        }

        services.AddA2AAgent<A2AAdapter.AgentHandlerProxy>(new global::A2A.AgentCard
        {
            Capabilities = new AgentCapabilities { Streaming = true, PushNotifications = true },
            DefaultInputModes = ["application/json"],
            DefaultOutputModes = ["application/json"],
            Skills = [],
        });
        services.TryAddSingleton(sp => new A2AAdapter(
            sp.GetRequiredService<ITaskStore>(),
            sp.GetRequiredService<ILoggerFactory>(),
            sp.GetRequiredService<ChannelEventNotifier>(),
            sp.GetService<A2AAdapterOptions>(),
            sp.GetService<IConfiguration>(),
            (A2AServer)sp.GetRequiredService<IA2ARequestHandler>(),
            sp.GetRequiredService<A2AProtocolExtensionRegistry>()));
        services.TryAddSingleton<A2AGrpcRequestInterceptor>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IStartupFilter, A2AEndpointValidationStartupFilter>());
        services.AddA2AGrpc();
        services.TryAddSingleton(sp => sp.GetRequiredService<A2AProtocolExtensionRegistry>().GrpcDispatch.Registry);
        services.Configure<GrpcServiceOptions>(options =>
        {
            if (!options.Interceptors.Any(registration => registration.Type == typeof(A2AGrpcRequestInterceptor)))
            {
                options.Interceptors.Add<A2AGrpcRequestInterceptor>();
            }
        });
    }

    /// <summary>
    /// Adds declared A2A transport endpoints for all AgentApplications defined in the calling assembly. Each
    /// AgentApplication must have been registered with <c>AddAgent&lt;TAgent&gt;</c>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder to configure.</param>
    /// <param name="requireAuth">Whether mapped endpoints require authorization. When <see langword="null"/>, uses the configured agent authorization policy.</param>
    /// <param name="defaultPath">The default A2A endpoint path for a single agent without an interface attribute.</param>
    /// <returns>The mapped endpoint group for additional configuration.</returns>
    /// <remarks>
    /// Native gRPC interfaces must declare <c>/</c> and cannot be mounted inside a prefixed route group.
    /// Only one gRPC agent can be mapped per application. Public HTTP Agent Card discovery remains available.
    /// </remarks>
    /// <exception cref="System.InvalidOperationException">No <see cref="Microsoft.Agents.Builder.App.AgentApplication"/> is available, a multi-agent application lacks a <see cref="Microsoft.Agents.Builder.App.AgentInterfaceAttribute"/>, or gRPC interfaces compete for the root or declare a prefix.</exception>
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
        var extensions = ResolveProtocolExtensions(endpoints.ServiceProvider);
        extensions.ValidateMappedAgents(allAgents);
        var agentInterfaces = allAgents.Select(agent => (
            Agent: agent,
            Interfaces: ResolveAgentInterfaces(agent, allAgents.Count, defaultPath))).ToArray();
        var grpcInterfaces = agentInterfaces.SelectMany(agent => agent.Interfaces
            .Where(agentInterface => agentInterface.Protocol == A2AAgentTransportProtocol.Grpc)
            .Select(agentInterface => (agent.Agent, Interface: agentInterface))).ToArray();
        if (grpcInterfaces.Any(agent => agent.Interface.Path != "/"))
        {
            throw new InvalidOperationException(
                "A2A gRPC interfaces must use the root path '/'. Path prefixes are not supported.");
        }
        if (grpcInterfaces.Length > 1)
        {
            throw new InvalidOperationException(
                "Only one A2A gRPC interface can be mapped at the application root. Use separate applications for multiple gRPC agents.");
        }
        if (grpcInterfaces.Length == 1)
        {
            extensions.SelectGrpcAgent(grpcInterfaces[0].Agent);
            A2AGrpcExtensionOptionsSetup.Configure(
                endpoints.ServiceProvider, extensions.GetDispatch(grpcInterfaces[0].Agent));
        }

        foreach (var (agentType, interfaces) in agentInterfaces)
        {
            var dispatch = extensions.GetDispatch(agentType);
            foreach (var agentInterface in interfaces)
            {
                if (agentInterface.Protocol == A2AAgentTransportProtocol.JsonRpc)
                {
                    a2aGroup.MapJsonRpcMethods(agentInterface.Path, dispatch, agentType);
                    a2aGroup.MapGet($"{agentInterface.Path}/.well-known/agent-card.json", (HttpRequest request, HttpResponse response, [FromServices] A2AAdapter adapter, CancellationToken cancellationToken) =>
                    {
                        var agent = A2AGrpcEndpointMetadata.ResolveAgent(request.HttpContext.RequestServices, agentType);
                        return adapter.ProcessAgentCardAsync(request, response, agent, agentInterface.Path, cancellationToken);
                    }).AllowAnonymous().WithMetadata(new A2AAgentCardEndpointMetadata());
                }
                else if (agentInterface.Protocol == A2AAgentTransportProtocol.HttpJson)
                {
                    a2aGroup.MapHttpMethods(agentInterface.Path, dispatch, agentType);
                }
                else if (agentInterface.Protocol == A2AAgentTransportProtocol.Grpc)
                {
                    var metadata = new A2AGrpcEndpointMetadata(agentType);
                    a2aGroup.MapGrpcA2A().WithMetadata(metadata);
                    a2aGroup.MapGrpcA2ACustomOperations().WithMetadata(metadata);
                }
            }

        }

        var discoveryAgent = grpcInterfaces.SingleOrDefault().Agent ?? allAgents[0];
        a2aGroup.MapGet(".well-known/agent-card.json", (HttpRequest request, HttpResponse response, [FromServices] A2AAdapter adapter, CancellationToken cancellationToken) =>
        {
            var agent = A2AGrpcEndpointMetadata.ResolveAgent(request.HttpContext.RequestServices, discoveryAgent);
            return adapter.ProcessAgentCardAsync(request, response, agent, defaultPath, cancellationToken);
        }).AllowAnonymous().WithMetadata(new A2AAgentCardEndpointMetadata());

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

        return a2aGroup.MapJsonRpcMethods(path, ResolveDefaultDispatch(endpoints.ServiceProvider));
    }

    private static RouteGroupBuilder MapJsonRpcMethods(
        this RouteGroupBuilder routeGroup,
        string prefixPath,
        A2AEndpointDispatch dispatch,
        Type agentType = null)
    {
        var operationGroup = routeGroup.MapGroup("");
        operationGroup.AddEndpointFilter((context, next) => UseRequestContextAsync(context, next, agentType));
        operationGroup.MapA2A(
            A2AAdapter.RequestHandlerProxy,
            prefixPath,
            dispatch.Extensions,
            dispatch.JsonRpcBindings)
            .WithMetadata(new AcceptsMetadata(["application/json"]))
            .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status200OK, contentTypes: ["text/event-stream"]))
            .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status202Accepted));

        return routeGroup;
    }

    private static async ValueTask<object?> UseRequestContextAsync(
        EndpointFilterInvocationContext invocationContext,
        EndpointFilterDelegate next,
        Type agentType)
    {
        var adapter = invocationContext.HttpContext.RequestServices.GetRequiredService<A2AAdapter>();
        var agent = A2AGrpcEndpointMetadata.ResolveAgent(invocationContext.HttpContext.RequestServices, agentType);
        var scope = adapter.BeginRequest(invocationContext.HttpContext, agent);
        try
        {
            var result = await next(invocationContext).ConfigureAwait(false);
            if (result is IResult httpResult)
            {
                return new A2AAdapter.RequestScopedResult(httpResult, scope);
            }
            scope.Dispose();
            return result;
        }
        catch
        {
            scope.Dispose();
            throw;
        }
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

        return routeGroup.MapHttpMethods(path, ResolveDefaultDispatch(endpoints.ServiceProvider));
    }

    private static RouteGroupBuilder MapHttpMethods(
        this RouteGroupBuilder routeGroup,
        string prefixPath,
        A2AEndpointDispatch dispatch,
        Type agentType = null)
    {
        routeGroup.MapGet(
            $"{prefixPath}/card",
            async (HttpRequest request, HttpResponse response, [FromServices] A2AAdapter adapter, CancellationToken cancellationToken) =>
            {
                var agent = A2AGrpcEndpointMetadata.ResolveAgent(request.HttpContext.RequestServices, agentType);
                await adapter.ProcessAgentCardAsync(request, response, agent, prefixPath, cancellationToken);
            })
            .AllowAnonymous()
            .WithMetadata(new A2AAgentCardEndpointMetadata());
        var operationGroup = routeGroup.MapGroup("");
        operationGroup.AddEndpointFilter((context, next) => UseRequestContextAsync(context, next, agentType));
        operationGroup.MapHttpA2A(
            A2AAdapter.RequestHandlerProxy,
            prefixPath,
            dispatch.Extensions,
            dispatch.HttpBindings);

        return routeGroup;
    }

    private static A2AProtocolExtensionRegistry ResolveProtocolExtensions(IServiceProvider services) =>
        services.GetService<A2AProtocolExtensionRegistry>()
            ?? new A2AProtocolExtensionRegistry(services, services.GetServices<A2AProtocolExtensionRegistration>());

    private static A2AEndpointDispatch ResolveDefaultDispatch(IServiceProvider services) =>
        ResolveProtocolExtensions(services).GetDefaultDispatch();
}
