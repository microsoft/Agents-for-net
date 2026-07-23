// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class AgentApplicationAttributeTests
    {
        [Fact]
        public async Task ActivityRouteAttribute_AddRoute_CreatesWorkingRoute()
        {
            var adapter = new TestAdapter();
            var app = new TestActivityRouteApp(new TestApplicationOptions(new MemoryStorage()));

            await new TestFlow(adapter, (ctx, ct) => app.OnTurnAsync(ctx, ct))
                .Send(new Activity { Type = ActivityTypes.Event })
                .StartTestAsync();

            Assert.True(app.HandlerCalled);
        }

        [Fact]
        public async Task ActivityRouteAttribute_AddRoute_DoesNotFireForOtherType()
        {
            var adapter = new TestAdapter();
            var app = new TestActivityRouteApp(new TestApplicationOptions(new MemoryStorage()));

            await new TestFlow(adapter, (ctx, ct) => app.OnTurnAsync(ctx, ct))
                .Send(new Activity { Type = ActivityTypes.Message })
                .StartTestAsync();

            Assert.False(app.HandlerCalled);
        }

        [Fact]
        public async Task MessageRouteAttribute_NoText_MatchesAnyMessage()
        {
            var adapter = new TestAdapter();
            var app = new TestMessageRouteAnyApp(new TestApplicationOptions(new MemoryStorage()));

            await new TestFlow(adapter, (ctx, ct) => app.OnTurnAsync(ctx, ct))
                .Send("anything at all")
                .StartTestAsync();

            Assert.True(app.HandlerCalled);
        }

        [Fact]
        public async Task MessageRouteAttribute_WithText_MatchesExactText()
        {
            var adapter = new TestAdapter();
            var app = new TestMessageRouteTextApp(new TestApplicationOptions(new MemoryStorage()));

            await new TestFlow(adapter, (ctx, ct) => app.OnTurnAsync(ctx, ct))
                .Send("hello")
                .StartTestAsync();

            Assert.True(app.HandlerCalled);
        }

        [Fact]
        public async Task MessageRouteAttribute_WithText_DoesNotMatchOtherText()
        {
            var adapter = new TestAdapter();
            var app = new TestMessageRouteTextApp(new TestApplicationOptions(new MemoryStorage()));

            await new TestFlow(adapter, (ctx, ct) => app.OnTurnAsync(ctx, ct))
                .Send("goodbye")
                .StartTestAsync();

            Assert.False(app.HandlerCalled);
        }

        [Fact]
        public async Task EventRouteAttribute_NoName_MatchesAnyEvent()
        {
            var adapter = new TestAdapter();
            var app = new TestEventRouteAnyApp(new TestApplicationOptions(new MemoryStorage()));

            await new TestFlow(adapter, (ctx, ct) => app.OnTurnAsync(ctx, ct))
                .Send(new Activity { Type = ActivityTypes.Event, Name = "someEvent" })
                .StartTestAsync();

            Assert.True(app.HandlerCalled);
        }

        [Fact]
        public async Task EventRouteAttribute_WithName_MatchesSpecificEvent()
        {
            var adapter = new TestAdapter();
            var app = new TestEventRouteNameApp(new TestApplicationOptions(new MemoryStorage()));

            await new TestFlow(adapter, (ctx, ct) => app.OnTurnAsync(ctx, ct))
                .Send(new Activity { Type = ActivityTypes.Event, Name = "myEvent" })
                .StartTestAsync();

            Assert.True(app.HandlerCalled);
        }

        [Fact]
        public async Task EventRouteAttribute_WithName_DoesNotMatchOtherEvent()
        {
            var adapter = new TestAdapter();
            var app = new TestEventRouteNameApp(new TestApplicationOptions(new MemoryStorage()));

            await new TestFlow(adapter, (ctx, ct) => app.OnTurnAsync(ctx, ct))
                .Send(new Activity { Type = ActivityTypes.Event, Name = "otherEvent" })
                .StartTestAsync();

            Assert.False(app.HandlerCalled);
        }

        [Fact]
        public async Task ConversationUpdateRouteAttribute_NoEvent_MatchesAnyConversationUpdate()
        {
            var adapter = new TestAdapter();
            var app = new TestConversationUpdateRouteAnyApp(new TestApplicationOptions(new MemoryStorage()));

            await new TestFlow(adapter, (ctx, ct) => app.OnTurnAsync(ctx, ct))
                .Send(new Activity { Type = ActivityTypes.ConversationUpdate })
                .StartTestAsync();

            Assert.True(app.HandlerCalled);
        }

        [Fact]
        public async Task MembersAddedRouteAttribute_AddRoute_CreatesWorkingRoute()
        {
            var adapter = new TestAdapter();
            var app = new TestMembersAddedRouteApp(new TestApplicationOptions(new MemoryStorage()));

            await new TestFlow(adapter, (ctx, ct) => app.OnTurnAsync(ctx, ct))
                .Send(new Activity { Type = ActivityTypes.ConversationUpdate, MembersAdded = [new() { Id = "user1" }] })
                .StartTestAsync();

            Assert.True(app.HandlerCalled);
        }

        [Fact]
        public async Task MembersAddedRouteAttribute_AddRoute_DoesNotFireWithoutMembers()
        {
            var adapter = new TestAdapter();
            var app = new TestMembersAddedRouteApp(new TestApplicationOptions(new MemoryStorage()));

            await new TestFlow(adapter, (ctx, ct) => app.OnTurnAsync(ctx, ct))
                .Send(new Activity { Type = ActivityTypes.ConversationUpdate })
                .StartTestAsync();

            Assert.False(app.HandlerCalled);
        }

        [Fact]
        public async Task MembersRemovedRouteAttribute_AddRoute_CreatesWorkingRoute()
        {
            var adapter = new TestAdapter();
            var app = new TestMembersRemovedRouteApp(new TestApplicationOptions(new MemoryStorage()));

            await new TestFlow(adapter, (ctx, ct) => app.OnTurnAsync(ctx, ct))
                .Send(new Activity { Type = ActivityTypes.ConversationUpdate, MembersRemoved = [new() { Id = "user1" }] })
                .StartTestAsync();

            Assert.True(app.HandlerCalled);
        }

        [Fact]
        public async Task MessageReactionsAddedRouteAttribute_AddRoute_CreatesWorkingRoute()
        {
            var adapter = new TestAdapter();
            var app = new TestMessageReactionsAddedRouteApp(new TestApplicationOptions(new MemoryStorage()));

            await new TestFlow(adapter, (ctx, ct) => app.OnTurnAsync(ctx, ct))
                .Send(new Activity { Type = ActivityTypes.MessageReaction, ReactionsAdded = [new() { Type = "like" }] })
                .StartTestAsync();

            Assert.True(app.HandlerCalled);
        }

        [Fact]
        public async Task MessageReactionsRemovedRouteAttribute_AddRoute_CreatesWorkingRoute()
        {
            var adapter = new TestAdapter();
            var app = new TestMessageReactionsRemovedRouteApp(new TestApplicationOptions(new MemoryStorage()));

            await new TestFlow(adapter, (ctx, ct) => app.OnTurnAsync(ctx, ct))
                .Send(new Activity { Type = ActivityTypes.MessageReaction, ReactionsRemoved = [new() { Type = "like" }] })
                .StartTestAsync();

            Assert.True(app.HandlerCalled);
        }

        [Fact]
        public async Task HandoffRouteAttribute_AddRoute_CreatesWorkingRoute()
        {
            var adapter = new TestAdapter();
            var app = new TestHandoffRouteApp(new TestApplicationOptions(new MemoryStorage()));

            await new TestFlow(adapter, (ctx, ct) => app.OnTurnAsync(ctx, ct))
                .Send(new Activity { Type = ActivityTypes.Invoke, Name = "handoff/action", Value = new { Continuation = "test-token" } })
                .StartTestAsync();

            Assert.True(app.HandlerCalled);
        }

        [Fact]
        public async Task FeedbackLoopRouteAttribute_AddRoute_CreatesWorkingRoute()
        {
            var adapter = new TestAdapter();
            var app = new TestFeedbackLoopRouteApp(new TestApplicationOptions(new MemoryStorage()));

            await new TestFlow(adapter, (ctx, ct) => app.OnTurnAsync(ctx, ct))
                .Send(new Activity { Type = ActivityTypes.Invoke, Name = "message/submitAction", Value = new { actionName = "feedback" } })
                .StartTestAsync();

            Assert.True(app.HandlerCalled);
        }

        [Fact]
        public async Task MessageRouteAttribute_StaticHandler_DoesNotThrowAndFiresRoute()
        {
            TestStaticMessageRouteApp.HandlerCalled = false;
            var adapter = new TestAdapter();
            var app = new TestStaticMessageRouteApp(new TestApplicationOptions(new MemoryStorage()));

            await new TestFlow(adapter, (ctx, ct) => app.OnTurnAsync(ctx, ct))
                .Send("any message")
                .StartTestAsync();

            Assert.True(TestStaticMessageRouteApp.HandlerCalled);
        }

        [Fact]
        public async Task ActivityRouteAttribute_StaticHandler_DoesNotThrowAndFiresRoute()
        {
            TestStaticActivityRouteApp.HandlerCalled = false;
            var adapter = new TestAdapter();
            var app = new TestStaticActivityRouteApp(new TestApplicationOptions(new MemoryStorage()));

            await new TestFlow(adapter, (ctx, ct) => app.OnTurnAsync(ctx, ct))
                .Send(new Activity { Type = ActivityTypes.Event })
                .StartTestAsync();

            Assert.True(TestStaticActivityRouteApp.HandlerCalled);
        }
    }

    class TestActivityRouteApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public bool HandlerCalled { get; private set; }

        [ActivityRoute(ActivityTypes.Event)]
        public Task OnEventActivityAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            return Task.CompletedTask;
        }
    }

    class TestMessageRouteAnyApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public bool HandlerCalled { get; private set; }

        [MessageRoute]
        public Task OnAnyMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            return Task.CompletedTask;
        }
    }

    class TestMessageRouteTextApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public bool HandlerCalled { get; private set; }

        [MessageRoute("hello")]
        public Task OnHelloMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            return Task.CompletedTask;
        }
    }

    class TestEventRouteAnyApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public bool HandlerCalled { get; private set; }

        [EventRoute]
        public Task OnAnyEventAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            return Task.CompletedTask;
        }
    }

    class TestEventRouteNameApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public bool HandlerCalled { get; private set; }

        [EventRoute("myEvent")]
        public Task OnMyEventAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            return Task.CompletedTask;
        }
    }

    class TestConversationUpdateRouteAnyApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public bool HandlerCalled { get; private set; }

        [ConversationUpdateRoute]
        public Task OnAnyConversationUpdateAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            return Task.CompletedTask;
        }
    }

    class TestMembersAddedRouteApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public bool HandlerCalled { get; private set; }

        [MembersAddedRoute]
        public Task OnMembersAddedAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            return Task.CompletedTask;
        }
    }

    class TestMembersRemovedRouteApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public bool HandlerCalled { get; private set; }

        [MembersRemovedRoute]
        public Task OnMembersRemovedAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            return Task.CompletedTask;
        }
    }

    class TestMessageReactionsAddedRouteApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public bool HandlerCalled { get; private set; }

        [MessageReactionsAddedRoute]
        public Task OnReactionsAddedAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            return Task.CompletedTask;
        }
    }

    class TestMessageReactionsRemovedRouteApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public bool HandlerCalled { get; private set; }

        [MessageReactionsRemovedRoute]
        public Task OnReactionsRemovedAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            return Task.CompletedTask;
        }
    }

    class TestHandoffRouteApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public bool HandlerCalled { get; private set; }

        [HandoffRoute]
        public Task OnHandoffAsync(ITurnContext turnContext, ITurnState turnState, string continuation, CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            return Task.CompletedTask;
        }
    }

    class TestFeedbackLoopRouteApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public bool HandlerCalled { get; private set; }

        [FeedbackLoopRoute]
        public Task OnFeedbackAsync(ITurnContext turnContext, ITurnState turnState, FeedbackData feedbackData, CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            return Task.CompletedTask;
        }
    }

    // Regression: static route handlers must not throw ArgumentException from CreateDelegate.
    class TestStaticMessageRouteApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public static bool HandlerCalled;

        [MessageRoute]
        public static Task OnAnyMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            return Task.CompletedTask;
        }
    }

    class TestStaticActivityRouteApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public static bool HandlerCalled;

        [ActivityRoute(ActivityTypes.Event)]
        public static Task OnEventActivityAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            return Task.CompletedTask;
        }
    }
}
