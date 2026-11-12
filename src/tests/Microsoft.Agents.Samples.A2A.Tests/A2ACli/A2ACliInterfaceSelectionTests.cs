// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using A2A;
using Microsoft.Agents.Samples.A2ACli;
using Microsoft.Agents.Samples.A2ACli.A2A;
using Xunit;

namespace Microsoft.Agents.Samples.A2ACli.Tests;

/// <summary>
/// Agent Card interface selection must reject anything that is not on the configured agent origin,
/// because the same authenticated <see cref="HttpClient"/> is reused for card and task operations.
/// </summary>
public class A2ACliInterfaceSelectionTests
{
    private static readonly Uri AgentUrl = new("https://agent.example/a2a");

    [Fact]
    public void CreateClient_SameOriginJsonRpcInterface_IsSelected()
    {
        AgentCard card = CreateCard((ProtocolBindingNames.JsonRpc, "https://agent.example/a2a/"));
        using var httpClient = CreateHttpClient();

        using A2AClientConnection connection = Program.CreateClient(card, httpClient, AgentUrl);

        Assert.IsType<global::A2A.A2AClient>(connection.Client);
    }

    [Fact]
    public void CreateClient_SameOriginHttpJsonInterface_IsSelected()
    {
        AgentCard card = CreateCard((ProtocolBindingNames.HttpJson, "https://agent.example/a2a/"));
        using var httpClient = CreateHttpClient();

        using A2AClientConnection connection = Program.CreateClient(card, httpClient, AgentUrl);

        Assert.IsType<A2AHttpJsonClient>(connection.Client);
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

        using A2AClientConnection connection = Program.CreateClient(card, httpClient, AgentUrl);

        Assert.IsType<global::A2A.A2AClient>(connection.Client);
    }

    [Fact]
    public void CreateClient_RelativeInterfaceUrl_Throws()
    {
        AgentCard card = CreateCard((ProtocolBindingNames.JsonRpc, "/a2a/"));
        using var httpClient = CreateHttpClient();

        Assert.Throws<InvalidOperationException>(() => Program.CreateClient(card, httpClient, AgentUrl));
    }

    [Theory]
    [InlineData("https://agent.example/")]
    [InlineData("https://agent.example:443")]
    [InlineData("agent.example:443")]
    public void CreateClient_GrpcOnlyCard_IsSelected(string target)
    {
        AgentCard card = CreateCard((ProtocolBindingNames.Grpc, target));
        using var httpClient = CreateHttpClient();

        using A2AClientConnection connection = Program.CreateClient(card, httpClient, AgentUrl);

        Assert.IsType<global::A2A.Grpc.A2AGrpcClient>(connection.Client);
    }

    [Fact]
    public void CreateClient_AutoSelection_PreservesCardOrder()
    {
        AgentCard card = CreateCard(
            (ProtocolBindingNames.Grpc, "https://agent.example/"),
            (ProtocolBindingNames.JsonRpc, "https://agent.example/a2a/"));
        using var httpClient = CreateHttpClient();

        using A2AClientConnection connection = Program.CreateClient(card, httpClient, AgentUrl);

        Assert.IsType<global::A2A.Grpc.A2AGrpcClient>(connection.Client);
    }

    [Theory]
    [InlineData("https://agent.example/a2a")]
    [InlineData("https://agent.example/?ignored=true")]
    [InlineData("https://agent.example/#ignored")]
    [InlineData("https://user@agent.example/")]
    public void CreateClient_GrpcTargetWithUnsupportedPrefix_ExplainsRootRequirement(string target)
    {
        AgentCard card = CreateCard((ProtocolBindingNames.Grpc, target));
        using var httpClient = CreateHttpClient();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => Program.CreateClient(card, httpClient, AgentUrl));

        Assert.Contains("root", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("jsonrpc", "JSONRPC")]
    [InlineData("http-json", "HTTP+JSON")]
    [InlineData("grpc", "GRPC")]
    public void CreateClient_ExplicitTransport_DoesNotUseEarlierBindings(string transport, string expectedBinding)
    {
        AgentCard card = CreateCard(
            (ProtocolBindingNames.HttpJson, "https://agent.example/a2a/"),
            (ProtocolBindingNames.Grpc, "agent.example:443"),
            (ProtocolBindingNames.JsonRpc, "https://agent.example/a2a/"));
        using var httpClient = CreateHttpClient();

        A2ATransport selection = Program.ParseStartupOptions(["--transport", transport]).Transport!.Value;
        using A2AClientConnection connection = Program.CreateClient(card, httpClient, AgentUrl, selection);

        Assert.Equal(expectedBinding, connection.ProtocolBinding);
    }

    [Fact]
    public void CreateClient_ExplicitUnavailableTransport_DoesNotFallback()
    {
        AgentCard card = CreateCard((ProtocolBindingNames.JsonRpc, "https://agent.example/a2a/"));
        using var httpClient = CreateHttpClient();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => Program.CreateClient(card, httpClient, AgentUrl, A2ATransport.Grpc));

        Assert.Contains("requested", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://foreign.example/")]
    [InlineData("agent.example:8443")]
    [InlineData("http://agent.example/")]
    public void CreateClient_GrpcForeignOrigin_IsRejected(string target)
    {
        AgentCard card = CreateCard((ProtocolBindingNames.Grpc, target));
        using var httpClient = CreateHttpClient();

        Assert.Throws<InvalidOperationException>(
            () => Program.CreateClient(card, httpClient, AgentUrl, A2ATransport.Grpc));
    }

    [Fact]
    public void CreateClient_NoSupportedBinding_Throws()
    {
        AgentCard card = CreateCard(("UNSUPPORTED", "https://agent.example/a2a/"));
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
