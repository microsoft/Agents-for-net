// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Builder.Testing.Http;
using Microsoft.Agents.Client;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.SampleTest;

public class AgentToAgentHttpSimulationTests
{
    private const string RemoteUrl = "https://remote-agent.example.test/api/messages";
    private const string CallbackUrl = "https://host-agent.example.test/api/agentresponse/";

    [Fact]
    public async Task NormalDeliveryWaitsForSeparateCallbacksAndReusesConversationUntilEnd()
    {
        using var http = new HttpSimulation(new HttpSimulationOptions { CaptureRequestBody = true });
        ExpectMessage(http, "agent hello", DeliveryModes.Normal).Respond(HttpStatusCode.Accepted);
        ExpectMessage(http, "second question", DeliveryModes.Normal).Respond(HttpStatusCode.Accepted);
        ExpectMessage(http, "agent again", DeliveryModes.Normal).Respond(HttpStatusCode.Accepted);

        await using (var fixture = new Fixture(http, streaming: false))
        {
            await fixture.Host.CreateTestFlow().Send("agent hello")
                .AssertReply("Got it, connecting you to the 'Echo' Agent...").StartTestAsync();
            Assert.Null(fixture.Host.Adapter.GetNextReply());
            string remoteConversation = ConversationId(http.Exchanges[0]);

            // A 202 acknowledges the send; the remote message arrives through the real callback handler on a later turn.
            await fixture.CallbackAsync(remoteConversation, new Activity
            {
                Type = ActivityTypes.Message,
                Text = "Remote answer",
                Conversation = new ConversationAccount(id: remoteConversation)
            });
            Assert.Equal("(Echo) Remote answer", fixture.Host.Adapter.GetNextReply()?.Text);

            await fixture.Host.CreateTestFlow().Send("second question").StartTestAsync();
            Assert.Null(fixture.Host.Adapter.GetNextReply());
            Assert.Equal(remoteConversation, ConversationId(http.Exchanges[1]));

            await fixture.CallbackAsync(remoteConversation, new Activity
            {
                Type = ActivityTypes.EndOfConversation,
                Conversation = new ConversationAccount(id: remoteConversation),
                Code = EndOfConversationCodes.CompletedSuccessfully,
                Value = new { result = "done" }
            });
            Assert.Equal("The 'Echo' Agent returned:\n\n{\"result\":\"done\"}", fixture.Host.Adapter.GetNextReply()?.Text);
            Assert.Equal("Back in HostAgent. Say \"agent\" and I'll patch you through", fixture.Host.Adapter.GetNextReply()?.Text);
            Assert.Null(await fixture.AgentHost.GetConversationReferenceAsync(remoteConversation));

            await fixture.Host.CreateTestFlow().Send("agent again")
                .AssertReply("Got it, connecting you to the 'Echo' Agent...").StartTestAsync();
            Assert.NotEqual(remoteConversation, ConversationId(http.Exchanges[2]));
            Assert.Null(fixture.Host.Adapter.GetNextReply());
        }

        http.Verify();
    }

    [Fact]
    public async Task StreamingDeliveryForwardsSseActivitiesAndClearsTheConversationOnRemoteEnd()
    {
        using var http = new HttpSimulation(new HttpSimulationOptions { CaptureRequestBody = true });
        RespondStream(ExpectMessage(http, "agent hello", DeliveryModes.Stream), ActivityTypes.Message, "Streamed answer");
        RespondStream(ExpectMessage(http, "end", DeliveryModes.Stream), ActivityTypes.EndOfConversation, value: new { result = "finished" });
        RespondStream(ExpectMessage(http, "agent again", DeliveryModes.Stream), ActivityTypes.Message, "New conversation");

        await using (var fixture = new Fixture(http, streaming: true))
        {
            await fixture.Host.CreateTestFlow()
                .Send("agent hello")
                .AssertReply("Got it, connecting you to the 'Echo' Agent...")
                .AssertReply("(Echo) Streamed answer")
                .Send("end")
                .AssertReply("The 'Echo' Agent returned:\n\n{\"result\":\"finished\"}")
                .AssertReply("Back in StreamingHostAgent. Say \"agent\" and I'll patch you through")
                .Send("agent again")
                .AssertReply("Got it, connecting you to the 'Echo' Agent...")
                .AssertReply("(Echo) New conversation")
                .StartTestAsync();

            string firstConversation = ConversationId(http.Exchanges[0]);
            Assert.Equal(firstConversation, ConversationId(http.Exchanges[1]));
            Assert.NotEqual(firstConversation, ConversationId(http.Exchanges[2]));
            Assert.Null(await fixture.AgentHost.GetConversationReferenceAsync(firstConversation));
            Assert.Null(fixture.Host.Adapter.GetNextReply());
        }

        http.Verify();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RemoteFailureRunsTheSamplesConversationCleanup(bool streaming, bool networkFault)
    {
        using var http = new HttpSimulation(new HttpSimulationOptions { CaptureRequestBody = true });
        var send = ExpectMessage(http, "agent hello", streaming ? DeliveryModes.Stream : DeliveryModes.Normal);
        if (networkFault)
        {
            send.Throw(() => new HttpRequestException("Remote agent transport unavailable"));
        }
        else
        {
            send.RespondJson(new { error = new { code = "Forbidden", message = "Access denied" } }, HttpStatusCode.Forbidden);
        }
        http.Expect(HttpMethod.Post, RemoteUrl)
            .WithJsonBody(body => body.GetProperty("type").GetString() == ActivityTypes.EndOfConversation)
            .Respond(HttpStatusCode.Accepted);

        await using (var fixture = new Fixture(http, streaming))
        {
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Host.CreateTestFlow().Send("agent hello").StartTestAsync());
            if (networkFault)
            {
                Assert.IsType<HttpRequestException>(failure.InnerException);
            }
            Assert.Equal("Got it, connecting you to the 'Echo' Agent...", fixture.Host.Adapter.GetNextReply()?.Text);
            Assert.Null(fixture.Host.Adapter.GetNextReply());
            Assert.Null(await fixture.AgentHost.GetConversationReferenceAsync(ConversationId(http.Exchanges[0])));
        }

        http.Verify();
    }

    private static HttpExpectation ExpectMessage(HttpSimulation http, string text, string deliveryMode) =>
        http.Expect(HttpMethod.Post, RemoteUrl)
            .ForClient("HttpAgentClient")
            .WithHeader("Authorization", "Bearer synthetic-agent-to-agent-token")
            .WithJsonBody(body => body.GetProperty("type").GetString() == ActivityTypes.Message &&
                body.GetProperty("text").GetString() == text &&
                body.GetProperty("deliveryMode").GetString() == deliveryMode &&
                body.GetProperty("serviceUrl").GetString() == CallbackUrl &&
                body.GetProperty("from").GetProperty("role").GetString() == RoleTypes.Agent &&
                body.GetProperty("recipient").GetProperty("role").GetString() == RoleTypes.Skill);

    private static string ConversationId(HttpExchange exchange)
    {
        Assert.NotNull(exchange.Body);
        using var body = JsonDocument.Parse(exchange.Body);
        return body.RootElement.GetProperty("conversation").GetProperty("id").GetString();
    }

    private static void RespondStream(HttpExpectation exchange, string type, string text = null, object value = null) =>
        exchange.RespondAsync(async (request, ct) =>
        {
            using var body = JsonDocument.Parse(await request.Content.ReadAsStringAsync(ct));
            var activity = new Activity
            {
                Type = type,
                Id = "remote-reply",
                Text = text,
                Value = value,
                ChannelId = Channels.Test,
                ServiceUrl = CallbackUrl,
                Conversation = new ConversationAccount(id: body.RootElement.GetProperty("conversation").GetProperty("id").GetString()),
                From = new ChannelAccount("remote-client", role: RoleTypes.Skill),
                Recipient = new ChannelAccount("host-client", role: RoleTypes.Agent),
                Code = type == ActivityTypes.EndOfConversation ? EndOfConversationCodes.CompletedSuccessfully : null
            };
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"event: activity\ndata: {ProtocolJsonSerializer.ToJson(activity)}\n\n", Encoding.UTF8)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return response;
        });

    private sealed class Fixture : IAsyncDisposable
    {
        private IServiceProvider _services;

        internal Fixture(HttpSimulation http, bool streaming)
        {
            Host = AgentTestHost.Create(builder =>
            {
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Agent:ClientId"] = "host-client",
                    ["Agent:Host:DefaultResponseEndpoint"] = CallbackUrl,
                    ["Agent:Host:Agents:Echo:ConnectionSettings:ClientId"] = "remote-client",
                    ["Agent:Host:Agents:Echo:ConnectionSettings:Endpoint"] = RemoteUrl,
                    ["Agent:Host:Agents:Echo:ConnectionSettings:TokenProvider"] = "ServiceConnection"
                });
                builder.Services.AddHttpSimulation(http);
                builder.Services.AddSingleton<IStorage, MemoryStorage>();
                builder.Services.AddSingleton(CreateConnections());
                builder.AddAgentHost();
                builder.Services.AddTransient<IAgent>(services =>
                {
                    _services = services;
                    var options = new AgentApplicationOptions(services.GetRequiredService<IStorage>(), NullLoggerFactory.Instance)
                    {
                        StartTypingTimer = false,
                        HttpClientFactory = services.GetRequiredService<IHttpClientFactory>()
                    };
                    var agentHost = services.GetRequiredService<IAgentHost>();
                    return streaming
                        ? new StreamingAgent1.StreamingHostAgent(options, agentHost)
                        : new Agent1.HostAgent(options, agentHost);
                });
            });
            Host.Adapter.ClaimsIdentity = AgentClaims.CreateIdentity("host-client", appId: "channel-client");
            Host.Adapter.Conversation.ServiceUrl = HttpSampleHost.ServiceUrl;
        }

        internal AgentTestHost Host { get; }
        internal IAgentHost AgentHost => _services.GetRequiredService<IAgentHost>();

        internal async Task CallbackAsync(string conversationId, IActivity response) =>
            await _services.GetRequiredService<IChannelApiHandler>().OnSendToConversationAsync(
                AgentClaims.CreateIdentity("host-client", appId: "remote-client"), conversationId, response);

        public ValueTask DisposeAsync() => Host.DisposeAsync();

        private static IConnections CreateConnections()
        {
            var tokenProvider = new Mock<IAccessTokenProvider>();
            tokenProvider.Setup(provider => provider.GetAccessTokenAsync(
                "api://remote-client", It.Is<IList<string>>(scopes => scopes.SequenceEqual(new[] { "remote-client/.default" })), It.IsAny<bool>()))
                .ReturnsAsync("synthetic-agent-to-agent-token");
            IAccessTokenProvider provider = tokenProvider.Object;
            var connections = new Mock<IConnections>();
            connections.Setup(value => value.TryGetConnection("ServiceConnection", out provider)).Returns(true);
            return connections.Object;
        }
    }
}
