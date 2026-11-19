// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Grpc.AspNetCore.Server;
using Microsoft.Agents.Extensions.A2A.Pipeline;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace Microsoft.Agents.Extensions.A2A.Hosting;

internal sealed class A2AEndpointValidationStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);

        var routes = app.ApplicationServices.GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>().ToArray();
        var grpcRoutes = routes.Where(endpoint =>
            endpoint.Metadata.GetMetadata<A2AGrpcEndpointMetadata>() != null
            && endpoint.Metadata.GetMetadata<GrpcMethodMetadata>() != null).ToArray();

        // Inspect fully composed route patterns so nested route groups cannot hide a prefix.
        foreach (var route in grpcRoutes)
        {
            var method = route.Metadata.GetMetadata<GrpcMethodMetadata>().Method;
            if (!string.Equals(route.RoutePattern.RawText, method.FullName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "A2A gRPC services must be mapped at the application root, not inside a prefixed route group.");
            }

            // Leading and trailing template slashes do not distinguish ASP.NET Core routes.
            if (routes.Count(candidate =>
                string.Equals(candidate.RoutePattern.RawText?.Trim('/'), route.RoutePattern.RawText.Trim('/'), StringComparison.OrdinalIgnoreCase)
                && (candidate.Metadata.GetMetadata<IHttpMethodMetadata>() is not { } methods
                    || methods.HttpMethods.Contains(HttpMethods.Post, StringComparer.OrdinalIgnoreCase))) > 1)
            {
                throw new InvalidOperationException(
                    $"A competing A2A gRPC mapping exists at the application root for '{method.FullName}'. Map the gRPC interface only once.");
            }
        }

        foreach (var route in routes.Where(endpoint =>
            endpoint.Metadata.GetMetadata<A2AAgentCardEndpointMetadata>() != null))
        {
            if (routes.Count(candidate =>
                string.Equals(candidate.RoutePattern.RawText?.Trim('/'), route.RoutePattern.RawText?.Trim('/'), StringComparison.OrdinalIgnoreCase)
                && (candidate.Metadata.GetMetadata<IHttpMethodMetadata>() is not { } methods
                    || methods.HttpMethods.Count == 0
                    || methods.HttpMethods.Contains(HttpMethods.Get, StringComparer.OrdinalIgnoreCase))) > 1)
            {
                throw new InvalidOperationException(
                    $"A competing A2A Agent Card endpoint is mapped for 'GET {route.RoutePattern.RawText}'. Protocol extensions and application routes cannot replace SDK-owned discovery endpoints.");
            }
        }

        if (grpcRoutes.Length > 0)
        {
            _ = app.ApplicationServices.GetRequiredService<A2AAdapter>();
        }
    };
}

internal sealed record A2AAgentCardEndpointMetadata;
