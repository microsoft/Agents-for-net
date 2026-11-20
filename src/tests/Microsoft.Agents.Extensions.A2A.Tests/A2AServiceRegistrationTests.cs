// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A.AspNetCore;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.Adapters;
using Microsoft.Agents.Builder.App;
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
    public async Task AddAgentCore_RegistersA2AAdapterFromExtensionManifest()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IStorage, MemoryStorage>();

        services.AddAgentCore<CloudAdapter>();

        await using var provider = services.BuildServiceProvider();
        var concreteAdapter = provider.GetRequiredService<A2AAdapter>();
        var channelAdapter = provider
            .GetRequiredService<IChannelAdapterRegistry>()
            .GetAdapter(Channels.A2A);

        Assert.Same(concreteAdapter, channelAdapter);
    }

    [Fact]
    public async Task EndpointOverloads_MapStandardAndAuthResponseThroughUpstreamBindings()
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
        Assert.Contains("/http-json/tasks/{taskId}:auth-response", routes);
        Assert.Equal("A2A.AspNetCore", typeof(A2AJsonRpcProcessor).Assembly.GetName().Name);
    }

    [Theory]
    [InlineData(false, "none")]
    [InlineData(true, "none")]
    [InlineData(false, "single-agent")]
    [InlineData(true, "single-agent")]
    [InlineData(false, "agent-scoped")]
    [InlineData(true, "agent-scoped")]
    public async Task EndpointOverloads_DoNotConstructRequestScopedAgentsDuringMapping(bool httpJson, string registration)
    {
        var builder = WebApplication.CreateBuilder();
        var tracker = new AgentConstructionTracker();
        builder.Services.AddSingleton(tracker);
        builder.Services.AddScoped<AsyncOnlyDependency>();
        builder.Services.AddTransient<IAgent, AsyncDependencyAgent>();
        if (registration == "single-agent")
        {
            builder.Services.AddA2AProtocolExtension<A2AProtocolExtensionRegistrationTests.OperationContributor>();
        }
        else if (registration == "agent-scoped")
        {
            builder.Services.AddA2AProtocolExtension<AsyncDependencyAgent, A2AProtocolExtensionRegistrationTests.OperationContributor>();
        }
        await using var app = builder.Build();

        if (httpJson)
        {
            app.MapA2AHttp(requireAuth: false, path: "/http");
        }
        else
        {
            app.MapA2AJsonRpc(requireAuth: false, path: "/rpc");
        }

        Assert.Equal(0, tracker.Constructions);
    }

    public sealed class AgentConstructionTracker
    {
        public int Constructions { get; set; }
    }

    public sealed class AsyncOnlyDependency : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    public sealed class AsyncDependencyAgent : AgentApplication
    {
        public AsyncDependencyAgent(AgentConstructionTracker tracker, AsyncOnlyDependency dependency)
            : base(new AgentApplicationOptions(new MemoryStorage()))
        {
            ArgumentNullException.ThrowIfNull(dependency);
            tracker.Constructions++;
        }
    }
}
