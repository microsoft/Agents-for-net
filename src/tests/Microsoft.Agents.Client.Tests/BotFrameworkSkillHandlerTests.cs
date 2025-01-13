// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.Client.Tests.Logger;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Agents.Client.Tests
{
    public class BotFrameworkSkillHandlerTests
    {
        private ILogger<BotFrameworkSkillHandlerTests> _logger = null;
        private static readonly string TestSkillId = Guid.NewGuid().ToString("N");
        private static readonly string TestAuthHeader = string.Empty; // Empty since claims extraction is being mocked
        private static readonly ChannelAccount TestMember = new ChannelAccount()
        {
            Id = "userId",
            Name = "userName"
        };


        public BotFrameworkSkillHandlerTests(ITestOutputHelper output)
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();



            ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
                    builder.AddSimpleConsole(options =>
                    {
                        options.IncludeScopes = true;
                        options.TimestampFormat = "hh:mm:ss ";
                    })
                    .AddConfiguration(config.GetSection("Logging"))
                    .AddProvider(new TraceConsoleLoggingProvider(output)));
            _logger = loggerFactory.CreateLogger<BotFrameworkSkillHandlerTests>();
        }


        [Theory]
        [InlineData(ActivityTypes.Message, null)]
        [InlineData(ActivityTypes.Message, "replyToId")]
        [InlineData(ActivityTypes.Event, null)]
        [InlineData(ActivityTypes.Event, "replyToId")]
        [InlineData(ActivityTypes.EndOfConversation, null)]
        [InlineData(ActivityTypes.EndOfConversation, "replyToId")]
        public async Task TestSendAndReplyToConversationAsync(string activityType, string replyToId)
        {
            // Arrange
            var mockObjects = new BotFrameworkSkillHandlerTestMocks(_logger);
            var activity = new Activity(activityType) { ReplyToId = replyToId };
            var conversationId = await mockObjects.CreateAndApplyConversationIdAsync(activity);

            // Act
            var sut = new BotFrameworkSkillHandler(mockObjects.Adapter.Object, mockObjects.Bot.Object, mockObjects.ConversationIdFactory);
            var response = replyToId == null ? await sut.OnSendToConversationAsync(mockObjects.CreateTestClaims(), conversationId, activity) : await sut.OnReplyToActivityAsync(mockObjects.CreateTestClaims(), conversationId, replyToId, activity);

            // Assert
            // Assert the turnContext.
            Assert.Equal($"{CallerIdConstants.BotToBotPrefix}{TestSkillId}", mockObjects.TurnContext.Activity.CallerId);
            Assert.NotNull(mockObjects.TurnContext.TurnState.Get<BotConversationReference>(BotFrameworkSkillHandler.SkillConversationReferenceKey));

            // Assert based on activity type,
            if (activityType == ActivityTypes.Message)
            {
                // Should be sent to the channel and not to the bot.
                Assert.NotNull(mockObjects.ChannelActivity);
                Assert.Null(mockObjects.BotActivity);

                // We should get the resourceId returned by the mock.
                Assert.Equal("resourceId", response.Id);

                // Assert the activity sent to the channel.
                Assert.Equal(activityType, mockObjects.ChannelActivity.Type);
                Assert.Null(mockObjects.ChannelActivity.CallerId);
                Assert.Equal(replyToId, mockObjects.ChannelActivity.ReplyToId);
            }
            else
            {
                // Should be sent to the bot and not to the channel.
                Assert.Null(mockObjects.ChannelActivity);
                Assert.NotNull(mockObjects.BotActivity);

                // If the activity is bounced back to the bot we will get a GUID and not the mocked resourceId.
                Assert.NotEqual("resourceId", response.Id);

                // Assert the activity sent back to the bot.
                Assert.Equal(activityType, mockObjects.BotActivity.Type);
                Assert.Equal(replyToId, mockObjects.BotActivity.ReplyToId);
            }
        }

        [Theory]
        [InlineData(ActivityTypes.Command, "application/myApplicationCommand", null)]
        [InlineData(ActivityTypes.Command, "application/myApplicationCommand", "replyToId")]
        [InlineData(ActivityTypes.Command, "other/myBotCommand", null)]
        [InlineData(ActivityTypes.Command, "other/myBotCommand", "replyToId")]
        [InlineData(ActivityTypes.CommandResult, "application/myApplicationCommandResult", null)]
        [InlineData(ActivityTypes.CommandResult, "application/myApplicationCommandResult", "replyToId")]
        [InlineData(ActivityTypes.CommandResult, "other/myBotCommand", null)]
        [InlineData(ActivityTypes.CommandResult, "other/myBotCommand", "replyToId")]
        public async Task TestCommandActivities(string commandActivityType, string name, string replyToId)
        {
            // Arrange
            var mockObjects = new BotFrameworkSkillHandlerTestMocks(_logger);
            var activity = new Activity(commandActivityType) { Name = name, ReplyToId = replyToId };
            var conversationId = await mockObjects.CreateAndApplyConversationIdAsync(activity);

            // Act
            var sut = new BotFrameworkSkillHandler(mockObjects.Adapter.Object, mockObjects.Bot.Object, mockObjects.ConversationIdFactory);
            var response = replyToId == null ? await sut.OnSendToConversationAsync(mockObjects.CreateTestClaims(), conversationId, activity) : await sut.OnReplyToActivityAsync(mockObjects.CreateTestClaims(), conversationId, replyToId, activity);

            // Assert
            // Assert the turnContext.
            Assert.Equal($"{CallerIdConstants.BotToBotPrefix}{TestSkillId}", mockObjects.TurnContext.Activity.CallerId);
            Assert.NotNull(mockObjects.TurnContext.TurnState.Get<BotConversationReference>(BotFrameworkSkillHandler.SkillConversationReferenceKey));
            if (name.StartsWith("application/"))
            {
                // Should be sent to the channel and not to the bot.
                Assert.NotNull(mockObjects.ChannelActivity);
                Assert.Null(mockObjects.BotActivity);

                // We should get the resourceId returned by the mock.
                Assert.Equal("resourceId", response.Id);
            }
            else
            {
                // Should be sent to the bot and not to the channel.
                Assert.Null(mockObjects.ChannelActivity);
                Assert.NotNull(mockObjects.BotActivity);

                // If the activity is bounced back to the bot we will get a GUID and not the mocked resourceId.
                Assert.NotEqual("resourceId", response.Id);
            }
        }

        [Fact]
        public async Task TestDeleteActivityAsync()
        {
            // Arrange
            var mockObjects = new BotFrameworkSkillHandlerTestMocks(_logger);
            var activity = new Activity(ActivityTypes.Message);
            var conversationId = await mockObjects.CreateAndApplyConversationIdAsync(activity);
            var activityToDelete = Guid.NewGuid().ToString();

            // Act
            var sut = new BotFrameworkSkillHandler(mockObjects.Adapter.Object, mockObjects.Bot.Object, mockObjects.ConversationIdFactory);
            await sut.OnDeleteActivityAsync(mockObjects.CreateTestClaims(), conversationId, activityToDelete);

            // Assert
            Assert.NotNull(mockObjects.TurnContext.TurnState.Get<BotConversationReference>(BotFrameworkSkillHandler.SkillConversationReferenceKey));
            Assert.Equal(activityToDelete, mockObjects.ActivityIdToDelete);
        }

        [Fact]
        public async Task TestUpdateActivityAsync()
        {
            // Arrange
            var mockObjects = new BotFrameworkSkillHandlerTestMocks(_logger);
            var activity = new Activity(ActivityTypes.Message) { Text = $"TestUpdate {DateTime.Now}." };
            var conversationId = await mockObjects.CreateAndApplyConversationIdAsync(activity);
            var activityToUpdate = Guid.NewGuid().ToString();

            // Act
            var sut = new BotFrameworkSkillHandler(mockObjects.Adapter.Object, mockObjects.Bot.Object, mockObjects.ConversationIdFactory);
            var response = await sut.OnUpdateActivityAsync(mockObjects.CreateTestClaims(), conversationId, activityToUpdate, activity);

            // Assert
            Assert.Equal("resourceId", response.Id);
            Assert.NotNull(mockObjects.TurnContext.TurnState.Get<BotConversationReference>(BotFrameworkSkillHandler.SkillConversationReferenceKey));
            Assert.Equal(activityToUpdate, mockObjects.TurnContext.Activity.Id);
            Assert.Equal(activity.Text, mockObjects.UpdateActivity.Text);
        }

        [Fact]
        public async Task TestGetConversationMemberAsync()
        {
            // Arrange
            var mockObjects = new BotFrameworkSkillHandlerTestMocks(_logger);            
            var activity = new Activity(ActivityTypes.Message) { Text = $"Get Member." };
            var conversationId = await mockObjects.CreateAndApplyConversationIdAsync(activity);

            // Act
            var sut = new BotFrameworkSkillHandler(mockObjects.Adapter.Object, mockObjects.Bot.Object, mockObjects.ConversationIdFactory);
            var member = await sut.OnGetConversationMemberAsync(mockObjects.CreateTestClaims(), TestMember.Id, conversationId);

            // Assert
            Assert.NotNull(member);
            Assert.Equal(TestMember.Id, member.Id);
            Assert.Equal(TestMember.Name, member.Name);
        }

        /// <summary>
        /// Helper class with mocks for adapter, bot and auth needed to instantiate BotFrameworkSkillHandler and run tests.
        /// This class also captures the turnContext and activities sent back to the bot and the channel so we can run asserts on them.
        /// </summary>
        private class BotFrameworkSkillHandlerTestMocks
        {
            private static readonly string TestBotId = Guid.NewGuid().ToString("N");
            private static readonly string TestBotEndpoint = "http://testbot.com/api/messages";

            private static readonly string TestSkillEndpoint = "http://testskill.com/api/messages";

            public BotFrameworkSkillHandlerTestMocks(ILogger logger)
            {
                Adapter = CreateMockAdapter(logger);
                Bot = CreateMockBot();
                ConversationIdFactory = new TestSkillConversationIdFactory();
                Client = CreateMockConnectorClient(); 
            }

            public IConversationIdFactory ConversationIdFactory { get; }

            public Mock<ChannelAdapter> Adapter { get; }

            public Mock<IChannelServiceClientFactory> Auth { get;  }

            public Mock<IBot> Bot { get;  }

            public IConnectorClient Client { get; }

            // Gets the TurnContext created to call the bot.
            public TurnContext TurnContext { get; private set; }
            
            /// <summary>
            /// Gets the Activity sent to the channel.
            /// </summary>
            public IActivity ChannelActivity { get; private set; }

            /// <summary>
            /// Gets the Activity sent to the Bot.
            /// </summary>
            public IActivity BotActivity { get; private set; }

            /// <summary>
            /// Gets the update activity.
            /// </summary>
            public IActivity UpdateActivity { get; private set; }

            /// <summary>
            /// Gets the Activity sent to the Bot.
            /// </summary>
            public string ActivityIdToDelete { get; private set; }

            public async Task<string> CreateAndApplyConversationIdAsync(Activity activity)
            {
                activity.ApplyConversationReference(new ConversationReference
                {
                    Conversation = new ConversationAccount(id: TestBotId),
                    ServiceUrl = TestBotEndpoint
                });

                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string>
                    {
                        {"Settings:Endpoint", TestSkillEndpoint},
                        {"Settings:ClientId", TestSkillId}
                    })
                    .Build();

                var skill = new BotFrameworkSkill
                {
                    Alias = "skill",
                    ConnectionSettings = configuration.GetValue<IDictionary<string, string>>("Settings")
                };

                var options = new ConversationIdFactoryOptions
                {
                    FromBotOAuthScope = TestBotId,
                    FromBotId = TestBotId,
                    Activity = activity,
                    ToBotName = "skill"
                };

                return await ConversationIdFactory.CreateConversationIdAsync(options, CancellationToken.None);
            }
            public ClaimsIdentity CreateTestClaims()
            {
                var claimsIdentity = new ClaimsIdentity();

                claimsIdentity.AddClaim(new Claim(AuthenticationConstants.AudienceClaim, TestBotId));
                claimsIdentity.AddClaim(new Claim(AuthenticationConstants.AppIdClaim, TestSkillId));
                claimsIdentity.AddClaim(new Claim(AuthenticationConstants.ServiceUrlClaim, TestBotEndpoint));

                return claimsIdentity;
            }

            private Mock<ChannelAdapter> CreateMockAdapter(ILogger logger)
            {
                var adapter = new Mock<ChannelAdapter>(logger);

                // Mock the adapter ContinueConversationAsync method
                // This code block catches and executes the custom bot callback created by the service handler.
                adapter.Setup(a => a.ContinueConversationAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<ConversationReference>(), It.IsAny<string>(), It.IsAny<BotCallbackHandler>(), It.IsAny<CancellationToken>()))
                    .Callback<ClaimsIdentity, ConversationReference, string, BotCallbackHandler, CancellationToken>(async (token, conv, audience, botCallbackHandler, cancel) =>
                    {
                        // Create and capture the TurnContext so we can run assertions on it.
                        TurnContext = new TurnContext(adapter.Object, conv.GetContinuationActivity());
                        TurnContext.TurnState.Add<IConnectorClient>(Client);

                        await botCallbackHandler(TurnContext, cancel);
                    });

                // Mock the adapter SendActivitiesAsync method (this for the cases where activity is sent back to the parent or channel)
                adapter.Setup(a => a.SendActivitiesAsync(It.IsAny<ITurnContext>(), It.IsAny<IActivity[]>(), It.IsAny<CancellationToken>()))
                    .Callback<ITurnContext, IActivity[], CancellationToken>((turn, activities, cancel) =>
                    {
                        // Capture the activity sent to the channel
                        ChannelActivity = activities[0];

                        // Do nothing, we don't want the activities sent to the channel in the tests.
                    })
                    .Returns(Task.FromResult(new[]
                    {
                        // Return a well known resourceId so we can assert we capture the right return value.
                        new ResourceResponse("resourceId")
                    }));

                // Mock the DeleteActivityAsync method
                adapter.Setup(a => a.DeleteActivityAsync(It.IsAny<ITurnContext>(), It.IsAny<ConversationReference>(), It.IsAny<CancellationToken>()))
                    .Callback<ITurnContext, ConversationReference, CancellationToken>((turn, conv, cancel) =>
                    {
                        // Capture the activity id to delete so we can assert it. 
                        ActivityIdToDelete = conv.ActivityId;
                    });

                // Mock the UpdateActivityAsync method
                adapter.Setup(a => a.UpdateActivityAsync(It.IsAny<ITurnContext>(), It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                    .Callback<ITurnContext, IActivity, CancellationToken>((turn, newActivity, cancel) =>
                    {
                        // Capture the activity to update.
                        UpdateActivity = newActivity;
                    })
                    .Returns(Task.FromResult(new ResourceResponse("resourceId")));

                return adapter;
            }

            private Mock<IBot> CreateMockBot()
            {
                var bot = new Mock<IBot>();
                bot.Setup(b => b.OnTurnAsync(It.IsAny<ITurnContext>(), It.IsAny<CancellationToken>()))
                    .Callback<ITurnContext, CancellationToken>((turnContext, ct) =>
                    {
                        BotActivity = turnContext.Activity;
                    });
                return bot;
            }

            private IConnectorClient CreateMockConnectorClient()
            {
                var httpResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(ProtocolJsonSerializer.ToJson(TestMember))
                };

                Func<HttpRequestMessage, HttpResponseMessage> sendRequest = request =>
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK);
                    response.Content = new StringContent(ProtocolJsonSerializer.ToJson(TestMember), Encoding.UTF8, "application/json");
                    return response;
                };

                var httpClient = new HttpClient(new MockClientHandler(sendRequest));


                var httpFactory = new Mock<IHttpClientFactory>();
                httpFactory.Setup(a => a.CreateClient(It.IsAny<string>()))
                    .Returns(httpClient);

                var client = new RestConnectorClient(new Uri("http://testbot/api/messages"), httpFactory.Object, () => Task.FromResult<string>("test"));

                return client;
            }
        }

        private class MockClientHandler : HttpClientHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _send;

            public MockClientHandler(Func<HttpRequestMessage, HttpResponseMessage> send)
            {
                _send = send;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_send(request));
            }
        }

        private class BotFrameworkSkill : IChannelInfo
        {
            public string Alias { get; set; }
            public string DisplayName { get; set; }
            public string ChannelFactory {  get; set; }
            public IDictionary<string, string> ConnectionSettings { get; set; }
        }

        private class TestSkillConversationIdFactory : IConversationIdFactory
        {
            private readonly ConcurrentDictionary<string, string> _conversationRefs = new ConcurrentDictionary<string, string>();

            public Task<string> CreateConversationIdAsync(ConversationIdFactoryOptions options, CancellationToken cancellationToken)
            {
                var BotConversationReference = new BotConversationReference
                {
                    ConversationReference = options.Activity.GetConversationReference(),
                    OAuthScope = options.FromBotOAuthScope
                };
                var key = $"{options.FromBotId}-{options.ToBotName}-{BotConversationReference.ConversationReference.Conversation.Id}-{BotConversationReference.ConversationReference.ChannelId}-skillconvo";
                _conversationRefs.GetOrAdd(key, ProtocolJsonSerializer.ToJson(BotConversationReference));
                return Task.FromResult(key);
            }

            public Task<BotConversationReference> GetBotConversationReferenceAsync(string skillConversationId, CancellationToken cancellationToken)
            {
                var conversationReference = ProtocolJsonSerializer.ToObject<BotConversationReference>(_conversationRefs[skillConversationId]);
                return Task.FromResult(conversationReference);
            }

            public Task<string> GetBotConversationIdAsync(ConversationIdFactoryOptions options, CancellationToken cancellationToken)
            {
                return null;
            }

            public Task DeleteConversationReferenceAsync(string skillConversationId, CancellationToken cancellationToken)
            {
                _conversationRefs.TryRemove(skillConversationId, out _);
                return Task.CompletedTask;
            }

            public Task<string> CreateConversationIdAsync(ITurnContext turnContext, string hostAppId, string botAlias, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<string> GetBotConversationIdAsync(ITurnContext turnContext, string hostAppId, string botAlias, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }
    }
}
