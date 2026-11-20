// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.MSTeams.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PlansAgent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

#pragma warning disable ExperimentalTeamsPlan // Exercise the experimental sample without any Azure resources.

namespace Microsoft.Agents.SampleTest;

public class TeamsPlansAgentTests
{
    [Theory]
    [InlineData("complete", "complete")]
    [InlineData("complete-stream", "complete")]
    [InlineData("stop", "stop")]
    [InlineData("error", "error")]
    public async Task Commands_ReportVersionedProgressAndCorrectTerminalOperation(string command, string terminal)
    {
        await using var host = CreateHost();
        var replies = new List<IActivity>();
        var input = new Activity { Type = ActivityTypes.Message, Text = command };
        await host.CreateTestFlow()
            .Send(input)
            .AssertReplySatisfies(reply =>
            {
                replies.Add(reply);
                var start = Assert.Single(reply.Entities.OfType<PlanInfoEntity>());
                Assert.Equal(ActivityTypes.Typing, reply.Type);
                Assert.Equal("start", start.PlanType.Value);
                Assert.Collection(start.Steps,
                    step =>
                    {
                        Assert.Equal("prepare", step.Id);
                        Assert.Equal(1, step.Version);
                        Assert.Equal("active", step.Status.Value);
                    },
                    step =>
                    {
                        Assert.Equal("response", step.Id);
                        Assert.Equal(1, step.Version);
                        Assert.Equal("proposed", step.Status.Value);
                    });
                return Task.CompletedTask;
            })
            .StartTestAsync().WaitAsync(TimeSpan.FromSeconds(30), TimeProvider.System);
        DrainReplies(host.Adapter, replies);

        Assert.All(replies, reply => Assert.Equal(input.Id, reply.ReplyToId));
        var operations = replies.SelectMany(reply => reply.Entities ?? []).OfType<PlanInfoEntity>().ToArray();
        Assert.Equal(terminal, operations.Last().PlanType.Value);
        Assert.DoesNotContain(replies.SelectMany(reply => reply.Entities ?? []).OfType<AIEntity>(),
            entity => entity.AdditionalType?.Contains("AIGeneratedContent") == true);

        if (terminal == "complete")
        {
            Assert.Equal(new[] { "start", "continue", "continue", "continue", "complete" },
                operations.Select(operation => operation.PlanType.Value));
            var blocked = Assert.Single(operations[1].Steps);
            Assert.Equal("prepare", blocked.Id);
            Assert.Equal(2, blocked.Version);
            Assert.Equal("blocked", blocked.Status.Value);
            var prepared = operations[2].Steps.Single(step => step.Id == "prepare");
            Assert.Equal(3, prepared.Version);
            Assert.Equal("completed", prepared.Status.Value);
            var source = Assert.Single(prepared.Sources);
            Assert.Equal("https://learn.microsoft.com/microsoft-365/agents-sdk/", source.Url.AbsoluteUri);
            Assert.Equal("Text", source.Icon.Value);
            var delivered = Assert.Single(operations[3].Steps);
            Assert.Equal("response", delivered.Id);
            Assert.Equal(3, delivered.Version);
            Assert.Equal("completed", delivered.Status.Value);

            var final = Assert.Single(replies, reply => reply.Type == ActivityTypes.Message);
            Assert.Equal("This is a fixed demonstration response. The application reported each step explicitly, and this message completes the Plan.", final.Text);
            Assert.Equal("complete", Assert.Single(final.Entities.OfType<PlanInfoEntity>()).PlanType.Value);
            if (command == "complete-stream")
            {
                Assert.Equal(StreamTypes.Final, final.GetStreamingEntity().StreamType);
                Assert.False(string.IsNullOrEmpty(final.GetStreamingEntity().StreamId));
                Assert.Contains(replies, reply => reply.GetStreamingEntity()?.StreamType == StreamTypes.Streaming);
            }
            else
            {
                Assert.Null(final.GetStreamingEntity());
            }
        }
        else
        {
            Assert.DoesNotContain(replies, reply => reply.Type == ActivityTypes.Message);
            Assert.Equal(terminal == "stop" ? new[] { "start", "stop" } : ["start", "continue", "error"],
                operations.Select(operation => operation.PlanType.Value));
            if (terminal == "error")
            {
                var failed = Assert.Single(operations[1].Steps);
                Assert.Equal(2, failed.Version);
                Assert.Equal("error", failed.Status.Value);
            }
        }
        Assert.Empty(host.Adapter.ActiveQueue);
    }

    [Fact]
    public async Task Help_DoesNotStartAPlan()
    {
        await using var host = CreateHost();
        await host.CreateTestFlow().Send("help")
            .AssertReplySatisfies(reply =>
            {
                Assert.Contains("complete-stream", reply.Text);
                Assert.DoesNotContain(reply.Entities ?? [], entity => entity is PlanInfoEntity);
                return Task.CompletedTask;
            })
            .StartTestAsync();

        Assert.Empty(host.Adapter.ActiveQueue);
    }

    [Fact]
    public async Task TargetedPrompt_ExplainsUnsupportedScenarioWithoutBroadcastingAPlan()
    {
        await using var host = CreateHost();
        host.Adapter.Conversation.Agent.Properties["isTargeted"] = JsonSerializer.SerializeToElement(true);
        await host.CreateTestFlow().Send("complete")
            .AssertReplySatisfies(reply =>
            {
                Assert.True(reply.IsTargetedActivity());
                Assert.Equal(host.Adapter.Conversation.User.Id, reply.Recipient.Id);
                Assert.DoesNotContain(reply.Entities ?? [], entity => entity is PlanInfoEntity);
                return Task.CompletedTask;
            })
            .StartTestAsync();

        Assert.Empty(host.Adapter.ActiveQueue);
    }

    private static AgentTestHost CreateHost()
    {
        var host = AgentTestHost.Create(builder =>
        {
            builder.Services.AddSingleton<IStorage, MemoryStorage>();
            builder.Services.AddTransient<IAgent>(services => new PlanAgent(
                new AgentApplicationOptions(services.GetRequiredService<IStorage>(), NullLoggerFactory.Instance)
                {
                    StartTypingTimer = false
                },
                NullLogger<PlanAgent>.Instance));
        });
        host.Adapter.Conversation.ChannelId = Channels.Msteams;
        host.Adapter.Use(new PlanTransportMiddleware());
        return host;
    }

    private static void DrainReplies(TestAdapter adapter, List<IActivity> replies)
    {
        while (adapter.GetNextReply() is { } reply)
        {
            replies.Add(reply);
        }
    }

    private sealed class PlanTransportMiddleware : IMiddleware
    {
        public async Task OnTurnAsync(ITurnContext context, NextDelegate next, CancellationToken cancellationToken)
        {
            using var handler = new PlanTransportHandler(context);
            var factory = new PlanHttpClientFactory(handler);
            context.Services.Set<IConnectorClient>(new RestConnectorClient(
                new Uri(context.Activity.ServiceUrl), factory, null));
            context.Services.Set<IUserTokenClient>(new RestUserTokenClient(
                "bot", new Uri("https://token.example/"), factory, null));
            context.StreamingResponse.InitialDelay = 1;
            context.StreamingResponse.Interval = 1;
            await next(cancellationToken);
        }
    }

    private sealed class PlanHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class PlanTransportHandler(ITurnContext context) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content.ReadAsStringAsync(cancellationToken);
            var activity = ProtocolJsonSerializer.ToObject<Activity>(body);
            // Plan helpers use the connector directly; bridge that fake service boundary into TestFlow's queue.
            var responses = await context.Adapter.SendActivitiesAsync(context, [activity], cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ProtocolJsonSerializer.ToJson(responses[0]), Encoding.UTF8, "application/json")
            };
        }
    }
}

#pragma warning restore ExperimentalTeamsPlan
