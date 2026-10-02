// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Samples.A2AClient.A2A;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Samples.A2AClient.OAuth;
using Microsoft.Agents.Samples.A2AClient.OAuth.Configuration;
using Microsoft.Agents.Samples.A2AClient.OAuth.Providers;
using Microsoft.Agents.Samples.A2AClient.OAuth.Registration;
using Microsoft.Agents.Samples.A2AClient.OAuth.Tokens;
using Xunit;

namespace Microsoft.Agents.Samples.A2AClient.Tests.A2A;

public class A2AInTaskAuthorizationClientTests
{
    [Fact]
    public async Task ResumeIfRequiredAsync_AcquiresCredentialAndCallsJsonRpcExtension()
    {
        var tokenProvider = new Mock<IA2AAccessTokenProvider>();
        tokenProvider
            .Setup(provider => provider.GetAccessTokenAsync(
                It.Is<A2AAgentCardAuthentication>(authentication =>
                    authentication.SecuritySchemeName == null
                    && authentication.MetadataUrl == null
                    && authentication.FlowType == A2AOAuthFlowType.DeviceCode
                    && authentication.Scopes.Count == 1
                    && authentication.Scopes[0] == "agent.read"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("procured-token");
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "agent-jwt");
        var client = A2AInTaskAuthorizationClient.Create(
            CreateAgentCard(ProtocolBindingNames.JsonRpc),
            httpClient,
            tokenProvider.Object,
            new Uri("https://agent.example/a2a"));

        AgentTask? result = await client.ResumeIfRequiredAsync(CreateAuthRequiredTask(), CancellationToken.None);

        Assert.Equal(TaskState.Completed, result!.Status.State);
        Assert.Equal("https://agent.example/a2a", handler.Request!.RequestUri!.AbsoluteUri);
        Assert.Equal(
            A2AInTaskAuthorizationClient.ExtensionUri,
            Assert.Single(handler.Request.Headers.GetValues(A2AInTaskAuthorizationClient.ExtensionHeader)));
        Assert.Equal(
            "Bearer agent-jwt",
            handler.Request.Headers.Authorization!.ToString());
        Assert.Equal(
            "procured-token",
            Assert.Single(handler.Request.Headers.GetValues("x-a2a-intask-authorization")));
        Assert.False(handler.Request.Headers.Contains("A2A-InTask-Authorization"));
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal("resumeAuth", body.RootElement.GetProperty("method").GetString());
        Assert.Equal("task-1", body.RootElement.GetProperty("params").GetProperty("taskId").GetString());
    }

    [Fact]
    public async Task ResumeIfRequiredAsync_HandlesConsecutiveAuthorizationRequests()
    {
        var requestedScopes = new List<string>();
        var tokenProvider = new Mock<IA2AAccessTokenProvider>();
        tokenProvider
            .Setup(provider => provider.GetAccessTokenAsync(
                It.IsAny<A2AAgentCardAuthentication>(),
                It.IsAny<CancellationToken>()))
            .Returns<A2AAgentCardAuthentication, CancellationToken>((authentication, _) =>
            {
                string scope = Assert.Single(authentication.Scopes);
                requestedScopes.Add(scope);
                return Task.FromResult<string?>($"{scope}-token");
            });
        var handler = new ConsecutiveAuthorizationHandler();
        using var httpClient = new HttpClient(handler);
        var client = A2AInTaskAuthorizationClient.Create(
            CreateAgentCard(ProtocolBindingNames.JsonRpc),
            httpClient,
            tokenProvider.Object,
            new Uri("https://agent.example/a2a"));

        AgentTask? result = await client.ResumeIfRequiredAsync(
            CreateAuthRequiredTask("auth-1", "agent.read"),
            CancellationToken.None);

        Assert.Equal(TaskState.Completed, result!.Status.State);
        Assert.Equal(["agent.read", "profile.read"], requestedScopes);
        Assert.Equal(["agent.read-token", "profile.read-token"], handler.Tokens);
    }

    [Fact]
    public async Task ResumeIfRequiredAsync_RepeatedAuthorizationRequestId_Throws()
    {
        var tokenProvider = new Mock<IA2AAccessTokenProvider>();
        tokenProvider
            .Setup(provider => provider.GetAccessTokenAsync(
                It.IsAny<A2AAgentCardAuthentication>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("procured-token");
        var handler = new RepeatedAuthorizationHandler();
        using var httpClient = new HttpClient(handler);
        var client = A2AInTaskAuthorizationClient.Create(
            CreateAgentCard(ProtocolBindingNames.JsonRpc),
            httpClient,
            tokenProvider.Object,
            new Uri("https://agent.example/a2a"));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ResumeIfRequiredAsync(CreateAuthRequiredTask(), CancellationToken.None));

        Assert.Equal("The authorization request 'auth-1' was repeated.", exception.Message);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task ResumeIfRequiredAsync_ResolvesInTaskAuthorizationByEndpointsWithoutLocalAlias()
    {
        var oauth = new Mock<IOAuthTokenClient>(MockBehavior.Strict);
        oauth.Setup(client => client.AcquireTokenAsync(
                It.Is<OAuthCredentialBinding>(binding =>
                    binding.ProviderId == "github-device"
                    && binding.RegistrationId == "device"
                    && binding.FlowType == A2AOAuthFlowType.DeviceCode
                    && binding.DeviceAuthorizationEndpoint!.AbsoluteUri == "https://github.com/login/device/code"
                    && binding.TokenEndpoint.AbsoluteUri == "https://github.com/login/oauth/access_token"
                    && binding.Registration.ClientId == "github-client-id"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OAuthAccessToken("procured-token", TimeSpan.FromMinutes(10)));
        var tokenProvider = new A2AAccessTokenProvider(
            new OAuthCredentialProviderResolver(
            [
                new GenericOAuth2CredentialProvider(
                    new OAuthCredentialProviderOptions
                    {
                        Id = "github-device",
                        Type = OAuthCredentialProviderType.GenericOAuth2,
                        AllowedOrigins = [new Uri("https://github.com")],
                        Registrations = new Dictionary<string, OAuthClientRegistration>(StringComparer.Ordinal)
                        {
                            ["device"] = new(
                                "device",
                                [A2AOAuthFlowType.DeviceCode],
                                "github-client-id",
                                ClientSecret: null,
                                RedirectUri: null,
                                TokenEndpointAuthenticationMethod: OAuthTokenEndpointAuthenticationMethod.None,
                                UsePkce: true),
                        },
                    }),
            ]),
            oauth.Object,
            new Uri("https://agent.example"));
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        var client = A2AInTaskAuthorizationClient.Create(
            CreateAgentCard(ProtocolBindingNames.JsonRpc),
            httpClient,
            tokenProvider,
            new Uri("https://agent.example/a2a"));

        AgentTask? result = await client.ResumeIfRequiredAsync(
            CreateAuthRequiredTask(
                authorizationRequestId: "auth-github",
                requiredScope: "repo",
                deviceAuthorizationUrl: "https://github.com/login/device/code",
                tokenUrl: "https://github.com/login/oauth/access_token"),
            CancellationToken.None);

        Assert.Equal(TaskState.Completed, result!.Status.State);
        Assert.Equal(
            "procured-token",
            Assert.Single(handler.Request!.Headers.GetValues(A2AInTaskAuthorizationClient.TokenHeader)));
        oauth.Verify(
            client => client.AcquireTokenAsync(It.IsAny<OAuthCredentialBinding>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static AgentCard CreateAgentCard(string protocolBinding)
    {
        return new AgentCard
        {
            Name = "Agent",
            Description = "Agent",
            Version = "1.0",
            DefaultInputModes = ["text"],
            DefaultOutputModes = ["text"],
            Skills = [],
            Capabilities = new AgentCapabilities(),
            SupportedInterfaces =
            [
                new AgentInterface
                {
                    ProtocolBinding = protocolBinding,
                    ProtocolVersion = "1.0",
                    Url = "https://agent.example/a2a",
                },
            ],
        };
    }

    private static AgentTask CreateAuthRequiredTask(
        string authorizationRequestId = "auth-1",
        string requiredScope = "agent.read",
        string deviceAuthorizationUrl = "https://login.example.com/devicecode",
        string tokenUrl = "https://login.example.com/token")
    {
        return new AgentTask
        {
            Id = "task-1",
            ContextId = "context-1",
            Status = new global::A2A.TaskStatus
            {
                State = TaskState.AuthRequired,
                Message = new Message
                {
                    Role = Role.Agent,
                    Parts = [Part.FromText("Authorization required")],
                    Metadata = new Dictionary<string, JsonElement>
                    {
                        [A2AInTaskAuthorizationClient.ExtensionUri] = JsonSerializer.SerializeToElement(new
                        {
                            authorizationRequest = new
                            {
                                id = authorizationRequestId,
                                oauth2 = new
                                {
                                    flows = new
                                    {
                                        deviceCode = new
                                        {
                                            deviceAuthorizationUrl,
                                            tokenUrl,
                                            scopes = new Dictionary<string, string>
                                            {
                                                [requiredScope] = "Access the resource",
                                            },
                                        },
                                    },
                                },
                                requiredScopes = new[] { requiredScope },
                            },
                        }),
                    },
                },
            },
        };
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        internal HttpRequestMessage? Request { get; private set; }

        internal string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            var completed = new AgentTask
            {
                Id = "task-1",
                ContextId = "context-1",
                Status = new global::A2A.TaskStatus { State = TaskState.Completed },
            };
            string result = JsonSerializer.Serialize(
                new
                {
                    jsonrpc = "2.0",
                    id = "response-1",
                    result = completed,
                },
                A2AJsonUtilities.DefaultOptions);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(result, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class ConsecutiveAuthorizationHandler : HttpMessageHandler
    {
        internal List<string> Tokens { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Tokens.Add(request.Headers.GetValues(A2AInTaskAuthorizationClient.TokenHeader).Single());
            await request.Content!.ReadAsStringAsync(cancellationToken);

            AgentTask task = Tokens.Count == 1
                ? CreateAuthRequiredTask("auth-2", "profile.read")
                : new AgentTask
                {
                    Id = "task-1",
                    ContextId = "context-1",
                    Status = new global::A2A.TaskStatus { State = TaskState.Completed },
                };
            string result = JsonSerializer.Serialize(
                new
                {
                    jsonrpc = "2.0",
                    id = $"response-{Tokens.Count}",
                    result = task,
                },
                A2AJsonUtilities.DefaultOptions);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(result, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class RepeatedAuthorizationHandler : HttpMessageHandler
    {
        internal int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            AgentTask task = RequestCount == 1
                ? CreateAuthRequiredTask()
                : new AgentTask
                {
                    Id = "task-1",
                    ContextId = "context-1",
                    Status = new global::A2A.TaskStatus { State = TaskState.Completed },
                };
            string result = JsonSerializer.Serialize(
                new
                {
                    jsonrpc = "2.0",
                    id = $"response-{RequestCount}",
                    result = task,
                },
                A2AJsonUtilities.DefaultOptions);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(result, Encoding.UTF8, "application/json"),
            });
        }
    }
}
