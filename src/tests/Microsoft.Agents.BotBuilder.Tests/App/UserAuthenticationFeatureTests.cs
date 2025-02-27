// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.App.UserAuth;
using Microsoft.Agents.BotBuilder.Testing;
using Microsoft.Agents.BotBuilder.Tests.App.TestUtils;
using Microsoft.Agents.BotBuilder.UserAuth;
using Microsoft.Agents.Core.Models;
using Moq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.BotBuilder.Tests.App
{
    public class UserAuthenticationFeatureTests
    {
        private const string GraphName = "graph";
        private const string SharePointName = "sharepoint";
        private const string GraphToken = "graph token";
        private const string SharePointToken = "sharePoint token";
        private Mock<IUserAuthentication> MockGraph;
        private Mock<IUserAuthentication> MockSharePoint;

        public UserAuthenticationFeatureTests()
        {
            MockGraph = new Mock<IUserAuthentication>();
            MockGraph
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new TokenResponse() { Token = GraphToken }));
            MockGraph
                .Setup(e => e.Name)
                .Returns(GraphName);

            MockSharePoint = new Mock<IUserAuthentication>();
            MockSharePoint
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new TokenResponse() { Token = SharePointToken }));
            MockSharePoint
                .Setup(e => e.Name)
                .Returns(SharePointName);
        }

        [Fact]
        public async Task Test_AutoSignIn_Default()
        {
            // arrange
            var app = new TestApplication(new TestApplicationOptions());
            var options = new UserAuthenticationOptions();
            options.Handlers =
            [
                MockGraph.Object,
                MockSharePoint.Object,
            ];

            var authManager = new TestUserAuthenticationFeature(app, options);
            var turnContext = MockTurnContext();
            var turnState = await TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            // act
            var response = await authManager.SignUserInAsync(turnContext, turnState);

            // assert
            Assert.False(response);
            Assert.True(turnState.Temp.AuthTokens.ContainsKey(GraphName));
            Assert.Equal(GraphToken, turnState.Temp.AuthTokens[GraphName]);
        }

        [Fact]
        public async Task Test_AutoSignIn_Named()
        {
            // arrange
            var app = new TestApplication(new TestApplicationOptions());
            var options = new UserAuthenticationOptions
            {
                Handlers =
                [
                    MockGraph.Object,
                    MockSharePoint.Object,
                ]
            };
            var authManager = new TestUserAuthenticationFeature(app, options);
            var turnContext = MockTurnContext();
            var turnState = await TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            // act
            var response = await authManager.SignUserInAsync(turnContext, turnState, SharePointName);

            // assert
            Assert.False(response);
            Assert.True(turnState.Temp.AuthTokens.ContainsKey(SharePointName));
            Assert.Equal(SharePointToken, turnState.Temp.AuthTokens[SharePointName]);
        }

        [Fact]
        public async Task Test_AutoSignIn_Pending()
        {
            MockGraph
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((TokenResponse) null));

            var app = new TestApplication(new TestApplicationOptions());
            var options = new UserAuthenticationOptions
            {
                Handlers =
                [
                    MockGraph.Object
                ]
            };
            var authManager = new TestUserAuthenticationFeature(app, options);
            var turnContext = MockTurnContext();
            var turnState = await TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            // act
            var response = await authManager.SignUserInAsync(turnContext, turnState);

            // assert
            Assert.True(response);
        }

        [Fact]
        public async Task Test_SignOut_DefaultHandler()
        {
            // arrange
            var app = new TestApplication(new TestApplicationOptions());
            var options = new UserAuthenticationOptions
            {
                Handlers =
                [
                    MockGraph.Object,
                    MockSharePoint.Object,
                ]
            };

            var authManager = new TestUserAuthenticationFeature(app, options);
            var turnContext = MockTurnContext();
            
            var turnState = await TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            turnState.Temp.AuthTokens = new Dictionary<string, string>()
            {
                {GraphName, "graph token" },
                {SharePointName, "sharepoint token" }
            };

            // act
            await authManager.SignOutUserAsync(turnContext, turnState);

            // assert
            Assert.False(turnState.Temp.AuthTokens.ContainsKey(GraphName));
            Assert.True(turnState.Temp.AuthTokens.ContainsKey(SharePointName));
        }

        [Fact]
        public async Task Test_SignOut_SpecificHandler()
        {
            // arrange
            var app = new TestApplication(new TestApplicationOptions());
            var options = new UserAuthenticationOptions
            {
                Handlers =
                [
                    MockGraph.Object,
                    MockSharePoint.Object,
                ]
            };
            var authManager = new TestUserAuthenticationFeature(app, options);
            var turnContext = MockTurnContext();
            var turnState = await TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            turnState.Temp.AuthTokens = new Dictionary<string, string>()
            {
                {GraphName, "graph token" },
                {SharePointName, "sharepoint token" }
            };

            // act
            await authManager.SignOutUserAsync(turnContext, turnState, SharePointName);

            // assert
            Assert.False(turnState.Temp.AuthTokens.ContainsKey(SharePointName));
            Assert.True(turnState.Temp.AuthTokens.ContainsKey(GraphName));
        }

        private static TurnContext MockTurnContext()
        {
            return new TurnContext(new SimpleAdapter(), new Activity()
            {
                Type = ActivityTypes.Message,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
        }
    }
}
