// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Agents.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class ContinueConversationRouteTests
    {
        private readonly ITestOutputHelper _output;

        public ContinueConversationRouteTests(ITestOutputHelper output)
        {
            _output = output;
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidDelegateNameAndNoTokenHandlers_ShouldCreateInstance()
        {
            // Arrange & Act
            var route = new ContinueConversationRoute<TestAgentApplication>("TestRouteHandler", string.Empty);

            // Assert
            Assert.NotNull(route);
            Assert.Null(route.TokenHandlers);
        }

        [Fact]
        public void Constructor_WithValidDelegateNameAndSingleTokenHandler_ShouldCreateInstance()
        {
            // Arrange & Act
            var route = new ContinueConversationRoute<TestAgentApplication>("TestRouteHandler", "handler1");

            // Assert
            Assert.NotNull(route);
            Assert.NotNull(route.TokenHandlers);
            Assert.Single(route.TokenHandlers);
            Assert.Equal("handler1", route.TokenHandlers[0]);
        }

        [Fact]
        public void Constructor_WithValidDelegateNameAndMultipleTokenHandlers_ShouldParseDelimitedString()
        {
            // Arrange & Act
            var route = new ContinueConversationRoute<TestAgentApplication>("TestRouteHandler", "handler1,handler2,handler3");

            // Assert
            Assert.NotNull(route);
            Assert.NotNull(route.TokenHandlers);
            Assert.Equal(3, route.TokenHandlers.Length);
            Assert.Equal("handler1", route.TokenHandlers[0]);
            Assert.Equal("handler2", route.TokenHandlers[1]);
            Assert.Equal("handler3", route.TokenHandlers[2]);
        }

        [Fact]
        public void Constructor_WithDelimitedTokenHandlersUsingSemicolon_ShouldParseCorrectly()
        {
            // Arrange & Act
            var route = new ContinueConversationRoute<TestAgentApplication>("TestRouteHandler", "handler1;handler2;handler3");

            // Assert
            Assert.NotNull(route);
            Assert.NotNull(route.TokenHandlers);
            Assert.Equal(3, route.TokenHandlers.Length);
            Assert.Equal("handler1", route.TokenHandlers[0]);
            Assert.Equal("handler2", route.TokenHandlers[1]);
            Assert.Equal("handler3", route.TokenHandlers[2]);
        }

        [Fact]
        public void Constructor_WithDelimitedTokenHandlersUsingSpaces_ShouldParseCorrectly()
        {
            // Arrange & Act
            var route = new ContinueConversationRoute<TestAgentApplication>("TestRouteHandler", "handler1 handler2 handler3");

            // Assert
            Assert.NotNull(route);
            Assert.NotNull(route.TokenHandlers);
            Assert.Equal(3, route.TokenHandlers.Length);
            Assert.Equal("handler1", route.TokenHandlers[0]);
            Assert.Equal("handler2", route.TokenHandlers[1]);
            Assert.Equal("handler3", route.TokenHandlers[2]);
        }

        [Fact]
        public void Constructor_WithDelimitedTokenHandlersWithMixedDelimiters_ShouldParseCorrectly()
        {
            // Arrange & Act
            var route = new ContinueConversationRoute<TestAgentApplication>("TestRouteHandler", "handler1, handler2; handler3");

            // Assert
            Assert.NotNull(route);
            Assert.NotNull(route.TokenHandlers);
            Assert.Equal(3, route.TokenHandlers.Length);
            Assert.Equal("handler1", route.TokenHandlers[0]);
            Assert.Equal("handler2", route.TokenHandlers[1]);
            Assert.Equal("handler3", route.TokenHandlers[2]);
        }

        [Fact]
        public void Constructor_WithEmptyDelimitedTokenHandlers_ShouldReturnNull()
        {
            // Arrange & Act
            var route = new ContinueConversationRoute<TestAgentApplication>("TestRouteHandler", "");

            // Assert
            Assert.NotNull(route);
            Assert.Null(route.TokenHandlers);
        }

        [Fact]
        public void Constructor_WithNullDelimitedTokenHandlers_ShouldReturnNull()
        {
            // Arrange & Act
            var route = new ContinueConversationRoute<TestAgentApplication>("TestRouteHandler", (string)null);

            // Assert
            Assert.NotNull(route);
            Assert.Null(route.TokenHandlers);
        }

        [Fact]
        public void Constructor_WithTokenHandlersArray_ShouldSetTokenHandlers()
        {
            // Arrange
            var tokenHandlers = new[] { "handler1", "handler2", "handler3" };

            // Act
            var route = new ContinueConversationRoute<TestAgentApplication>("TestRouteHandler", tokenHandlers);

            // Assert
            Assert.NotNull(route);
            Assert.NotNull(route.TokenHandlers);
            Assert.Equal(3, route.TokenHandlers.Length);
            Assert.Equal("handler1", route.TokenHandlers[0]);
            Assert.Equal("handler2", route.TokenHandlers[1]);
            Assert.Equal("handler3", route.TokenHandlers[2]);
        }

        [Fact]
        public void Constructor_WithNullTokenHandlersArray_ShouldSetTokenHandlersToNull()
        {
            // Arrange & Act
            var route = new ContinueConversationRoute<TestAgentApplication>("TestRouteHandler", (string[])null);

            // Assert
            Assert.NotNull(route);
            Assert.Null(route.TokenHandlers);
        }

        [Fact]
        public void Constructor_WithInvalidDelegateName_ShouldThrowInvalidOperationException()
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
                new ContinueConversationRoute<TestAgentApplication>("NonExistentMethod", string.Empty));

            Assert.Contains("NonExistentMethod", exception.Message);
            Assert.Contains("was not found", exception.Message);
            Assert.Contains(typeof(TestAgentApplication).FullName, exception.Message);
        }

        [Fact]
        public void Constructor_WithPublicMethod_ShouldSucceed()
        {
            // Arrange & Act
            var route = new ContinueConversationRoute<TestAgentApplication>("PublicRouteHandler", string.Empty);

            // Assert
            Assert.NotNull(route);
        }

        [Fact]
        public void Constructor_WithPrivateMethod_ShouldSucceed()
        {
            // Arrange & Act
            var route = new ContinueConversationRoute<TestAgentApplication>("PrivateRouteHandler", string.Empty);

            // Assert
            Assert.NotNull(route);
        }

        [Fact]
        public void Constructor_WithProtectedMethod_ShouldSucceed()
        {
            // Arrange & Act
            var route = new ContinueConversationRoute<TestAgentApplication>("ProtectedRouteHandler", string.Empty);

            // Assert
            Assert.NotNull(route);
        }

        #endregion

        #region RouteHandler Tests

        [Theory]
        [InlineData("TypedRouteHandler")]
        [InlineData("StaticTypedRouteHandler")]
        public async Task RouteHandler_AcceptsSpecializedContext(string method)
        {
            var agent = new TestAgentApplication(new AgentApplicationOptions(new MemoryStorage()));
            var route = new ContinueConversationRoute<TestAgentApplication>(method, string.Empty);
            var handler = route.RouteHandler(agent);
            using var inner = CreateEmptyContext();
            var context = new SpecializedTurnContext(inner);
            var state = new TurnState();
            using var cancellation = new CancellationTokenSource();

            await handler(context, state, cancellation.Token);

            Assert.Same(state, context.State);
            Assert.Equal(cancellation.Token, context.CancellationToken);
        }

        [Fact]
        public async Task RouteHandler_RejectsUnavailableSpecializedContext()
        {
            var agent = new TestAgentApplication(new AgentApplicationOptions(new MemoryStorage()));
            var route = new ContinueConversationRoute<TestAgentApplication>("TypedRouteHandler", string.Empty);
            var handler = route.RouteHandler(agent);
            using var context = CreateEmptyContext();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler(context, new TurnState(), default));

            Assert.Contains(nameof(ISpecializedTurnContext), exception.Message);
            Assert.Contains("EmptyContext", exception.Message);
        }

        [Theory]
        [InlineData("EmptyContext", "EmptyContext", true)]
        [InlineData("EmptyContext", "EmptyContext:subchannel", true)]
        [InlineData("EmptyContext:subchannel", "EmptyContext:subchannel", true)]
        [InlineData("EmptyContext:other", "EmptyContext:subchannel", false)]
        [InlineData("other", "EmptyContext", false)]
        public async Task RouteHandler_UsesOnlyMatchingExtension(string extensionChannel, string turnChannel, bool matches)
        {
            var agent = new TestAgentApplication(new AgentApplicationOptions(new MemoryStorage()));
            agent.RegisterExtension(new SpecializedExtension { ChannelId = extensionChannel });
            var handler = new ContinueConversationRoute<TestAgentApplication>("TypedRouteHandler", string.Empty).RouteHandler(agent);
            using var context = CreateEmptyContext();
            context.Activity.ChannelId = turnChannel;
            var state = new TurnState();

            if (matches)
            {
                await handler(context, state, default);
                Assert.Same(state, context.Services.Get<ITurnState>("captured-state"));
            }
            else
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => handler(context, state, default));
                Assert.Null(context.Services.Get<ITurnState>("captured-state"));
            }
        }

        [Theory]
        [InlineData("InvalidTypedSignatureMethod")]
        [InlineData("NonAsyncTypedMethod")]
        public void RouteHandler_RejectsInvalidSpecializedSignatures(string method)
        {
            var agent = new TestAgentApplication(new AgentApplicationOptions(new MemoryStorage()));
            var route = new ContinueConversationRoute<TestAgentApplication>(method, string.Empty);

            var exception = Assert.Throws<ArgumentException>(() => route.RouteHandler(agent));

            ExceptionTester.IsException<ArgumentException>(exception, -50004, _output);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RouteHandler_PreservesAttributeTokenAcquisition(bool signedIn)
        {
            var authorization = new Mock<IUserAuthorization>();
            authorization.SetupGet(handler => handler.Name).Returns("required");
            authorization.Setup(handler => handler.GetRefreshedUserTokenAsync(
                It.IsAny<ITurnContext>(), null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(signedIn ? new TokenResponse { Token = "test-token", Expiration = DateTimeOffset.UtcNow.AddHours(1) } : null);
            var storage = new MemoryStorage();
            var agent = new TestAgentApplication(new AgentApplicationOptions(storage)
            {
                Proactive = new ProactiveOptions(storage),
                UserAuthorization = new UserAuthorizationOptions(NullLoggerFactory.Instance, storage, new Mock<IConnections>().Object, authorization.Object)
                {
                    AutoSignIn = UserAuthorizationOptions.AutoSignInOff,
                },
            });
            agent.RegisterExtension(new SpecializedExtension { ChannelId = "EmptyContext" });
            using var context = CreateEmptyContext();
            var conversation = new Conversation(new ClaimsIdentity(), context.Activity.GetConversationReference());
            var handler = new ContinueConversationRoute<TestAgentApplication>("SecuredTypedRouteHandler", string.Empty).RouteHandler(agent);

            if (signedIn)
            {
                await agent.Proactive.ContinueConversationAsync(context.Adapter, conversation, handler);
                Assert.Equal("test-token", agent.CapturedToken);
                Assert.True(agent.SecuredHandlerCalled);
            }
            else
            {
                await Assert.ThrowsAsync<UserNotSignedIn>(() => agent.Proactive.ContinueConversationAsync(context.Adapter, conversation, handler));
                Assert.False(agent.SecuredHandlerCalled);
            }
        }

        [Fact]
        public void RouteHandler_ShouldReturnValidDelegate()
        {
            // Arrange
            var agent = new TestAgentApplication(new AgentApplicationOptions(new MemoryStorage()));
            var route = new ContinueConversationRoute<TestAgentApplication>("TestRouteHandler", string.Empty);

            // Act
            var handler = route.RouteHandler(agent);

            // Assert
            Assert.NotNull(handler);
        }

        [Fact]
        public async Task RouteHandler_ShouldInvokeCorrectMethod()
        {
            // Arrange
            var agent = new TestAgentApplication(new AgentApplicationOptions(new MemoryStorage()));
            var route = new ContinueConversationRoute<TestAgentApplication>("TestRouteHandler", string.Empty);
            var handler = route.RouteHandler(agent);

            var turnContext = CreateEmptyContext();
            var turnState = new TurnState();

            // Act
            await handler(turnContext, turnState, CancellationToken.None);

            // Assert
            Assert.True(agent.TestRouteHandlerCalled);
        }

        [Fact]
        public async Task RouteHandler_WithPublicMethod_ShouldInvokeCorrectly()
        {
            // Arrange
            var agent = new TestAgentApplication(new AgentApplicationOptions(new MemoryStorage()));
            var route = new ContinueConversationRoute<TestAgentApplication>("PublicRouteHandler", string.Empty);
            var handler = route.RouteHandler(agent);

            var turnContext = CreateEmptyContext();
            var turnState = new TurnState();

            // Act
            await handler(turnContext, turnState, CancellationToken.None);

            // Assert
            Assert.True(agent.PublicRouteHandlerCalled);
        }

        [Fact]
        public async Task RouteHandler_WithPrivateMethod_ShouldInvokeCorrectly()
        {
            // Arrange
            var agent = new TestAgentApplication(new AgentApplicationOptions(new MemoryStorage()));
            var route = new ContinueConversationRoute<TestAgentApplication>("PrivateRouteHandler", string.Empty);
            var handler = route.RouteHandler(agent);

            var turnContext = CreateEmptyContext();
            var turnState = new TurnState();

            // Act
            await handler(turnContext, turnState, CancellationToken.None);

            // Assert
            Assert.True(agent.PrivateRouteHandlerCalled);
        }

        [Fact]
        public async Task RouteHandler_WithProtectedMethod_ShouldInvokeCorrectly()
        {
            // Arrange
            var agent = new TestAgentApplication(new AgentApplicationOptions(new MemoryStorage()));
            var route = new ContinueConversationRoute<TestAgentApplication>("ProtectedRouteHandler", string.Empty);
            var handler = route.RouteHandler(agent);

            var turnContext = CreateEmptyContext();
            var turnState = new TurnState();

            // Act
            await handler(turnContext, turnState, CancellationToken.None);

            // Assert
            Assert.True(agent.ProtectedRouteHandlerCalled);
        }

        [Fact]
        public void RouteHandler_WithInvalidMethodSignature_ShouldThrowArgumentException()
        {
            // Arrange
            var agent = new TestAgentApplication(new AgentApplicationOptions(new MemoryStorage()));
            var route = new ContinueConversationRoute<TestAgentApplication>("InvalidSignatureMethod", string.Empty);

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => route.RouteHandler(agent));
            ExceptionTester.IsException<ArgumentException>(exception, -50004, _output);
        }

        [Fact]
        public void RouteHandler_WithNonAsyncMethod_ShouldThrowArgumentException()
        {
            // Arrange
            var agent = new TestAgentApplication(new AgentApplicationOptions(new MemoryStorage()));
            var route = new ContinueConversationRoute<TestAgentApplication>("NonAsyncMethod", string.Empty);

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => route.RouteHandler(agent));
            ExceptionTester.IsException<ArgumentException>(exception, -50004, _output);
        }

        #endregion

        #region TokenHandlers Property Tests

        [Fact]
        public void TokenHandlers_WithNoHandlers_ShouldBeNull()
        {
            // Arrange & Act
            var route = new ContinueConversationRoute<TestAgentApplication>("TestRouteHandler", string.Empty);

            // Assert
            Assert.Null(route.TokenHandlers);
        }

        [Fact]
        public void TokenHandlers_WithHandlers_ShouldReturnArray()
        {
            // Arrange
            var tokenHandlers = new[] { "handler1", "handler2" };

            // Act
            var route = new ContinueConversationRoute<TestAgentApplication>("TestRouteHandler", tokenHandlers);

            // Assert
            Assert.NotNull(route.TokenHandlers);
            Assert.Equal(2, route.TokenHandlers.Length);
        }

        #endregion

        #region Test Helper Class

        public interface ISpecializedTurnContext : ITurnContext
        {
            ITurnState State { get; set; }
            CancellationToken CancellationToken { get; set; }
        }

        private sealed class SpecializedTurnContext(ITurnContext context) : TurnContextWrapper(context), ISpecializedTurnContext
        {
            public ITurnState State { get; set; }
            public CancellationToken CancellationToken { get; set; }
        }

        private sealed class SpecializedExtension : AgentExtension
        {
            public override ITurnContext WrapTurnContext(ITurnContext turnContext)
                => new SpecializedTurnContext(turnContext);
        }

        public class TestAgentApplication : AgentApplication
        {
            public TestAgentApplication(AgentApplicationOptions options) : base(options)
            {
            }

            public bool TestRouteHandlerCalled { get; private set; }
            public bool PublicRouteHandlerCalled { get; private set; }
            public bool PrivateRouteHandlerCalled { get; private set; }
            public bool ProtectedRouteHandlerCalled { get; private set; }
            public bool SecuredHandlerCalled { get; private set; }
            public string CapturedToken { get; private set; }

            private Task TypedRouteHandler(ISpecializedTurnContext context, ITurnState state, CancellationToken cancellationToken)
                => StaticTypedRouteHandler(context, state, cancellationToken);

            private static Task StaticTypedRouteHandler(ISpecializedTurnContext context, ITurnState state, CancellationToken cancellationToken)
            {
                context.State = state;
                context.Services.Set("captured-state", state);
                context.CancellationToken = cancellationToken;
                return Task.CompletedTask;
            }

            [ContinueConversation(autoSignInHandlers: "required")]
            private async Task SecuredTypedRouteHandler(ISpecializedTurnContext context, ITurnState state, CancellationToken ct)
            {
                SecuredHandlerCalled = true;
                CapturedToken = await UserAuthorization.GetTurnTokenAsync(context, cancellationToken: ct);
            }

            private Task InvalidTypedSignatureMethod(ISpecializedTurnContext context, string state, CancellationToken ct)
                => Task.CompletedTask;

            private void NonAsyncTypedMethod(ISpecializedTurnContext context, ITurnState state, CancellationToken ct)
            {
            }

            public Task TestRouteHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                TestRouteHandlerCalled = true;
                return Task.CompletedTask;
            }

            public Task PublicRouteHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                PublicRouteHandlerCalled = true;
                return Task.CompletedTask;
            }

            private Task PrivateRouteHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                PrivateRouteHandlerCalled = true;
                return Task.CompletedTask;
            }

            protected Task ProtectedRouteHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                ProtectedRouteHandlerCalled = true;
                return Task.CompletedTask;
            }

            // Invalid signature - missing ITurnState parameter
            public Task InvalidSignatureMethod(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            // Non-async method
            public void NonAsyncMethod(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
            }
        }

        private static TurnContext CreateEmptyContext()
        {
            var b = new TestAdapter();
            var a = new Activity
            {
                Type = ActivityTypes.Message,
                ChannelId = "EmptyContext",
                From = new ChannelAccount
                {
                    Id = "empty@empty.context.org",
                },

                Conversation = new ConversationAccount()
                {
                    Id = "213123123123",
                },
            };
            var bc = new TurnContext(b, a);

            return bc;
        }
        #endregion
    }
}
