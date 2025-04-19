// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class UserAuthenticationFeatureTests
    {
        private const string GraphName = "graph";
        private const string SharePointName = "sharepoint";
        private const string GraphToken = "graph token";
        private const string SharePointToken = "sharePoint token";
        private Mock<IUserAuthorization> MockGraph;
        private Mock<IUserAuthorization> MockSharePoint;
        private Mock<IChannelAdapter> MockChannelAdapter;
        private Mock<IConnections> MockConnections;

        public UserAuthenticationFeatureTests()
        {
            MockGraph = new Mock<IUserAuthorization>();
            MockGraph
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new TokenResponse() { Token = GraphToken, Expiration = DateTime.UtcNow + TimeSpan.FromMinutes(5) }));
            MockGraph
                .Setup(e => e.Name)
                .Returns(GraphName);

            MockSharePoint = new Mock<IUserAuthorization>();
            MockSharePoint
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new TokenResponse() { Token = SharePointToken, Expiration = DateTime.UtcNow + TimeSpan.FromMinutes(5) }));
            MockSharePoint
                .Setup(e => e.Name)
                .Returns(SharePointName);

            MockChannelAdapter = new Mock<IChannelAdapter>();

            MockConnections = new Mock<IConnections>();
        }

        [Fact]
        public void Test_NoAdapter()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                var storage = new MemoryStorage();
                var app = new TestApplication(new TestApplicationOptions(storage));
                var options = new UserAuthorizationOptions(MockConnections.Object, MockGraph.Object, MockSharePoint.Object);
                var authManager = new TestUserAuthenticationFeature(app, options);
            });
        }

        [Fact]
        public void Test_DefaultHandlerNameNotFound()
        {
            Assert.Throws<IndexOutOfRangeException>(() => new AgentApplication(new AgentApplicationOptions((IStorage) null) 
            { 
                Adapter = MockChannelAdapter.Object,
                UserAuthorization = new UserAuthorizationOptions(MockConnections.Object, MockGraph.Object) 
                { 
                    DefaultHandlerName = "notfound" 
                } 
            }));
        }

        [Fact]
        public void Test_DefaultHandlerFirst()
        {
            var app = new AgentApplication(new AgentApplicationOptions((IStorage) null)
            {
                Adapter = MockChannelAdapter.Object,
                UserAuthorization = new UserAuthorizationOptions(MockConnections.Object, MockGraph.Object)
            });

            Assert.Equal(GraphName, app.UserAuthorization.DefaultHandlerName);
        }

        [Fact]
        public async Task Test_AutoSignIn_Default()
        {
            // arrange
            var options = new TestApplicationOptions((IStorage)null) 
            { 
                Adapter = MockChannelAdapter.Object, 
                UserAuthorization = new UserAuthorizationOptions(MockConnections.Object, MockGraph.Object, MockSharePoint.Object) 
            };
            var app = new TestApplication(options);

            var turnContext = MockTurnContext();
            var turnState = await TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            // act
            var response = await app.UserAuthorization.StartOrContinueSignInUserAsync(turnContext, turnState);

            // assert
            Assert.True(response);
            Assert.NotNull(app.UserAuthorization.GetTurnTokenForCaller(turnContext, turnState, GraphName));
            Assert.Equal(GraphToken, await app.UserAuthorization.GetTurnTokenForCaller(turnContext, turnState, GraphName));
        }

        [Fact]
        public async Task Test_AutoSignIn_Named()
        {
            // arrange
            var options = new TestApplicationOptions((IStorage)null)
            {
                Adapter = MockChannelAdapter.Object,
                UserAuthorization = new UserAuthorizationOptions(MockConnections.Object, MockGraph.Object, MockSharePoint.Object)
            };
            var app = new TestApplication(options);

            var turnContext = MockTurnContext();
            var turnState = await TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            // act
            var signInComplete = await app.UserAuthorization.StartOrContinueSignInUserAsync(turnContext, turnState, SharePointName);

            // assert
            Assert.True(signInComplete);
            Assert.NotNull(app.UserAuthorization.GetTurnTokenForCaller(turnContext, turnState,  SharePointName));
            Assert.Equal(SharePointToken, await app.UserAuthorization.GetTurnTokenForCaller(turnContext, turnState,  SharePointName));
        }

        [Fact]
        public async Task Test_AutoSignIn_Pending()
        {
            MockGraph
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((TokenResponse)null));

            var options = new TestApplicationOptions((IStorage)null)
            {
                Adapter = MockChannelAdapter.Object,
                UserAuthorization = new UserAuthorizationOptions(MockConnections.Object, MockGraph.Object)
            };
            var app = new TestApplication(options);
            var turnContext = MockTurnContext();
            var turnState = await TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            // act
            var signInComplete = await app.UserAuthorization.StartOrContinueSignInUserAsync(turnContext, turnState);

            // assert
            Assert.False(signInComplete);
        }

        [Fact]
        public async Task Test_SignOut_DefaultHandler()
        {
            // arrange
            var options = new TestApplicationOptions((IStorage)null)
            {
                Adapter = MockChannelAdapter.Object,
                UserAuthorization = new UserAuthorizationOptions(MockConnections.Object, MockGraph.Object, MockSharePoint.Object)
            };
            var app = new TestApplication(options);
            var turnContext = MockTurnContext();
            var turnState = await TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            // act
            await app.UserAuthorization.StartOrContinueSignInUserAsync(turnContext, turnState);
            await app.UserAuthorization.StartOrContinueSignInUserAsync(turnContext, turnState, SharePointName);
            await app.UserAuthorization.SignOutUserAsync(turnContext, turnState);

            // assert
            Assert.Null(await app.UserAuthorization.GetTurnTokenForCaller(turnContext, turnState, GraphName));
            Assert.NotNull(await app.UserAuthorization.GetTurnTokenForCaller(turnContext, turnState,  SharePointName));
        }

        [Fact]
        public async Task Test_SignOut_SpecificHandler()
        {
            // arrange
            var options = new TestApplicationOptions((IStorage)null)
            {
                Adapter = MockChannelAdapter.Object,
                UserAuthorization = new UserAuthorizationOptions(MockConnections.Object, MockGraph.Object, MockSharePoint.Object)
            };
            var app = new TestApplication(options);
            var turnContext = MockTurnContext();
            var turnState = await TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            // act
            await app.UserAuthorization.StartOrContinueSignInUserAsync(turnContext, turnState);
            await app.UserAuthorization.StartOrContinueSignInUserAsync(turnContext, turnState, SharePointName);
            await app.UserAuthorization.SignOutUserAsync(turnContext, turnState, SharePointName);

            // assert
            Assert.Null(await app.UserAuthorization.GetTurnTokenForCaller(turnContext, turnState,  SharePointName));
            Assert.NotNull(await app.UserAuthorization.GetTurnTokenForCaller(turnContext, turnState, GraphName));
        }

        [Fact]
        public async Task Test_ManualSignInOnSuccessForCached()
        {
            // arrange
            var storage = new MemoryStorage();
            var adapter = new TestAdapter();

            // a IUserAuthorization that already has a token (user already signed in)
            var graphMock = new Mock<IUserAuthorization>();
            graphMock
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new TokenResponse() { Token = GraphToken, Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(5) }));
            graphMock
                .Setup(e => e.Name)
                .Returns(GraphName);

            // arrange
            var options = new TestApplicationOptions(storage)
            {
                Adapter = adapter,
                UserAuthorization = new UserAuthorizationOptions(MockConnections.Object, graphMock.Object)
                {
                    AutoSignIn = UserAuthorizationOptions.AutoSignInOff
                }
            };
            var app = new TestApplication(options);

            app.OnMessage("/signin", async (turnContext, turnState, cancellationToken) =>
            {
                await app.UserAuthorization.SignInUserAsync(turnContext, turnState, GraphName);
            });

            app.UserAuthorization.OnUserSignInSuccess(async (turnContext, turnState, handlerName, token, activity, CancellationToken) =>
            {
                await turnContext.SendActivityAsync($"sign in success for '{handlerName}' and you said '{activity.Text}'", cancellationToken: CancellationToken.None);
            });

            // act
            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await app.OnTurnAsync(turnContext, cancellationToken);
            })
            .Send("/signin")
            .AssertReply($"sign in success for '{GraphName}' and you said '/signin'")
            .StartTestAsync();

            // assert
            Assert.NotNull(await app.UserAuthorization.GetTurnTokenForCaller(null, null, GraphName));
        }

        [Fact]
        public async Task Test_ManualSignInOnSuccessForFlow()
        {
            // arrange
            var storage = new MemoryStorage();
            var adapter = new TestAdapter();

            // mock IUserAuthorization that returns null the first attempt, then a token after that.
            int attempt = 0;
            var graphMock = new Mock<IUserAuthorization>();
            graphMock
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    if (attempt++ == 0)
                    {
                        return Task.FromResult((TokenResponse)null);
                    }
                    return Task.FromResult(new TokenResponse() { Token = GraphToken, Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(5) });
                });
            graphMock
                .Setup(e => e.Name)
                .Returns(GraphName);

            // arrange
            var options = new TestApplicationOptions(storage)
            {
                Adapter = adapter,
                UserAuthorization = new UserAuthorizationOptions(MockConnections.Object, graphMock.Object)
                {
                    AutoSignIn = UserAuthorizationOptions.AutoSignInOff
                }
            };
            var app = new TestApplication(options);

            app.OnMessage("/signin", async (turnContext, turnState, cancellationToken) =>
            {
                await app.UserAuthorization.SignInUserAsync(turnContext, turnState, GraphName);
            }); 

            app.UserAuthorization.OnUserSignInSuccess(async (turnContext, turnState, handlerName, token, activity, CancellationToken) =>
            {
                await turnContext.SendActivityAsync($"sign in success for '{handlerName}' and you said '{activity.Text}'", cancellationToken: CancellationToken.None);
            });

            // act
            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await app.OnTurnAsync(turnContext, cancellationToken);
            })
            .Send("/signin")
            .Send("magic code")
            .AssertReply($"sign in success for '{GraphName}' and you said '/signin'")
            .StartTestAsync();

            // assert
            Assert.NotNull(await app.UserAuthorization.GetTurnTokenForCaller(null, null, GraphName));
        }

        [Fact]
        public async Task Test_AutoSignInForCached()
        {
            // arrange
            var storage = new MemoryStorage();
            var adapter = new TestAdapter();

            var graphMock = new Mock<IUserAuthorization>();
            graphMock
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new TokenResponse() { Token = GraphToken, Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(5) }));
            graphMock
                .Setup(e => e.Name)
                .Returns(GraphName);

            // arrange
            var options = new TestApplicationOptions(storage)
            {
                Adapter = adapter,
                UserAuthorization = new UserAuthorizationOptions(MockConnections.Object, graphMock.Object)
                {
                    AutoSignIn = UserAuthorizationOptions.AutoSignInOn
                }
            };
            var app = new TestApplication(options);

            app.OnActivity(ActivityTypes.Message, async (turnContext, turnState, cancellationToken) =>
            {
                await turnContext.SendActivityAsync(MessageFactory.Text($"You said: {turnContext.Activity.Text}"), cancellationToken);
            });

            // act
            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await app.OnTurnAsync(turnContext, cancellationToken);
            })
            .Send("first message")
            .AssertReply("You said: first message")
            .StartTestAsync();

            // assert
            Assert.NotNull(await app.UserAuthorization.GetTurnTokenForCaller(null, null, GraphName));
        }

        [Fact]
        public async Task Test_AutoSignInForFlow()
        {
            // arrange
            var storage = new MemoryStorage();
            var adapter = new TestAdapter();

            // mock IUserAuthorization that returns null the first attempt, then a token after that.
            int attempt = 0;
            var graphMock = new Mock<IUserAuthorization>();
            graphMock
                .Setup(e => e.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    if (attempt++ == 0)
                    {
                        return Task.FromResult((TokenResponse)null);
                    }
                    return Task.FromResult(new TokenResponse() { Token = GraphToken, Expiration = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(5) });
                });
            graphMock
                .Setup(e => e.Name)
                .Returns(GraphName);

            // arrange
            var options = new TestApplicationOptions(storage)
            {
                Adapter = adapter,
                UserAuthorization = new UserAuthorizationOptions(MockConnections.Object, graphMock.Object)
                {
                    AutoSignIn = UserAuthorizationOptions.AutoSignInOn
                }
            };
            var app = new TestApplication(options);

            app.OnActivity(ActivityTypes.Message, async (turnContext, turnState, cancellationToken) =>
            {
                await turnContext.SendActivityAsync(MessageFactory.Text($"You said: {turnContext.Activity.Text}"), cancellationToken);
            });

            // act
            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                await app.OnTurnAsync(turnContext, cancellationToken);
            })
            .Send("first message")
            .Send("magic code")
            .AssertReply("You said: first message")
            .StartTestAsync();

            // assert
            Assert.NotNull(await app.UserAuthorization.GetTurnTokenForCaller(null, null, GraphName));
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
