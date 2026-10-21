// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.A2A.Pipeline;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.A2A.Tests;

public class A2AProactiveTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SendActivity_ContinuesStoredOrSuppliedTaskAndPublishesUpdate(bool stored)
    {
        await using var fixture = await Fixture.CreateAsync();
        var conversation = fixture.Conversation;
        if (stored)
        {
            await fixture.Agent.Proactive.StoreConversationAsync(conversation);
            await fixture.Agent.Proactive.SendActivityAsync(fixture.Adapter, "task-1", MessageFactory.Text("External update"));
        }
        else
        {
            await Proactive.SendActivityAsync(fixture.Adapter, conversation, MessageFactory.Text("External update"));
        }

        var task = await fixture.Tasks.GetTaskAsync("task-1");
        Assert.Equal("context-1", task.ContextId);
        Assert.Equal(TaskState.Working, task.Status.State);
        Assert.Equal("External update", task.Status.Message.Parts[0].Text);
        var update = Assert.Single(fixture.Sender.Events).StatusUpdate;
        Assert.Equal("task-1", update.TaskId);
        Assert.Equal("context-1", update.ContextId);
        Assert.Equal("External update", update.Status.Message.Parts[0].Text);
    }

    [Fact]
    public async Task ContinueConversation_AttributedTypedHandlerReceivesClient()
    {
        await using var fixture = await Fixture.CreateAsync();
        var agent = new TypedContinuationAgent(new AgentApplicationOptions(new MemoryStorage()));
        agent.RegisterExtension(new A2AAgentExtension(agent));
        var route = new ContinueConversationRoute<TypedContinuationAgent>("OnContinueAsync", string.Empty);

        await agent.Proactive.ContinueConversationAsync(fixture.Adapter, fixture.Conversation, route.RouteHandler(agent));

        Assert.NotNull(agent.Client);
        Assert.Equal("task-1", agent.Client.RequestContext.TaskId);
        Assert.Null(agent.Client.RequestContext.IncomingMessage);
        Assert.Same(fixture.Tasks, agent.Client.TaskStore);
        Assert.NotNull(agent.Client.EventQueue);
        Assert.Equal("Typed continuation", (await fixture.Tasks.GetTaskAsync("task-1")).Status.Message.Parts[0].Text);
    }

    [Fact]
    public async Task ContinueConversation_ProvidesRealA2AContextAndPersistsConversationState()
    {
        await using var fixture = await Fixture.CreateAsync();
        var middlewareTurns = 0;
        fixture.Adapter.Use(new ActionMiddleware(async (context, next, ct) =>
        {
            middlewareTurns++;
            await next(ct);
        }));

        async Task ContinueAsync(ITurnContext context, ITurnState state, CancellationToken ct)
        {
            var client = new A2ATurnContext(context).Client;
            Assert.NotNull(client.RequestContext);
            Assert.Null(client.RequestContext.IncomingMessage);
            Assert.Equal("task-1", client.RequestContext.TaskId);
            Assert.Equal("context-1", client.RequestContext.ContextId);
            Assert.Same(fixture.Tasks, client.TaskStore);
            Assert.NotNull(client.EventQueue);
            var count = state.Conversation.GetValue<int>("count") + 1;
            state.Conversation.SetValue("count", count);
            await context.SendActivityAsync($"Continuation {count}", cancellationToken: ct);
        }

        await fixture.Agent.Proactive.ContinueConversationAsync(fixture.Adapter, fixture.Conversation, ContinueAsync);
        await fixture.Agent.Proactive.ContinueConversationAsync(fixture.Adapter, fixture.Conversation, ContinueAsync);

        Assert.Equal(2, middlewareTurns);
        var task = await fixture.Tasks.GetTaskAsync("task-1");
        Assert.Equal("Continuation 2", task.Status.Message.Parts[0].Text);
        Assert.Equal(2, fixture.Sender.Events.Count);
    }

    [Theory]
    [InlineData(TaskState.Completed)]
    [InlineData(TaskState.Canceled)]
    [InlineData(TaskState.Failed)]
    public async Task ContinueConversation_RejectsTerminalTasksBeforeInvokingCallback(TaskState state)
    {
        await using var fixture = await Fixture.CreateAsync();
        var task = await fixture.Tasks.GetTaskAsync("task-1");
        task.Status.State = state;
        await fixture.Tasks.SaveTaskAsync(task.Id, task);
        var invoked = false;

        var exception = await Assert.ThrowsAsync<TaskContinuationException>(() =>
            fixture.Adapter.ContinueConversationAsync(new ClaimsIdentity(), fixture.Conversation.Reference, (_, _) =>
            {
                invoked = true;
                return Task.CompletedTask;
            }));

        Assert.Equal(TaskContinuationFailureKind.NonContinuableTask, exception.FailureKind);
        Assert.False(invoked);
        Assert.Empty(fixture.Sender.Events);
    }

    [Fact]
    public async Task ContinueConversation_RequiresRegisteredPushDelivery()
    {
        await using var fixture = await Fixture.CreateAsync(registerPush: false);
        var exception = await Assert.ThrowsAsync<TaskContinuationException>(() =>
            fixture.Adapter.ContinueConversationAsync(new ClaimsIdentity(), fixture.Conversation.Reference,
                (context, ct) => context.SendActivityAsync("Not delivered", cancellationToken: ct)));

        Assert.Equal(TaskContinuationFailureKind.MissingPushRegistration, exception.FailureKind);
        Assert.Empty(fixture.Sender.Events);
    }

    [Fact]
    public async Task ContinueConversation_PropagatesCallbackFailure()
    {
        await using var fixture = await Fixture.CreateAsync();
        var failure = new InvalidOperationException("Continuation failed");

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Agent.Proactive.ContinueConversationAsync(fixture.Adapter, fixture.Conversation,
                (_, _, _) => throw failure));

        Assert.Same(failure, actual);
    }

    [Fact]
    public async Task SendActivity_PropagatesSendHookFailure()
    {
        await using var fixture = await Fixture.CreateAsync();
        var failure = new InvalidOperationException("Send hook failed");
        fixture.Adapter.Use(new ActionMiddleware(async (context, next, ct) =>
        {
            context.OnSendActivities((_, _, _) => throw failure);
            await next(ct);
        }));
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Proactive.SendActivityAsync(fixture.Adapter, fixture.Conversation, MessageFactory.Text("Not sent")));
        Assert.Same(failure, actual);
    }

    [Fact]
    public async Task ContinueConversation_RejectsMissingTaskBeforeInvokingCallback()
    {
        await using var fixture = await Fixture.CreateAsync(registerPush: false);
        fixture.Conversation.Reference.Conversation.Id = "missing";
        var invoked = false;
        var exception = await Assert.ThrowsAsync<TaskContinuationException>(() =>
            fixture.Adapter.ContinueConversationAsync(new ClaimsIdentity(), fixture.Conversation.Reference, (_, _) =>
            {
                invoked = true;
                return Task.CompletedTask;
            }));
        Assert.Equal(TaskContinuationFailureKind.TaskNotFound, exception.FailureKind);
        Assert.False(invoked);
    }

    [Fact]
    public async Task ContinueConversation_SameTaskReusesCurrentExecution()
    {
        await using var fixture = await Fixture.CreateAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await fixture.Adapter.ContinueConversationAsync(new ClaimsIdentity(), fixture.Conversation.Reference, async (outer, ct) =>
        {
            var original = new A2ATurnContext(outer).Client;
            await fixture.Adapter.ContinueConversationAsync(new ClaimsIdentity(), fixture.Conversation.Reference, async (inner, innerCt) =>
            {
                var client = new A2ATurnContext(inner).Client;
                Assert.Same(original.RequestContext, client.RequestContext);
                Assert.Same(original.EventQueue, client.EventQueue);
                await inner.SendActivityAsync("Nested update", cancellationToken: innerCt);
            }, ct);
        }, timeout.Token);

        Assert.Equal("Nested update", Assert.Single(fixture.Sender.Events).StatusUpdate.Status.Message.Parts[0].Text);
    }

    [Fact]
    public async Task ContinueConversation_CrossTaskDoesNotReuseCurrentExecution()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Tasks.SaveTaskAsync("task-2", new AgentTask
        {
            Id = "task-2",
            ContextId = "context-2",
            Status = new global::A2A.TaskStatus { State = TaskState.InputRequired },
        });
        await fixture.RegisterPushAsync("task-2");
        var second = new ConversationReference
        {
            ChannelId = Channels.A2A,
            Conversation = new ConversationAccount { Id = "task-2" },
            Agent = new ChannelAccount { Id = "assistant" },
            User = new ChannelAccount { Id = "task-2" },
        };
        await fixture.Adapter.ContinueConversationAsync(new ClaimsIdentity(), fixture.Conversation.Reference, async (outer, ct) =>
        {
            var original = new A2ATurnContext(outer).Client;
            await fixture.Adapter.ContinueConversationAsync(new ClaimsIdentity(), second, async (inner, innerCt) =>
            {
                var client = new A2ATurnContext(inner).Client;
                Assert.NotSame(original.RequestContext, client.RequestContext);
                Assert.NotSame(original.EventQueue, client.EventQueue);
                Assert.Null(client.RequestContext.IncomingMessage);
                await inner.SendActivityAsync("Second task", cancellationToken: innerCt);
            }, ct);
            await outer.SendActivityAsync("First task", cancellationToken: ct);
        });

        Assert.Equal("Second task", (await fixture.Tasks.GetTaskAsync("task-2")).Status.Message.Parts[0].Text);
        Assert.Equal("First task", (await fixture.Tasks.GetTaskAsync("task-1")).Status.Message.Parts[0].Text);
    }

    [Fact]
    public async Task ContinueConversation_EndOfConversationCompletesTaskAndPublishesArtifact()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Adapter.ContinueConversationAsync(new ClaimsIdentity(), fixture.Conversation.Reference,
            (context, ct) => context.SendActivityAsync(new Activity
            {
                Type = ActivityTypes.EndOfConversation,
                Text = "Complete",
                Value = new { answer = 42 },
            }, ct));
        var task = await fixture.Tasks.GetTaskAsync("task-1");
        Assert.Equal(TaskState.Completed, task.Status.State);
        Assert.Equal("Complete", task.Status.Message.Parts[0].Text);
        Assert.Single(task.Artifacts);
        Assert.Equal(2, fixture.Sender.Events.Count);
    }

    [Fact]
    public async Task UnsupportedOperations_ThrowWithoutStartingNewTasks()
    {
        await using var fixture = await Fixture.CreateAsync();
        using var context = new TurnContext(fixture.Adapter, fixture.Conversation.Reference.GetContinuationActivity());
        await Assert.ThrowsAsync<NotSupportedException>(() => fixture.Adapter.CreateConversationAsync(
            new ClaimsIdentity(), Channels.A2A, null, null, new ConversationParameters(), (_, _) => Task.CompletedTask));
        await Assert.ThrowsAsync<NotSupportedException>(() => fixture.Adapter.UpdateActivityAsync(context, MessageFactory.Text("update"), default));
        await Assert.ThrowsAsync<NotSupportedException>(() => fixture.Adapter.DeleteActivityAsync(context, fixture.Conversation.Reference, default));
    }

    private sealed class TypedContinuationAgent(AgentApplicationOptions options) : AgentApplication(options)
    {
        public A2AClient Client { get; private set; }

        [ContinueConversation]
        private Task OnContinueAsync(IA2ATurnContext context, ITurnState state, CancellationToken ct)
        {
            Client = context.Client;
            return context.SendActivityAsync("Typed continuation", cancellationToken: ct);
        }
    }

    private sealed class Fixture(ServiceProvider services, InMemoryTaskStore tasks, RecordingSender sender) : IAsyncDisposable
    {
        public InMemoryTaskStore Tasks { get; } = tasks;
        public RecordingSender Sender { get; } = sender;
        public A2AAdapter Adapter { get; } = services.GetRequiredService<A2AAdapter>();
        public AgentApplication Agent { get; } = new(new AgentApplicationOptions(services.GetRequiredService<IStorage>())
        {
            Proactive = new ProactiveOptions(services.GetRequiredService<IStorage>()),
        });
        public Conversation Conversation { get; } = new(new ClaimsIdentity(), new ConversationReference
        {
            ChannelId = Channels.A2A,
            Conversation = new ConversationAccount { Id = "task-1" },
            Agent = new ChannelAccount { Id = "assistant" },
            User = new ChannelAccount { Id = "task-1" },
        });

        public static async Task<Fixture> CreateAsync(bool registerPush = true)
        {
            var tasks = new InMemoryTaskStore();
            await tasks.SaveTaskAsync("task-1", new AgentTask
            {
                Id = "task-1",
                ContextId = "context-1",
                Status = new global::A2A.TaskStatus { State = TaskState.InputRequired },
            });
            var sender = new RecordingSender();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IStorage, MemoryStorage>();
            services.AddSingleton<ITaskStore>(tasks);
            services.AddSingleton<IPushNotificationSender>(sender);
            services.AddA2AAdapter();
            var fixture = new Fixture(services.BuildServiceProvider(), tasks, sender);
            if (registerPush)
            {
                await fixture.RegisterPushAsync("task-1");
            }
            return fixture;
        }

        public async Task RegisterPushAsync(string taskId)
        {
            using var scope = Adapter.BeginRequest(new DefaultHttpContext(), Agent);
            await A2AAdapter.RequestHandlerProxy.CreateTaskPushNotificationConfigAsync(new TaskPushNotificationConfig
            {
                TaskId = taskId,
                Url = "https://8.8.8.8/notify",
            }, null);
        }

        public ValueTask DisposeAsync() => services.DisposeAsync();
    }

    private sealed class RecordingSender : IPushNotificationSender
    {
        public ConcurrentQueue<StreamResponse> Events { get; } = new();

        public Task EnqueueAsync(PushNotificationConfigSnapshot registration, StreamResponse response, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Events.Enqueue(response);
            return Task.CompletedTask;
        }
    }

    private sealed class ActionMiddleware(Func<ITurnContext, NextDelegate, CancellationToken, Task> action) : Builder.IMiddleware
    {
        public Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken = default)
            => action(turnContext, next, cancellationToken);
    }
}
