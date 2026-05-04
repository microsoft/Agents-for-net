// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class ConversationUpdateRouteBuilderTests
    {
        #region Create Tests

        [Fact]
        public void ConversationUpdateRouteBuilder_Create_ReturnsNewInstance()
        {
            var builder = ConversationUpdateRouteBuilder.Create();

            Assert.NotNull(builder);
            Assert.IsType<ConversationUpdateRouteBuilder>(builder);
        }

        #endregion

        #region WithUpdateEvent Tests

        [Fact]
        public async Task ConversationUpdateRouteBuilder_WithUpdateEvent_MembersAdded_MatchesWhenMembersPresent()
        {
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                MembersAdded = new List<ChannelAccount> { new ChannelAccount { Id = "user1" } }
            });

            var route = ConversationUpdateRouteBuilder.Create()
                .WithUpdateEvent(ConversationUpdateEvents.MembersAdded)
                .WithHandler((ctx, state, ct) => Task.CompletedTask)
                .Build();

            Assert.True(await route.Selector(mockContext.Object, CancellationToken.None));
        }

        [Fact]
        public async Task ConversationUpdateRouteBuilder_WithUpdateEvent_MembersAdded_NoMatchWhenMembersEmpty()
        {
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                MembersAdded = new List<ChannelAccount>()
            });

            var route = ConversationUpdateRouteBuilder.Create()
                .WithUpdateEvent(ConversationUpdateEvents.MembersAdded)
                .WithHandler((ctx, state, ct) => Task.CompletedTask)
                .Build();

            Assert.False(await route.Selector(mockContext.Object, CancellationToken.None));
        }

        [Fact]
        public async Task ConversationUpdateRouteBuilder_WithUpdateEvent_MembersAdded_NoMatchWhenMembersNull()
        {
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                MembersAdded = null
            });

            var route = ConversationUpdateRouteBuilder.Create()
                .WithUpdateEvent(ConversationUpdateEvents.MembersAdded)
                .WithHandler((ctx, state, ct) => Task.CompletedTask)
                .Build();

            Assert.False(await route.Selector(mockContext.Object, CancellationToken.None));
        }

        [Fact]
        public async Task ConversationUpdateRouteBuilder_WithUpdateEvent_MembersAdded_NoMatchNonConversationUpdate()
        {
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Message,
                MembersAdded = new List<ChannelAccount> { new ChannelAccount { Id = "user1" } }
            });

            var route = ConversationUpdateRouteBuilder.Create()
                .WithUpdateEvent(ConversationUpdateEvents.MembersAdded)
                .WithHandler((ctx, state, ct) => Task.CompletedTask)
                .Build();

            Assert.False(await route.Selector(mockContext.Object, CancellationToken.None));
        }

        [Fact]
        public async Task ConversationUpdateRouteBuilder_WithUpdateEvent_MembersRemoved_MatchesWhenMembersPresent()
        {
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                MembersRemoved = new List<ChannelAccount> { new ChannelAccount { Id = "user1" } }
            });

            var route = ConversationUpdateRouteBuilder.Create()
                .WithUpdateEvent(ConversationUpdateEvents.MembersRemoved)
                .WithHandler((ctx, state, ct) => Task.CompletedTask)
                .Build();

            Assert.True(await route.Selector(mockContext.Object, CancellationToken.None));
        }

        [Fact]
        public async Task ConversationUpdateRouteBuilder_WithUpdateEvent_MembersRemoved_NoMatchWhenMembersEmpty()
        {
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                MembersRemoved = new List<ChannelAccount>()
            });

            var route = ConversationUpdateRouteBuilder.Create()
                .WithUpdateEvent(ConversationUpdateEvents.MembersRemoved)
                .WithHandler((ctx, state, ct) => Task.CompletedTask)
                .Build();

            Assert.False(await route.Selector(mockContext.Object, CancellationToken.None));
        }

        [Fact]
        public async Task ConversationUpdateRouteBuilder_WithUpdateEvent_UnknownEvent_MatchesAnyConversationUpdate()
        {
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.ConversationUpdate
            });

            var route = ConversationUpdateRouteBuilder.Create()
                .WithUpdateEvent("someOtherEvent")
                .WithHandler((ctx, state, ct) => Task.CompletedTask)
                .Build();

            Assert.True(await route.Selector(mockContext.Object, CancellationToken.None));
        }

        [Fact]
        public async Task ConversationUpdateRouteBuilder_WithUpdateEvent_UnknownEvent_NoMatchNonConversationUpdate()
        {
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Message
            });

            var route = ConversationUpdateRouteBuilder.Create()
                .WithUpdateEvent("someOtherEvent")
                .WithHandler((ctx, state, ct) => Task.CompletedTask)
                .Build();

            Assert.False(await route.Selector(mockContext.Object, CancellationToken.None));
        }

        [Fact]
        public void ConversationUpdateRouteBuilder_WithUpdateEvent_NullEventName_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                ConversationUpdateRouteBuilder.Create().WithUpdateEvent(null));
        }

        [Fact]
        public void ConversationUpdateRouteBuilder_WithUpdateEvent_WhitespaceEventName_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                ConversationUpdateRouteBuilder.Create().WithUpdateEvent("   "));
        }

        [Fact]
        public void ConversationUpdateRouteBuilder_WithUpdateEvent_ThrowsWhenSelectorAlreadyDefined()
        {
            var builder = ConversationUpdateRouteBuilder.Create()
                .WithUpdateEvent(ConversationUpdateEvents.MembersAdded);

            Assert.Throws<InvalidOperationException>(() =>
                builder.WithUpdateEvent(ConversationUpdateEvents.MembersRemoved));
        }

        #endregion

        #region WithSelector Tests

        [Fact]
        public async Task ConversationUpdateRouteBuilder_WithSelector_WrapsWithConversationUpdateTypeCheck()
        {
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.ConversationUpdate
            });

            var selectorCalled = false;
            RouteSelector selector = (ctx, ct) =>
            {
                selectorCalled = true;
                return Task.FromResult(true);
            };

            var route = ConversationUpdateRouteBuilder.Create()
                .WithSelector(selector)
                .WithHandler((ctx, state, ct) => Task.CompletedTask)
                .Build();

            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            Assert.True(result);
            Assert.True(selectorCalled);
        }

        [Fact]
        public async Task ConversationUpdateRouteBuilder_WithSelector_BlocksNonConversationUpdateActivity()
        {
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Message
            });

            var selectorCalled = false;
            RouteSelector selector = (ctx, ct) =>
            {
                selectorCalled = true;
                return Task.FromResult(true);
            };

            var route = ConversationUpdateRouteBuilder.Create()
                .WithSelector(selector)
                .WithHandler((ctx, state, ct) => Task.CompletedTask)
                .Build();

            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            Assert.False(result);
            Assert.False(selectorCalled);
        }

        [Fact]
        public void ConversationUpdateRouteBuilder_WithSelector_ThrowsWhenSelectorAlreadyDefined()
        {
            var builder = ConversationUpdateRouteBuilder.Create()
                .WithSelector((ctx, ct) => Task.FromResult(true));

            Assert.Throws<InvalidOperationException>(() =>
                builder.WithSelector((ctx, ct) => Task.FromResult(false)));
        }

        [Fact]
        public void ConversationUpdateRouteBuilder_WithSelector_ThrowsWhenUpdateEventAlreadyDefined()
        {
            var builder = ConversationUpdateRouteBuilder.Create()
                .WithUpdateEvent(ConversationUpdateEvents.MembersAdded);

            Assert.Throws<InvalidOperationException>(() =>
                builder.WithSelector((ctx, ct) => Task.FromResult(true)));
        }

        #endregion

        #region WithHandler Tests

        [Fact]
        public void ConversationUpdateRouteBuilder_WithHandler_SetsHandler()
        {
            RouteHandler handler = (ctx, state, ct) => Task.CompletedTask;

            var route = ConversationUpdateRouteBuilder.Create()
                .WithUpdateEvent(ConversationUpdateEvents.MembersAdded)
                .WithHandler(handler)
                .Build();

            Assert.Same(handler, route.Handler);
        }

        [Fact]
        public void ConversationUpdateRouteBuilder_Build_WithoutHandler_ThrowsArgumentNullException()
        {
            var builder = ConversationUpdateRouteBuilder.Create()
                .WithUpdateEvent(ConversationUpdateEvents.MembersAdded);

            Assert.Throws<ArgumentNullException>(() => builder.Build());
        }

        #endregion

        #region AsInvoke Tests

        [Fact]
        public void ConversationUpdateRouteBuilder_AsInvoke_DoesNotSetInvokeFlag()
        {
            var route = ConversationUpdateRouteBuilder.Create()
                .WithUpdateEvent(ConversationUpdateEvents.MembersAdded)
                .AsInvoke()
                .WithHandler((ctx, state, ct) => Task.CompletedTask)
                .Build();

            Assert.False(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        #endregion

        #region Agentic and ChannelId Tests

        [Fact]
        public async Task ConversationUpdateRouteBuilder_WithUpdateEvent_RespectsAgenticFlag()
        {
            var agenticContext = new Mock<ITurnContext>();
            agenticContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                MembersAdded = new List<ChannelAccount> { new ChannelAccount { Id = "user1" } },
                Recipient = new ChannelAccount { Role = RoleTypes.AgenticUser }
            });

            var nonAgenticContext = new Mock<ITurnContext>();
            nonAgenticContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                MembersAdded = new List<ChannelAccount> { new ChannelAccount { Id = "user1" } }
            });

            var route = ConversationUpdateRouteBuilder.Create()
                .WithUpdateEvent(ConversationUpdateEvents.MembersAdded)
                .AsAgentic()
                .WithHandler((ctx, state, ct) => Task.CompletedTask)
                .Build();

            Assert.True(await route.Selector(agenticContext.Object, CancellationToken.None));
            Assert.False(await route.Selector(nonAgenticContext.Object, CancellationToken.None));
        }

        [Fact]
        public async Task ConversationUpdateRouteBuilder_WithUpdateEvent_RespectsChannelId()
        {
            var matchingContext = new Mock<ITurnContext>();
            matchingContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                MembersAdded = new List<ChannelAccount> { new ChannelAccount { Id = "user1" } },
                ChannelId = Channels.Msteams
            });

            var nonMatchingContext = new Mock<ITurnContext>();
            nonMatchingContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                MembersAdded = new List<ChannelAccount> { new ChannelAccount { Id = "user1" } },
                ChannelId = Channels.Directline
            });

            var route = ConversationUpdateRouteBuilder.Create()
                .WithUpdateEvent(ConversationUpdateEvents.MembersAdded)
                .WithChannelId(Channels.Msteams)
                .WithHandler((ctx, state, ct) => Task.CompletedTask)
                .Build();

            Assert.True(await route.Selector(matchingContext.Object, CancellationToken.None));
            Assert.False(await route.Selector(nonMatchingContext.Object, CancellationToken.None));
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void ConversationUpdateRouteBuilder_Build_MinimalConfiguration_ReturnsRoute()
        {
            var route = ConversationUpdateRouteBuilder.Create()
                .WithUpdateEvent(ConversationUpdateEvents.MembersAdded)
                .WithHandler((ctx, state, ct) => Task.CompletedTask)
                .Build();

            Assert.NotNull(route);
            Assert.NotNull(route.Selector);
            Assert.NotNull(route.Handler);
            Assert.Null(route.ChannelId);
            Assert.Equal(RouteRank.Unspecified, route.Rank);
            Assert.Equal(RouteFlags.None, route.Flags);
        }

        [Fact]
        public void ConversationUpdateRouteBuilder_FluentAPI_AllMethodsChainCorrectly()
        {
            var route = ConversationUpdateRouteBuilder.Create()
                .WithUpdateEvent(ConversationUpdateEvents.MembersAdded)
                .WithHandler((ctx, state, ct) => Task.CompletedTask)
                .WithChannelId(Channels.Msteams)
                .WithOrderRank(10)
                .AsAgentic()
                .AsNonTerminal()
                .WithOAuthHandlers("handler1,handler2")
                .AsInvoke()
                .Build();

            Assert.NotNull(route);
            Assert.Equal(Channels.Msteams, route.ChannelId);
            Assert.Equal((ushort)10, route.Rank);
            Assert.True(route.Flags.HasFlag(RouteFlags.Agentic));
            Assert.True(route.Flags.HasFlag(RouteFlags.NonTerminal));
            Assert.False(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        #endregion
    }
}
