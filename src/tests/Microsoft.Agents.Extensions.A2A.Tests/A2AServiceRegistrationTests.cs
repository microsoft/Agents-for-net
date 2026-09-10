// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Adapters;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.DependencyInjection;

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
        var httpAdapter = provider.GetRequiredService<IA2AHttpAdapter>();
        var channelAdapter = provider
            .GetRequiredService<IChannelAdapterRegistry>()
            .GetAdapter(Channels.A2A);

        Assert.Same(concreteAdapter, httpAdapter);
        Assert.Same(concreteAdapter, channelAdapter);
    }
}
