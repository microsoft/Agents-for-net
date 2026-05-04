// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using System.Linq;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class ActivityRouteAttributeTests
    {
        [Fact]
        public async Task MessageRouteAttribute_Any()
        {
            var app = new TestApp(new AgentApplicationOptions((IStorage) null));
            var turnContext = new Mock<ITurnContext>();
            turnContext
                .Setup(c => c.Activity)
                .Returns(new Activity() { Type = ActivityTypes.Message });

            await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

            Assert.Single(app.calls);
            Assert.Equal("OnAnyMessageAsync", app.calls[0]);
        }

        [Fact]
        public async Task MessageRouteAttribute_Text()
        {
            var app = new TestApp(new AgentApplicationOptions((IStorage)null));
            var turnContext = new Mock<ITurnContext>();
            turnContext
                .Setup(c => c.Activity)
                .Returns(new Activity() { Type = "message", Text = "-test" });

            await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

            Assert.Single(app.calls);
            Assert.Equal("OnTestAsync", app.calls[0]);
        }

        [Fact]
        public async Task MessageRouteAttribute_Regex()
        {
            var app = new TestApp(new AgentApplicationOptions((IStorage)null));
            var turnContext = new Mock<ITurnContext>();
            turnContext
                .Setup(c => c.Activity)
                .Returns(new Activity() { Type = "message", Text = "testActivity" });

            await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

            Assert.Single(app.calls);
            Assert.Equal("OnRegExAsync", app.calls[0]);
        }

        // ---------------------------------------------------------------------------
        // ActivityRouteAttribute
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task ActivityRouteAttribute_ExactType()
        {
            var app = new ActivityRouteTypeApp(new AgentApplicationOptions((IStorage)null));
            var turnContext = new Mock<ITurnContext>();
            turnContext.Setup(c => c.Activity).Returns(new Activity { Type = ActivityTypes.Event });

            await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

            Assert.Single(app.calls);
            Assert.Equal("OnEvent", app.calls[0]);
        }

        [Fact]
        public async Task ActivityRouteAttribute_Regex()
        {
            var app = new ActivityRouteRegexApp(new AgentApplicationOptions((IStorage)null));
            var turnContext = new Mock<ITurnContext>();
            turnContext.Setup(c => c.Activity).Returns(new Activity { Type = ActivityTypes.Invoke });

            await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

            Assert.Single(app.calls);
            Assert.Equal("OnEventOrInvoke", app.calls[0]);
        }

        [Fact]
        public async Task ActivityRouteAttribute_Any_FiresWhenNoOtherRouteMatches()
        {
            var app = new ActivityRouteAnyApp(new AgentApplicationOptions((IStorage)null));
            var turnContext = new Mock<ITurnContext>();
            // Use a type that the specific routes don't match.
            turnContext.Setup(c => c.Activity).Returns(new Activity { Type = "customActivityType" });

            await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

            Assert.Single(app.calls);
            Assert.Equal("OnAny", app.calls[0]);
        }

        // ---------------------------------------------------------------------------
        // InstallationUpdateRouteAttribute
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task InstallationUpdateRouteAttribute_FiresOnInstallationUpdate()
        {
            var app = new InstallationUpdateRouteApp(new AgentApplicationOptions((IStorage)null));
            var turnContext = new Mock<ITurnContext>();
            turnContext.Setup(c => c.Activity).Returns(new Activity { Type = ActivityTypes.InstallationUpdate });

            await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

            Assert.Single(app.calls);
            Assert.Equal("OnInstallationUpdate", app.calls[0]);
        }

        // ---------------------------------------------------------------------------
        // EventRouteAttribute
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task EventRouteAttribute_ExactName()
        {
            var app = new EventRouteNameApp(new AgentApplicationOptions((IStorage)null));
            var turnContext = new Mock<ITurnContext>();
            turnContext.Setup(c => c.Activity).Returns(new Activity { Type = ActivityTypes.Event, Name = "myEvent" });

            await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

            Assert.Single(app.calls);
            Assert.Equal("OnMyEvent", app.calls[0]);
        }

        [Fact]
        public async Task EventRouteAttribute_NameRegex()
        {
            var app = new EventRouteNameRegexApp(new AgentApplicationOptions((IStorage)null));
            var turnContext = new Mock<ITurnContext>();
            turnContext.Setup(c => c.Activity).Returns(new Activity { Type = ActivityTypes.Event, Name = "mySpecialEvent" });

            await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

            Assert.Single(app.calls);
            Assert.Equal("OnMyRegexEvent", app.calls[0]);
        }

        [Fact]
        public async Task EventRouteAttribute_Any_FiresWhenNoNamedRouteMatches()
        {
            var app = new EventRouteAnyApp(new AgentApplicationOptions((IStorage)null));
            var turnContext = new Mock<ITurnContext>();
            turnContext.Setup(c => c.Activity).Returns(new Activity { Type = ActivityTypes.Event, Name = "unknownEvent" });

            await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

            Assert.Single(app.calls);
            Assert.Equal("OnAnyEvent", app.calls[0]);
        }

        // ---------------------------------------------------------------------------
        // ConversationUpdateRouteAttribute
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task ConversationUpdateRouteAttribute_Any()
        {
            var app = new ConversationUpdateRouteApp(new AgentApplicationOptions((IStorage)null));
            var turnContext = new Mock<ITurnContext>();
            turnContext.Setup(c => c.Activity).Returns(new Activity { Type = ActivityTypes.ConversationUpdate });

            await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

            Assert.Single(app.calls);
            Assert.Equal("OnAnyConversationUpdate", app.calls[0]);
        }

        // ---------------------------------------------------------------------------
        // MembersAddedRouteAttribute / MembersRemovedRouteAttribute
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task MembersAddedRouteAttribute_FiresOnMembersAdded()
        {
            var app = new MembersAddedRouteApp(new AgentApplicationOptions((IStorage)null));
            var turnContext = new Mock<ITurnContext>();
            turnContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                MembersAdded = new List<ChannelAccount> { new ChannelAccount { Id = "user1" } }
            });

            await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

            Assert.Single(app.calls);
            Assert.Equal("OnMembersAdded", app.calls[0]);
        }

        [Fact]
        public async Task MembersRemovedRouteAttribute_FiresOnMembersRemoved()
        {
            var app = new MembersRemovedRouteApp(new AgentApplicationOptions((IStorage)null));
            var turnContext = new Mock<ITurnContext>();
            turnContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                MembersRemoved = new List<ChannelAccount> { new ChannelAccount { Id = "user1" } }
            });

            await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

            Assert.Single(app.calls);
            Assert.Equal("OnMembersRemoved", app.calls[0]);
        }

        // ---------------------------------------------------------------------------
        // MessageReactionsAddedRouteAttribute / MessageReactionsRemovedRouteAttribute
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task MessageReactionsAddedRouteAttribute_FiresOnReactionsAdded()
        {
            var app = new MessageReactionsAddedRouteApp(new AgentApplicationOptions((IStorage)null));
            var turnContext = new Mock<ITurnContext>();
            turnContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.MessageReaction,
                ReactionsAdded = new List<MessageReaction> { new MessageReaction { Type = "like" } }
            });

            await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

            Assert.Single(app.calls);
            Assert.Equal("OnReactionsAdded", app.calls[0]);
        }

        [Fact]
        public async Task MessageReactionsRemovedRouteAttribute_FiresOnReactionsRemoved()
        {
            var app = new MessageReactionsRemovedRouteApp(new AgentApplicationOptions((IStorage)null));
            var turnContext = new Mock<ITurnContext>();
            turnContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.MessageReaction,
                ReactionsRemoved = new List<MessageReaction> { new MessageReaction { Type = "like" } }
            });

            await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

            Assert.Single(app.calls);
            Assert.Equal("OnReactionsRemoved", app.calls[0]);
        }

        // ---------------------------------------------------------------------------
        // EndOfConversationRouteAttribute
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task EndOfConversationRouteAttribute_FiresOnEndOfConversation()
        {
            var app = new EndOfConversationRouteApp(new AgentApplicationOptions((IStorage)null));
            var turnContext = new Mock<ITurnContext>();
            turnContext.Setup(c => c.Activity).Returns(new Activity { Type = ActivityTypes.EndOfConversation });

            await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

            Assert.Single(app.calls);
            Assert.Equal("OnEndOfConversation", app.calls[0]);
        }

        // ---------------------------------------------------------------------------
        // Static method as autoSignInHandlers delegate
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task RouteAttribute_StaticSignInHandlers_AppConstructsAndRoutesFire()
        {
            // Regression test for the fix that allows static methods to be used as autoSignInHandlers.
            // Prior to the fix, CreateDelegate with a target threw for static methods.
            var app = new StaticSignInHandlersApp(new AgentApplicationOptions((IStorage)null));
            var turnContext = new Mock<ITurnContext>();
            turnContext.Setup(c => c.Activity).Returns(new Activity { Type = ActivityTypes.Message, Text = "hello" });

            await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

            Assert.Single(app.calls);
            Assert.Equal("OnMessageWithStaticHandlers", app.calls[0]);
        }
    }

    // ---------------------------------------------------------------------------
    // Test agent apps
    // ---------------------------------------------------------------------------

    class ActivityRouteTypeApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> calls = [];

        [ActivityRoute(ActivityTypes.Event)]
        public Task OnEvent(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnEvent"); return Task.CompletedTask; }
    }

    class ActivityRouteRegexApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> calls = [];

        [ActivityRoute(typeRegex: "event|invoke")]
        public Task OnEventOrInvoke(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnEventOrInvoke"); return Task.CompletedTask; }
    }

    class ActivityRouteAnyApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> calls = [];

        [ActivityRoute(ActivityTypes.Event)]
        public Task OnEvent(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnEvent"); return Task.CompletedTask; }

        [ActivityRoute]  // matches anything — registered RouteRank.Last
        public Task OnAny(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnAny"); return Task.CompletedTask; }
    }

    class InstallationUpdateRouteApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> calls = [];

        [InstallationUpdateRoute]
        public Task OnInstallationUpdate(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnInstallationUpdate"); return Task.CompletedTask; }
    }

    class EventRouteNameApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> calls = [];

        [EventRoute(name: "myEvent")]
        public Task OnMyEvent(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnMyEvent"); return Task.CompletedTask; }
    }

    class EventRouteNameRegexApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> calls = [];

        [EventRoute(nameRegex: "my.*Event")]
        public Task OnMyRegexEvent(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnMyRegexEvent"); return Task.CompletedTask; }
    }

    class EventRouteAnyApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> calls = [];

        [EventRoute(name: "knownEvent")]
        public Task OnKnownEvent(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnKnownEvent"); return Task.CompletedTask; }

        [EventRoute]  // matches any event — registered RouteRank.Last
        public Task OnAnyEvent(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnAnyEvent"); return Task.CompletedTask; }
    }

    class ConversationUpdateRouteApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> calls = [];

        [ConversationUpdateRoute]
        public Task OnAnyConversationUpdate(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnAnyConversationUpdate"); return Task.CompletedTask; }
    }

    class MembersAddedRouteApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> calls = [];

        [MembersAddedRoute]
        public Task OnMembersAdded(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnMembersAdded"); return Task.CompletedTask; }
    }

    class MembersRemovedRouteApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> calls = [];

        [MembersRemovedRoute]
        public Task OnMembersRemoved(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnMembersRemoved"); return Task.CompletedTask; }
    }

    class MessageReactionsAddedRouteApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> calls = [];

        [MessageReactionsAddedRoute]
        public Task OnReactionsAdded(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnReactionsAdded"); return Task.CompletedTask; }
    }

    class MessageReactionsRemovedRouteApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> calls = [];

        [MessageReactionsRemovedRoute]
        public Task OnReactionsRemoved(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnReactionsRemoved"); return Task.CompletedTask; }
    }

    class EndOfConversationRouteApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> calls = [];

        [EndOfConversationRoute]
        public Task OnEndOfConversation(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnEndOfConversation"); return Task.CompletedTask; }
    }

    class StaticSignInHandlersApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> calls = [];

        public static string[] GetSignInHandlers(ITurnContext _) => ["handler1", "handler2"];

        [MessageRoute(text: "hello", autoSignInHandlers: nameof(GetSignInHandlers))]
        public Task OnMessageWithStaticHandlers(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnMessageWithStaticHandlers"); return Task.CompletedTask; }
    }

    class TestApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> calls = [];

        [MessageRoute]
        public Task OnAnyMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            calls.Add("OnAnyMessageAsync");
            return Task.CompletedTask;
        }

        [MessageRoute(text: "-test")]
        public Task OnTestAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            calls.Add("OnTestAsync");
            return Task.CompletedTask;
        }

        [MessageRoute(textRegex: "test.*")]
        public Task OnRegExAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            calls.Add("OnRegExAsync");
            return Task.CompletedTask;
        }
    }
}
