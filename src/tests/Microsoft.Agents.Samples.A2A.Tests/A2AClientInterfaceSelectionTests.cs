// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using A2A;
using Microsoft.Agents.Samples.A2AClient;
using Xunit;

namespace Microsoft.Agents.Samples.A2A.Tests;

/// <summary>
/// Agent Card interface selection must reject anything that is not on the configured agent origin,
/// because the same authenticated <see cref="HttpClient"/> is reused for card and task operations.
/// </summary>
public class A2AClientInterfaceSelectionTests
{
    private static readonly Uri AgentUrl = new("https://agent.example/a2a");

    [Fact]
    public void CreateClient_SameOriginJsonRpcInterface_IsSelected()
    {
        AgentCard card = CreateCard((ProtocolBindingNames.JsonRpc, "https://agent.example/a2a/"));
        using var httpClient = CreateHttpClient();

        IA2AClient client = Program.CreateClient(card, httpClient, AgentUrl);

        Assert.IsType<global::A2A.A2AClient>(client);
    }

    [Fact]
    public void CreateClient_SameOriginHttpJsonInterface_IsSelected()
    {
        AgentCard card = CreateCard((ProtocolBindingNames.HttpJson, "https://agent.example/a2a/"));
        using var httpClient = CreateHttpClient();

        IA2AClient client = Program.CreateClient(card, httpClient, AgentUrl);

        Assert.IsType<A2AHttpJsonClient>(client);
    }

    [Fact]
    public void CreateClient_CrossOriginInterface_Throws()
    {
        AgentCard card = CreateCard((ProtocolBindingNames.JsonRpc, "https://attacker.example/a2a/"));
        using var httpClient = CreateHttpClient();

        var exception = Assert.Throws<InvalidOperationException>(
            () => Program.CreateClient(card, httpClient, AgentUrl));

        Assert.Contains("agent origin", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateClient_DifferentPortInterface_Throws()
    {
        AgentCard card = CreateCard((ProtocolBindingNames.JsonRpc, "https://agent.example:8443/a2a/"));
        using var httpClient = CreateHttpClient();

        Assert.Throws<InvalidOperationException>(() => Program.CreateClient(card, httpClient, AgentUrl));
    }

    [Fact]
    public void CreateClient_PrefersSameOriginOverAdvertisedForeignInterface()
    {
        AgentCard card = CreateCard(
            (ProtocolBindingNames.JsonRpc, "https://attacker.example/a2a/"),
            (ProtocolBindingNames.JsonRpc, "https://agent.example/a2a/"));
        using var httpClient = CreateHttpClient();

        IA2AClient client = Program.CreateClient(card, httpClient, AgentUrl);

        Assert.IsType<global::A2A.A2AClient>(client);
    }

    [Fact]
    public void CreateClient_RelativeInterfaceUrl_Throws()
    {
        AgentCard card = CreateCard((ProtocolBindingNames.JsonRpc, "/a2a/"));
        using var httpClient = CreateHttpClient();

        Assert.Throws<InvalidOperationException>(() => Program.CreateClient(card, httpClient, AgentUrl));
    }

    [Fact]
    public void CreateClient_NoSupportedBinding_Throws()
    {
        AgentCard card = CreateCard((ProtocolBindingNames.Grpc, "https://agent.example/a2a/"));
        using var httpClient = CreateHttpClient();

        var exception = Assert.Throws<InvalidOperationException>(
            () => Program.CreateClient(card, httpClient, AgentUrl));

        Assert.Contains("JSON-RPC", exception.Message, StringComparison.Ordinal);
    }

    private static AgentCard CreateCard(params (string Binding, string Url)[] interfaces)
    {
        var card = new AgentCard { SupportedInterfaces = [] };
        foreach ((string binding, string url) in interfaces)
        {
            card.SupportedInterfaces.Add(new AgentInterface { ProtocolBinding = binding, Url = url });
        }

        return card;
    }

    private static HttpClient CreateHttpClient() => new(new StubHandler());

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
