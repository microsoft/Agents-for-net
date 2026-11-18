// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using A2A.AspNetCore;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.A2A.ProtocolExtensions;

/// <summary>
/// Adds A2A protocol extensions and their transport bindings to a mapped agent.
/// </summary>
/// <remarks>
/// The SDK finalizes the registry after every contributor has run. Registrations are additive:
/// duplicate extension identifiers, operation identifiers, or transport bindings are rejected.
/// </remarks>
public sealed class A2AProtocolExtensionBuilder
{
    private readonly Dictionary<string, Func<HttpContext, CancellationToken, ValueTask<bool>>> _availability = new(StringComparer.Ordinal);

    internal A2AProtocolExtensionBuilder()
    {
    }

    internal A2ACustomOperationRegistryBuilder Operations { get; } = new();

    /// <summary>
    /// Gets the builder for JSON-RPC bindings of registered operations.
    /// </summary>
    public A2AJsonRpcCustomOperationBuilder JsonRpc { get; } = new();

    /// <summary>
    /// Gets the builder for HTTP+JSON bindings of registered operations.
    /// </summary>
    public A2AHttpCustomOperationBuilder Http { get; } = new();

    /// <summary>
    /// Registers an immutable support declaration, optionally with a request availability policy.
    /// </summary>
    /// <param name="extension">The declaration, including its URI, description, required flag, and JSON parameters.</param>
    /// <param name="canActivate">An optional policy evaluated once per request when the client requests this extension.
    /// The HTTP context is available for all transports, including native gRPC. This is not an authorization grant.</param>
    /// <returns>The registry-owned handle used to associate operations with this extension.</returns>
    public A2AExtensionRegistration RegisterExtension(
        global::A2A.AgentExtension extension,
        Func<HttpContext, CancellationToken, ValueTask<bool>>? canActivate = null)
    {
        var registration = Operations.RegisterExtension(extension);
        if (canActivate != null)
        {
            _availability.Add(registration.Uri, canActivate);
        }
        return registration;
    }

    /// <summary>
    /// Registers a typed unary operation that requires its owning extension to be active.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="extension">An extension registered by this builder.</param>
    /// <param name="id">The stable operation identifier, also used by the generic gRPC service.</param>
    /// <param name="handler">The typed operation handler.</param>
    /// <param name="requestTypeInfo">Source-generated request JSON metadata.</param>
    /// <param name="resultTypeInfo">Source-generated result JSON metadata.</param>
    /// <param name="validator">An optional semantic request validator.</param>
    /// <returns>The operation handle to bind to JSON-RPC or HTTP+JSON.</returns>
    public A2ACustomOperation<TRequest, TResult> Map<TRequest, TResult>(
        A2AExtensionRegistration extension,
        A2AOperationId id,
        A2ACustomOperationHandler<TRequest, TResult> handler,
        JsonTypeInfo<TRequest> requestTypeInfo,
        JsonTypeInfo<TResult> resultTypeInfo,
        A2ACustomOperationValidator<TRequest>? validator = null)
        => Operations.Map(extension, id, handler, requestTypeInfo, resultTypeInfo, validator);

    /// <summary>
    /// Registers an extension-owned unary operation with access to request-scoped transport features.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="extension">An extension registered by this builder.</param>
    /// <param name="id">The stable operation identifier, also used by the generic gRPC service.</param>
    /// <param name="handler">The contextual operation handler.</param>
    /// <param name="requestTypeInfo">Source-generated request JSON metadata.</param>
    /// <param name="resultTypeInfo">Source-generated result JSON metadata.</param>
    /// <param name="validator">An optional semantic request validator.</param>
    /// <returns>The operation handle to bind to JSON-RPC or HTTP+JSON.</returns>
    public A2ACustomOperation<TRequest, TResult> Map<TRequest, TResult>(
        A2AExtensionRegistration extension,
        A2AOperationId id,
        A2AContextualCustomOperationHandler<TRequest, TResult> handler,
        JsonTypeInfo<TRequest> requestTypeInfo,
        JsonTypeInfo<TResult> resultTypeInfo,
        A2ACustomOperationValidator<TRequest>? validator = null)
        => Operations.Map(extension, id, handler, requestTypeInfo, resultTypeInfo, validator);

    /// <summary>
    /// Registers a typed server-streaming operation that requires its owning extension to be active.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TEvent">The streamed event type.</typeparam>
    /// <param name="extension">An extension registered by this builder.</param>
    /// <param name="id">The stable operation identifier, also used by the generic gRPC service.</param>
    /// <param name="handler">The typed streaming handler.</param>
    /// <param name="requestTypeInfo">Source-generated request JSON metadata.</param>
    /// <param name="eventTypeInfo">Source-generated event JSON metadata.</param>
    /// <param name="validator">An optional semantic request validator.</param>
    /// <returns>The operation handle to bind to JSON-RPC or HTTP+JSON.</returns>
    public A2AStreamingCustomOperation<TRequest, TEvent> MapStreaming<TRequest, TEvent>(
        A2AExtensionRegistration extension,
        A2AOperationId id,
        A2AStreamingCustomOperationHandler<TRequest, TEvent> handler,
        JsonTypeInfo<TRequest> requestTypeInfo,
        JsonTypeInfo<TEvent> eventTypeInfo,
        A2ACustomOperationValidator<TRequest>? validator = null)
        => Operations.MapStreaming(extension, id, handler, requestTypeInfo, eventTypeInfo, validator);

    /// <summary>
    /// Registers an extension-owned server-streaming operation with access to request-scoped transport features.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TEvent">The streamed event type.</typeparam>
    /// <param name="extension">An extension registered by this builder.</param>
    /// <param name="id">The stable operation identifier, also used by the generic gRPC service.</param>
    /// <param name="handler">The contextual streaming handler.</param>
    /// <param name="requestTypeInfo">Source-generated request JSON metadata.</param>
    /// <param name="eventTypeInfo">Source-generated event JSON metadata.</param>
    /// <param name="validator">An optional semantic request validator.</param>
    /// <returns>The operation handle to bind to JSON-RPC or HTTP+JSON.</returns>
    public A2AStreamingCustomOperation<TRequest, TEvent> MapStreaming<TRequest, TEvent>(
        A2AExtensionRegistration extension,
        A2AOperationId id,
        A2AContextualStreamingCustomOperationHandler<TRequest, TEvent> handler,
        JsonTypeInfo<TRequest> requestTypeInfo,
        JsonTypeInfo<TEvent> eventTypeInfo,
        A2ACustomOperationValidator<TRequest>? validator = null)
        => Operations.MapStreaming(extension, id, handler, requestTypeInfo, eventTypeInfo, validator);

    internal A2AEndpointDispatch Build()
    {
        var registry = Operations.Build();
        return new A2AEndpointDispatch(
            registry,
            JsonRpc.Build(registry),
            Http.Build(registry),
            _availability.Count == 0 ? null : CanActivateAsync);
    }

    private ValueTask<bool> CanActivateAsync(
        HttpContext context,
        A2AExtensionRegistration extension,
        CancellationToken cancellationToken)
        => _availability.TryGetValue(extension.Uri, out var policy)
            ? policy(context, cancellationToken)
            : ValueTask.FromResult(true);
}
