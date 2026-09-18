// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Samples.A2AClient;
using Moq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class A2AInTaskAuthorizationClientTests
{
    [Fact]
    public async Task ResumeIfRequiredAsync_AcquiresCredentialAndCallsJsonRpcExtension()
    {
        var tokenProvider = new Mock<IA2AAccessTokenProvider>();
        tokenProvider
            .Setup(provider => provider.GetAccessTokenAsync(
                It.Is<A2AAgentCardAuthentication>(authentication =>
                    authentication.SecuritySchemeName == "delegated"
                    && authentication.FlowType == A2AOAuthFlowType.DeviceCode
                    && authentication.Scopes.Count == 1
                    && authentication.Scopes[0] == "agent.read"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("procured-token");
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
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
            "Bearer procured-token",
            Assert.Single(handler.Request.Headers.GetValues("A2A-InTask-Authorization")));
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal("resumeAuth", body.RootElement.GetProperty("method").GetString());
        Assert.Equal("task-1", body.RootElement.GetProperty("params").GetProperty("taskId").GetString());
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

    private static AgentTask CreateAuthRequiredTask()
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
                                id = "auth-1",
                                oauth2 = new
                                {
                                    flows = new
                                    {
                                        deviceCode = new
                                        {
                                            deviceAuthorizationUrl = "https://login.example.com/devicecode",
                                            tokenUrl = "https://login.example.com/token",
                                            scopes = new Dictionary<string, string>
                                            {
                                                ["agent.read"] = "Read the agent",
                                            },
                                        },
                                    },
                                },
                                requiredScopes = new[] { "agent.read" },
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
}
