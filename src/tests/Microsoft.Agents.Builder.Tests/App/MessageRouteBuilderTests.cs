// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Moq;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class MessageRouteBuilderTests
    {
        #region MessageRouteBuilder.Create Tests

        [Fact]
        public void MessageRouteBuilder_Create_ReturnsNewInstance()
        {
            // Act
            var builder = MessageRouteBuilder.Create();

            // Assert
            Assert.NotNull(builder);
            Assert.IsType<MessageRouteBuilder>(builder);
        }

        #endregion

        #region WithText(string) Tests

        [Fact]
        public void MessageRouteBuilder_WithText_String_SetsSelector()
        {
            // Arrange
            var builder = MessageRouteBuilder.Create();
            var text = "hello";

            // Act
            var result = builder.WithText(text);

            // Assert
            Assert.Same(builder, result);
            var route = builder
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();
            Assert.NotNull(route.Selector);
        }

        [Fact]
        public void MessageRouteBuilder_WithText_String_NullText_ThrowsArgumentNullException()
        {
            // Arrange
            var builder = MessageRouteBuilder.Create();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.WithText((string)null));
        }

        [Fact]
        public void MessageRouteBuilder_WithText_String_EmptyText_ThrowsArgumentException()
        {
            // Arrange
            var builder = MessageRouteBuilder.Create();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => builder.WithText(string.Empty));
        }

        [Fact]
        public void MessageRouteBuilder_WithText_String_WhitespaceText_ThrowsArgumentException()
        {
            // Arrange
            var builder = MessageRouteBuilder.Create();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => builder.WithText("   "));
        }

        [Fact]
        public async Task MessageRouteBuilder_WithText_String_MatchesTextCaseInsensitive()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Message,
                Text = "HELLO"
            });

            var builder = MessageRouteBuilder.Create()
                .WithText("hello")
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task MessageRouteBuilder_WithText_String_DoesNotMatchDifferentText()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Message,
                Text = "goodbye"
            });

            var builder = MessageRouteBuilder.Create()
                .WithText("hello")
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task MessageRouteBuilder_WithText_String_DoesNotMatchNonMessageActivity()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Event,
                Text = "hello"
            });

            var builder = MessageRouteBuilder.Create()
                .WithText("hello")
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region WithText(Regex) Tests

        [Fact]
        public void MessageRouteBuilder_WithText_Regex_SetsSelector()
        {
            // Arrange
            var builder = MessageRouteBuilder.Create();
            var pattern = new Regex("hello");

            // Act
            var result = builder.WithText(pattern);

            // Assert
            Assert.Same(builder, result);
            var route = builder
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();
            Assert.NotNull(route.Selector);
        }

        [Fact]
        public void MessageRouteBuilder_WithText_Regex_NullPattern_ThrowsArgumentNullException()
        {
            // Arrange
            var builder = MessageRouteBuilder.Create();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.WithText((Regex)null));
        }

        [Fact]
        public async Task MessageRouteBuilder_WithText_Regex_MatchesPattern()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello world"
            });

            var builder = MessageRouteBuilder.Create()
                .WithText(new Regex(@"\bhello\b"))
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task MessageRouteBuilder_WithText_Regex_DoesNotMatchNonPattern()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Message,
                Text = "goodbye world"
            });

            var builder = MessageRouteBuilder.Create()
                .WithText(new Regex(@"\bhello\b"))
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task MessageRouteBuilder_WithText_Regex_DoesNotMatchNonMessageActivity()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                Text = "hello"
            });

            var builder = MessageRouteBuilder.Create()
                .WithText(new Regex("hello"))
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void MessageRouteBuilder_WithText_Regex_ThrowsWhenSelectorAlreadyDefined()
        {
            // Arrange
            var builder = MessageRouteBuilder.Create()
                .WithText(new Regex("hello"));

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => builder.WithText(new Regex("world")));
        }

        #endregion

        #region WithSelector Tests

        [Fact]
        public void MessageRouteBuilder_WithSelector_SetsSelector()
        {
            // Arrange
            var builder = MessageRouteBuilder.Create();
            RouteSelector selector = (context, token) => Task.FromResult(true);

            // Act
            var result = builder.WithSelector(selector);

            // Assert
            Assert.Same(builder, result);
        }

        [Fact]
        public void MessageRouteBuilder_WithSelector_NullSelector_ThrowsArgumentNullException()
        {
            // Arrange
            var builder = MessageRouteBuilder.Create();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.WithSelector(null));
        }

        [Fact]
        public async Task MessageRouteBuilder_WithSelector_WrapsWithMessageTypeCheck()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Message
            });

            var selectorCalled = false;
            RouteSelector selector = (context, token) =>
            {
                selectorCalled = true;
                return Task.FromResult(true);
            };

            var builder = MessageRouteBuilder.Create()
                .WithSelector(selector)
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.True(result);
            Assert.True(selectorCalled);
        }

        [Fact]
        public async Task MessageRouteBuilder_WithSelector_BlocksNonMessageActivity()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Event
            });

            var selectorCalled = false;
            RouteSelector selector = (context, token) =>
            {
                selectorCalled = true;
                return Task.FromResult(true);
            };

            var builder = MessageRouteBuilder.Create()
                .WithSelector(selector)
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.False(result);
            Assert.False(selectorCalled); // Selector should not be called
        }

        [Fact]
        public void MessageRouteBuilder_WithSelector_ThrowsWhenSelectorAlreadyDefined()
        {
            // Arrange
            var builder = MessageRouteBuilder.Create()
                .WithSelector((context, token) => Task.FromResult(true));

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => builder.WithSelector((context, token) => Task.FromResult(false)));
        }

        #endregion

        #region WithHandler Tests

        [Fact]
        public void MessageRouteBuilder_WithHandler_SetsHandler()
        {
            // Arrange
            var builder = MessageRouteBuilder.Create();
            RouteHandler handler = (context, state, token) => Task.CompletedTask;

            // Act
            var result = builder.WithHandler(handler);

            // Assert
            Assert.Same(builder, result);
            var route = builder
                .WithText("test")
                .Build();
            Assert.Same(handler, route.Handler);
        }

        [Fact]
        public void MessageRouteBuilder_WithHandler_NullHandler_ThrowsArgumentNullException()
        {
            // Arrange
            var builder = MessageRouteBuilder.Create();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.WithHandler(null));
        }

        #endregion

        #region AsInvoke Tests

        [Fact]
        public void MessageRouteBuilder_AsInvoke_DoesNotSetInvokeFlag()
        {
            // Arrange
            var builder = MessageRouteBuilder.Create();

            // Act
            var result = builder.AsInvoke();

            // Assert
            Assert.Same(builder, result);
            var route = builder
                .WithText("test")
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();

            // AsInvoke is ignored for message routes
            Assert.False(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        [Fact]
        public void MessageRouteBuilder_AsInvoke_False_DoesNothing()
        {
            // Arrange
            var builder = MessageRouteBuilder.Create()
                .AsInvoke();

            // Act
            var result = builder.AsInvoke(false);

            // Assert
            var route = builder
                .WithText("test")
                .WithHandler((context, state, token) => Task.CompletedTask)
                .Build();
            Assert.False(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        #endregion

        #region Agentic and ChannelId Tests

        [Fact]
        public async Task MessageRouteBuilder_WithText_RespectsAgenticFlag()
        {
            // Arrange - Agentic context
            var agenticContext = new Mock<ITurnContext>();
            agenticContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello",
                Recipient = new ChannelAccount { Role = RoleTypes.AgenticUser }
            });

            // Arrange - Non-agentic context
            var nonAgenticContext = new Mock<ITurnContext>();
            nonAgenticContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello"
            });

            var builder = MessageRouteBuilder.Create()
                .WithText("hello")
                .AsAgentic()
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var agenticResult = await route.Selector(agenticContext.Object, CancellationToken.None);
            var nonAgenticResult = await route.Selector(nonAgenticContext.Object, CancellationToken.None);

            // Assert
            Assert.True(agenticResult);
            Assert.False(nonAgenticResult);
        }

        [Fact]
        public async Task MessageRouteBuilder_WithText_RespectsChannelId()
        {
            // Arrange - Matching channel
            var matchingContext = new Mock<ITurnContext>();
            matchingContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello",
                ChannelId = Channels.Msteams
            });

            // Arrange - Non-matching channel
            var nonMatchingContext = new Mock<ITurnContext>();
            nonMatchingContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello",
                ChannelId = Channels.Directline
            });

            var builder = MessageRouteBuilder.Create()
                .WithText("hello")
                .WithChannelId(Channels.Msteams)
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var matchingResult = await route.Selector(matchingContext.Object, CancellationToken.None);
            var nonMatchingResult = await route.Selector(nonMatchingContext.Object, CancellationToken.None);

            // Assert
            Assert.True(matchingResult);
            Assert.False(nonMatchingResult);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void MessageRouteBuilder_FluentAPI_AllMethodsChainCorrectly()
        {
            // Arrange & Act
            var route = MessageRouteBuilder.Create()
                .WithText("hello")
                .WithHandler((context, state, token) => Task.CompletedTask)
                .WithChannelId(Channels.Msteams)
                .WithOrderRank(10)
                .AsAgentic()
                .AsNonTerminal()
                .WithOAuthHandlers("handler1,handler2")
                .Build();

            // Assert
            Assert.NotNull(route);
            Assert.Equal(Channels.Msteams, route.ChannelId);
            Assert.Equal((ushort)10, route.Rank);
            Assert.True(route.Flags.HasFlag(RouteFlags.Agentic));
            Assert.True(route.Flags.HasFlag(RouteFlags.NonTerminal));
            Assert.False(route.Flags.HasFlag(RouteFlags.Invoke)); // Should not be set for messages
        }

        [Fact]
        public async Task MessageRouteBuilder_CompleteScenario_ExecutesCorrectly()
        {
            // Arrange
            var handlerExecuted = false;

            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello world",
                ChannelId = Channels.Msteams,
                Recipient = new ChannelAccount { Role = RoleTypes.AgenticUser }
            });

            var route = MessageRouteBuilder.Create()
                .WithText(new Regex(@"\bhello\b"))
                .WithHandler((context, state, token) =>
                {
                    handlerExecuted = true;
                    return Task.CompletedTask;
                })
                .WithChannelId(Channels.Msteams)
                .AsAgentic()
                .Build();

            // Act
            var selectorResult = await route.Selector(mockContext.Object, CancellationToken.None);
            if (selectorResult)
            {
                await route.Handler(mockContext.Object, null, CancellationToken.None);
            }

            // Assert
            Assert.True(selectorResult);
            Assert.True(handlerExecuted);
        }

        [Fact]
        public async Task MessageRouteBuilder_WithText_String_HandlesNullActivityText()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Message,
                Text = null
            });

            var builder = MessageRouteBuilder.Create()
                .WithText("hello")
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act
            var result = await route.Selector(mockContext.Object, CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task MessageRouteBuilder_WithText_Regex_HandlesNullActivityText()
        {
            // Arrange
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Message,
                Text = null
            });

            var builder = MessageRouteBuilder.Create()
                .WithText(new Regex("hello"))
                .WithHandler((context, state, token) => Task.CompletedTask);

            var route = builder.Build();

            // Act & Assert - Should not throw
            var result = await route.Selector(mockContext.Object, CancellationToken.None);
            Assert.False(result);
        }

        #endregion

        #region No-Filter (Any Message) Tests

        [Fact]
        public async Task MessageRouteBuilder_WithoutFilter_MatchesAnyMessage()
        {
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Message,
                Text = "anything"
            });

            var route = MessageRouteBuilder.Create()
                .WithHandler((ctx, state, ct) => Task.CompletedTask)
                .Build();

            Assert.True(await route.Selector(mockContext.Object, CancellationToken.None));
        }

        [Fact]
        public async Task MessageRouteBuilder_WithoutFilter_DoesNotMatchNonMessage()
        {
            var mockContext = new Mock<ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Event
            });

            var route = MessageRouteBuilder.Create()
                .WithHandler((ctx, state, ct) => Task.CompletedTask)
                .Build();

            Assert.False(await route.Selector(mockContext.Object, CancellationToken.None));
        }

        #endregion

        #region RouteRank.Last Defaulting Tests

        [Fact]
        public void MessageRouteBuilder_WithoutFilter_DefaultsToRankLast()
        {
            var route = MessageRouteBuilder.Create()
                .WithHandler((ctx, state, ct) => Task.CompletedTask)
                .Build();

            Assert.Equal(RouteRank.Last, route.Rank);
        }

        [Fact]
        public void MessageRouteBuilder_WithText_DoesNotDefaultToRankLast()
        {
            var route = MessageRouteBuilder.Create()
                .WithText("hello")
                .WithHandler((ctx, state, ct) => Task.CompletedTask)
                .Build();

            Assert.Equal(RouteRank.Unspecified, route.Rank);
        }

        [Fact]
        public void MessageRouteBuilder_WithTextRegex_DoesNotDefaultToRankLast()
        {
            var route = MessageRouteBuilder.Create()
                .WithText(new Regex("hello.*"))
                .WithHandler((ctx, state, ct) => Task.CompletedTask)
                .Build();

            Assert.Equal(RouteRank.Unspecified, route.Rank);
        }

        [Fact]
        public void MessageRouteBuilder_WithoutFilter_ExplicitRankPreserved()
        {
            var route = MessageRouteBuilder.Create()
                .WithOrderRank(50)
                .WithHandler((ctx, state, ct) => Task.CompletedTask)
                .Build();

            Assert.Equal((ushort)50, route.Rank);
        }

        #endregion

        #region WithText and WithSelector Combined Tests

        [Fact]
        public async Task MessageRouteBuilder_WithTextAndSelector_BothMustMatch()
        {
            RouteSelector customSelector = (ctx, ct) =>
                Task.FromResult(ctx.Activity.ChannelId == Channels.Msteams);

            var matchContext = new Mock<ITurnContext>();
            matchContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello",
                ChannelId = Channels.Msteams
            });

            var wrongTextContext = new Mock<ITurnContext>();
            wrongTextContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Message,
                Text = "goodbye",
                ChannelId = Channels.Msteams
            });

            var wrongChannelContext = new Mock<ITurnContext>();
            wrongChannelContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello",
                ChannelId = Channels.Directline
            });

            var route = MessageRouteBuilder.Create()
                .WithText("hello")
                .WithSelector(customSelector)
                .WithHandler((ctx, state, ct) => Task.CompletedTask)
                .Build();

            Assert.True(await route.Selector(matchContext.Object, CancellationToken.None));
            Assert.False(await route.Selector(wrongTextContext.Object, CancellationToken.None));
            Assert.False(await route.Selector(wrongChannelContext.Object, CancellationToken.None));
        }

        #endregion
    }
}