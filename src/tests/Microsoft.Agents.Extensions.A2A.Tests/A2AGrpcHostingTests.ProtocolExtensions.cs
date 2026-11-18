// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using A2A.AspNetCore;
using A2A.Grpc.Extensions.Protos;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Agents.Extensions.A2A.ProtocolExtensions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.A2A.Tests;

public partial class A2AGrpcHostingTests
{
    private const string CustomExtensionUri = "urn:example:echo";
    private const string ProfileExtensionUri = "urn:example:profile";

    [Theory]
    [InlineData("json-rpc")]
    [InlineData("http")]
    [InlineData("grpc")]
    public async Task ProtocolExtensions_CustomOperationUsesSharedNegotiationAcrossBindings(string transport)
    {
        await using var host = new GrpcHost([AllBindings], configureServices: (services, _) =>
        {
            services.AddA2AProtocolExtension<EchoExtensionContributor>();
            services.AddA2AProtocolExtension<ProfileExtensionContributor>();
        });
        await host.StartAsync();

        var card = await host.GetCardAsync();
        Assert.Contains(card.Capabilities.Extensions, extension => extension.Uri == CustomExtensionUri);
        Assert.Contains(card.Capabilities.Extensions, extension => extension.Uri == ProfileExtensionUri);

        if (transport == "grpc")
        {
            using var channel = host.CreateChannel();
            var client = new A2AExtensionService.A2AExtensionServiceClient(channel);
            using var call = client.InvokeExtensionOperationAsync(CustomOperationRequest(),
                new Metadata { { "a2a-extensions", CustomExtensionUri } });
            var response = await call.ResponseAsync;
            AssertEchoResult(response.Payload.ToStringUtf8());
            Assert.Equal(CustomExtensionUri, (await call.ResponseHeadersAsync).GetValue("a2a-extensions"));
        }
        else
        {
            using var response = await InvokeCustomHttpAsync(host.Http, transport, "/a2a", activate: true);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);
            var result = transport == "json-rpc"
                ? document.RootElement.GetProperty("result").GetRawText()
                : json;
            AssertEchoResult(result);
            Assert.Equal(CustomExtensionUri, Assert.Single(response.Headers.GetValues("a2a-extensions")));
        }
    }

    [Theory]
    [InlineData("json-rpc", false)]
    [InlineData("json-rpc", true)]
    [InlineData("http", false)]
    [InlineData("http", true)]
    [InlineData("grpc", false)]
    [InlineData("grpc", true)]
    public async Task ProtocolExtensions_InactiveOrUnavailableOwnerRejectsCustomOperation(string transport, bool declined)
    {
        await using var host = new GrpcHost([AllBindings], configureServices: (services, _) =>
            services.AddA2AProtocolExtension<EchoExtensionContributor>());
        host.Scenario.ExtensionAvailable = !declined;
        await host.StartAsync();

        if (transport == "grpc")
        {
            using var channel = host.CreateChannel();
            var client = new A2AExtensionService.A2AExtensionServiceClient(channel);
            var headers = declined ? new Metadata { { "a2a-extensions", CustomExtensionUri } } : new Metadata();
            using var call = client.InvokeExtensionOperationAsync(CustomOperationRequest(), headers);
            var exception = await Assert.ThrowsAsync<RpcException>(() => call.ResponseAsync);
            Assert.Equal(StatusCode.FailedPrecondition, exception.StatusCode);
            Assert.Contains(CustomExtensionUri, exception.Status.Detail);
        }
        else
        {
            using var response = await InvokeCustomHttpAsync(host.Http, transport, "/a2a", activate: declined);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (transport == "json-rpc")
            {
                Assert.Equal((int)A2AErrorCode.ExtensionSupportRequired,
                    document.RootElement.GetProperty("error").GetProperty("code").GetInt32());
            }
            else
            {
                Assert.False(response.IsSuccessStatusCode);
                Assert.Contains(CustomExtensionUri, document.RootElement.GetRawText());
            }
        }
    }

    [Theory]
    [InlineData("json-rpc", false)]
    [InlineData("json-rpc", true)]
    [InlineData("http", false)]
    [InlineData("http", true)]
    [InlineData("grpc", false)]
    [InlineData("grpc", true)]
    public async Task ProtocolExtensions_StandardTurnsReceiveActivationForUnaryAndStreaming(string transport, bool streaming)
    {
        await using var host = new GrpcHost([AllBindings], configureServices: (services, _) =>
        {
            services.AddA2AProtocolExtension<EchoExtensionContributor>();
            services.AddA2AProtocolExtension<ProfileExtensionContributor>();
        });
        await host.StartAsync();
        var extensions = $"{CustomExtensionUri}, {ProfileExtensionUri}";

        if (transport == "grpc")
        {
            using var client = host.CreateClient(extensionUri: extensions);
            if (streaming)
            {
                await foreach (var _ in client.SendStreamingMessageAsync(Request("activated")))
                {
                }
            }
            else
            {
                await client.SendMessageAsync(Request("activated"));
            }
        }
        else
        {
            object body = Request("activated");
            if (transport == "json-rpc")
            {
                body = new JsonRpcRequest
                {
                    Id = "1",
                    Method = streaming ? A2AMethods.SendStreamingMessage : A2AMethods.SendMessage,
                    Params = JsonSerializer.SerializeToElement(body, A2AJsonUtilities.DefaultOptions),
                };
            }
            using var request = new HttpRequestMessage(HttpMethod.Post, transport == "json-rpc"
                ? "/a2a"
                : streaming ? "/a2a/message:stream" : "/a2a/message:send")
            {
                Content = JsonContent(body),
            };
            request.Headers.Add("a2a-extensions", extensions);
            using var response = await host.Http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            Assert.Contains("GrpcAgent:anonymous:none", await response.Content.ReadAsStringAsync());
        }

        var activation = host.Scenario.For("activated").ExtensionActivation;
        Assert.NotNull(activation);
        Assert.Contains(CustomExtensionUri, activation.ActivatedExtensions);
        Assert.Contains(ProfileExtensionUri, activation.ActivatedExtensions);
    }

    [Fact]
    public async Task ProtocolExtensions_MappedAgentsUseTheirOwnBindingsCardsAndTurns()
    {
        await using var host = new GrpcHost(
        [
            new AgentSpec("FirstAgent",
                (A2AAgentTransportProtocol.JsonRpc, "/first"), (A2AAgentTransportProtocol.HttpJson, "/first")),
            new AgentSpec("SecondAgent",
                (A2AAgentTransportProtocol.JsonRpc, "/second"), (A2AAgentTransportProtocol.HttpJson, "/second"),
                (A2AAgentTransportProtocol.Grpc, "/")),
        ], configureServices: (services, agents) => AddAgentContributor<EchoExtensionContributor>(services, agents[1]));
        await host.StartAsync();

        using var firstCardResponse = await host.Http.GetAsync("/first/.well-known/agent-card.json");
        var firstCard = await firstCardResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(CustomExtensionUri, firstCard);
        Assert.Contains((await host.GetCardAsync()).Capabilities.Extensions, extension => extension.Uri == CustomExtensionUri);
        using var firstCustom = await InvokeCustomHttpAsync(host.Http, "http", "/first", activate: true);
        Assert.Equal(HttpStatusCode.NotFound, firstCustom.StatusCode);
        using var secondCustom = await InvokeCustomHttpAsync(host.Http, "http", "/second", activate: true);
        secondCustom.EnsureSuccessStatusCode();
        AssertEchoResult(await secondCustom.Content.ReadAsStringAsync());

        using var firstTurn = await host.Http.PostAsync("/first/message:send", JsonContent(Request("first")));
        using var secondTurn = await host.Http.PostAsync("/second/message:send", JsonContent(Request("second")));
        firstTurn.EnsureSuccessStatusCode();
        secondTurn.EnsureSuccessStatusCode();
        Assert.Contains("FirstAgent:anonymous:none", await firstTurn.Content.ReadAsStringAsync());
        Assert.Contains("SecondAgent:anonymous:none", await secondTurn.Content.ReadAsStringAsync());

        using var channel = host.CreateChannel();
        var client = new A2AExtensionService.A2AExtensionServiceClient(channel);
        using var grpcCall = client.InvokeExtensionOperationAsync(CustomOperationRequest(),
            new Metadata { { "a2a-extensions", CustomExtensionUri } });
        AssertEchoResult((await grpcCall.ResponseAsync).Payload.ToStringUtf8());
    }

    [Fact]
    public async Task ProtocolExtensions_IndependentHostsDoNotShareGrpcRegistries()
    {
        await using var first = new GrpcHost([AllBindings], configureServices: (services, _) =>
            services.AddA2AProtocolExtension<EchoExtensionContributor>());
        await using var second = new GrpcHost([AllBindings]);
        await first.StartAsync();
        await second.StartAsync();

        Assert.Contains((await first.GetCardAsync()).Capabilities.Extensions, extension => extension.Uri == CustomExtensionUri);
        Assert.DoesNotContain((await second.GetCardAsync()).Capabilities.Extensions ?? [], extension => extension.Uri == CustomExtensionUri);
        using var firstChannel = first.CreateChannel();
        using var secondChannel = second.CreateChannel();
        var headers = new Metadata { { "a2a-extensions", CustomExtensionUri } };
        using var accepted = new A2AExtensionService.A2AExtensionServiceClient(firstChannel)
            .InvokeExtensionOperationAsync(CustomOperationRequest(), headers);
        AssertEchoResult((await accepted.ResponseAsync).Payload.ToStringUtf8());
        using var rejected = new A2AExtensionService.A2AExtensionServiceClient(secondChannel)
            .InvokeExtensionOperationAsync(CustomOperationRequest(), headers);
        await Assert.ThrowsAsync<RpcException>(() => rejected.ResponseAsync);
    }

    [Theory]
    [InlineData("json-rpc")]
    [InlineData("http")]
    public async Task ProtocolExtensions_EndpointHelpersShareTheFactoryAgentsRegistryWithoutConstructingIt(string transport)
    {
        await using var host = new GrpcHost(
            [new AgentSpec("DefaultAgent")],
            configureServices: (services, _) => services.AddA2AProtocolExtension<EchoExtensionContributor>(),
            useEndpointHelpers: true);
        await host.StartAsync();
        Assert.Empty(host.Scenario.AgentRequests);

        using var response = await InvokeCustomHttpAsync(host.Http, transport, "/a2a", activate: true);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        AssertEchoResult(transport == "json-rpc"
            ? body.RootElement.GetProperty("result").GetRawText()
            : body.RootElement.GetRawText());
        Assert.Contains(CustomExtensionUri, await host.Http.GetStringAsync("/a2a/card"));
        Assert.Equal(1, host.Scenario.ExtensionRegistrations);
    }

    private static void AddAgentContributor<TContributor>(IServiceCollection services, Type agentType)
        where TContributor : class, IA2AProtocolExtensionContributor
        => typeof(A2AServiceExtensions).GetMethods()
            .Single(method => method.Name == nameof(A2AServiceExtensions.AddA2AProtocolExtension)
                && method.GetGenericArguments().Length == 2)
            .MakeGenericMethod(agentType, typeof(TContributor))
            .Invoke(null, [services]);

    private static ExtensionOperationRequest CustomOperationRequest() => new()
    {
        OperationId = CustomExtensionUri + "#echo",
        Payload = ByteString.CopyFromUtf8("""{"text":"hello"}"""),
    };

    private static async Task<HttpResponseMessage> InvokeCustomHttpAsync(
        HttpClient client, string transport, string prefix, bool activate)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            transport == "json-rpc" ? prefix : prefix + "/example:echo")
        {
            Content = new StringContent(transport == "json-rpc"
                ? """{"jsonrpc":"2.0","id":"1","method":"example/echo","params":{"text":"hello"}}"""
                : """{"text":"hello"}""", Encoding.UTF8, "application/json"),
        };
        if (activate)
        {
            request.Headers.Add("a2a-extensions", CustomExtensionUri);
        }
        return await client.SendAsync(request);
    }

    private static void AssertEchoResult(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Equal("echo:hello", document.RootElement.GetProperty("text").GetString());
    }

    public sealed record ExtensionRequest([property: JsonPropertyName("text")] string Text);
    public sealed record ExtensionResult([property: JsonPropertyName("text")] string Text);

    public sealed class EchoExtensionContributor(Scenario scenario) : IA2AProtocolExtensionContributor
    {
        public void Register(A2AProtocolExtensionBuilder builder)
        {
            scenario.ExtensionRegistrations++;
            var extension = builder.RegisterExtension(
                new global::A2A.AgentExtension { Uri = CustomExtensionUri, Description = "An example echo operation" },
                (_, cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return ValueTask.FromResult(scenario.ExtensionAvailable);
                });
            var operation = builder.Map<ExtensionRequest, ExtensionResult>(
                extension,
                new A2AOperationId(CustomExtensionUri + "#echo"),
                (_, request, _) => ValueTask.FromResult(new ExtensionResult("echo:" + request.Text)),
                (JsonTypeInfo<ExtensionRequest>)JsonSerializerOptions.Default.GetTypeInfo(typeof(ExtensionRequest)),
                (JsonTypeInfo<ExtensionResult>)JsonSerializerOptions.Default.GetTypeInfo(typeof(ExtensionResult)));
            builder.JsonRpc.Map("example/echo", operation);
            builder.Http.Map("POST", "/example:echo", operation, async (context, cancellationToken) =>
                await JsonSerializer.DeserializeAsync<ExtensionRequest>(context.Request.Body, cancellationToken: cancellationToken));
        }
    }

    public sealed class ProfileExtensionContributor : IA2AProtocolExtensionContributor
    {
        public void Register(A2AProtocolExtensionBuilder builder) =>
            builder.RegisterExtension(new global::A2A.AgentExtension { Uri = ProfileExtensionUri });
    }
}
