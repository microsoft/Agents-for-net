// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Builder.Tests;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.MSTeams.MessageExtensions;
using Microsoft.Agents.Storage;
using Moq;
using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.MSTeams.Tests.App
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
                Name = Microsoft.Teams.Apps.InvokeNames.MessageExtensionSubmitAction,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new Microsoft.Teams.Apps.MessageExtensions.MessageExtensionAction
                {
                    CommandId = commandId,
                    CommandContext = Microsoft.Teams.Apps.MessageExtensions.MessageExtensionCommandContexts.Message,
                    Data = new
                    {
                        title = "test-title",
                        content = "test-content"
                    }
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
            });

            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestSubmitActionAppWithAttribute(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new TestHttpClientFactory(),
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
                Name = Microsoft.Teams.Apps.InvokeNames.MessageExtensionQuery,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new Microsoft.Teams.Apps.MessageExtensions.MessageExtensionQuery
                {
                    CommandId = commandId,
                    Parameters = []
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
            });

            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestQueryAppWithAttribute(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new TestHttpClientFactory(),
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
                Name = Microsoft.Teams.Apps.InvokeNames.MessageExtensionSelectItem,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new { id = "item1" }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
            });

            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestSelectItemAppWithAttribute(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new TestHttpClientFactory(),
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
        public async Task QuerySettingUrlAttribute_AddRoute_CreatesWorkingRoute()
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
                Name = Microsoft.Teams.Apps.InvokeNames.MessageExtensionQuerySettingUrl,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
            });

            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestQuerySettingUrlAppWithAttribute(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new TestHttpClientFactory(),
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
        public async Task SettingAttribute_AddRoute_CreatesWorkingRoute()
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
                Name = Microsoft.Teams.Apps.InvokeNames.MessageExtensionSetting,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new { state = "test-state" }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
            });

            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestSettingAppWithAttribute(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new TestHttpClientFactory(),
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
                Name = Microsoft.Teams.Apps.InvokeNames.MessageExtensionQueryLink,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new { url = "https://example.com" }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
            });

            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestQueryLinkAppWithAttribute(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new TestHttpClientFactory(),
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
                Name = Microsoft.Teams.Apps.InvokeNames.MessageExtensionFetchTask,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new Microsoft.Teams.Apps.MessageExtensions.MessageExtensionAction
                {
                    CommandId = commandId,
                    CommandContext = Microsoft.Teams.Apps.MessageExtensions.MessageExtensionCommandContexts.Message,
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
                HttpClientFactory = new TestHttpClientFactory(),
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
                Name = Microsoft.Teams.Apps.InvokeNames.MessageExtensionCardButtonClicked,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new { title = "card1", content = "content1" }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
            });

            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestCardButtonClickedAppWithAttribute(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new TestHttpClientFactory(),
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
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
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
                Name = Microsoft.Teams.Apps.InvokeNames.MessageExtensionSubmitAction,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new Microsoft.Teams.Apps.MessageExtensions.MessageExtensionAction
                {
                    CommandId = commandId,
                    CommandContext = Microsoft.Teams.Apps.MessageExtensions.MessageExtensionCommandContexts.Message,
                    BotMessagePreviewAction = Microsoft.Teams.Apps.MessageExtensions.BotMessagePreviewActionTypes.Edit,
                    BotActivityPreview = [new() { Type = previewActivity.Type }]
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
            });

            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestMessagePreviewEditAppWithAttribute(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new TestHttpClientFactory(),
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
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
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
                Name = Microsoft.Teams.Apps.InvokeNames.MessageExtensionSubmitAction,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new Microsoft.Teams.Apps.MessageExtensions.MessageExtensionAction
                {
                    CommandId = commandId,
                    CommandContext = Microsoft.Teams.Apps.MessageExtensions.MessageExtensionCommandContexts.Message,
                    BotMessagePreviewAction = Microsoft.Teams.Apps.MessageExtensions.BotMessagePreviewActionTypes.Send,
                    BotActivityPreview = [new() { Type = previewActivity.Type }]
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
            });

            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestMessagePreviewSendAppWithAttribute(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new TestHttpClientFactory(),
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

        [TeamsSubmitActionRoute("testCommand")]

        public Task<Microsoft.Teams.Apps.MessageExtensions.MessageExtensionResponse> OnSubmitActionAsync(
            ITeamsTurnContext turnContext,
            ITurnState turnState,
            Microsoft.Teams.Apps.MessageExtensions.MessageExtensionAction action,
            CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            var response = new Microsoft.Teams.Apps.MessageExtensions.MessageExtensionResponse();
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

        [TeamsQueryRoute("queryCommand")]
        public Task<Microsoft.Teams.Apps.MessageExtensions.MessageExtensionResponse> OnQueryAsync(
            ITeamsTurnContext turnContext,
            ITurnState turnState,
            Microsoft.Teams.Apps.MessageExtensions.MessageExtensionQuery query,
            CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            var response = new Microsoft.Teams.Apps.MessageExtensions.MessageExtensionResponse();
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

        [TeamsQueryLinkRoute]
        public Task<Microsoft.Teams.Apps.MessageExtensions.MessageExtensionResponse> OnQueryLinkAsync(
            ITeamsTurnContext turnContext,
            ITurnState turnState,
            Microsoft.Teams.Apps.MessageExtensions.MessageExtensionQueryLink query,
            CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            Assert.Equal(new System.Uri("https://example.com"), query.Url);
            var response = new Microsoft.Teams.Apps.MessageExtensions.MessageExtensionResponse();
            return Task.FromResult(response);
        }
    }

    class TestQuerySettingUrlAppWithAttribute : AgentApplication
    {
        public bool HandlerCalled { get; set; }

        public TestQuerySettingUrlAppWithAttribute(AgentApplicationOptions options) : base(options)
        {
            // Register the Teams extension
            var extension = new TeamsAgentExtension(this);
            this.RegisterExtension(extension, (ext) => { });
        }

        [TeamsQuerySettingUrlRoute]
        public Task<Microsoft.Teams.Apps.MessageExtensions.MessageExtensionResponse> OnQuerySettingUrlAsync(
            ITeamsTurnContext turnContext,
            ITurnState turnState,
            CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            var response = new Microsoft.Teams.Apps.MessageExtensions.MessageExtensionResponse();
            return Task.FromResult(response);
        }
    }

    class TestSettingAppWithAttribute : AgentApplication
    {
        public bool HandlerCalled { get; set; }

        public TestSettingAppWithAttribute(AgentApplicationOptions options) : base(options)
        {
            // Register the Teams extension
            var extension = new TeamsAgentExtension(this);
            this.RegisterExtension(extension, (ext) => { });
        }

        [TeamsSettingRoute]
        public Task<Microsoft.Teams.Apps.MessageExtensions.MessageExtensionResponse> OnSettingAsync(
            ITeamsTurnContext turnContext,
            ITurnState turnState,
            Microsoft.Teams.Apps.MessageExtensions.MessageExtensionQuery settings,
            CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            return Task.FromResult(new Microsoft.Teams.Apps.MessageExtensions.MessageExtensionResponse());
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

        [TeamsFetchActionRoute("fetchCommand")]
        public Task<Microsoft.Teams.Apps.MessageExtensions.MessageExtensionActionResponse> OnFetchTaskAsync(
            ITeamsTurnContext turnContext,
            ITurnState turnState,
            Microsoft.Teams.Apps.MessageExtensions.MessageExtensionAction action,
            CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            var response = new Microsoft.Teams.Apps.MessageExtensions.MessageExtensionActionResponse();
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

        [TeamsCardButtonClickedRoute]
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

        [TeamsMessagePreviewEditRoute("previewEditCommand")]
        public Task<Microsoft.Teams.Apps.MessageExtensions.MessageExtensionResponse> OnMessagePreviewEditAsync(
            ITeamsTurnContext turnContext,
            ITurnState turnState,
            Microsoft.Teams.Apps.MessageExtensions.MessageExtensionActivityPreview activityPreview,
            CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            Assert.Equal(ActivityTypes.Message, activityPreview.Type);
            var response = new Microsoft.Teams.Apps.MessageExtensions.MessageExtensionResponse();
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

        [TeamsMessagePreviewSendRoute("previewSendCommand")]
        public Task OnMessagePreviewSendAsync(
            ITeamsTurnContext turnContext,
            ITurnState turnState,
            Microsoft.Teams.Apps.MessageExtensions.MessageExtensionActivityPreview activityPreview,
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

        [TeamsSelectItemRoute]
        public Task<Microsoft.Teams.Apps.MessageExtensions.MessageExtensionResponse> OnSelectItemAsync(
            ITurnContext turnContext,
            ITurnState turnState,
            JsonElement item,
            CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            Assert.Equal("item1", item.GetProperty("id").GetString());
            var response = new Microsoft.Teams.Apps.MessageExtensions.MessageExtensionResponse();
            return Task.FromResult(response);
        }
    }
}
