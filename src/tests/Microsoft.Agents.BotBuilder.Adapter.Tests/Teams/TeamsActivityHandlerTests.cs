// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using Microsoft.Bot.Builder.Tests;
using Microsoft.Agents.Core.Adapter.Tests;
using Microsoft.Agents.Core.Adapter.Tests.Teams;
using Microsoft.Agents.Core.Models;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Agents.Core.Teams.Models;
using Microsoft.Agents.BotBuilder;

namespace Microsoft.Bot.Builder.Teams.Tests
{
    public class TeamsActivityHandlerTests
    {
        IActivity[] _activitiesToSend = null;

        public TeamsActivityHandlerTests()
        {
            // called between each test, and resets state to prevent leakage. 
            _activitiesToSend = null;
        }
        void CaptureSend(IActivity[] arg)
        {
            _activitiesToSend = arg;
        }

        [Fact]
        public async Task TestConversationUpdateBotTeamsMemberAdded()
        {
            // Arrange            
            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                MembersAdded =
                [
                    new ChannelAccount { Id = "bot" },
                ],
                Recipient = new ChannelAccount { Id = "bot" },
                ChannelData = new TeamsChannelData
                {
                    EventType = "teamMemberAdded",
                    Team = new TeamInfo
                    {
                        Id = "team-id",
                    },
                },
                ChannelId = Channels.Msteams,
            };

            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnConversationUpdateActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMembersAddedAsync", bot.Record[1]);
        }

        //[Fact]
        //public async Task TestConversationUpdateTeamsMemberAdded()
        //{
        //    // Arrange
        //    var baseUri = new Uri("https://test.coffee");
        //    var customHttpClient = new HttpClient(new RosterHttpMessageHandler());

        //    // Set a special base address so then we can make sure the connector client is honoring this http client
        //    customHttpClient.BaseAddress = baseUri;
        //    var connectorClient = new ConnectorClient(new Uri("http://localhost/"), new MicrosoftAppCredentials(string.Empty, string.Empty), customHttpClient);

        //    var activity = new Activity
        //    {
        //        Type = ActivityTypes.ConversationUpdate,
        //        MembersAdded = new List<ChannelAccount>
        //        {
        //            new ChannelAccount { Id = "id-1" },
        //        },
        //        Recipient = new ChannelAccount { Id = "b" },
        //        ChannelData = new TeamsChannelData
        //        {
        //            EventType = "teamMemberAdded",
        //            Team = new TeamInfo
        //            {
        //                Id = "team-id",
        //            },
        //        },
        //        ChannelId = Channels.Msteams,
        //    };

        //    var turnContext = new TurnContext(new SimpleAdapter(), activity);
        //    turnContext.TurnState.Add<IConnectorClient>(connectorClient);

        //    // Act
        //    var bot = new TestActivityHandler();
        //    await ((IBot)bot).OnTurnAsync(turnContext);

        //    // Assert
        //    Assert.Equal(2, bot.Record.Count);
        //    Assert.Equal("OnConversationUpdateActivityAsync", bot.Record[0]);
        //    Assert.Equal("OnTeamsMembersAddedAsync", bot.Record[1]);
        //}

        //[Fact]
        //public async Task TestConversationUpdateTeamsMemberAddedNoTeam()
        //{
        //    // Arrange
        //    var baseUri = new Uri("https://test.coffee");
        //    var customHttpClient = new HttpClient(new RosterHttpMessageHandler());

        //    // Set a special base address so then we can make sure the connector client is honoring this http client
        //    customHttpClient.BaseAddress = baseUri;
        //    var connectorClient = new ConnectorClient(new Uri("http://localhost/"), new MicrosoftAppCredentials(string.Empty, string.Empty), customHttpClient);

        //    var activity = new Activity
        //    {
        //        Type = ActivityTypes.ConversationUpdate,
        //        MembersAdded = new List<ChannelAccount>
        //        {
        //            new ChannelAccount { Id = "id-1" },
        //        },
        //        Recipient = new ChannelAccount { Id = "b" },
        //        Conversation = new ConversationAccount { Id = "conversation-id" },
        //        ChannelId = Channels.Msteams,
        //    };

        //    var turnContext = new TurnContext(new SimpleAdapter(), activity);
        //    turnContext.TurnState.Add<IConnectorClient>(connectorClient);

        //    // Act
        //    var bot = new TestActivityHandler();
        //    await ((IBot)bot).OnTurnAsync(turnContext);

        //    // Assert
        //    Assert.Equal(2, bot.Record.Count);
        //    Assert.Equal("OnConversationUpdateActivityAsync", bot.Record[0]);
        //    Assert.Equal("OnTeamsMembersAddedAsync", bot.Record[1]);
        //}

        //[Fact]
        //public async Task TestConversationUpdateTeamsMemberAddedFullDetailsInEvent()
        //{
        //    // Arrange
        //    var baseUri = new Uri("https://test.coffee");
        //    var customHttpClient = new HttpClient(new RosterHttpMessageHandler());

        //    // Set a special base address so then we can make sure the connector client is honoring this http client
        //    customHttpClient.BaseAddress = baseUri;
        //    var connectorClient = new ConnectorClient(new Uri("http://localhost/"), new MicrosoftAppCredentials(string.Empty, string.Empty), customHttpClient);

        //    var activity = new Activity
        //    {
        //        Type = ActivityTypes.ConversationUpdate,
        //        MembersAdded = new List<ChannelAccount>
        //        {
        //            new TeamsChannelAccount
        //            {
        //                Id = "id-1",
        //                Name = "name-1",
        //                AadObjectId = "aadobject-1",
        //                Email = "test@microsoft.com",
        //                GivenName = "given-1",
        //                Surname = "surname-1",
        //                UserPrincipalName = "t@microsoft.com",
        //            },
        //        },
        //        Recipient = new ChannelAccount { Id = "b" },
        //        ChannelData = new TeamsChannelData
        //        {
        //            EventType = "teamMemberAdded",
        //            Team = new TeamInfo
        //            {
        //                Id = "team-id",
        //            },
        //        },
        //        ChannelId = Channels.Msteams,
        //    };

        //    // code taken from connector - i.e. the send or serialize side
        //    var serializationSettings = new JsonSerializerSettings();
        //    serializationSettings.ContractResolver = new DefaultContractResolver();
        //    var json = Rest.Serialization.SafeJsonConvert.SerializeObject(activity, serializationSettings);

        //    // code taken from integration layer - i.e. the receive or deserialize side
        //    var botMessageSerializer = JsonSerializer.Create(new JsonSerializerSettings
        //    {
        //        NullValueHandling = NullValueHandling.Ignore,
        //        Formatting = Formatting.Indented,
        //        DateFormatHandling = DateFormatHandling.IsoDateFormat,
        //        DateTimeZoneHandling = DateTimeZoneHandling.Utc,
        //        ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
        //        ContractResolver = new ReadOnlyJsonContractResolver(),
        //        Converters = new List<JsonConverter> { new Iso8601TimeSpanConverter() },
        //    });

        //    using (var bodyReader = new JsonTextReader(new StringReader(json)))
        //    {
        //        activity = botMessageSerializer.Deserialize<Activity>(bodyReader);
        //    }

        //    var turnContext = new TurnContext(new SimpleAdapter(), activity);
        //    turnContext.TurnState.Add<IConnectorClient>(connectorClient);

        //    // Act
        //    var bot = new TestActivityHandler();
        //    await ((IBot)bot).OnTurnAsync(turnContext);

        //    // Assert
        //    Assert.Equal(2, bot.Record.Count);
        //    Assert.Equal("OnConversationUpdateActivityAsync", bot.Record[0]);
        //    Assert.Equal("OnTeamsMembersAddedAsync", bot.Record[1]);
        //}

        [Fact]
        public async Task TestConversationUpdateTeamsMemberRemoved()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                MembersRemoved =
                [
                    new ChannelAccount { Id = "a" },
                ],
                Recipient = new ChannelAccount { Id = "b" },
                ChannelData = new TeamsChannelData { EventType = "teamMemberRemoved" },
                ChannelId = Channels.Msteams,
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnConversationUpdateActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMembersRemovedAsync", bot.Record[1]);
        }

        [Fact]
        public async Task TestConversationUpdateTeamsChannelCreated()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelData = new TeamsChannelData { EventType = "channelCreated" },
                ChannelId = Channels.Msteams,
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnConversationUpdateActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsChannelCreatedAsync", bot.Record[1]);
        }

        [Fact]
        public async Task TestConversationUpdateTeamsChannelDeleted()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelData = new TeamsChannelData { EventType = "channelDeleted" },
                ChannelId = Channels.Msteams,
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnConversationUpdateActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsChannelDeletedAsync", bot.Record[1]);
        }

        [Fact]
        public async Task TestConversationUpdateTeamsChannelRenamed()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelData = new TeamsChannelData { EventType = "channelRenamed" },
                ChannelId = Channels.Msteams,
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnConversationUpdateActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsChannelRenamedAsync", bot.Record[1]);
        }

        [Fact]
        public async Task TestConversationUpdateTeamsChannelRestored()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelData = new TeamsChannelData { EventType = "channelRestored" },
                ChannelId = Channels.Msteams,
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnConversationUpdateActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsChannelRestoredAsync", bot.Record[1]);
        }

        [Fact]
        public async Task TestConversationUpdateTeamsTeamArchived()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelData = new TeamsChannelData { EventType = "teamArchived" },
                ChannelId = Channels.Msteams,
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnConversationUpdateActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsTeamArchivedAsync", bot.Record[1]);
        }

        [Fact]
        public async Task TestConversationUpdateTeamsTeamDeleted()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelData = new TeamsChannelData { EventType = "teamDeleted" },
                ChannelId = Channels.Msteams,
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnConversationUpdateActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsTeamDeletedAsync", bot.Record[1]);
        }

        [Fact]
        public async Task TestConversationUpdateTeamsTeamHardDeleted()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelData = new TeamsChannelData { EventType = "teamHardDeleted" },
                ChannelId = Channels.Msteams,
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnConversationUpdateActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsTeamHardDeletedAsync", bot.Record[1]);
        }

        [Fact]
        public async Task TestConversationUpdateTeamsTeamRenamed()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelData = new TeamsChannelData { EventType = "teamRenamed" },
                ChannelId = Channels.Msteams,
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnConversationUpdateActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsTeamRenamedAsync", bot.Record[1]);
        }

        [Fact]
        public async Task TestConversationUpdateTeamsTeamRestored()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelData = new TeamsChannelData { EventType = "teamRestored" },
                ChannelId = Channels.Msteams,
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnConversationUpdateActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsTeamRestoredAsync", bot.Record[1]);
        }

        [Fact]
        public async Task TestConversationUpdateTeamsTeamUnarchived()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelData = new TeamsChannelData { EventType = "teamUnarchived" },
                ChannelId = Channels.Msteams,
            };
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnConversationUpdateActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsTeamUnarchivedAsync", bot.Record[1]);
        }

        [Fact]
        public async Task TestFileConsentAccept()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "fileConsent/invoke",
                Value = JsonSerializer.SerializeToElement(new FileConsentCardResponse
                {
                    Action = "accept",
                    UploadInfo = new FileUploadInfo
                    {
                        UniqueId = "uniqueId",
                        FileType = "fileType",
                        UploadUrl = "uploadUrl",
                    },
                }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(3, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsFileConsentAsync", bot.Record[1]);
            Assert.Equal("OnTeamsFileConsentAcceptAsync", bot.Record[2]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestFileConsentDecline()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "fileConsent/invoke",
                Value = JsonSerializer.SerializeToElement(new FileConsentCardResponse
                {
                    Action = "decline",
                    UploadInfo = new FileUploadInfo
                    {
                        UniqueId = "uniqueId",
                        FileType = "fileType",
                        UploadUrl = "uploadUrl",
                    },
                }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(3, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsFileConsentAsync", bot.Record[1]);
            Assert.Equal("OnTeamsFileConsentDeclineAsync", bot.Record[2]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestActionableMessageExecuteAction()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "actionableMessage/executeAction",
                Value = JsonSerializer.SerializeToElement(new O365ConnectorCardActionQuery()),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsO365ConnectorCardActionAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestComposeExtensionQueryLink()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/queryLink",
                Value = JsonSerializer.SerializeToElement(new AppBasedLinkQuery()),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsAppBasedLinkQueryAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestComposeExtensionAnonymousQueryLink()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/anonymousQueryLink",
                Value = JsonSerializer.SerializeToElement(new AppBasedLinkQuery()),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsAnonymousAppBasedLinkQueryAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestComposeExtensionQuery()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/query",
                Value = JsonSerializer.SerializeToElement(new MessagingExtensionQuery()),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await ((IBot)bot).OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMessagingExtensionQueryAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestMessagingExtensionSelectItemAsync()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/selectItem",
                Value = new JsonElement(),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMessagingExtensionSelectItemAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestMessagingExtensionSubmitAction()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/submitAction",
                Value = JsonSerializer.SerializeToElement(new MessagingExtensionQuery()),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(3, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMessagingExtensionSubmitActionDispatchAsync", bot.Record[1]);
            Assert.Equal("OnTeamsMessagingExtensionSubmitActionAsync", bot.Record[2]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestMessagingExtensionSubmitActionPreviewActionEdit()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/submitAction",
                Value = JsonSerializer.SerializeToElement(new MessagingExtensionAction
                {
                    BotMessagePreviewAction = "edit",
                }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(3, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMessagingExtensionSubmitActionDispatchAsync", bot.Record[1]);
            Assert.Equal("OnTeamsMessagingExtensionBotMessagePreviewEditAsync", bot.Record[2]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestMessagingExtensionSubmitActionPreviewActionSend()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/submitAction",
                Value = JsonSerializer.SerializeToElement(new MessagingExtensionAction
                {
                    BotMessagePreviewAction = "send",
                }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(3, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMessagingExtensionSubmitActionDispatchAsync", bot.Record[1]);
            Assert.Equal("OnTeamsMessagingExtensionBotMessagePreviewSendAsync", bot.Record[2]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestMessagingExtensionFetchTask()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/fetchTask",
                Value = JsonSerializer.SerializeToElement(new { commandId = "testCommand" }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMessagingExtensionFetchTaskAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestMessagingExtensionConfigurationQuerySettingUrl()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/querySettingUrl",
                Value = JsonSerializer.SerializeToElement(new { commandId = "testCommand" }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMessagingExtensionConfigurationQuerySettingUrlAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestMessagingExtensionConfigurationSetting()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/setting",
                Value = JsonSerializer.SerializeToElement(new { commandId = "testCommand" }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMessagingExtensionConfigurationSettingAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestTaskModuleFetch()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "task/fetch",
                Value = JsonSerializer.SerializeToElement(new
                {
                    data = new
                    {
                        key = "value",
                        type = "task / fetch",
                    },
                    context = new
                    {
                        theme = "default"
                    }
                }),
                //Value = JObject.Parse(@"{""data"":{""key"":""value"",""type"":""task / fetch""},""context"":{""theme"":""default""}}"),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await ((IBot)bot).OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsTaskModuleFetchAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestTaskModuleSubmit()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "task/submit",
                Value = JsonSerializer.SerializeToElement(new
                {
                    data = new
                    {
                        key = "value",
                        type = "task / fetch",
                    },
                    context = new
                    {
                        theme = "default"
                    }
                }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsTaskModuleSubmitAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestTabFetch()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "tab/fetch",
                Value = JsonSerializer.SerializeToElement(new
                {
                    data = new
                    {
                        key = "value",
                        type = "task / fetch",
                    },
                    context = new
                    {
                        theme = "default"
                    }
                }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await ((IBot)bot).OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsTabFetchAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestTabSubmit()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "tab/submit",
                Value = JsonSerializer.SerializeToElement(new
                {
                    data = new
                    {
                        key = "value",
                        type = "tab / submit",
                    },
                    context = new
                    {
                        theme = "default"
                    }
                }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsTabSubmitAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestConfigFetch()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "config/fetch",
                Value = JsonSerializer.SerializeToElement(new
                {
                    data = new
                    {
                        key = "value",
                        type = "config / fetch",
                    },
                    context = new
                    {
                        theme = "default"
                    }
                }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsConfigFetchAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestConfigSubmit()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "config/submit",
                Value = JsonSerializer.SerializeToElement(new
                {
                    data = new
                    {
                        key = "value",
                        type = "config / submit",
                    },
                    context = new
                    {
                        theme = "default"
                    }
                }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsConfigSubmitAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestSigninVerifyState()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "signin/verifyState",
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnInvokeActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsSigninVerifyStateAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.IsType<InvokeResponse>(_activitiesToSend[0].Value);
            Assert.Equal(200, ((InvokeResponse)_activitiesToSend[0].Value).Status);
        }

        [Fact]
        public async Task TestOnEventActivity()
        {
            // Arrange
            var activity = new Activity
            {
                ChannelId = Channels.Directline,
                Type = ActivityTypes.Event
            };

            var turnContext = new TurnContext(new SimpleAdapter(), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Single(bot.Record);
            Assert.Equal("OnEventActivityAsync", bot.Record[0]);
        }

        [Fact]
        public async Task TestMeetingStartEvent()
        {
            // Arrange
            var activity = new Activity
            {
                ChannelId = Channels.Msteams,
                Type = ActivityTypes.Event,
                Name = "application/vnd.microsoft.meetingStart",
                Value = JsonSerializer.SerializeToElement(new
                {
                    StartTime = "2021-06-05T00:01:02.0Z"
                }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnEventActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMeetingStartAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.Contains("12:01:02 AM", _activitiesToSend[0].Text); // Date format differs between OSs, so we just Assert.Contains instead of Assert.Equals
        }

        [Fact]
        public async Task TestMeetingEndEvent()
        {
            // Arrange
            var activity = new Activity
            {
                ChannelId = Channels.Msteams,
                Type = ActivityTypes.Event,
                Name = "application/vnd.microsoft.meetingEnd",
                Value = JsonSerializer.SerializeToElement(new
                {
                    EndTime = "2021-06-05T01:02:03.0Z"
                }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnEventActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsMeetingEndAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.Contains("1:02:03 AM", _activitiesToSend[0].Text); // Date format differs between OSs, so we just Assert.Contains instead of Assert.Equals
        }

        [Fact]
        public async Task TeamsReadReceiptEvent()
        {
            // Arrange
            var activity = new Activity
            {
                ChannelId = Channels.Msteams,
                Type = ActivityTypes.Event,
                Name = "application/vnd.microsoft.readReceipt",
                Value = JsonSerializer.SerializeToElement(new
                {
                    lastReadMessageId = "10101010"
                }),
            };

            var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            // Act
            var bot = new TestActivityHandler();
            await bot.OnTurnAsync(turnContext);

            // Assert
            Assert.Equal(2, bot.Record.Count);
            Assert.Equal("OnEventActivityAsync", bot.Record[0]);
            Assert.Equal("OnTeamsReadReceiptAsync", bot.Record[1]);
            Assert.NotNull(_activitiesToSend);
            Assert.Single(_activitiesToSend);
            Assert.Equal("10101010", _activitiesToSend[0].Text);
        }

        //[Fact]
        //public async Task TestMeetingParticipantsJoinEvent()
        //{

            // Arrange
            //string json = @"{
            //        Members: [
            //            {
            //                User: 
            //                {
            //                    Id: 'id', 
            //                    Name: 'name'
            //                }, 
            //                Meeting: 
            //                {
            //                    Role: 'role', 
            //                    InMeeting: true
            //                }
            //            }
            //        ]}";

            //var x = new TeamsMeetingMember(
            //    new TeamsChannelAccount() { Id = "id", Name = "name" },
            //    new UserMeetingDetails() { InMeeting = true, Role = "role" }
            //);

            //MeetingParticipantsEventDetails details = new MeetingParticipantsEventDetails();
            //details.Members.Add(x);

            ////var serialized = JsonSerializer.SerializeToElement(details);
            //var serialized = SerializationExtensions.ToJson(details);
            //var backAgain = SerializationExtensions.ToObject<MeetingParticipantsEventDetails>(serialized);

            //Assert.True(backAgain.Members.Count == 1);


            //var activity = new Activity
            //{
            //    ChannelId = Channels.Msteams,
            //    Type = ActivityTypes.Event,
            //    Name = "application/vnd.microsoft.meetingParticipantJoin",
            //    Value = JsonSerializer.SerializeToElement(details)
            //};


            //var activity = new Activity
            //{
            //    ChannelId = Channels.Msteams,
            //    Type = ActivityTypes.Event,
            //    Name = "application/vnd.microsoft.meetingParticipantJoin",                
            //    Value = JsonSerializer.SerializeToElement( new
            //    {                    
            //        Members = new[]
            //        {
            //            new
            //            {
            //                User = new
            //                {
            //                    Id = "id",
            //                    Name = "name"
            //                },
            //                Meeting = new
            //                {
            //                    Role = "role",
            //                    InMeeting = true
            //                }
            //            }
            //        }
            //    }),
            //};


            //Value = JObject.Parse(@"{
            //    Members: [
            //        {
            //            User: 
            //            {
            //                Id: 'id', 
            //                Name: 'name'
            //            }, 
            //            Meeting: 
            //            {
            //                Role: 'role', 
            //                InMeeting: true
            //            }
            //        }
            //    ]
            //}"),
            //

            //var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

            //// Act
            //var bot = new TestActivityHandler();
            //await bot.OnTurnAsync(turnContext);

            //// Assert
            //Assert.Equal(2, bot.Record.Count);
            //Assert.Equal("OnEventActivityAsync", bot.Record[0]);
            //Assert.Equal("OnTeamsMeetingParticipantsJoinAsync", bot.Record[1]);
            //Assert.NotNull(_activitiesToSend);
            //Assert.Single(_activitiesToSend);
            //Assert.Equal("id", _activitiesToSend[0].Text);
        //}

        //[Fact]
        //public async Task TestMeetingParticipantsLeaveEvent()
        //{
        //    // Arrange
        //    var activity = new Activity
        //    {
        //        ChannelId = Channels.Msteams,
        //        Type = ActivityTypes.Event,
        //        Name = "application/vnd.microsoft.meetingParticipantLeave",
        //        Value = JObject.Parse(@"{
        //            Members: [
        //                {
        //                    User: 
        //                    {
        //                        Id: 'id', 
        //                        Name: 'name'
        //                    }, 
        //                    Meeting: 
        //                    {
        //                        Role: 'role', 
        //                        InMeeting: true
        //                    }
        //                }
        //            ]
        //        }"),
        //    };

        //    _activitiesToSend = null;

        //    var turnContext = new TurnContext(new SimpleAdapter(CaptureSend), activity);

        //    // Act
        //    var bot = new TestActivityHandler();
        //    await ((IBot)bot).OnTurnAsync(turnContext);

        //    // Assert
        //    Assert.Equal(2, bot.Record.Count);
        //    Assert.Equal("OnEventActivityAsync", bot.Record[0]);
        //    Assert.Equal("OnTeamsMeetingParticipantsLeaveAsync", bot.Record[1]);
        //    Assert.NotNull(_activitiesToSend);
        //    Assert.Single(_activitiesToSend);
        //    Assert.Equal("id", _activitiesToSend[0].Text);
        //}

        //[Fact]
        //public async Task TestMessageUpdateActivityTeamsMessageEdit()
        //{
        //    // Arrange
        //    var activity = new Activity
        //    {
        //        Type = ActivityTypes.MessageUpdate,
        //        ChannelData = new TeamsChannelData { EventType = "editMessage" },
        //        ChannelId = Channels.Msteams,
        //    };
        //    var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

        //    // Act
        //    var bot = new TestActivityHandler();
        //    await ((IBot)bot).OnTurnAsync(turnContext);

        //    // Assert
        //    Assert.Equal(2, bot.Record.Count);
        //    Assert.Equal("OnMessageUpdateActivityAsync", bot.Record[0]);
        //    Assert.Equal("OnTeamsMessageEditAsync", bot.Record[1]);
        //}

        //[Fact]
        //public async Task TestMessageUpdateActivityTeamsMessageUndelete()
        //{
        //    // Arrange
        //    var activity = new Activity
        //    {
        //        Type = ActivityTypes.MessageUpdate,
        //        ChannelData = new TeamsChannelData { EventType = "undeleteMessage" },
        //        ChannelId = Channels.Msteams,
        //    };
        //    var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

        //    // Act
        //    var bot = new TestActivityHandler();
        //    await ((IBot)bot).OnTurnAsync(turnContext);

        //    // Assert
        //    Assert.Equal(2, bot.Record.Count);
        //    Assert.Equal("OnMessageUpdateActivityAsync", bot.Record[0]);
        //    Assert.Equal("OnTeamsMessageUndeleteAsync", bot.Record[1]);
        //}

        //[Fact]
        //public async Task TestMessageUpdateActivityTeamsMessageUndelete_NoMsteams()
        //{
        //    // Arrange
        //    var activity = new Activity
        //    {
        //        Type = ActivityTypes.MessageUpdate,
        //        ChannelData = new TeamsChannelData { EventType = "undeleteMessage" },
        //    };
        //    var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

        //    // Act
        //    var bot = new TestActivityHandler();
        //    await ((IBot)bot).OnTurnAsync(turnContext);

        //    // Assert
        //    Assert.Single(bot.Record);
        //    Assert.Equal("OnMessageUpdateActivityAsync", bot.Record[0]);
        //}

        //[Fact]
        //public async Task TestMessageUpdateActivityTeams_NoChannelData()
        //{
        //    // Arrange
        //    var activity = new Activity
        //    {
        //        Type = ActivityTypes.MessageUpdate,
        //        ChannelId = Channels.Msteams,
        //    };
        //    var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

        //    // Act
        //    var bot = new TestActivityHandler();
        //    await ((IBot)bot).OnTurnAsync(turnContext);

        //    // Assert
        //    Assert.Single(bot.Record);
        //    Assert.Equal("OnMessageUpdateActivityAsync", bot.Record[0]);
        //}

        //[Fact]
        //public async Task TestMessageDeleteActivityTeamsMessageSoftDelete()
        //{
        //    // Arrange
        //    var activity = new Activity
        //    {
        //        Type = ActivityTypes.MessageDelete,
        //        ChannelData = new TeamsChannelData { EventType = "softDeleteMessage" },
        //        ChannelId = Channels.Msteams
        //    };
        //    var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

        //    // Act
        //    var bot = new TestActivityHandler();
        //    await ((IBot)bot).OnTurnAsync(turnContext);

        //    // Assert
        //    Assert.Equal(2, bot.Record.Count);
        //    Assert.Equal("OnMessageDeleteActivityAsync", bot.Record[0]);
        //    Assert.Equal("OnTeamsMessageSoftDeleteAsync", bot.Record[1]);
        //}

        //[Fact]
        //public async Task TestMessageDeleteActivityTeamsMessageSoftDelete_NoMsteams()
        //{
        //    // Arrange
        //    var activity = new Activity
        //    {
        //        Type = ActivityTypes.MessageDelete,
        //        ChannelData = new TeamsChannelData { EventType = "softMessage" }
        //    };
        //    var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

        //    // Act
        //    var bot = new TestActivityHandler();
        //    await ((IBot)bot).OnTurnAsync(turnContext);

        //    // Assert
        //    Assert.Single(bot.Record);
        //    Assert.Equal("OnMessageDeleteActivityAsync", bot.Record[0]);
        //}

        //[Fact]
        //public async Task TestMessageDeleteActivityTeams_NoChannelData()
        //{
        //    // Arrange
        //    var activity = new Activity
        //    {
        //        Type = ActivityTypes.MessageDelete,
        //        ChannelId = Channels.Msteams,
        //    };
        //    var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

        //    // Act
        //    var bot = new TestActivityHandler();
        //    await ((IBot)bot).OnTurnAsync(turnContext);

        //    // Assert
        //    Assert.Single(bot.Record);
        //    Assert.Equal("OnMessageDeleteActivityAsync", bot.Record[0]);
        //}



        //private class RosterHttpMessageHandler : HttpMessageHandler
        //{
        //    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        //    {
        //        var response = new HttpResponseMessage(HttpStatusCode.OK);

        //        // GetMembers (Team)
        //        if (request.RequestUri.PathAndQuery.EndsWith("team-id/members"))
        //        {
        //            var content = new JArray
        //            {
        //                new JObject
        //                {
        //                    new JProperty("id", "id-1"),
        //                    new JProperty("objectId", "objectId-1"),
        //                    new JProperty("name", "name-1"),
        //                    new JProperty("givenName", "givenName-1"),
        //                    new JProperty("surname", "surname-1"),
        //                    new JProperty("email", "email-1"),
        //                    new JProperty("userPrincipalName", "userPrincipalName-1"),
        //                    new JProperty("tenantId", "tenantId-1"),
        //                },
        //                new JObject
        //                {
        //                    new JProperty("id", "id-2"),
        //                    new JProperty("objectId", "objectId-2"),
        //                    new JProperty("name", "name-2"),
        //                    new JProperty("givenName", "givenName-2"),
        //                    new JProperty("surname", "surname-2"),
        //                    new JProperty("email", "email-2"),
        //                    new JProperty("userPrincipalName", "userPrincipalName-2"),
        //                    new JProperty("tenantId", "tenantId-2"),
        //                },
        //            };
        //            response.Content = new StringContent(content.ToString());
        //        }

        //        // GetMembers (Group Chat)
        //        else if (request.RequestUri.PathAndQuery.EndsWith("conversation-id/members"))
        //        {
        //            var content = new JArray
        //            {
        //                new JObject
        //                {
        //                    new JProperty("id", "id-3"),
        //                    new JProperty("objectId", "objectId-3"),
        //                    new JProperty("name", "name-3"),
        //                    new JProperty("givenName", "givenName-3"),
        //                    new JProperty("surname", "surname-3"),
        //                    new JProperty("email", "email-3"),
        //                    new JProperty("userPrincipalName", "userPrincipalName-3"),
        //                    new JProperty("tenantId", "tenantId-3"),
        //                },
        //                new JObject
        //                {
        //                    new JProperty("id", "id-4"),
        //                    new JProperty("objectId", "objectId-4"),
        //                    new JProperty("name", "name-4"),
        //                    new JProperty("givenName", "givenName-4"),
        //                    new JProperty("surname", "surname-4"),
        //                    new JProperty("email", "email-4"),
        //                    new JProperty("userPrincipalName", "userPrincipalName-4"),
        //                    new JProperty("tenantId", "tenantId-4"),
        //                },
        //            };
        //            response.Content = new StringContent(content.ToString());
        //        }
        //        else if (request.RequestUri.PathAndQuery.EndsWith("team-id/members/id-1") || request.RequestUri.PathAndQuery.EndsWith("conversation-id/members/id-1"))
        //        {
        //            var content = new JObject
        //                {
        //                    new JProperty("id", "id-1"),
        //                    new JProperty("objectId", "objectId-1"),
        //                    new JProperty("name", "name-1"),
        //                    new JProperty("givenName", "givenName-1"),
        //                    new JProperty("surname", "surname-1"),
        //                    new JProperty("email", "email-1"),
        //                    new JProperty("userPrincipalName", "userPrincipalName-1"),
        //                    new JProperty("tenantId", "tenantId-1"),
        //                };
        //            response.Content = new StringContent(content.ToString());
        //        }

        //        return Task.FromResult(response);
        //    }
        //}
    }
}
