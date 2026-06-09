// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Authentication.Msal;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Builder.UserAuth.TeamsAgentic;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Storage;
using Microsoft.Identity.Client;
using Moq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests
{
    public class TeamsAgenticAuthorizationTests
    {
        [Fact]
        public async Task OnContinueFlow_ReturnsTokenFromVerifyStateInvoke()
        {
            // Arrange
            var storage = new MemoryStorage();
            var settings = new TeamsAgenticSettings(
                "connection",
                ["api://test/.default"],
                redirectUri: "https://bot.test/auth/callback",
                connectionName: "connection");

            var appConfig = new Mock<IAppConfig>();
            appConfig.Setup(c => c.ClientId).Returns("test-client-id");
            appConfig.Setup(c => c.TenantId).Returns("test-tenant-id");

            var msalApp = new Mock<IConfidentialClientApplication>();
            msalApp.Setup(a => a.AppConfig).Returns(appConfig.Object);

            var connection = new Mock<IAccessTokenProvider>();
            connection.As<IMSALProvider>()
                .Setup(provider => provider.CreateClientApplication())
                .Returns(msalApp.Object);

            var connections = new Mock<IConnections>();
            connections
                .Setup(provider => provider.GetConnection(settings.ConnectionName))
                .Returns(connection.Object);

            var authorization = new TeamsAgenticAuthorization("authName", storage, connections.Object, settings);

            // Simulate a signin/verifyState invoke with a token in the value
            var turnContext = new TurnContext(
                new SimpleAdapter(),
                new Activity
                {
                    Type = ActivityTypes.Invoke,
                    Name = "signin/verifyState",
                    ChannelId = Channels.Msteams,
                    From = new ChannelAccount { Id = "user1", AadObjectId = "aad-user" },
                    Conversation = new ConversationAccount { Id = "conversation1", TenantId = "tenant1" },
                    Value = ProtocolJsonSerializer.ToJsonElements(new Dictionary<string, string> { ["token"] = "my-access-token" })
                });

            typeof(TeamsAgenticAuthorization)
                .GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(
                    authorization,
                    new AgenticFlowState
                    {
                        FlowStarted = true,
                        FlowExpires = DateTime.UtcNow.AddMinutes(5),
                        ContinueCount = 0
                    });

            // Act
            var result = await InvokeOnContinueFlowAsync(authorization, turnContext);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("my-access-token", result.Token);
        }

        [Fact]
        public async Task OnContinueFlow_ThrowsAuthException_OnSignInFailureInvoke()
        {
            // Arrange
            var storage = new MemoryStorage();
            var settings = new TeamsAgenticSettings(
                "connection",
                ["api://test/.default"],
                redirectUri: "https://bot.test/auth/callback",
                connectionName: "connection");

            var appConfig = new Mock<IAppConfig>();
            appConfig.Setup(c => c.ClientId).Returns("test-client-id");
            appConfig.Setup(c => c.TenantId).Returns("test-tenant-id");

            var msalApp = new Mock<IConfidentialClientApplication>();
            msalApp.Setup(a => a.AppConfig).Returns(appConfig.Object);

            var connection = new Mock<IAccessTokenProvider>();
            connection.As<IMSALProvider>()
                .Setup(provider => provider.CreateClientApplication())
                .Returns(msalApp.Object);

            var connections = new Mock<IConnections>();
            connections
                .Setup(provider => provider.GetConnection(settings.ConnectionName))
                .Returns(connection.Object);

            var authorization = new TeamsAgenticAuthorization("authName", storage, connections.Object, settings);

            // Simulate a signin/failure invoke sent by the callback on code exchange failure
            var turnContext = new TurnContext(
                new SimpleAdapter(),
                new Activity
                {
                    Type = ActivityTypes.Invoke,
                    Name = SignInConstants.SignInFailure,
                    ChannelId = Channels.Msteams,
                    From = new ChannelAccount { Id = "user1", AadObjectId = "aad-user" },
                    Conversation = new ConversationAccount { Id = "conversation1", TenantId = "tenant1" },
                    Value = ProtocolJsonSerializer.ToJsonElements(new Dictionary<string, string> { ["error"] = "code exchange failed" })
                });

            typeof(TeamsAgenticAuthorization)
                .GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(
                    authorization,
                    new AgenticFlowState
                    {
                        FlowStarted = true,
                        FlowExpires = DateTime.UtcNow.AddMinutes(5),
                        ContinueCount = 0
                    });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AuthException>(() => InvokeOnContinueFlowAsync(authorization, turnContext));
            Assert.Equal(AuthExceptionReason.InvalidSignIn, ex.Cause);
            Assert.Contains("Sign in failed", ex.Message);
        }

        [Fact]
        public async Task SignInUserAsync_WhenMsalCacheHasNoAccount_SendsAdaptiveCard()
        {
            // Arrange: MSAL app has no cached account — falls through to interactive card
            var storage = new MemoryStorage();
            var settings = new TeamsAgenticSettings(
                "oauthConnection",
                ["api://test/.default"],
                redirectUri: "https://bot.test/auth/callback",
                connectionName: "connection");

            var appConfig = new Mock<IAppConfig>();
            appConfig.Setup(c => c.ClientId).Returns("test-client-id");
            appConfig.Setup(c => c.TenantId).Returns("test-tenant-id");

            var msalApp = new Mock<IConfidentialClientApplication>();
            msalApp.Setup(a => a.AppConfig).Returns(appConfig.Object);
            msalApp.Setup(a => a.GetAccountAsync(It.IsAny<string>()))
                .ReturnsAsync((IAccount)null);

            var connection = new Mock<IAccessTokenProvider>();
            connection.As<IMSALProvider>()
                .Setup(p => p.CreateClientApplication())
                .Returns(msalApp.Object);
            connection.As<IMSALProvider>()
                .Setup(p => p.GetOrCreateConfidentialClient("https://bot.test/auth/callback"))
                .Returns(msalApp.Object);

            var connections = new Mock<IConnections>();
            connections.Setup(c => c.GetConnection("connection")).Returns(connection.Object);
            connections.Setup(c => c.GetConnection("oauthConnection")).Returns(connection.Object);

            var authorization = new TeamsAgenticAuthorization("authName", storage, connections.Object, settings);

            var sentActivities = new List<IActivity>();
            var turnContext = new TurnContext(
                new SimpleAdapter(activities => sentActivities.AddRange(activities)),
                new Activity
                {
                    Type = ActivityTypes.Message,
                    Text = "hello",
                    ChannelId = Channels.Msteams,
                    From = new ChannelAccount { Id = "user1", AadObjectId = "aad-user-id" },
                    Conversation = new ConversationAccount { Id = "conv1", TenantId = "tenant-id" }
                });

            // Act
            var result = await authorization.SignInUserAsync(turnContext, true, null, null, CancellationToken.None);

            // Assert — no cached account, so adaptive card was sent
            Assert.Null(result?.Token);
            Assert.NotEmpty(sentActivities);
        }

        [Fact]
        public async Task GetRefreshedUserTokenAsync_WhenNoRedirectUri_ReturnsNull()
        {
            // Arrange
            var storage = new MemoryStorage();
            var settings = new TeamsAgenticSettings("connection", ["api://test/.default"]);

            var appConfig = new Mock<IAppConfig>();
            appConfig.Setup(c => c.ClientId).Returns("test-client-id");
            appConfig.Setup(c => c.TenantId).Returns("test-tenant-id");

            var msalApp = new Mock<IConfidentialClientApplication>();
            msalApp.Setup(a => a.AppConfig).Returns(appConfig.Object);

            var connection = new Mock<IAccessTokenProvider>();
            connection.As<IMSALProvider>()
                .Setup(p => p.CreateClientApplication())
                .Returns(msalApp.Object);

            var connections = new Mock<IConnections>();
            connections.Setup(c => c.GetConnection("connection")).Returns(connection.Object);

            var authorization = new TeamsAgenticAuthorization("authName", storage, connections.Object, settings);

            var turnContext = new TurnContext(
                new SimpleAdapter(),
                new Activity
                {
                    Type = ActivityTypes.Message,
                    Text = "hello",
                    ChannelId = Channels.Msteams,
                    From = new ChannelAccount { Id = "user1", AadObjectId = "aad-user-id" },
                    Conversation = new ConversationAccount { Id = "conv1", TenantId = "tenant-id" }
                });

            // Act
            var result = await authorization.GetRefreshedUserTokenAsync(turnContext);

            // Assert
            Assert.Null(result);
        }

        private static async Task<TokenResponse> InvokeOnContinueFlowAsync(TeamsAgenticAuthorization authorization, ITurnContext turnContext)
        {
            var onContinueFlow = typeof(TeamsAgenticAuthorization).GetMethod("OnContinueFlow", BindingFlags.Instance | BindingFlags.NonPublic);
            var task = (Task<TokenResponse>)onContinueFlow.Invoke(authorization, [turnContext, CancellationToken.None]);
            return await task;
        }
    }
}
