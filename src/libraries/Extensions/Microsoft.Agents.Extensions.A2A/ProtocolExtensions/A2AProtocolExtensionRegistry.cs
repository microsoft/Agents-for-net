// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using A2A.AspNetCore;
using A2A.Grpc.AspNetCore;
using Grpc.Core;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Extensions.A2A.ProtocolExtensions.InTaskAuthorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.A2A.ProtocolExtensions;

internal sealed class A2AProtocolExtensionRegistry(
    IServiceProvider services,
    IEnumerable<A2AProtocolExtensionRegistration> registrations,
    Type defaultAgentType = null)
{
    private const string MultiAgentRegistrationError =
        "A2A protocol extensions in a multi-agent application must use AddA2AProtocolExtension<TAgent, TContributor>.";
    private readonly A2AProtocolExtensionRegistration[] _registrations = registrations.ToArray();
    private readonly ConcurrentDictionary<Type, Lazy<A2AEndpointDispatch>> _dispatches = new();
    private Type _grpcAgentType;
    private Type _singleAgentType;

    internal A2AEndpointDispatch GetDispatch(Type agentType)
    {
        ValidateSingleAgentType(agentType);
        if (defaultAgentType == null
            && !_registrations.Any(registration => registration.AgentType != null)
            && _dispatches.TryGetValue(typeof(IAgent), out var defaultDispatch))
        {
            return defaultDispatch.Value;
        }
        return _dispatches.GetOrAdd(agentType, type => new Lazy<A2AEndpointDispatch>(() => BuildDispatch(type))).Value;
    }

    internal A2AEndpointDispatch GetDefaultDispatch()
    {
        var agentType = defaultAgentType ?? _singleAgentType;
        if (agentType == null && _registrations.Any(registration => registration.AgentType != null))
        {
            throw new InvalidOperationException(
                "Agent-scoped protocol extensions for a factory-registered IAgent require MapA2AApplicationEndpoints.");
        }
        return GetDispatch(agentType ?? typeof(IAgent));
    }

    internal A2AEndpointDispatch GrpcDispatch => GetDispatch(_grpcAgentType ?? defaultAgentType ?? typeof(IAgent));

    internal void SelectGrpcAgent(Type agentType)
    {
        var previous = Interlocked.CompareExchange(ref _grpcAgentType, agentType, null);
        if (previous != null && previous != agentType)
        {
            throw new InvalidOperationException("Only one A2A gRPC agent can be mapped per application.");
        }
    }

    internal void ValidateMappedAgents(IReadOnlyCollection<Type> agentTypes)
    {
        if (agentTypes.Count > 1 && _registrations.Any(registration => registration.AgentType == null))
        {
            throw new InvalidOperationException(MultiAgentRegistrationError);
        }
    }

    private void ValidateSingleAgentType(Type agentType)
    {
        if (agentType != typeof(IAgent) && _registrations.Any(registration => registration.AgentType == null))
        {
            var previous = Interlocked.CompareExchange(ref _singleAgentType, agentType, null);
            if (previous != null && previous != agentType)
            {
                throw new InvalidOperationException(MultiAgentRegistrationError);
            }
        }
    }

    private A2AEndpointDispatch BuildDispatch(Type agentType)
    {
        var builder = new A2AProtocolExtensionBuilder();
        new InTaskAuthorizationOperation().Register(builder);

        foreach (var contributorType in _registrations
            .Where(registration => registration.AgentType == null || registration.AgentType == agentType)
            .Select(registration => registration.ContributorType)
            .Distinct())
        {
            var contributor = (IA2AProtocolExtensionContributor)services.GetRequiredService(contributorType);
            contributor.Register(builder);
        }

        return builder.Build();
    }
}

internal sealed record A2AProtocolExtensionRegistration(Type AgentType, Type ContributorType);

internal sealed record A2AEndpointDispatch(
    A2ACustomOperationRegistry Registry,
    A2AJsonRpcCustomOperationBindings JsonRpcBindings,
    A2AHttpCustomOperationBindings HttpBindings,
    Func<HttpContext, A2AExtensionRegistration, CancellationToken, ValueTask<bool>> CanActivate = null)
{
    internal A2AExtensionOptions Extensions { get; } = new(Registry) { CanActivate = CanActivate };

    internal A2AGrpcExtensionOptions GrpcExtensions { get; } = new(Registry)
    {
        CanActivate = CanActivate == null
            ? null
            : (context, extension, cancellationToken) => CanActivate(context.GetHttpContext(), extension, cancellationToken),
    };
}
