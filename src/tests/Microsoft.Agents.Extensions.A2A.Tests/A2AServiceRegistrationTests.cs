// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A.AspNetCore;
using Microsoft.Agents.Builder.Adapters;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.A2A.Pipeline;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.A2A.Tests;

public class A2AServiceRegistrationTests
{
    [Fact]
    public void AddAgentCore_RegistersA2AAdapterFromExtensionManifest()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IStorage, MemoryStorage>();

        services.AddAgentCore<CloudAdapter>();

        using var provider = services.BuildServiceProvider();
        var concreteAdapter = provider.GetRequiredService<A2AAdapter>();
        var channelAdapter = provider
            .GetRequiredService<IChannelAdapterRegistry>()
            .GetAdapter(Channels.A2A);

        Assert.Same(concreteAdapter, channelAdapter);
    }

    [Fact]
    public async Task EndpointOverloads_MapStandardAndResumeAuthThroughUpstreamBindings()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddLogging();

        await using var app = builder.Build();
        app.MapA2AJsonRpc(requireAuth: false, path: "/json-rpc");
        app.MapA2AHttp(requireAuth: false, path: "/http-json");

        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToArray();

        Assert.Contains("/json-rpc", routes);
        Assert.Contains("/http-json/message:send", routes);
        Assert.Contains("/http-json/tasks/{taskId}:resumeAuth", routes);
        Assert.Equal("A2A.AspNetCore", typeof(A2AJsonRpcProcessor).Assembly.GetName().Name);
    }
}
