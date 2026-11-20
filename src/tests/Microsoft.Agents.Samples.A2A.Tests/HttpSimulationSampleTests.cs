// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

extern alias A2AAgentSample;

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder.Testing.Http;
using Microsoft.Agents.Samples.A2ACli;
using Microsoft.Agents.Samples.A2ACli.A2A;
using Microsoft.Agents.Samples.A2ACli.OAuth;
using Microsoft.Agents.Samples.A2ACli.OAuth.Configuration;
using Microsoft.Agents.Samples.A2ACli.OAuth.Registration;
using Microsoft.Agents.Samples.A2ACli.OAuth.Tokens;
using A2AProtocol = global::A2A;
using GraphProfileClient = A2AAgentSample::A2AAgent.GraphProfileClient;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class GraphProfileHttpSimulationTests
{
    [Fact]
    public async Task ProfileClientUsesItsBearerTokenAndParsesTheSelectedGraphFields()
    {
        using var http = new HttpSimulation();
        ExpectProfile(http)
            .WithHeader("Authorization", "Bearer synthetic-graph-token")
            .RespondJson(new { displayName = "Ada Lovelace", userPrincipalName = "ada@example.test" });
        using var transport = CreateClient(http);
        var client = new GraphProfileClient(transport);

        var profile = await client.GetMeAsync("synthetic-graph-token", CancellationToken.None);

        Assert.Equal("Ada Lovelace", profile.DisplayName);
        Assert.Equal("ada@example.test", profile.UserPrincipalName);
        http.Verify();
    }

    [Theory]
    [InlineData("forbidden")]
    [InlineData("malformed")]
    [InlineData("empty")]
    [InlineData("network")]
    public async Task ProfileFailuresAreNotConvertedToAFabricatedProfile(string outcome)
    {
        using var http = new HttpSimulation();
        var request = ExpectProfile(http);
        switch (outcome)
        {
            case "forbidden":
                request.RespondJson(new { error = new { code = "Forbidden", message = "Access denied" } }, HttpStatusCode.Forbidden);
                break;
            case "malformed":
                request.RespondText("{not json}", "application/json");
                break;
            case "empty":
                request.RespondText("null", "application/json");
                break;
            case "network":
                request.Throw(() => new HttpRequestException("Graph transport unavailable"));
                break;
        }
        using var transport = CreateClient(http);
        var client = new GraphProfileClient(transport);

        Exception? failure = await Record.ExceptionAsync(() => client.GetMeAsync("synthetic-token", CancellationToken.None));

        switch (outcome)
        {
            case "forbidden":
                Assert.Equal(HttpStatusCode.Forbidden, Assert.IsType<HttpRequestException>(failure).StatusCode);
                break;
            case "malformed":
                Assert.IsAssignableFrom<JsonException>(failure);
                break;
            case "empty":
                Assert.Contains("empty", Assert.IsType<InvalidOperationException>(failure).Message, StringComparison.OrdinalIgnoreCase);
                break;
            case "network":
                Assert.IsType<HttpRequestException>(failure);
                break;
        }
        http.Verify();
    }

    private static HttpExpectation ExpectProfile(HttpSimulation http) =>
        http.Expect(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me")
            .WithQuery("$select", "displayName,userPrincipalName");

    private static HttpClient CreateClient(HttpSimulation http)
    {
        var client = http.CreateClient();
        client.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/");
        return client;
    }
}

public class DeviceCodeHttpSimulationTests
{
    private const string DeviceUrl = "https://identity.example.test/oauth/device";
    private const string TokenUrl = "https://identity.example.test/oauth/token";

    [Fact]
    public async Task PendingAndSlowDownPollsUseFiniteHttpOutcomesAndTheProductionBackoff()
    {
        using var http = new HttpSimulation();
        ArrangeDeviceAuthorization(http);
        http.ExpectSequence(HttpMethod.Post, TokenUrl)
            .WithHeader("Content-Type", "application/x-www-form-urlencoded")
            .RespondAsync((request, ct) => RespondToPoll(request, ct, new { error = "authorization_pending" }, HttpStatusCode.BadRequest))
            .RespondAsync((request, ct) => RespondToPoll(request, ct, new { error = "slow_down" }, HttpStatusCode.BadRequest))
            .RespondAsync((request, ct) => RespondToPoll(request, ct,
                new { access_token = "synthetic-access-token", token_type = "Bearer", expires_in = 3600, refresh_token = "synthetic-refresh-token" }, HttpStatusCode.OK));
        using var transport = http.CreateClient();
        using var output = new StringWriter();
        var delays = new List<TimeSpan>();
        var client = new OAuthDeviceCodeTokenClient(transport, output, (delay, _) =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        });

        OAuthAccessToken token = await client.AcquireTokenAsync(CreateBinding(), CancellationToken.None);

        Assert.Equal("synthetic-access-token", token.AccessToken);
        Assert.Equal("synthetic-refresh-token", token.RefreshToken);
        Assert.Equal(TimeSpan.FromHours(1), token.ExpiresIn);
        Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)], delays);
        Assert.Contains("ABCD-EFGH", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-access-token", output.ToString(), StringComparison.Ordinal);
        http.Verify();
    }

    [Theory]
    [InlineData("access_denied", "denied")]
    [InlineData("expired_token", "expired")]
    public async Task TerminalOAuthErrorsStopPollingAndIncludeProviderDetails(string error, string expectedMessage)
    {
        using var http = new HttpSimulation();
        ArrangeDeviceAuthorization(http);
        http.Expect(HttpMethod.Post, TokenUrl)
            .RespondJson(new { error, error_description = "Provider explanation" }, HttpStatusCode.BadRequest);
        using var transport = http.CreateClient();
        var client = new OAuthDeviceCodeTokenClient(transport, TextWriter.Null, static (_, _) => Task.CompletedTask);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => client.AcquireTokenAsync(CreateBinding(), CancellationToken.None));

        Assert.Contains(expectedMessage, failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Provider explanation", failure.Message, StringComparison.Ordinal);
        http.Verify();
    }

    [Fact]
    public async Task CancellationOfATokenPollReachesTheSimulatedTransport()
    {
        using var http = new HttpSimulation();
        ArrangeDeviceAuthorization(http);
        var poll = http.Expect(HttpMethod.Post, TokenUrl).WaitForCancellation();
        using var transport = http.CreateClient();
        var client = new OAuthDeviceCodeTokenClient(transport, TextWriter.Null, static (_, _) => Task.CompletedTask);
        using var cancellation = new CancellationTokenSource();

        Task<OAuthAccessToken> pending = client.AcquireTokenAsync(CreateBinding(), cancellation.Token);
        await poll.Received.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);

        http.Verify();
    }

    private static void ArrangeDeviceAuthorization(HttpSimulation http) =>
        http.Expect(HttpMethod.Post, DeviceUrl)
            .WithHeader("Content-Type", "application/x-www-form-urlencoded")
            .RespondAsync(async (request, ct) =>
            {
                var form = System.Web.HttpUtility.ParseQueryString(await request.Content!.ReadAsStringAsync(ct));
                Assert.Equal("sample-client", form["client_id"]);
                Assert.Equal("profile.read offline_access", form["scope"]);
                return JsonResponse(new
                {
                    device_code = "synthetic-device-code",
                    user_code = "ABCD-EFGH",
                    verification_uri = "https://identity.example.test/device",
                    verification_uri_complete = "https://identity.example.test/device?code=ABCD-EFGH",
                    expires_in = 900,
                    interval = 5
                }, HttpStatusCode.OK);
            });

    private static async Task<HttpResponseMessage> RespondToPoll(HttpRequestMessage request, CancellationToken ct, object payload, HttpStatusCode status)
    {
        var form = System.Web.HttpUtility.ParseQueryString(await request.Content!.ReadAsStringAsync(ct));
        Assert.Equal("sample-client", form["client_id"]);
        Assert.Equal("synthetic-device-code", form["device_code"]);
        Assert.Equal("urn:ietf:params:oauth:grant-type:device_code", form["grant_type"]);
        return JsonResponse(payload, status);
    }

    private static HttpResponseMessage JsonResponse(object payload, HttpStatusCode status) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
    };

    private static OAuthCredentialBinding CreateBinding() => new(
        ProviderId: "provider",
        ProviderType: OAuthCredentialProviderType.GenericOAuth2,
        RegistrationId: "device-code",
        Registration: new OAuthClientRegistration(
            "device-code", [A2AOAuthFlowType.DeviceCode], "sample-client", ClientSecret: null, RedirectUri: null,
            TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None, UsePkce: true),
        FlowType: A2AOAuthFlowType.DeviceCode,
        AuthorizationEndpoint: null,
        DeviceAuthorizationEndpoint: new Uri(DeviceUrl),
        TokenEndpoint: new Uri(TokenUrl),
        MetadataUrl: null,
        RegistrationEndpoint: null,
        EffectiveScopes: ["profile.read", "offline_access"],
        ProviderIdentity: "provider");
}

public class A2AClientHttpSimulationTests
{
    [Fact]
    public async Task CliDiscoversTheAgentCardAndUsesItsSameOriginJsonRpcInterface()
    {
        using var http = new HttpSimulation();
        http.Expect(HttpMethod.Get, "https://a2a.example.test/.well-known/agent-card.json")
            .RespondJson(CreateCard(), options: A2AProtocol.A2AJsonUtilities.DefaultOptions);
        http.Expect(HttpMethod.Post, "https://a2a.example.test/a2a")
            .WithJsonBody(body => body.GetProperty("jsonrpc").GetString() == "2.0" &&
                body.GetProperty("params").GetProperty("message").GetProperty("parts")[0].GetProperty("text").GetString() == "hello")
            .RespondAsync(async (request, cancellationToken) =>
            {
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                return JsonRpcResponse(new
                {
                    jsonrpc = "2.0",
                    id = body.RootElement.GetProperty("id"),
                    result = new A2AProtocol.SendMessageResponse
                    {
                        Task = new A2AProtocol.AgentTask
                        {
                            Id = "task-42",
                            ContextId = "context-42",
                            Status = new A2AProtocol.TaskStatus
                            {
                                State = A2AProtocol.TaskState.Completed,
                                Message = new A2AProtocol.Message
                                {
                                    MessageId = "reply-42",
                                    Role = A2AProtocol.Role.Agent,
                                    Parts = [A2AProtocol.Part.FromText("Hello from A2A")]
                                }
                            }
                        }
                    }
                });
            });
        using var transport = http.CreateClient();

        A2AProtocol.AgentCard card = await new A2AProtocol.A2ACardResolver(new Uri("https://a2a.example.test/"), transport).GetAgentCardAsync();
        using A2AClientConnection connection = Program.CreateClient(card, transport, new Uri("https://a2a.example.test/"));
        A2AProtocol.SendMessageResponse response = await connection.Client.SendMessageAsync(CreateRequest());

        Assert.Equal("task-42", response.Task!.Id);
        Assert.Equal(A2AProtocol.TaskState.Completed, response.Task.Status.State);
        Assert.Equal("Hello from A2A", response.Task.Status.Message!.Parts[0].Text);
        http.Verify();
    }

    [Fact]
    public async Task CliConnectionSurfacesJsonRpcServiceErrors()
    {
        using var http = new HttpSimulation();
        http.Expect(HttpMethod.Post, "https://a2a.example.test/a2a")
            .RespondAsync(async (request, cancellationToken) =>
            {
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                return JsonRpcResponse(new
                {
                    jsonrpc = "2.0",
                    id = body.RootElement.GetProperty("id"),
                    error = new { code = -32603, message = "Synthetic service failure" }
                });
            });
        using var transport = http.CreateClient();
        using A2AClientConnection connection = Program.CreateClient(CreateCard(), transport, new Uri("https://a2a.example.test/"));

        var failure = await Assert.ThrowsAsync<A2AProtocol.A2AException>(() => connection.Client.SendMessageAsync(CreateRequest()));

        Assert.Contains("Synthetic service failure", failure.Message, StringComparison.Ordinal);
        http.Verify();
    }

    private static A2AProtocol.AgentCard CreateCard() => new()
    {
        Name = "Synthetic A2A agent",
        Description = "An offline agent-card fixture",
        Version = "1.0",
        Capabilities = new A2AProtocol.AgentCapabilities { Streaming = false },
        DefaultInputModes = ["text/plain"],
        DefaultOutputModes = ["text/plain"],
        Skills = [new A2AProtocol.AgentSkill { Id = "echo", Name = "Echo", Description = "Echoes text", Tags = ["echo"] }],
        SupportedInterfaces = [new A2AProtocol.AgentInterface { ProtocolBinding = A2AProtocol.ProtocolBindingNames.JsonRpc, ProtocolVersion = "1.0", Url = "https://a2a.example.test/a2a" }]
    };

    private static A2AProtocol.SendMessageRequest CreateRequest() => new()
    {
        Message = new A2AProtocol.Message
        {
            MessageId = "request-42",
            Role = A2AProtocol.Role.User,
            Parts = [A2AProtocol.Part.FromText("hello")]
        }
    };

    private static HttpResponseMessage JsonRpcResponse(object body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(body, A2AProtocol.A2AJsonUtilities.DefaultOptions), Encoding.UTF8, "application/json")
    };
}
