// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Builder.UserAuth.TokenService;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests
{
    public class AzureBotUserAuthorizationTests
    {
        private const string ConnectionName = "testConnection";
        private const string HandlerName = "testHandler";

        private readonly Mock<IConnections> _mockConnections;
        private readonly MemoryStorage _storage;
        private readonly OAuthSettings _settings;

        public AzureBotUserAuthorizationTests()
        {
            _mockConnections = new Mock<IConnections>();
            _storage = new MemoryStorage();
            _settings = new OAuthSettings
            {
                AzureBotOAuthConnectionName = ConnectionName,
                Title = "Sign In",
                Text = "Please sign in",
                Timeout = 900000 // 15 minutes
            };
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_ThrowsOnNullName()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new AzureBotUserAuthorization(null, _storage, _mockConnections.Object, _settings));
        }

        [Fact]
        public void Constructor_ThrowsOnNullSettings()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new AzureBotUserAuthorization(HandlerName, _storage, _mockConnections.Object, (OAuthSettings)null));
        }

        [Fact]
        public void Constructor_ThrowsOnNullStorage()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new AzureBotUserAuthorization(HandlerName, null, _mockConnections.Object, _settings));
        }

        [Fact]
        public void Constructor_ThrowsOnNullConnections()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new AzureBotUserAuthorization(HandlerName, _storage, null, _settings));
        }

        [Fact]
        public void Constructor_SetsNameCorrectly()
        {
            var handler = new AzureBotUserAuthorization(HandlerName, _storage, _mockConnections.Object, _settings);
            Assert.Equal(HandlerName, handler.Name);
        }

        #endregion

        #region SignInUserAsync Tests

        [Fact]
        public async Task SignInUserAsync_ReturnsToken_WhenAlreadySignedIn()
        {
            // Arrange
            var expectedToken = new TokenResponse { Token = "cached-token", Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30) };
            var mockTokenClient = new Mock<IUserTokenClient>();
            mockTokenClient
                .Setup(c => c.GetTokenOrSignInResourceAsync(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenOrSignInResourceResponse { TokenResponse = expectedToken });

            var turnContext = CreateTurnContext(ActivityTypes.Message, mockTokenClient.Object);
            var handler = CreateHandler();

            // Act
            var result = await handler.SignInUserAsync(turnContext, forceSignIn: true, cancellationToken: CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("cached-token", result.Token);
        }

        [Fact]
        public async Task SignInUserAsync_ReturnsNull_WhenFlowStarted()
        {
            // Arrange - Token service returns null token (user needs to sign in)
            var sentActivities = new List<IActivity>();
            var mockTokenClient = new Mock<IUserTokenClient>();
            mockTokenClient
                .Setup(c => c.GetTokenOrSignInResourceAsync(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenOrSignInResourceResponse
                {
                    SignInResource = new SignInResource
                    {
                        SignInLink = "https://login.test.com",
                        TokenExchangeResource = new TokenExchangeResource { Id = "id", Uri = "uri" }
                    }
                });

            var turnContext = CreateTurnContextWithCapture(
                CreateMessageActivity(), mockTokenClient.Object, sentActivities);
            var handler = CreateHandler();

            // Act
            var result = await handler.SignInUserAsync(turnContext, forceSignIn: true, cancellationToken: CancellationToken.None);

            // Assert - flow started, no token yet
            Assert.Null(result);

            // Assert - OAuthCard was sent to prompt user sign-in
            var oauthCardActivity = sentActivities
                .OfType<Activity>()
                .FirstOrDefault(a => a.Attachments?.Any(att => att.ContentType == OAuthCard.ContentType) == true);
            Assert.NotNull(oauthCardActivity);
            Assert.Contains(oauthCardActivity.Attachments, a => a.ContentType == OAuthCard.ContentType);
        }

        [Fact]
        public async Task SignInUserAsync_ReturnsNull_ForNonMatchingActivityType()
        {
            // Arrange - ConversationUpdate shouldn't match IsValidActivity
            var mockTokenClient = new Mock<IUserTokenClient>();
            var turnContext = CreateTurnContext(ActivityTypes.ConversationUpdate, mockTokenClient.Object);
            var handler = CreateHandler();

            // Act
            var result = await handler.SignInUserAsync(turnContext, forceSignIn: false, cancellationToken: CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task SignInUserAsync_ForceSignIn_OverridesActivityTypeFilter()
        {
            // Arrange
            var expectedToken = new TokenResponse { Token = "forced-token", Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30) };
            var mockTokenClient = new Mock<IUserTokenClient>();
            mockTokenClient
                .Setup(c => c.GetTokenOrSignInResourceAsync(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenOrSignInResourceResponse { TokenResponse = expectedToken });

            var turnContext = CreateTurnContext(ActivityTypes.ConversationUpdate, mockTokenClient.Object);
            var handler = CreateHandler();

            // Act - forceSignIn bypasses IsValidActivity
            var result = await handler.SignInUserAsync(turnContext, forceSignIn: true, cancellationToken: CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("forced-token", result.Token);
        }

        [Fact]
        public async Task SignInUserAsync_HandlesVerifyStateInvoke()
        {
            // Arrange - simulate token exchange via invoke
            var sentActivities = new List<IActivity>();
            var expectedToken = new TokenResponse { Token = "invoke-token", Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30) };
            var mockTokenClient = new Mock<IUserTokenClient>();

            // First call: begin flow (returns sign-in resource)
            mockTokenClient
                .Setup(c => c.GetTokenOrSignInResourceAsync(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenOrSignInResourceResponse
                {
                    SignInResource = new SignInResource
                    {
                        SignInLink = "https://login.test.com",
                        TokenExchangeResource = new TokenExchangeResource { Id = "id", Uri = "uri" }
                    }
                });

            // Start the flow with a message activity
            var startContext = CreateTurnContext(ActivityTypes.Message, mockTokenClient.Object);
            var handler = CreateHandler();
            var result = await handler.SignInUserAsync(startContext, forceSignIn: true, cancellationToken: CancellationToken.None);
            Assert.Null(result); // flow started

            // Second call: verify state invoke with magic code
            mockTokenClient
                .Setup(c => c.GetUserTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ChannelId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedToken);

            var invokeActivity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = SignInConstants.VerifyStateOperationName,
                From = new ChannelAccount { Id = "user1" },
                Recipient = new ChannelAccount { Id = "bot" },
                Conversation = new ConversationAccount { Id = "convo1" },
                ChannelId = "test",
                Value = new Dictionary<string, object> { { "state", "123456" } }
            };
            var continueContext = CreateTurnContextWithCapture(invokeActivity, mockTokenClient.Object, sentActivities);

            // Act
            result = await handler.SignInUserAsync(continueContext, forceSignIn: false, cancellationToken: CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("invoke-token", result.Token);

            // Assert - InvokeResponse with 200 sent back for the VerifyState invoke
            var invokeResponse = sentActivities.FirstOrDefault(a => a.Type == ActivityTypes.InvokeResponse);
            Assert.NotNull(invokeResponse);
            Assert.Equal((int)HttpStatusCode.OK, ((InvokeResponse)invokeResponse.Value).Status);

            // Assert - StackState has InvokeResponse for CloudAdapter HTTP response
            var stackStateResponse = continueContext.StackState.Get<Activity>(ChannelAdapter.InvokeResponseKey);
            Assert.NotNull(stackStateResponse);
            Assert.Equal((int)HttpStatusCode.OK, ((InvokeResponse)stackStateResponse.Value).Status);
        }

        [Fact]
        public async Task SignInUserAsync_HandlesTokenResponseEvent()
        {
            // Arrange
            var expectedToken = new TokenResponse { Token = "event-token", ConnectionName = ConnectionName, Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30) };
            var mockTokenClient = new Mock<IUserTokenClient>();

            // First: start the flow
            mockTokenClient
                .Setup(c => c.GetTokenOrSignInResourceAsync(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenOrSignInResourceResponse
                {
                    SignInResource = new SignInResource
                    {
                        SignInLink = "https://login.test.com",
                        TokenExchangeResource = new TokenExchangeResource { Id = "id", Uri = "uri" }
                    }
                });

            var startContext = CreateTurnContext(ActivityTypes.Message, mockTokenClient.Object);
            var handler = CreateHandler();
            await handler.SignInUserAsync(startContext, forceSignIn: true, cancellationToken: CancellationToken.None);

            // Then: token response event
            var eventActivity = new Activity
            {
                Type = ActivityTypes.Event,
                Name = SignInConstants.TokenResponseEventName,
                From = new ChannelAccount { Id = "user1" },
                Recipient = new ChannelAccount { Id = "bot" },
                Conversation = new ConversationAccount { Id = "convo1" },
                ChannelId = "test",
                Value = expectedToken
            };
            var eventContext = CreateTurnContext(eventActivity, mockTokenClient.Object);

            // Act
            var result = await handler.SignInUserAsync(eventContext, forceSignIn: false, cancellationToken: CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("event-token", result.Token);
        }

        #endregion

        #region Timeout Tests

        [Fact]
        public async Task SignInUserAsync_ThrowsAuthException_WhenFlowTimesOut_InTeams()
        {
            // Arrange - use a very short timeout
            var settings = new OAuthSettings
            {
                AzureBotOAuthConnectionName = ConnectionName,
                Timeout = 1 // 1 millisecond
            };
            var handler = new AzureBotUserAuthorization(HandlerName, _storage, _mockConnections.Object, settings);
            var mockTokenClient = new Mock<IUserTokenClient>();

            // Start flow
            mockTokenClient
                .Setup(c => c.GetTokenOrSignInResourceAsync(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenOrSignInResourceResponse
                {
                    SignInResource = new SignInResource
                    {
                        SignInLink = "https://login.test.com",
                        TokenExchangeResource = new TokenExchangeResource { Id = "id", Uri = "uri" }
                    }
                });

            var startContext = CreateTeamsMessageTurnContext(mockTokenClient.Object);
            await handler.SignInUserAsync(startContext, forceSignIn: true, cancellationToken: CancellationToken.None);

            // Wait for timeout
            await Task.Delay(10);

            // Continue after timeout
            var continueContext = CreateTeamsMessageTurnContext(mockTokenClient.Object);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AuthException>(() =>
                handler.SignInUserAsync(continueContext, forceSignIn: false, cancellationToken: CancellationToken.None));
            Assert.Equal(AuthExceptionReason.Timeout, ex.Cause);

            // The timed-out flow should be cleared so a new sign-in can start.
            var restartContext = CreateTeamsMessageTurnContext(mockTokenClient.Object);
            var restartResult = await handler.SignInUserAsync(restartContext, forceSignIn: true, cancellationToken: CancellationToken.None);
            Assert.Null(restartResult);
        }

        #endregion

        #region SignOutUserAsync Tests

        [Fact]
        public async Task SignOutUserAsync_CallsTokenClientAndClearsState()
        {
            // Arrange
            var mockTokenClient = new Mock<IUserTokenClient>();
            mockTokenClient
                .Setup(c => c.SignOutUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ChannelId>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var turnContext = CreateTurnContext(ActivityTypes.Message, mockTokenClient.Object);
            var handler = CreateHandler();

            // Act
            await handler.SignOutUserAsync(turnContext, CancellationToken.None);

            // Assert
            mockTokenClient.Verify(
                c => c.SignOutUserAsync("user1", ConnectionName, It.IsAny<ChannelId>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SignOutUserAsync_ThrowsWhenNoTokenClient()
        {
            // Arrange - no IUserTokenClient registered
            var turnContext = new TurnContext(new SimpleAdapter(), new Activity
            {
                Type = ActivityTypes.Message,
                From = new ChannelAccount { Id = "user1" },
                Recipient = new ChannelAccount { Id = "bot" },
                Conversation = new ConversationAccount { Id = "convo1" },
                ChannelId = "test"
            });
            var handler = CreateHandler();

            // Act & Assert
            await Assert.ThrowsAsync<NotSupportedException>(() =>
                handler.SignOutUserAsync(turnContext, CancellationToken.None));
        }

        #endregion

        #region ResetStateAsync Tests

        [Fact]
        public async Task ResetStateAsync_ClearsFlowState()
        {
            // Arrange - start a flow to create state
            var mockTokenClient = new Mock<IUserTokenClient>();
            mockTokenClient
                .Setup(c => c.GetTokenOrSignInResourceAsync(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenOrSignInResourceResponse
                {
                    SignInResource = new SignInResource
                    {
                        SignInLink = "https://login.test.com",
                        TokenExchangeResource = new TokenExchangeResource { Id = "id", Uri = "uri" }
                    }
                });

            var turnContext = CreateTurnContext(ActivityTypes.Message, mockTokenClient.Object);
            var handler = CreateHandler();

            // Start flow to persist state
            await handler.SignInUserAsync(turnContext, forceSignIn: true, cancellationToken: CancellationToken.None);

            // Act - reset state
            await handler.ResetStateAsync(turnContext, CancellationToken.None);

            // Assert - can start a new flow without continuing the old one
            // If state wasn't cleared, the second call would try to ContinueFlow
            var expectedToken = new TokenResponse { Token = "after-reset", Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30) };
            mockTokenClient
                .Setup(c => c.GetTokenOrSignInResourceAsync(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenOrSignInResourceResponse { TokenResponse = expectedToken });

            var result = await handler.SignInUserAsync(turnContext, forceSignIn: true, cancellationToken: CancellationToken.None);
            Assert.NotNull(result);
            Assert.Equal("after-reset", result.Token);
        }

        #endregion

        #region GetRefreshedUserTokenAsync Tests

        [Fact]
        public async Task GetRefreshedUserTokenAsync_ReturnsRefreshedToken()
        {
            // Arrange
            var refreshedToken = new TokenResponse { Token = "refreshed-token", Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30) };
            var mockTokenClient = new Mock<IUserTokenClient>();
            mockTokenClient
                .Setup(c => c.GetUserTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ChannelId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(refreshedToken);

            var turnContext = CreateTurnContext(ActivityTypes.Message, mockTokenClient.Object);
            var handler = CreateHandler();

            // Act
            var result = await handler.GetRefreshedUserTokenAsync(turnContext, cancellationToken: CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("refreshed-token", result.Token);
        }

        [Fact]
        public async Task GetRefreshedUserTokenAsync_ReturnsNull_WhenNoToken()
        {
            // Arrange
            var mockTokenClient = new Mock<IUserTokenClient>();
            mockTokenClient
                .Setup(c => c.GetUserTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ChannelId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((TokenResponse)null);

            var turnContext = CreateTurnContext(ActivityTypes.Message, mockTokenClient.Object);
            var handler = CreateHandler();

            // Act
            var result = await handler.GetRefreshedUserTokenAsync(turnContext, cancellationToken: CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetRefreshedUserTokenAsync_WithOBO_ExchangesToken()
        {
            // Arrange
            IList<string> oboScopes = new List<string> { "scope1" };
            var settings = new OAuthSettings
            {
                AzureBotOAuthConnectionName = ConnectionName,
                OBOConnectionName = "oboConn",
                OBOScopes = oboScopes
            };

            var exchangeableToken = new TokenResponse { Token = "exchangeable", Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30), IsExchangeable = true };
            var exchangedToken = new TokenResponse { Token = "obo-token", Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30) };

            var mockTokenClient = new Mock<IUserTokenClient>();
            mockTokenClient
                .Setup(c => c.GetUserTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ChannelId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(exchangeableToken);

            var accessTokenProvider = new Mock<IAccessTokenProvider>();
            var oboExchangeProvider = accessTokenProvider.As<IOBOExchange>();
            oboExchangeProvider
                .Setup(o => o.AcquireTokenOnBehalfOf(oboScopes, "exchangeable"))
                .ReturnsAsync(exchangedToken);

            var connections = new Mock<IConnections>();
            connections
                .Setup(c => c.TryGetConnection("oboConn", out It.Ref<IAccessTokenProvider>.IsAny))
                .Callback(new TryGetConnectionCallback((string name, out IAccessTokenProvider provider) =>
                {
                    provider = accessTokenProvider.Object;
                }))
                .Returns(true);

            var handler = new AzureBotUserAuthorization(HandlerName, _storage, connections.Object, settings);
            var turnContext = CreateTurnContext(ActivityTypes.Message, mockTokenClient.Object);

            // Act
            var result = await handler.GetRefreshedUserTokenAsync(turnContext, cancellationToken: CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("obo-token", result.Token);
        }

        #endregion

        #region OBO Failure Tests

        [Fact]
        public async Task SignInUserAsync_SignsOut_WhenOBOFails()
        {
            // Arrange
            IList<string> oboScopes = new List<string> { "scope1" };
            var settings = new OAuthSettings
            {
                AzureBotOAuthConnectionName = ConnectionName,
                OBOConnectionName = "oboConn",
                OBOScopes = oboScopes
            };

            var exchangeableToken = new TokenResponse { Token = "exchangeable", Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30), IsExchangeable = true };

            var mockTokenClient = new Mock<IUserTokenClient>();
            mockTokenClient
                .Setup(c => c.GetTokenOrSignInResourceAsync(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenOrSignInResourceResponse { TokenResponse = exchangeableToken });
            mockTokenClient
                .Setup(c => c.SignOutUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ChannelId>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var accessTokenProvider = new Mock<IAccessTokenProvider>();
            var oboExchangeProvider = accessTokenProvider.As<IOBOExchange>();
            oboExchangeProvider
                .Setup(o => o.AcquireTokenOnBehalfOf(oboScopes, "exchangeable"))
                .ThrowsAsync(new InvalidOperationException("OBO exchange failed"));

            var connections = new Mock<IConnections>();
            connections
                .Setup(c => c.TryGetConnection("oboConn", out It.Ref<IAccessTokenProvider>.IsAny))
                .Callback(new TryGetConnectionCallback((string name, out IAccessTokenProvider provider) =>
                {
                    provider = accessTokenProvider.Object;
                }))
                .Returns(true);

            var handler = new AzureBotUserAuthorization(HandlerName, _storage, connections.Object, settings);
            var turnContext = CreateTurnContext(ActivityTypes.Message, mockTokenClient.Object);

            // Act & Assert - should throw and sign out
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.SignInUserAsync(turnContext, forceSignIn: true, cancellationToken: CancellationToken.None));

            // Verify sign-out was called on failure
            mockTokenClient.Verify(
                c => c.SignOutUserAsync(It.IsAny<string>(), ConnectionName, It.IsAny<ChannelId>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SignInUserAsync_ThrowsWhenOBONotExchangeable()
        {
            // Arrange - token that is not exchangeable but OBO is configured
            IList<string> oboScopes = new List<string> { "scope1" };
            var settings = new OAuthSettings
            {
                AzureBotOAuthConnectionName = ConnectionName,
                OBOConnectionName = "oboConn",
                OBOScopes = oboScopes
            };

            var nonExchangeableToken = new TokenResponse { Token = "not-exchangeable", Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30), IsExchangeable = false };

            var mockTokenClient = new Mock<IUserTokenClient>();
            mockTokenClient
                .Setup(c => c.GetTokenOrSignInResourceAsync(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenOrSignInResourceResponse { TokenResponse = nonExchangeableToken });
            mockTokenClient
                .Setup(c => c.SignOutUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ChannelId>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var connections = new Mock<IConnections>();
            var handler = new AzureBotUserAuthorization(HandlerName, _storage, connections.Object, settings);
            var turnContext = CreateTurnContext(ActivityTypes.Message, mockTokenClient.Object);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.SignInUserAsync(turnContext, forceSignIn: true, cancellationToken: CancellationToken.None));
        }

        #endregion

        #region InvalidSignInRetryMax Tests

        [Fact]
        public async Task SignInUserAsync_ThrowsAuthException_WhenRetryMaxReached()
        {
            // Arrange - settings with max 2 retries
            var settings = new OAuthSettings
            {
                AzureBotOAuthConnectionName = ConnectionName,
                Timeout = 900000,
                InvalidSignInRetryMax = 2
            };

            var mockTokenClient = new Mock<IUserTokenClient>();
            // Start flow - returns sign-in resource (no token)
            mockTokenClient
                .Setup(c => c.GetTokenOrSignInResourceAsync(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenOrSignInResourceResponse
                {
                    SignInResource = new SignInResource
                    {
                        SignInLink = "https://login.test.com",
                        TokenExchangeResource = new TokenExchangeResource { Id = "id", Uri = "uri" }
                    }
                });

            // Continue flow with bad code - returns null token
            mockTokenClient
                .Setup(c => c.GetUserTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ChannelId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((TokenResponse)null);

            var handler = new AzureBotUserAuthorization(HandlerName, _storage, _mockConnections.Object, settings);

            // Start the flow
            var startContext = CreateTurnContext(ActivityTypes.Message, mockTokenClient.Object);
            await handler.SignInUserAsync(startContext, forceSignIn: true, cancellationToken: CancellationToken.None);

            // Send bad codes - first attempt (continue count = 1)
            var badCode1 = CreateVerifyStateInvokeContext("badcode1", mockTokenClient.Object);
            await handler.SignInUserAsync(badCode1, cancellationToken: CancellationToken.None);

            // Send bad codes - second attempt (continue count = 2 = max) -> should throw
            var badCode2 = CreateVerifyStateInvokeContext("badcode2", mockTokenClient.Object);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AuthException>(() =>
                handler.SignInUserAsync(badCode2, cancellationToken: CancellationToken.None));
            Assert.Equal(AuthExceptionReason.InvalidSignIn, ex.Cause);

            // Retry exhaustion should clear the stale flow state.
            var restartContext = CreateTurnContext(ActivityTypes.Message, mockTokenClient.Object);
            var restartResult = await handler.SignInUserAsync(restartContext, forceSignIn: true, cancellationToken: CancellationToken.None);
            Assert.Null(restartResult);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public async Task Integration_FullSignInFlow_WithTestAdapter()
        {
            // Arrange
            var storage = new MemoryStorage();

            var mockTokenClient = new Mock<IUserTokenClient>();

            // First call: no token cached, returns sign-in resource (OAuthCard is sent by OAuthFlow)
            mockTokenClient
                .Setup(c => c.GetTokenOrSignInResourceAsync(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenOrSignInResourceResponse
                {
                    SignInResource = new SignInResource
                    {
                        SignInLink = "https://login.test.com",
                        TokenExchangeResource = new TokenExchangeResource { Id = "id", Uri = "uri" }
                    }
                });

            var handler = new AzureBotUserAuthorization(HandlerName, storage, _mockConnections.Object, _settings);
            var adapterWithToken = new TestAdapter(tokenClient: mockTokenClient.Object);

            // Act - simulate the first turn which starts the flow and sends an OAuthCard
            await new TestFlow(adapterWithToken, async (turnContext, cancellationToken) =>
            {
                var result = await handler.SignInUserAsync(turnContext, forceSignIn: true, cancellationToken: cancellationToken);
                if (result != null)
                {
                    await turnContext.SendActivityAsync($"Token: {result.Token}", cancellationToken: cancellationToken);
                }
            })
            .Send("hello") // starts the flow
            .AssertReply(activity =>
            {
                // OAuthFlow sends an OAuthCard when the user needs to sign in
                var messageActivity = (Activity)activity;
                Assert.NotNull(messageActivity.Attachments);
                Assert.Contains(messageActivity.Attachments, a => a.ContentType == OAuthCard.ContentType);
            })
            .StartTestAsync();
        }

        [Fact]
        public async Task Integration_SignOutClearsStorageState()
        {
            // Arrange
            var storage = new MemoryStorage();
            var mockTokenClient = new Mock<IUserTokenClient>();
            mockTokenClient
                .Setup(c => c.GetTokenOrSignInResourceAsync(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenOrSignInResourceResponse
                {
                    SignInResource = new SignInResource
                    {
                        SignInLink = "https://login.test.com",
                        TokenExchangeResource = new TokenExchangeResource { Id = "id", Uri = "uri" }
                    }
                });
            mockTokenClient
                .Setup(c => c.SignOutUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ChannelId>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var handler = new AzureBotUserAuthorization(HandlerName, storage, _mockConnections.Object, _settings);
            var turnContext = CreateTurnContext(ActivityTypes.Message, mockTokenClient.Object);

            // Start a flow (creates state)
            await handler.SignInUserAsync(turnContext, forceSignIn: true, cancellationToken: CancellationToken.None);

            // Act - sign out (should clear state)
            await handler.SignOutUserAsync(turnContext, CancellationToken.None);

            // Assert - verify by starting a new flow (shouldn't try to continue old one)
            var expectedToken = new TokenResponse { Token = "new-token", Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30) };
            mockTokenClient
                .Setup(c => c.GetTokenOrSignInResourceAsync(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenOrSignInResourceResponse { TokenResponse = expectedToken });

            var result = await handler.SignInUserAsync(turnContext, forceSignIn: true, cancellationToken: CancellationToken.None);
            Assert.NotNull(result);
            Assert.Equal("new-token", result.Token);
        }

        #endregion

        #region Teams SSO Flow Tests

        [Fact]
        public async Task TeamsSso_SuccessfulTokenExchange_ReturnsTokenAndSends200()
        {
            // Arrange
            var sentActivities = new List<IActivity>();
            var mockTokenClient = new Mock<IUserTokenClient>();
            var exchangedToken = new TokenResponse { Token = "sso-token", Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30) };

            // Start flow - returns sign-in resource (OAuthCard sent)
            mockTokenClient
                .Setup(c => c.GetTokenOrSignInResourceAsync(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenOrSignInResourceResponse
                {
                    SignInResource = new SignInResource
                    {
                        SignInLink = "https://login.test.com",
                        TokenExchangeResource = new TokenExchangeResource { Id = "token-exchange-id", Uri = "api://botid/token" }
                    }
                });

            // Token exchange succeeds
            mockTokenClient
                .Setup(c => c.ExchangeTokenAsync(It.IsAny<string>(), ConnectionName, It.IsAny<ChannelId>(), It.IsAny<TokenExchangeRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(exchangedToken);

            var handler = CreateHandler();

            // Start the flow with a Teams message
            var startContext = CreateTeamsMessageTurnContext(mockTokenClient.Object);
            await handler.SignInUserAsync(startContext, forceSignIn: true, cancellationToken: CancellationToken.None);

            // Simulate Teams sending signin/tokenExchange invoke
            var tokenExchangeActivity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = SignInConstants.TokenExchangeOperationName,
                From = new ChannelAccount { Id = "user1" },
                Recipient = new ChannelAccount { Id = "bot" },
                Conversation = new ConversationAccount { Id = "convo1" },
                ChannelId = Channels.Msteams,
                Value = new TokenExchangeInvokeRequest
                {
                    Id = "token-exchange-id",
                    ConnectionName = ConnectionName,
                    Token = "teams-sso-token"
                }
            };

            var exchangeContext = CreateTurnContextWithCapture(tokenExchangeActivity, mockTokenClient.Object, sentActivities);

            // Act
            var result = await handler.SignInUserAsync(exchangeContext, cancellationToken: CancellationToken.None);

            // Assert - token returned
            Assert.NotNull(result);
            Assert.Equal("sso-token", result.Token);

            // Assert - InvokeResponse with 200 was sent
            var invokeResponse = sentActivities.FirstOrDefault(a => a.Type == ActivityTypes.InvokeResponse);
            Assert.NotNull(invokeResponse);
            var responseValue = (InvokeResponse)invokeResponse.Value;
            Assert.Equal((int)HttpStatusCode.OK, responseValue.Status);

            // Assert - StackState has the InvokeResponse for CloudAdapter to return in HTTP response body
            var stackStateResponse = exchangeContext.StackState.Get<Activity>(ChannelAdapter.InvokeResponseKey);
            Assert.NotNull(stackStateResponse);
            Assert.Equal((int)HttpStatusCode.OK, ((InvokeResponse)stackStateResponse.Value).Status);
        }

        [Fact]
        public async Task TeamsSso_ConsentRequired_Sends412AndReturnsNull()
        {
            // Arrange
            var sentActivities = new List<IActivity>();
            var mockTokenClient = new Mock<IUserTokenClient>();

            // Start flow
            mockTokenClient
                .Setup(c => c.GetTokenOrSignInResourceAsync(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenOrSignInResourceResponse
                {
                    SignInResource = new SignInResource
                    {
                        SignInLink = "https://login.test.com",
                        TokenExchangeResource = new TokenExchangeResource { Id = "id", Uri = "uri" }
                    }
                });

            // Token exchange throws ConsentRequired
            mockTokenClient
                .Setup(c => c.ExchangeTokenAsync(It.IsAny<string>(), ConnectionName, It.IsAny<ChannelId>(), It.IsAny<TokenExchangeRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ErrorResponseException("Consent required") { Body = new ErrorResponse { Error = new Error { Code = Error.ConsentRequiredCode } } });

            var handler = CreateHandler();

            // Start the flow
            var startContext = CreateTeamsMessageTurnContext(mockTokenClient.Object);
            await handler.SignInUserAsync(startContext, forceSignIn: true, cancellationToken: CancellationToken.None);

            // Simulate Teams sending signin/tokenExchange
            var tokenExchangeActivity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = SignInConstants.TokenExchangeOperationName,
                From = new ChannelAccount { Id = "user1" },
                Recipient = new ChannelAccount { Id = "bot" },
                Conversation = new ConversationAccount { Id = "convo1" },
                ChannelId = Channels.Msteams,
                Value = new TokenExchangeInvokeRequest
                {
                    Id = "exchange-id",
                    ConnectionName = ConnectionName,
                    Token = "teams-token"
                }
            };

            var exchangeContext = CreateTurnContextWithCapture(tokenExchangeActivity, mockTokenClient.Object, sentActivities);

            // Act
            var result = await handler.SignInUserAsync(exchangeContext, cancellationToken: CancellationToken.None);

            // Assert - returns null (consent pending, flow continues)
            Assert.Null(result);

            // Assert - InvokeResponse with 412 (PreconditionFailed) sent to Teams
            var invokeResponse = sentActivities.FirstOrDefault(a => a.Type == ActivityTypes.InvokeResponse);
            Assert.NotNull(invokeResponse);
            var responseValue = (InvokeResponse)invokeResponse.Value;
            Assert.Equal((int)HttpStatusCode.PreconditionFailed, responseValue.Status);
            var body = (TokenExchangeInvokeResponse)responseValue.Body;
            Assert.Contains("unable to exchange token", body.FailureDetail);

            // Assert - StackState has the InvokeResponse for HTTP response
            var stackStateResponse = exchangeContext.StackState.Get<Activity>(ChannelAdapter.InvokeResponseKey);
            Assert.NotNull(stackStateResponse);
            Assert.Equal((int)HttpStatusCode.PreconditionFailed, ((InvokeResponse)stackStateResponse.Value).Status);
        }

        [Fact]
        public async Task TeamsSso_NonConsentError_Sends400AndThrows()
        {
            // Arrange
            var sentActivities = new List<IActivity>();
            var mockTokenClient = new Mock<IUserTokenClient>();

            // Start flow
            mockTokenClient
                .Setup(c => c.GetTokenOrSignInResourceAsync(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenOrSignInResourceResponse
                {
                    SignInResource = new SignInResource
                    {
                        SignInLink = "https://login.test.com",
                        TokenExchangeResource = new TokenExchangeResource { Id = "id", Uri = "uri" }
                    }
                });

            // Token exchange throws a non-consent error (e.g. misconfiguration)
            mockTokenClient
                .Setup(c => c.ExchangeTokenAsync(It.IsAny<string>(), ConnectionName, It.IsAny<ChannelId>(), It.IsAny<TokenExchangeRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ErrorResponseException("Configuration error") { Body = new ErrorResponse { Error = new Error { Code = "ServerError" } } });

            var handler = CreateHandler();

            // Start the flow
            var startContext = CreateTeamsMessageTurnContext(mockTokenClient.Object);
            await handler.SignInUserAsync(startContext, forceSignIn: true, cancellationToken: CancellationToken.None);

            // Simulate Teams sending signin/tokenExchange
            var tokenExchangeActivity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = SignInConstants.TokenExchangeOperationName,
                From = new ChannelAccount { Id = "user1" },
                Recipient = new ChannelAccount { Id = "bot" },
                Conversation = new ConversationAccount { Id = "convo1" },
                ChannelId = Channels.Msteams,
                Value = new TokenExchangeInvokeRequest
                {
                    Id = "exchange-id",
                    ConnectionName = ConnectionName,
                    Token = "teams-token"
                }
            };

            var exchangeContext = CreateTurnContextWithCapture(tokenExchangeActivity, mockTokenClient.Object, sentActivities);

            // Act & Assert - non-consent errors propagate (after sending 400)
            await Assert.ThrowsAsync<ErrorResponseException>(() =>
                handler.SignInUserAsync(exchangeContext, cancellationToken: CancellationToken.None));

            // Assert - InvokeResponse with 400 (BadRequest) was sent
            var invokeResponse = sentActivities.FirstOrDefault(a => a.Type == ActivityTypes.InvokeResponse);
            Assert.NotNull(invokeResponse);
            var responseValue = (InvokeResponse)invokeResponse.Value;
            Assert.Equal((int)HttpStatusCode.BadRequest, responseValue.Status);

            // Assert - StackState has the InvokeResponse for HTTP response
            var stackStateResponse = exchangeContext.StackState.Get<Activity>(ChannelAdapter.InvokeResponseKey);
            Assert.NotNull(stackStateResponse);
            Assert.Equal((int)HttpStatusCode.BadRequest, ((InvokeResponse)stackStateResponse.Value).Status);
        }

        [Fact]
        public async Task TeamsSso_ConnectionNameMismatch_Sends400AndReturnsNull()
        {
            // Arrange
            var sentActivities = new List<IActivity>();
            var mockTokenClient = new Mock<IUserTokenClient>();

            // Start flow
            mockTokenClient
                .Setup(c => c.GetTokenOrSignInResourceAsync(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenOrSignInResourceResponse
                {
                    SignInResource = new SignInResource
                    {
                        SignInLink = "https://login.test.com",
                        TokenExchangeResource = new TokenExchangeResource { Id = "id", Uri = "uri" }
                    }
                });

            var handler = CreateHandler();

            // Start the flow
            var startContext = CreateTeamsMessageTurnContext(mockTokenClient.Object);
            await handler.SignInUserAsync(startContext, forceSignIn: true, cancellationToken: CancellationToken.None);

            // Simulate Teams sending signin/tokenExchange with WRONG connection name
            var tokenExchangeActivity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = SignInConstants.TokenExchangeOperationName,
                From = new ChannelAccount { Id = "user1" },
                Recipient = new ChannelAccount { Id = "bot" },
                Conversation = new ConversationAccount { Id = "convo1" },
                ChannelId = Channels.Msteams,
                Value = new TokenExchangeInvokeRequest
                {
                    Id = "exchange-id",
                    ConnectionName = "wrong-connection",
                    Token = "teams-token"
                }
            };

            var exchangeContext = CreateTurnContextWithCapture(tokenExchangeActivity, mockTokenClient.Object, sentActivities);

            // Act
            var result = await handler.SignInUserAsync(exchangeContext, cancellationToken: CancellationToken.None);

            // Assert - returns null (connection name didn't match, token exchange not attempted)
            Assert.Null(result);

            // Assert - InvokeResponse with 400 sent with failure detail about connection name
            var invokeResponse = sentActivities.FirstOrDefault(a => a.Type == ActivityTypes.InvokeResponse);
            Assert.NotNull(invokeResponse);
            var responseValue = (InvokeResponse)invokeResponse.Value;
            Assert.Equal((int)HttpStatusCode.BadRequest, responseValue.Status);
            var body = (TokenExchangeInvokeResponse)responseValue.Body;
            Assert.Contains("ConnectionName", body.FailureDetail);

            // Assert - StackState has the InvokeResponse
            var stackStateResponse = exchangeContext.StackState.Get<Activity>(ChannelAdapter.InvokeResponseKey);
            Assert.NotNull(stackStateResponse);
            Assert.Equal((int)HttpStatusCode.BadRequest, ((InvokeResponse)stackStateResponse.Value).Status);
        }

        [Fact]
        public async Task TeamsSso_NullTokenExchangeRequest_Sends400()
        {
            // Arrange
            var sentActivities = new List<IActivity>();
            var mockTokenClient = new Mock<IUserTokenClient>();

            // Start flow
            mockTokenClient
                .Setup(c => c.GetTokenOrSignInResourceAsync(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenOrSignInResourceResponse
                {
                    SignInResource = new SignInResource
                    {
                        SignInLink = "https://login.test.com",
                        TokenExchangeResource = new TokenExchangeResource { Id = "id", Uri = "uri" }
                    }
                });

            var handler = CreateHandler();

            // Start the flow
            var startContext = CreateTeamsMessageTurnContext(mockTokenClient.Object);
            await handler.SignInUserAsync(startContext, forceSignIn: true, cancellationToken: CancellationToken.None);

            // Simulate Teams sending signin/tokenExchange with null Value
            var tokenExchangeActivity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = SignInConstants.TokenExchangeOperationName,
                From = new ChannelAccount { Id = "user1" },
                Recipient = new ChannelAccount { Id = "bot" },
                Conversation = new ConversationAccount { Id = "convo1" },
                ChannelId = Channels.Msteams,
                Value = null
            };

            var exchangeContext = CreateTurnContextWithCapture(tokenExchangeActivity, mockTokenClient.Object, sentActivities);

            // Act
            var result = await handler.SignInUserAsync(exchangeContext, cancellationToken: CancellationToken.None);

            // Assert
            Assert.Null(result);

            // Assert - InvokeResponse with 400 sent
            var invokeResponse = sentActivities.FirstOrDefault(a => a.Type == ActivityTypes.InvokeResponse);
            Assert.NotNull(invokeResponse);
            var responseValue = (InvokeResponse)invokeResponse.Value;
            Assert.Equal((int)HttpStatusCode.BadRequest, responseValue.Status);
            var body = (TokenExchangeInvokeResponse)responseValue.Body;
            Assert.Contains("missing a TokenExchangeInvokeRequest", body.FailureDetail);

            // Assert - StackState has the InvokeResponse
            var stackStateResponse = exchangeContext.StackState.Get<Activity>(ChannelAdapter.InvokeResponseKey);
            Assert.NotNull(stackStateResponse);
            Assert.Equal((int)HttpStatusCode.BadRequest, ((InvokeResponse)stackStateResponse.Value).Status);
        }

        [Fact]
        public async Task TeamsSso_ExchangeReturnsNull_Sends412()
        {
            // Arrange - exchange succeeds but returns null/empty token (user hasn't consented yet)
            var sentActivities = new List<IActivity>();
            var mockTokenClient = new Mock<IUserTokenClient>();

            // Start flow
            mockTokenClient
                .Setup(c => c.GetTokenOrSignInResourceAsync(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenOrSignInResourceResponse
                {
                    SignInResource = new SignInResource
                    {
                        SignInLink = "https://login.test.com",
                        TokenExchangeResource = new TokenExchangeResource { Id = "id", Uri = "uri" }
                    }
                });

            // Exchange returns empty token
            mockTokenClient
                .Setup(c => c.ExchangeTokenAsync(It.IsAny<string>(), ConnectionName, It.IsAny<ChannelId>(), It.IsAny<TokenExchangeRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenResponse { Token = null });

            var handler = CreateHandler();

            // Start the flow
            var startContext = CreateTeamsMessageTurnContext(mockTokenClient.Object);
            await handler.SignInUserAsync(startContext, forceSignIn: true, cancellationToken: CancellationToken.None);

            // Token exchange invoke
            var tokenExchangeActivity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = SignInConstants.TokenExchangeOperationName,
                From = new ChannelAccount { Id = "user1" },
                Recipient = new ChannelAccount { Id = "bot" },
                Conversation = new ConversationAccount { Id = "convo1" },
                ChannelId = Channels.Msteams,
                Value = new TokenExchangeInvokeRequest
                {
                    Id = "exchange-id",
                    ConnectionName = ConnectionName,
                    Token = "teams-token"
                }
            };

            var exchangeContext = CreateTurnContextWithCapture(tokenExchangeActivity, mockTokenClient.Object, sentActivities);

            // Act
            var result = await handler.SignInUserAsync(exchangeContext, cancellationToken: CancellationToken.None);

            // Assert - returns null (pending consent)
            Assert.Null(result);

            // Assert - 412 PreconditionFailed triggers Teams consent prompt
            var invokeResponse = sentActivities.FirstOrDefault(a => a.Type == ActivityTypes.InvokeResponse);
            Assert.NotNull(invokeResponse);
            var responseValue = (InvokeResponse)invokeResponse.Value;
            Assert.Equal((int)HttpStatusCode.PreconditionFailed, responseValue.Status);

            // Assert - StackState has the InvokeResponse
            var stackStateResponse = exchangeContext.StackState.Get<Activity>(ChannelAdapter.InvokeResponseKey);
            Assert.NotNull(stackStateResponse);
            Assert.Equal((int)HttpStatusCode.PreconditionFailed, ((InvokeResponse)stackStateResponse.Value).Status);
        }

        [Fact]
        public async Task TeamsSso_FullFlow_StartToTokenExchangeSuccess()
        {
            // Arrange - end-to-end Teams SSO flow
            var sentActivities = new List<IActivity>();
            var mockTokenClient = new Mock<IUserTokenClient>();
            var finalToken = new TokenResponse { Token = "final-sso-token", Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30) };

            // BeginFlow: no cached token, returns sign-in resource
            mockTokenClient
                .Setup(c => c.GetTokenOrSignInResourceAsync(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenOrSignInResourceResponse
                {
                    SignInResource = new SignInResource
                    {
                        SignInLink = "https://login.microsoftonline.com/...",
                        TokenExchangeResource = new TokenExchangeResource { Id = "token-exchange-id", Uri = "api://botid/access_as_user" },
                        TokenPostResource = new TokenPostResource { SasUrl = "https://token.botframework.com/..." }
                    }
                });

            // ExchangeToken succeeds
            mockTokenClient
                .Setup(c => c.ExchangeTokenAsync("user1", ConnectionName, It.IsAny<ChannelId>(), It.Is<TokenExchangeRequest>(r => r.Token == "teams-sso-jwt"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(finalToken);

            var handler = CreateHandler();

            // Step 1: User sends message in Teams -> OAuthCard is sent
            var step1Context = CreateTeamsMessageTurnContext(mockTokenClient.Object);
            var step1Result = await handler.SignInUserAsync(step1Context, forceSignIn: true, cancellationToken: CancellationToken.None);
            Assert.Null(step1Result); // flow started, waiting for token exchange

            // Step 2: Teams sends signin/tokenExchange invoke with SSO token
            var tokenExchangeActivity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = SignInConstants.TokenExchangeOperationName,
                From = new ChannelAccount { Id = "user1" },
                Recipient = new ChannelAccount { Id = "bot" },
                Conversation = new ConversationAccount { Id = "convo1" },
                ChannelId = Channels.Msteams,
                Value = new TokenExchangeInvokeRequest
                {
                    Id = "token-exchange-id",
                    ConnectionName = ConnectionName,
                    Token = "teams-sso-jwt"
                }
            };
            var step2Context = CreateTurnContextWithCapture(tokenExchangeActivity, mockTokenClient.Object, sentActivities);

            // Act
            var step2Result = await handler.SignInUserAsync(step2Context, cancellationToken: CancellationToken.None);

            // Assert - token returned
            Assert.NotNull(step2Result);
            Assert.Equal("final-sso-token", step2Result.Token);

            // Assert - 200 InvokeResponse sent back to Teams
            var invokeResponse = sentActivities.FirstOrDefault(a => a.Type == ActivityTypes.InvokeResponse);
            Assert.NotNull(invokeResponse);
            var responseValue = (InvokeResponse)invokeResponse.Value;
            Assert.Equal((int)HttpStatusCode.OK, responseValue.Status);

            // Assert - StackState has the InvokeResponse (CloudAdapter returns this in HTTP response body)
            var stackStateResponse = step2Context.StackState.Get<Activity>(ChannelAdapter.InvokeResponseKey);
            Assert.NotNull(stackStateResponse);
            Assert.Equal((int)HttpStatusCode.OK, ((InvokeResponse)stackStateResponse.Value).Status);

            // Verify ExchangeTokenAsync was called with the correct token
            mockTokenClient.Verify(
                c => c.ExchangeTokenAsync("user1", ConnectionName, It.IsAny<ChannelId>(), It.Is<TokenExchangeRequest>(r => r.Token == "teams-sso-jwt"), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task TeamsSso_ConsentThenRetry_SucceedsOnSecondExchange()
        {
            // Arrange - simulates: first exchange needs consent (412), second exchange succeeds (200)
            var sentActivities = new List<IActivity>();
            var mockTokenClient = new Mock<IUserTokenClient>();
            var finalToken = new TokenResponse { Token = "consent-granted-token", Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30) };

            // BeginFlow
            mockTokenClient
                .Setup(c => c.GetTokenOrSignInResourceAsync(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenOrSignInResourceResponse
                {
                    SignInResource = new SignInResource
                    {
                        SignInLink = "https://login.test.com",
                        TokenExchangeResource = new TokenExchangeResource { Id = "id", Uri = "uri" }
                    }
                });

            int exchangeAttempt = 0;
            mockTokenClient
                .Setup(c => c.ExchangeTokenAsync(It.IsAny<string>(), ConnectionName, It.IsAny<ChannelId>(), It.IsAny<TokenExchangeRequest>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    exchangeAttempt++;
                    if (exchangeAttempt == 1)
                    {
                        // First attempt: consent required
                        throw new ErrorResponseException("Consent required") { Body = new ErrorResponse { Error = new Error { Code = Error.ConsentRequiredCode } } };
                    }
                    // Second attempt: success after consent
                    return Task.FromResult(finalToken);
                });

            var handler = CreateHandler();

            // Start flow
            var startContext = CreateTeamsMessageTurnContext(mockTokenClient.Object);
            await handler.SignInUserAsync(startContext, forceSignIn: true, cancellationToken: CancellationToken.None);

            // First token exchange - consent required -> 412
            var exchange1Activity = CreateTokenExchangeInvokeActivity("pre-consent-token");
            var exchange1Context = CreateTurnContextWithCapture(exchange1Activity, mockTokenClient.Object, sentActivities);
            var result1 = await handler.SignInUserAsync(exchange1Context, cancellationToken: CancellationToken.None);
            Assert.Null(result1);

            var firstResponse = sentActivities.FirstOrDefault(a => a.Type == ActivityTypes.InvokeResponse);
            Assert.Equal((int)HttpStatusCode.PreconditionFailed, ((InvokeResponse)firstResponse.Value).Status);

            // Second token exchange - after user consented -> 200
            sentActivities.Clear();
            var exchange2Activity = CreateTokenExchangeInvokeActivity("post-consent-token");
            var exchange2Context = CreateTurnContextWithCapture(exchange2Activity, mockTokenClient.Object, sentActivities);
            var result2 = await handler.SignInUserAsync(exchange2Context, cancellationToken: CancellationToken.None);

            // Assert
            Assert.NotNull(result2);
            Assert.Equal("consent-granted-token", result2.Token);

            var secondResponse = sentActivities.FirstOrDefault(a => a.Type == ActivityTypes.InvokeResponse);
            Assert.Equal((int)HttpStatusCode.OK, ((InvokeResponse)secondResponse.Value).Status);
        }

        [Fact]
        public async Task TeamsSso_ConsentRequired_ThenVerifyState_Succeeds()
        {
            // Arrange
            var sentActivities = new List<IActivity>();
            var mockTokenClient = new Mock<IUserTokenClient>();
            var finalToken = new TokenResponse { Token = "verify-state-token", Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30) };

            mockTokenClient
                .Setup(c => c.GetTokenOrSignInResourceAsync(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenOrSignInResourceResponse
                {
                    SignInResource = new SignInResource
                    {
                        SignInLink = "https://login.test.com",
                        TokenExchangeResource = new TokenExchangeResource { Id = "id", Uri = "uri" }
                    }
                });

            mockTokenClient
                .Setup(c => c.ExchangeTokenAsync(It.IsAny<string>(), ConnectionName, It.IsAny<ChannelId>(), It.IsAny<TokenExchangeRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ErrorResponseException("Consent required") { Body = new ErrorResponse { Error = new Error { Code = Error.ConsentRequiredCode } } });

            mockTokenClient
                .Setup(c => c.GetUserTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ChannelId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(finalToken);

            var handler = CreateHandler();

            var startContext = CreateTeamsMessageTurnContext(mockTokenClient.Object);
            await handler.SignInUserAsync(startContext, forceSignIn: true, cancellationToken: CancellationToken.None);

            var tokenExchangeContext = CreateTurnContextWithCapture(CreateTokenExchangeInvokeActivity("pre-consent-token"), mockTokenClient.Object, sentActivities);
            var tokenExchangeResult = await handler.SignInUserAsync(tokenExchangeContext, cancellationToken: CancellationToken.None);

            Assert.Null(tokenExchangeResult);
            var tokenExchangeResponse = sentActivities.FirstOrDefault(a => a.Type == ActivityTypes.InvokeResponse);
            Assert.NotNull(tokenExchangeResponse);
            Assert.Equal((int)HttpStatusCode.PreconditionFailed, ((InvokeResponse)tokenExchangeResponse.Value).Status);

            sentActivities.Clear();

            var verifyStateActivity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = SignInConstants.VerifyStateOperationName,
                From = new ChannelAccount { Id = "user1" },
                Recipient = new ChannelAccount { Id = "bot" },
                Conversation = new ConversationAccount { Id = "convo1" },
                ChannelId = Channels.Msteams,
                Value = new Dictionary<string, object> { { "state", "123456" } }
            };

            var verifyStateContext = CreateTurnContextWithCapture(verifyStateActivity, mockTokenClient.Object, sentActivities);

            // Act
            var result = await handler.SignInUserAsync(verifyStateContext, cancellationToken: CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("verify-state-token", result.Token);

            var verifyStateResponse = sentActivities.FirstOrDefault(a => a.Type == ActivityTypes.InvokeResponse);
            Assert.NotNull(verifyStateResponse);
            Assert.Equal((int)HttpStatusCode.OK, ((InvokeResponse)verifyStateResponse.Value).Status);
        }

        [Fact]
        public async Task SignInUserAsync_ThrowsAuthException_WhenTextMessageDuringFlow()
        {
            // Arrange - Teams may deliver a user message before the expected signin invoke arrives.
            var settings = new OAuthSettings
            {
                AzureBotOAuthConnectionName = ConnectionName,
                Timeout = 900000,
                InvalidSignInRetryMax = 1,
            };

            var sentActivities = new List<IActivity>();
            var mockTokenClient = new Mock<IUserTokenClient>();

            mockTokenClient
                .Setup(c => c.GetTokenOrSignInResourceAsync(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenOrSignInResourceResponse
                {
                    SignInResource = new SignInResource
                    {
                        SignInLink = "https://login.test.com",
                        TokenExchangeResource = new TokenExchangeResource { Id = "id", Uri = "uri" }
                    }
                });

            var handler = new AzureBotUserAuthorization(HandlerName, _storage, _mockConnections.Object, settings);

            // Start the flow.
            var startContext = CreateTurnContext(ActivityTypes.Message, mockTokenClient.Object);
            var startResult = await handler.SignInUserAsync(startContext, forceSignIn: true, cancellationToken: CancellationToken.None);
            Assert.Null(startResult);

            // User sends a text message before the expected signin/tokenExchange arrives.
            sentActivities.Clear();
            var textDuringFlow = CreateMessageActivity(text: "hello?");
            var textContext = CreateTurnContextWithCapture(textDuringFlow, mockTokenClient.Object, sentActivities);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AuthException>(() =>
                handler.SignInUserAsync(textContext, forceSignIn: false, cancellationToken: CancellationToken.None));
            Assert.Equal(AuthExceptionReason.InvalidSignIn, ex.Cause);
        }

        [Fact]
        public async Task TeamsSSO_TextMessageDuringFlow_SendsInProgressMessage_AndDoesNotConsumeRetry()
        {
            // Arrange - Teams may deliver a user message before the expected signin invoke arrives.
            var settings = new OAuthSettings
            {
                AzureBotOAuthConnectionName = ConnectionName,
                Timeout = 900000,
                InvalidSignInRetryMax = 1,
                TeamsSignInInProgressMessage = "Please finish signing and wait for completion"
            };

            var sentActivities = new List<IActivity>();
            var mockTokenClient = new Mock<IUserTokenClient>();
            var finalToken = new TokenResponse { Token = "teams-sso-token", Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30) };

            mockTokenClient
                .Setup(c => c.GetTokenOrSignInResourceAsync(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenOrSignInResourceResponse
                {
                    SignInResource = new SignInResource
                    {
                        SignInLink = "https://login.test.com",
                        TokenExchangeResource = new TokenExchangeResource { Id = "id", Uri = "uri" }
                    }
                });

            mockTokenClient
                .Setup(c => c.ExchangeTokenAsync(It.IsAny<string>(), ConnectionName, It.IsAny<ChannelId>(), It.IsAny<TokenExchangeRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(finalToken);

            var handler = new AzureBotUserAuthorization(HandlerName, _storage, _mockConnections.Object, settings);

            // Start the flow.
            var startContext = CreateTeamsMessageTurnContext(mockTokenClient.Object);
            var startResult = await handler.SignInUserAsync(startContext, forceSignIn: true, cancellationToken: CancellationToken.None);
            Assert.Null(startResult);

            // User sends a text message before Teams sends signin/tokenExchange.
            sentActivities.Clear();
            var textDuringFlow = CreateMessageActivity(Channels.Msteams, "Hello?");
            var textContext = CreateTurnContextWithCapture(textDuringFlow, mockTokenClient.Object, sentActivities);

            var pendingResult = await handler.SignInUserAsync(textContext, cancellationToken: CancellationToken.None);

            // Assert - flow remains pending and user gets a Teams-specific in-progress message.
            Assert.Null(pendingResult);
            var inProgressMessage = sentActivities.OfType<Activity>().LastOrDefault(a => a.Type == ActivityTypes.Message);
            Assert.NotNull(inProgressMessage);
            Assert.Equal(settings.TeamsSignInInProgressMessage, inProgressMessage.Text);

            // The delayed signin/tokenExchange should still succeed even though retry max is 1.
            sentActivities.Clear();
            var tokenExchangeActivity = CreateTokenExchangeInvokeActivity("teams-sso-jwt");
            var tokenExchangeContext = CreateTurnContextWithCapture(tokenExchangeActivity, mockTokenClient.Object, sentActivities);

            var finalResult = await handler.SignInUserAsync(tokenExchangeContext, cancellationToken: CancellationToken.None);

            Assert.NotNull(finalResult);
            Assert.Equal("teams-sso-token", finalResult.Token);
        }

        [Fact]
        public async Task TeamsSso_CancelCommand_DuringFlow_CancelsFlow_AndSendsConfiguredMessage()
        {
            // Arrange
            var settings = new OAuthSettings
            {
                AzureBotOAuthConnectionName = ConnectionName,
                Timeout = 900000,
                TeamsSignInInProgressMessage = "Please finish signing and wait for completion",
                CancelSignInCommands = ["-cancel", "/cancel"],
                SignInCancelledMessage = "Authentication cancelled."
            };

            var sentActivities = new List<IActivity>();
            var mockTokenClient = new Mock<IUserTokenClient>();

            mockTokenClient
                .Setup(c => c.GetTokenOrSignInResourceAsync(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenOrSignInResourceResponse
                {
                    SignInResource = new SignInResource
                    {
                        SignInLink = "https://login.test.com",
                        TokenExchangeResource = new TokenExchangeResource { Id = "id", Uri = "uri" }
                    }
                });

            var handler = new AzureBotUserAuthorization(HandlerName, _storage, _mockConnections.Object, settings);

            var startContext = CreateTeamsMessageTurnContext(mockTokenClient.Object);
            var startResult = await handler.SignInUserAsync(startContext, forceSignIn: true, cancellationToken: CancellationToken.None);
            Assert.Null(startResult);

            sentActivities.Clear();
            var cancelActivity = CreateMessageActivity(Channels.Msteams, "-cancel");
            var cancelContext = CreateTurnContextWithCapture(cancelActivity, mockTokenClient.Object, sentActivities);

            var pendingResult = await handler.SignInUserAsync(cancelContext, cancellationToken: CancellationToken.None);
            Assert.Null(pendingResult);

            var cancelMessage = sentActivities.OfType<Activity>().LastOrDefault(a => a.Type == ActivityTypes.Message);
            Assert.NotNull(cancelMessage);
            Assert.Equal(settings.SignInCancelledMessage, cancelMessage.Text);
            Assert.DoesNotContain(sentActivities, a => a.Type == ActivityTypes.Message && ((Activity)a).Text == settings.TeamsSignInInProgressMessage);

            // Flow state should be cleared so a new message starts a fresh flow instead of continuing the old one.
            sentActivities.Clear();
            var restartContext = CreateTurnContextWithCapture(CreateTeamsMessageTurnContext(mockTokenClient.Object).Activity, mockTokenClient.Object, sentActivities);
            var restartResult = await handler.SignInUserAsync(restartContext, forceSignIn: true, cancellationToken: CancellationToken.None);

            Assert.Null(restartResult);
            Assert.Contains(sentActivities.OfType<Activity>(), a => a.Attachments?.Any(att => att.ContentType == OAuthCard.ContentType) == true);
        }

        [Fact]
        public async Task SignInUserAsync_CancelCommand_IsCaseInsensitive_AndDoesNotUseRetryBudget()
        {
            // Arrange
            var settings = new OAuthSettings
            {
                AzureBotOAuthConnectionName = ConnectionName,
                Timeout = 900000,
                InvalidSignInRetryMax = 1,
                CancelSignInCommands = ["-cancel"],
                SignInCancelledMessage = "Sign in cancelled."
            };

            var sentActivities = new List<IActivity>();
            var mockTokenClient = new Mock<IUserTokenClient>();

            mockTokenClient
                .Setup(c => c.GetTokenOrSignInResourceAsync(It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenOrSignInResourceResponse
                {
                    SignInResource = new SignInResource
                    {
                        SignInLink = "https://login.test.com",
                        TokenExchangeResource = new TokenExchangeResource { Id = "id", Uri = "uri" }
                    }
                });

            var handler = new AzureBotUserAuthorization(HandlerName, _storage, _mockConnections.Object, settings);

            var startContext = CreateTurnContext(ActivityTypes.Message, mockTokenClient.Object);
            await handler.SignInUserAsync(startContext, forceSignIn: true, cancellationToken: CancellationToken.None);

            var cancelActivity = CreateMessageActivity("test", "  -CANCEL  ");
            var cancelContext = CreateTurnContextWithCapture(cancelActivity, mockTokenClient.Object, sentActivities);

            var pendingResult = await handler.SignInUserAsync(cancelContext, cancellationToken: CancellationToken.None);

            Assert.Null(pendingResult);
            Assert.Contains(sentActivities.OfType<Activity>(), a => a.Text == settings.SignInCancelledMessage);

            sentActivities.Clear();
            var nextMessage = CreateTurnContextWithCapture(CreateMessageActivity("test", "hello"), mockTokenClient.Object, sentActivities);
            var result = await handler.SignInUserAsync(nextMessage, forceSignIn: true, cancellationToken: CancellationToken.None);

            Assert.Null(result);
            Assert.Contains(sentActivities.OfType<Activity>(), a => a.Attachments?.Any(att => att.ContentType == OAuthCard.ContentType) == true);
        }

        #endregion

        #region Helpers

        private delegate void TryGetConnectionCallback(string name, out IAccessTokenProvider provider);

        private AzureBotUserAuthorization CreateHandler()
        {
            return new AzureBotUserAuthorization(HandlerName, _storage, _mockConnections.Object, _settings);
        }

        private static TurnContext CreateTurnContext(string activityType, IUserTokenClient tokenClient)
        {
            var activity = new Activity
            {
                Type = activityType,
                From = new ChannelAccount { Id = "user1" },
                Recipient = new ChannelAccount { Id = "bot" },
                Conversation = new ConversationAccount { Id = "convo1" },
                ChannelId = "test"
            };
            return CreateTurnContext(activity, tokenClient);
        }

        private static TurnContext CreateTurnContext(IActivity activity, IUserTokenClient tokenClient)
        {
            var context = new TurnContext(new SimpleAdapter(), (Activity)activity);
            if (tokenClient != null)
            {
                context.Services.Set(tokenClient);
            }
            return context;
        }

        private static TurnContext CreateTeamsMessageTurnContext(IUserTokenClient tokenClient)
        {
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                From = new ChannelAccount { Id = "user1" },
                Recipient = new ChannelAccount { Id = "bot" },
                Conversation = new ConversationAccount { Id = "convo1" },
                ChannelId = Channels.Msteams,
                Text = "some message"
            };
            return CreateTurnContext(activity, tokenClient);
        }

        private static TurnContext CreateVerifyStateInvokeContext(string magicCode, IUserTokenClient tokenClient)
        {
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = SignInConstants.VerifyStateOperationName,
                From = new ChannelAccount { Id = "user1" },
                Recipient = new ChannelAccount { Id = "bot" },
                Conversation = new ConversationAccount { Id = "convo1" },
                ChannelId = "test",
                Value = new Dictionary<string, object> { { "state", magicCode } }
            };
            return CreateTurnContext(activity, tokenClient);
        }

        private static TurnContext CreateTurnContextWithCapture(IActivity activity, IUserTokenClient tokenClient, List<IActivity> capturedActivities)
        {
            void CaptureActivities(IActivity[] activities)
            {
                capturedActivities.AddRange(activities);
            }

            var context = new TurnContext(new SimpleAdapter(CaptureActivities), (Activity)activity);
            if (tokenClient != null)
            {
                context.Services.Set(tokenClient);
            }

            // Mimic ChannelServiceAdapterBase behavior: when an InvokeResponse activity is sent,
            // set it on StackState so ProcessTurnResults can return it in the HTTP response.
            context.OnSendActivities(async (ctx, acts, next) =>
            {
                var responses = await next().ConfigureAwait(false);
                foreach (var sent in acts)
                {
                    if (sent.Type == ActivityTypes.InvokeResponse)
                    {
                        ctx.StackState.Set(ChannelAdapter.InvokeResponseKey, (Activity)sent);
                    }
                }
                return responses;
            });

            return context;
        }

        private static Activity CreateMessageActivity(string channelId = "test", string text = null)
        {
            return new Activity
            {
                Type = ActivityTypes.Message,
                From = new ChannelAccount { Id = "user1" },
                Recipient = new ChannelAccount { Id = "bot" },
                Conversation = new ConversationAccount { Id = "convo1" },
                ChannelId = channelId,
                Text = text
            };
        }

        private static Activity CreateTokenExchangeInvokeActivity(string token)
        {
            return new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = SignInConstants.TokenExchangeOperationName,
                From = new ChannelAccount { Id = "user1" },
                Recipient = new ChannelAccount { Id = "bot" },
                Conversation = new ConversationAccount { Id = "convo1" },
                ChannelId = Channels.Msteams,
                Value = new TokenExchangeInvokeRequest
                {
                    Id = "exchange-id",
                    ConnectionName = ConnectionName,
                    Token = token
                }
            };
        }

        #endregion
    }
}
