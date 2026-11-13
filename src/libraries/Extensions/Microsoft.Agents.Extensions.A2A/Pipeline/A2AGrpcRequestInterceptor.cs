// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Agents.Extensions.A2A.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.A2A.Pipeline;

internal sealed class A2AGrpcRequestInterceptor : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        using var scope = BeginRequest(context);
        return await continuation(request, context).ConfigureAwait(false);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        using var scope = BeginRequest(context);
        await continuation(request, responseStream, context).ConfigureAwait(false);
    }

    private static IDisposable BeginRequest(ServerCallContext context)
    {
        var httpContext = context.GetHttpContext();
        var metadata = httpContext.GetEndpoint()?.Metadata.GetMetadata<A2AGrpcEndpointMetadata>();
        if (metadata == null)
        {
            return null;
        }

        var services = httpContext.RequestServices;
        var agent = A2AGrpcEndpointMetadata.ResolveAgent(services, metadata.AgentType);
        return services.GetRequiredService<A2AAdapter>().BeginRequest(httpContext, agent);
    }
}
