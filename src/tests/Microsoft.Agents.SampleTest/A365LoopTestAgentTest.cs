// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A365LoopTest;
using Microsoft.Agents.A365.Notifications.Models;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.SampleTest
{
    /// <summary>
    /// Tests for the <c>A365LoopTest.MyAgent</c> sample, which handles A365 notification
    /// activities (email, Word/Excel/PowerPoint comments, and lifecycle events).
    ///
    /// <para>
    /// The agent requires agentic user authentication. These tests mock
    /// <see cref="IConnections"/> and <see cref="IUserAuthorization"/> so that
    /// <c>UserAuthorization.GetTurnTokenAsync</c> returns a simulated token without
    /// real Azure AD infrastructure.
    /// </para>
    /// </summary>
    public class A365LoopTestAgentTest
    {
        private const string FakeToken = "fake-agentic-token-for-testing";
        private const string AgenticHandlerName = "agentic";

        /// <summary>
        /// Creates a mock <see cref="IUserAuthorization"/> named "agentic" that immediately
        /// returns a fake token on sign-in (no multi-turn OAuth flow).
        /// </summary>
        private static IUserAuthorization CreateMockAgenticHandler()
        {
            var mock = new Mock<IUserAuthorization>();
            mock.Setup(h => h.Name).Returns(AgenticHandlerName);
            mock.Setup(h => h.SignInUserAsync(
                    It.IsAny<ITurnContext>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenResponse(token: FakeToken));
            mock.Setup(h => h.GetRefreshedUserTokenAsync(
                    It.IsAny<ITurnContext>(),
                    It.IsAny<string>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenResponse(token: FakeToken));
            mock.Setup(h => h.SignOutUserAsync(It.IsAny<ITurnContext>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            mock.Setup(h => h.ResetStateAsync(It.IsAny<ITurnContext>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            return mock.Object;
        }

        /// <summary>
        /// Creates a mock <see cref="IConnections"/> that returns a mock
        /// <see cref="IAccessTokenProvider"/> implementing <see cref="IAgenticTokenProvider"/>.
        /// </summary>
        private static IConnections CreateMockConnections()
        {
            var mockTokenProvider = new Mock<IAccessTokenProvider>();

            // Also implement IAgenticTokenProvider so AgenticUserAuthorization can cast.
            var mockAgenticProvider = mockTokenProvider.As<IAgenticTokenProvider>();
            mockAgenticProvider.Setup(p => p.GetAgenticUserTokenAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(FakeToken);
            mockAgenticProvider.Setup(p => p.GetAgenticInstanceTokenAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(FakeToken);

            var mockConnections = new Mock<IConnections>();
            mockConnections.Setup(c => c.GetConnection(It.IsAny<string>()))
                .Returns(mockTokenProvider.Object);
            mockConnections.Setup(c => c.TryGetConnection(It.IsAny<string>(), out It.Ref<IAccessTokenProvider>.IsAny))
                .Returns((string _, out IAccessTokenProvider provider) =>
                {
                    provider = mockTokenProvider.Object;
                    return true;
                });
            mockConnections.Setup(c => c.GetDefaultConnection())
                .Returns(mockTokenProvider.Object);
            mockConnections.Setup(c => c.GetTokenProvider(It.IsAny<ClaimsIdentity>(), It.IsAny<string>()))
                .Returns(mockTokenProvider.Object);
            mockConnections.Setup(c => c.GetTokenProvider(It.IsAny<ClaimsIdentity>(), It.IsAny<IActivity>()))
                .Returns(mockTokenProvider.Object);

            return mockConnections.Object;
        }

        /// <summary>
        /// Creates the <see cref="AgentTestHost"/> with the real <c>A365LoopTest.MyAgent</c>,
        /// configured with mocked authentication so handlers can call GetTurnTokenAsync.
        /// </summary>
        private static AgentTestHost CreateHost()
        {
            var connections = CreateMockConnections();
            var agenticHandler = CreateMockAgenticHandler();

            var host = AgentTestHost.Create(builder =>
            {
                builder.Services.AddSingleton<IStorage, MemoryStorage>();
                builder.Services.AddSingleton<IConnections>(connections);
                builder.Services.AddTransient<IAgent>(sp =>
                {
                    var storage = sp.GetRequiredService<IStorage>();
                    var options = new AgentApplicationOptions(storage)
                    {
                        UserAuthorization = new UserAuthorizationOptions(
                            NullLoggerFactory.Instance,
                            storage,
                            connections,
                            agenticHandler)
                        {
                            DefaultHandlerName = AgenticHandlerName,
                            AutoSignIn = UserAuthorizationOptions.AutoSignInOnForAny
                        }
                    };
                    return new MyAgent(options, connections);
                });
            });

            // TestAdapter overwrites activity.Recipient with Conversation.Agent on ProcessActivityAsync.
            // A365 notification routes require Recipient.Role = "agenticUser" (isAgenticOnly guard).
            host.Adapter.Conversation.Agent = new ChannelAccount
            {
                Id = "bot",
                Name = "Bot",
                Role = RoleTypes.AgenticUser
            };

            return host;
        }

        /// <summary>
        /// Creates an event activity that simulates an A365 email notification.
        /// Requires:
        ///   - ChannelId with Channel="agents" and SubChannel="email"
        ///   - An entity of type "emailNotification" for NotificationType detection
        /// Note: TestAdapter overwrites Recipient/Conversation/ServiceUrl from Conversation ref.
        /// </summary>
        private static Activity CreateEmailNotificationActivity(TestAdapter adapter)
        {
            return new Activity
            {
                Type = ActivityTypes.Event,
                ChannelId = new ChannelId("agents:email"),
                From = adapter.Conversation.User,
                Entities = new List<Entity>
                {
                    new Entity { Type = EmailReference.EntityTypeName }
                }
            };
        }

        /// <summary>
        /// Creates an event activity that simulates an A365 WpxComment notification
        /// from a specific sub-channel (word, excel, or powerpoint).
        /// </summary>
        private static Activity CreateWpxCommentActivity(TestAdapter adapter, string subChannel)
        {
            return new Activity
            {
                Type = ActivityTypes.Event,
                ChannelId = new ChannelId($"agents:{subChannel}"),
                From = adapter.Conversation.User,
                Entities = new List<Entity>
                {
                    new Entity { Type = "wpxComment" }
                }
            };
        }

        /// <summary>
        /// Creates an event activity that simulates an A365 lifecycle notification.
        /// Requires:
        ///   - Type="event", Name="agentLifecycle"
        ///   - ChannelId.Channel="agents"
        /// </summary>
        private static Activity CreateLifecycleActivity(TestAdapter adapter)
        {
            return new Activity
            {
                Type = ActivityTypes.Event,
                Name = "agentLifecycle",
                ChannelId = new ChannelId("agents"),
                From = adapter.Conversation.User,
                ValueType = "agenticUserIdentityCreated"
            };
        }

        [Fact]
        public async Task A365LoopTest_EmailNotification_SendsEmailResponse()
        {
            await using var host = CreateHost();

            var emailActivity = CreateEmailNotificationActivity(host.Adapter);

            await host.CreateTestFlow()
                .Send(emailActivity)
                .AssertReplySatisfies(reply =>
                {
                    Assert.NotNull(reply.Entities);
                    Assert.Contains(reply.Entities, e =>
                        string.Equals(e.Type, "emailResponse", StringComparison.OrdinalIgnoreCase)
                        || e.GetType().Name == "EmailResponse");
                    return Task.CompletedTask;
                })
                .AssertNoMoreReplies()
                .StartTestAsync();
        }

        [Fact]
        public async Task A365LoopTest_WordWpxComment_SendsCommentReply()
        {
            await using var host = CreateHost();

            var wordActivity = CreateWpxCommentActivity(host.Adapter, "word");

            await host.CreateTestFlow()
                .Send(wordActivity)
                .AssertReplyContains("Comment Received By loop Test tool")
                .AssertNoMoreReplies()
                .StartTestAsync();
        }

        [Fact]
        public async Task A365LoopTest_ExcelWpxComment_SendsCommentReply()
        {
            await using var host = CreateHost();

            var excelActivity = CreateWpxCommentActivity(host.Adapter, "excel");

            await host.CreateTestFlow()
                .Send(excelActivity)
                .AssertReplyContains("Comment Received By loop Test tool")
                .AssertNoMoreReplies()
                .StartTestAsync();
        }

        [Fact]
        public async Task A365LoopTest_PowerPointWpxComment_SendsCommentReply()
        {
            await using var host = CreateHost();

            var pptActivity = CreateWpxCommentActivity(host.Adapter, "powerpoint");

            await host.CreateTestFlow()
                .Send(pptActivity)
                .AssertReplyContains("Comment Received By loop Test tool")
                .AssertNoMoreReplies()
                .StartTestAsync();
        }

        [Fact]
        public async Task A365LoopTest_LifecycleNotification_NoReply()
        {
            await using var host = CreateHost();

            var lifecycleActivity = CreateLifecycleActivity(host.Adapter);

            await host.CreateTestFlow()
                .Send(lifecycleActivity)
                .AssertNoMoreReplies()
                .StartTestAsync();
        }
    }
}
