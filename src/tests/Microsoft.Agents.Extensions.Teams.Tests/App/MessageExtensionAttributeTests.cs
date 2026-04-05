// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.MessageExtensions;
using Microsoft.Agents.Storage;
using Moq;
using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.App
{
    public class MessageExtensionAttributeTests
    {
        [Fact]
        public async Task SubmitActionAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            const string commandId = "testCommand";
            var storage = new MemoryStorage();

            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);

            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.SubmitAction,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new Microsoft.Teams.Api.MessageExtensions.Action
                {
                    CommandId = commandId,
                    CommandContext = Microsoft.Teams.Api.Commands.Context.Message,
                    Data = new
                    {
                        title = "test-title",
                        content = "test-content"
                    }
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });

            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestSubmitActionAppWithAttribute(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.HandlerCalled);
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
        }

        [Fact]
        public async Task QueryAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            const string commandId = "queryCommand";

            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);

            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.Query,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new Microsoft.Teams.Api.MessageExtensions.Query
                {
                    CommandId = commandId,
                    Parameters = []
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });

            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestQueryAppWithAttribute(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.HandlerCalled);
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
        }

        [Fact]
        public async Task SelectItemAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);

            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.SelectItem,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new { id = "item1" }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });

            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestSelectItemAppWithAttribute(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.HandlerCalled);
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
        }

        [Fact]
        public async Task QueryUrlSettingAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);

            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.QuerySettingUrl,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });

            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestQueryUrlSettingAppWithAttribute(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.HandlerCalled);
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
        }

        [Fact]
        public async Task ConfigureSettingsAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);

            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.Setting,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new { state = "test-state" }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });

            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestConfigureSettingsAppWithAttribute(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.HandlerCalled);
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
        }

        [Fact]
        public async Task QueryLinkAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);

            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.QueryLink,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new { url = "https://example.com" }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });

            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestQueryLinkAppWithAttribute(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.HandlerCalled);
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
        }

        [Fact]
        public async Task FetchTaskAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            const string commandId = "fetchCommand";

            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);

            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.FetchTask,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new Microsoft.Teams.Api.MessageExtensions.Action
                {
                    CommandId = commandId,
                    CommandContext = Microsoft.Teams.Api.Commands.Context.Message,
                    Data = new
                    {
                        title = "test-title",
                        content = "test-content"
                    }
                }),
            });

            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestFetchTaskAppWithAttribute(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.HandlerCalled);
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
        }

        [Fact]
        public async Task CardButtonClickedAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);

            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.CardButtonClicked,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new { title = "card1", content = "content1" }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });

            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestCardButtonClickedAppWithAttribute(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.HandlerCalled);
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
        }

        [Fact]
        public async Task MessagePreviewEditAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            const string commandId = "previewEditCommand";
            var previewActivity = new Activity()
            {
                Type = ActivityTypes.Message,
                Text = "preview text",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            };

            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);

            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.SubmitAction,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new Microsoft.Teams.Api.MessageExtensions.Action
                {
                    CommandId = commandId,
                    CommandContext = Microsoft.Teams.Api.Commands.Context.Message,
                    BotMessagePreviewAction = Microsoft.Teams.Api.MessageExtensions.MessagePreviewAction.Edit,
                    BotActivityPreview = [previewActivity.ToTeamsActivity()]
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });

            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestMessagePreviewEditAppWithAttribute(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.HandlerCalled);
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
        }

        [Fact]
        public async Task MessagePreviewSendAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            const string commandId = "previewSendCommand";
            var previewActivity = new Activity()
            {
                Type = ActivityTypes.Message,
                Text = "preview text",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            };

            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);

            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.SubmitAction,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new Microsoft.Teams.Api.MessageExtensions.Action
                {
                    CommandId = commandId,
                    CommandContext = Microsoft.Teams.Api.Commands.Context.Message,
                    BotMessagePreviewAction = Microsoft.Teams.Api.MessageExtensions.MessagePreviewAction.Send,
                    BotActivityPreview = [previewActivity.ToTeamsActivity()]
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });

            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestMessagePreviewSendAppWithAttribute(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.HandlerCalled);
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
        }
    }

    // Test application classes
    class TestSubmitActionAppWithAttribute : AgentApplication
    {
        public bool HandlerCalled { get; set; }

        public TestSubmitActionAppWithAttribute(AgentApplicationOptions options) : base(options)
        {
            // Register the Teams extension
            var extension = new TeamsAgentExtension(this);
            this.RegisterExtension(extension, (ext) => { });
        }

        [SubmitActionRoute("testCommand")]

        public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnSubmitActionAsync(
            ITurnContext turnContext,
            ITurnState turnState,
            Microsoft.Teams.Api.MessageExtensions.Action action,
            CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            var response = new Microsoft.Teams.Api.MessageExtensions.Response();
            return Task.FromResult(response);
        }
    }

    class TestQueryAppWithAttribute : AgentApplication
    {
        public bool HandlerCalled { get; set; }

        public TestQueryAppWithAttribute(AgentApplicationOptions options) : base(options)
        {
            // Register the Teams extension
            var extension = new TeamsAgentExtension(this);
            this.RegisterExtension(extension, (ext) => { });
        }

        [QueryRoute("queryCommand")]
        public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnQueryAsync(
            ITurnContext turnContext,
            ITurnState turnState,
            Microsoft.Teams.Api.MessageExtensions.Query query,
            CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            var response = new Microsoft.Teams.Api.MessageExtensions.Response();
            return Task.FromResult(response);
        }
    }

    class TestQueryLinkAppWithAttribute : AgentApplication
    {
        public bool HandlerCalled { get; set; }

        public TestQueryLinkAppWithAttribute(AgentApplicationOptions options) : base(options)
        {
            // Register the Teams extension
            var extension = new TeamsAgentExtension(this);
            this.RegisterExtension(extension, (ext) => { });
        }

        [QueryLinkRoute]
        public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnQueryLinkAsync(
            ITurnContext turnContext,
            ITurnState turnState,
            string url,
            CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            Assert.Equal("https://example.com", url);
            var response = new Microsoft.Teams.Api.MessageExtensions.Response();
            return Task.FromResult(response);
        }
    }

    class TestQueryUrlSettingAppWithAttribute : AgentApplication
    {
        public bool HandlerCalled { get; set; }

        public TestQueryUrlSettingAppWithAttribute(AgentApplicationOptions options) : base(options)
        {
            // Register the Teams extension
            var extension = new TeamsAgentExtension(this);
            this.RegisterExtension(extension, (ext) => { });
        }

        [QueryUrlSettingRoute]
        public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnQueryUrlSettingAsync(
            ITurnContext turnContext,
            ITurnState turnState,
            CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            var response = new Microsoft.Teams.Api.MessageExtensions.Response();
            return Task.FromResult(response);
        }
    }

    class TestConfigureSettingsAppWithAttribute : AgentApplication
    {
        public bool HandlerCalled { get; set; }

        public TestConfigureSettingsAppWithAttribute(AgentApplicationOptions options) : base(options)
        {
            // Register the Teams extension
            var extension = new TeamsAgentExtension(this);
            this.RegisterExtension(extension, (ext) => { });
        }

        [ConfigureSettingsRoute]
        public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnConfigureSettingsAsync(
            ITurnContext turnContext,
            ITurnState turnState,
            Microsoft.Teams.Api.MessageExtensions.Query settings,
            CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            return Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
        }
    }

    class TestFetchTaskAppWithAttribute : AgentApplication
    {
        public bool HandlerCalled { get; set; }

        public TestFetchTaskAppWithAttribute(AgentApplicationOptions options) : base(options)
        {
            // Register the Teams extension
            var extension = new TeamsAgentExtension(this);
            this.RegisterExtension(extension, (ext) => { });
        }

        [FetchActionRoute("fetchCommand")]
        public Task<Microsoft.Teams.Api.MessageExtensions.ActionResponse> OnFetchTaskAsync(
            ITurnContext turnContext,
            ITurnState turnState,
            Microsoft.Teams.Api.MessageExtensions.Action action,
            CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            var response = new Microsoft.Teams.Api.MessageExtensions.ActionResponse();
            return Task.FromResult(response);
        }
    }

    class TestCardButtonClickedAppWithAttribute : AgentApplication
    {
        public bool HandlerCalled { get; set; }

        public TestCardButtonClickedAppWithAttribute(AgentApplicationOptions options) : base(options)
        {
            // Register the Teams extension
            var extension = new TeamsAgentExtension(this);
            this.RegisterExtension(extension, (ext) => { });
        }

        [CardButtonClickedRoute]
        public Task OnCardButtonClickedAsync(
            ITurnContext turnContext,
            ITurnState turnState,
            CardData cardData,
            CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            Assert.Equal("card1", cardData.Title);
            Assert.Equal("content1", cardData.Content);
            return Task.CompletedTask;
        }
    }

    class CardData
    {
        public string Title { get; set; }
        public string Content { get; set; }
    }

    class TestMessagePreviewEditAppWithAttribute : AgentApplication
    {
        public bool HandlerCalled { get; set; }

        public TestMessagePreviewEditAppWithAttribute(AgentApplicationOptions options) : base(options)
        {
            // Register the Teams extension
            var extension = new TeamsAgentExtension(this);
            this.RegisterExtension(extension, (ext) => { });
        }

        [MessagePreviewEditRoute("previewEditCommand")]
        public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnMessagePreviewEditAsync(
            ITurnContext turnContext,
            ITurnState turnState,
            IActivity activityPreview,
            CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            Assert.Equal(ActivityTypes.Message, activityPreview.Type);
            var response = new Microsoft.Teams.Api.MessageExtensions.Response();
            return Task.FromResult(response);
        }
    }

    class TestMessagePreviewSendAppWithAttribute : AgentApplication
    {
        public bool HandlerCalled { get; set; }

        public TestMessagePreviewSendAppWithAttribute(AgentApplicationOptions options) : base(options)
        {
            // Register the Teams extension
            var extension = new TeamsAgentExtension(this);
            this.RegisterExtension(extension, (ext) => { });
        }

        [MessagePreviewSendRoute("previewSendCommand")]
        public Task OnMessagePreviewSendAsync(
            ITurnContext turnContext,
            ITurnState turnState,
            IActivity activityPreview,
            CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            Assert.Equal(ActivityTypes.Message, activityPreview.Type);
            return Task.CompletedTask;
        }
    }

    class TestSelectItemAppWithAttribute : AgentApplication
    {
        public bool HandlerCalled { get; set; }

        public TestSelectItemAppWithAttribute(AgentApplicationOptions options) : base(options)
        {
            // Register the Teams extension
            var extension = new TeamsAgentExtension(this);
            this.RegisterExtension(extension, (ext) => { });
        }

        [SelectItemRoute]
        public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnSelectItemAsync(
            ITurnContext turnContext,
            ITurnState turnState,
            JsonElement item,
            CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            Assert.Equal("item1", item.GetProperty("id").GetString());
            var response = new Microsoft.Teams.Api.MessageExtensions.Response();
            return Task.FromResult(response);
        }
    }
}
