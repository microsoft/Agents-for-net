// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using ConversationAgent;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.Testing.Http;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.SampleTest;

public class TeamsDeliverySampleTests
{
    private const string ActivityUrl = HttpSampleHost.ServiceUrl + "v3/conversations/conversation-id/activities/existing-card";
    private const string ReplyUrl = HttpSampleHost.ServiceUrl + "v3/conversations/conversation-id/activities/incoming-message";

    [Fact]
    public async Task Update_SendsUpdatedCardToOriginalActivity()
    {
        using var http = new HttpSimulation();
        http.Expect(HttpMethod.Put, ActivityUrl)
            .WithJsonBody(body =>
                body.GetProperty("attachments")[0].GetProperty("content").GetProperty("text").GetString() == "Update count - 3")
            .RespondJson(new { id = "existing-card" });

        await RunAsync(http, "update", new { count = 2 });
        http.Verify();
    }

    [Fact]
    public async Task Delete_DeletesOriginalActivityWithoutSendingAnotherMessage()
    {
        using var http = new HttpSimulation();
        http.Expect(HttpMethod.Delete, ActivityUrl).Respond(HttpStatusCode.OK);

        await RunAsync(http, "delete");
        http.Verify();
    }

    [Theory]
    [InlineData("update", "PUT")]
    [InlineData("delete", "DELETE")]
    public async Task EditFailure_PropagatesDeserializedConnectorError(string command, string method)
    {
        using var http = new HttpSimulation();
        http.Expect(new HttpMethod(method), ActivityUrl).RespondJson(
            new { error = new { code = "MessageNotFound", message = "Synthetic missing activity" } },
            HttpStatusCode.NotFound);

        var exception = await Assert.ThrowsAsync<ErrorResponseException>(() => RunAsync(http, command));

        Assert.Equal("MessageNotFound", exception.Body.Error.Code);
        http.Verify();
    }

    [Fact]
    public async Task MessageAll_CreatesAndMessagesPrivateConversationsAcrossPages()
    {
        using var http = new HttpSimulation();
        var membersUrl = HttpSampleHost.ServiceUrl + "v3/conversations/team-id/pagedmembers?pageSize=100";
        http.Expect(HttpMethod.Get, membersUrl).RespondJson(new
        {
            continuationToken = "next",
            members = new[] { new { id = "member-1", name = "First Member" } }
        });
        http.Expect(HttpMethod.Get, membersUrl + "&continuationToken=next").RespondJson(new
        {
            members = new[] { new { id = "member-2", name = "Second Member" } }
        });
        ExpectPrivateConversation(http, "member-1", "First Member", "private-1");
        ExpectPrivateConversation(http, "member-2", "Second Member", "private-2");
        http.Expect(HttpMethod.Post, ReplyUrl)
            .WithJsonBody(body => body.GetProperty("text").GetString() == "All messages have been sent.")
            .RespondJson(new { id = "completion" });

        await RunAsync(http, "messageall");
        http.Verify();
    }

    [Fact]
    public async Task MessageAll_FailedConversationDoesNotReportSuccess()
    {
        using var http = new HttpSimulation();
        http.Expect(HttpMethod.Get, HttpSampleHost.ServiceUrl + "v3/conversations/team-id/pagedmembers?pageSize=100")
            .RespondJson(new { members = new[] { new { id = "member-1", name = "First Member" } } });
        http.Expect(HttpMethod.Post, HttpSampleHost.ServiceUrl + "v3/conversations")
            .RespondJson(new { error = new { code = "Forbidden", message = "Synthetic create failure" } },
                HttpStatusCode.Forbidden);

        var exception = await Assert.ThrowsAsync<ErrorResponseException>(() => RunAsync(http, "messageall"));

        Assert.Equal("Forbidden", exception.Body.Error.Code);
        http.Verify();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StreamingFinalTimeout_UsesCompletePayloadForConnectorRecovery(bool recoveryFails)
    {
        using var http = new HttpSimulation();
        http.Expect(HttpMethod.Post, ReplyUrl)
            .WithJsonBody(body => body.GetProperty("text").GetString() == "Streaming validation completed.")
            .RespondJson(new
            {
                error = new
                {
                    code = "ContentStreamNotAllowed",
                    message = "Content stream finished due to exceeded streaming time."
                }
            }, HttpStatusCode.BadRequest);
        http.Expect(HttpMethod.Put, HttpSampleHost.ServiceUrl + "v3/conversations/conversation-id/activities/stream-id")
            .WithJsonBody(body =>
                body.GetProperty("text").GetString() == "Streaming validation completed." &&
                body.GetProperty("attachments").GetArrayLength() == 1 &&
                !body.GetRawText().Contains("\"streamType\""))
            .RespondJson(recoveryFails
                ? (object)new { error = new { code = "Forbidden", message = "Synthetic recovery failure" } }
                : new { id = "stream-id" },
                recoveryFails ? HttpStatusCode.Forbidden : HttpStatusCode.OK);

        await RunAsync(http, "validatestream", captureTyping: true);
        http.Verify();
    }

    private static void ExpectPrivateConversation(HttpSimulation http, string user, string name, string conversation)
    {
        http.Expect(HttpMethod.Post, HttpSampleHost.ServiceUrl + "v3/conversations")
            .WithJsonBody(body =>
                body.GetProperty("members")[0].GetProperty("id").GetString() == user &&
                !body.GetProperty("isGroup").GetBoolean() &&
                body.GetProperty("tenantId").GetString() == "tenant-id")
            .RespondJson(new { id = conversation });
        http.Expect(HttpMethod.Post, HttpSampleHost.ServiceUrl + $"v3/conversations/{conversation}/activities")
            .WithJsonBody(body => body.GetProperty("text").GetString() == $"Hello {name}. I'm a Teams agent.")
            .RespondJson(new { id = $"message-{user}" });
    }

    private static async Task RunAsync(HttpSimulation http, string command, object value = null, bool captureTyping = false)
    {
        var clients = new HttpSampleHost.SimulationClientFactory(http);
        var adapter = new ConnectorAdapter(new ConnectorFactory(clients), captureTyping);
        var agent = new TeamsConversationAgent(new AgentApplicationOptions(new MemoryStorage(), NullLoggerFactory.Instance)
        {
            StartTypingTimer = false,
            HttpClientFactory = clients
        }, NullLogger<TeamsConversationAgent>.Instance);
        var activity = HttpSampleHost.TeamsMessage(command);
        activity.Id = "incoming-message";
        activity.ServiceUrl = HttpSampleHost.ServiceUrl;
        activity.From = new ChannelAccount("user-id", "Incoming User");
        activity.Recipient = new ChannelAccount("agent-id", "Agent");
        activity.Conversation = new ConversationAccount(id: "conversation-id")
        {
            ConversationType = "channel",
            TenantId = "tenant-id"
        };
        activity.ReplyToId = "existing-card";
        activity.Value = value;
        var identity = new ClaimsIdentity(new[] { new Claim("aud", "agent-id") }, "sample-test");

        await adapter.ProcessActivityAsync(identity, activity, agent.OnTurnAsync, CancellationToken.None);
    }

    private sealed class ConnectorAdapter(IChannelServiceClientFactory factory, bool captureTyping)
        : ChannelServiceAdapterBase(factory)
    {
        public override Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext context, IActivity[] activities, CancellationToken cancellationToken)
        {
            // Intermediate timing is not under test; the final send and recovery use the real REST client.
            if (captureTyping && activities.Length == 1 && activities[0].Type == ActivityTypes.Typing)
            {
                return Task.FromResult(new[] { new ResourceResponse("stream-id") });
            }
            return base.SendActivitiesAsync(context, activities, cancellationToken);
        }
    }

    private sealed class ConnectorFactory(IHttpClientFactory clients) : IChannelServiceClientFactory
    {
        public Task<IConnectorClient> CreateConnectorClientAsync(ITurnContext turnContext, string audience = null,
            IList<string> scopes = null, bool useAnonymous = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<IConnectorClient>(new RestConnectorClient(
                new Uri(turnContext.Activity.ServiceUrl), clients, () => Task.FromResult("synthetic-agent-token")));

        public Task<IConnectorClient> CreateConnectorClientAsync(ClaimsIdentity identity, string serviceUrl,
            string audience, CancellationToken cancellationToken, IList<string> scopes = null, bool useAnonymous = false) =>
            Task.FromResult<IConnectorClient>(new RestConnectorClient(
                new Uri(serviceUrl), clients, () => Task.FromResult("synthetic-agent-token")));

        public Task<IUserTokenClient> CreateUserTokenClientAsync(ClaimsIdentity identity, bool? useAnonymous = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IUserTokenClient>(new RestUserTokenClient(
                "agent-id", new Uri("https://tokens.example.test/"), clients, () => Task.FromResult("synthetic-agent-token")));
    }
}
