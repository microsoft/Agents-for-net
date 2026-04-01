// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.App;
using Microsoft.Agents.Extensions.Teams.App.MessageExtensions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.App
{
    public class MessageExtensionsTests
    {
        [Fact]
        public async Task Test_OnSubmitAction_CommandId()
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
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.SubmitAction,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new Microsoft.Teams.Api.MessageExtensions.Action
                {
                    CommandId = "test-command",
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
            var actionResponseMock = new Mock<Microsoft.Teams.Api.MessageExtensions.Response>();
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200,
                Body = actionResponseMock.Object
            };
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            SubmitActionHandler handler = (turnContext, turnState, data, cancellationToken) =>
            {
                MessageExtensionActionData actionData = Cast<MessageExtensionActionData>(data);
                Assert.Equal("test-title", actionData.Title);
                Assert.Equal("test-content", actionData.Content);
                return Task.FromResult(actionResponseMock.Object);
            };
            app.RegisterExtension(extension, (ext) =>
            {
#pragma warning disable CS0618 // Type or member is obsolete
                ext.MessageExtensions.OnSubmitAction("test-command", handler);
#pragma warning restore CS0618 // Type or member is obsolete
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnSubmitActionTyped_CommandId()
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
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.SubmitAction,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new Microsoft.Teams.Api.MessageExtensions.Action
                {
                    CommandId = "test-command",
                    CommandContext = Microsoft.Teams.Api.Commands.Context.Message,
                    Data = new MessageExtensionActionData
                    {
                        Title = "test-title",
                        Content = "test-content"
                    }
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var actionResponseMock = new Mock<Microsoft.Teams.Api.MessageExtensions.Response>();
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200,
                Body = actionResponseMock.Object
            };
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            SubmitActionHandler<MessageExtensionActionData> handler = (turnContext, turnState, data, cancellationToken) =>
            {
                Assert.Equal("test-title", data.Title);
                Assert.Equal("test-content", data.Content);
                return Task.FromResult(actionResponseMock.Object);
            };
            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnSubmitAction("test-command", handler);
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnSubmitAction_CommandId_NotHit()
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
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.SubmitAction,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    commandId = "test-command",
                    data = new
                    {
                        title = "test-title",
                        content = "test-content"
                    }
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var actionResponseMock = new Mock<Microsoft.Teams.Api.MessageExtensions.Response>();
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            SubmitActionHandler handler = (turnContext, turnState, data, cancellationToken) =>
            {
                MessageExtensionActionData actionData = Cast<MessageExtensionActionData>(data);
                Assert.Equal("test-title", actionData.Title);
                Assert.Equal("test-content", actionData.Content);
                return Task.FromResult(actionResponseMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
#pragma warning disable CS0618 // Type or member is obsolete
                ext.MessageExtensions.OnSubmitAction("not-test-command", handler);
#pragma warning restore CS0618 // Type or member is obsolete
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Null(activitiesToSend);
        }

        [Fact]
        public async Task Test_OnSubmitAction_RouteSelector_ActivityNotMatched()
        {
            var adapter = new SimpleAdapter();
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.FetchTask,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var actionResponseMock = new Mock<Microsoft.Teams.Api.MessageExtensions.Response>();
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);

            RouteSelector routeSelector = (turnContext, cancellationToken) =>
            {
                return Task.FromResult(true);
            };
            SubmitActionHandler handler = (turnContext, turnState, data, cancellationToken) =>
            {
                return Task.FromResult(actionResponseMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
#pragma warning disable CS0618 // Type or member is obsolete
                ext.MessageExtensions.OnSubmitAction(routeSelector, handler);
#pragma warning restore CS0618 // Type or member is obsolete
            });
            // Act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await app.OnTurnAsync(turnContext, CancellationToken.None));

            // Assert
            Assert.Equal("Unexpected SubmitActionRouteBuilder triggered for activity type: invoke, name: composeExtension/fetchTask", exception.Message);
        }

        [Fact]
        public async Task Test_OnAgentMessagePreviewEdit_CommandId()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);
            var activity = new Activity()
            {
                Type = ActivityTypes.Message,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            };

            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.SubmitAction,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new Microsoft.Teams.Api.MessageExtensions.Action
                {
                    CommandId = "test-command",
                    CommandContext = Microsoft.Teams.Api.Commands.Context.Message,
                    BotMessagePreviewAction = Microsoft.Teams.Api.MessageExtensions.MessagePreviewAction.Edit,
                    BotActivityPreview = [activity.ToTeamsActivity()]
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });
            var actionResponseMock = new Mock<Microsoft.Teams.Api.MessageExtensions.Response>();
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200,
                Body = actionResponseMock.Object,
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);

            AgentMessagePreviewEditHandler handler = (turnContext, turnState, activityPreview, cancellationToken) =>
            {
                Assert.Equivalent(activity, activityPreview);
                return Task.FromResult(actionResponseMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnAgentMessagePreviewEdit("test-command", handler);
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnAgentMessagePreviewEdit_CommandId_NotHit()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);
            var activity = new Activity()
            {
                Type = ActivityTypes.Message,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.SubmitAction,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    commandId = "test-command",
                    botMessagePreviewAction = "send",
                    botActivityPreview = new List<Activity> { activity }
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var actionResponseMock = new Mock<Microsoft.Teams.Api.MessageExtensions.Response>();
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            AgentMessagePreviewEditHandler handler = (turnContext, turnState, activityPreview, cancellationToken) =>
            {
                Assert.Equivalent(activity, activityPreview);
                return Task.FromResult(actionResponseMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnAgentMessagePreviewEdit("not-test-command", handler);
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Null(activitiesToSend);
        }

        [Fact]
        public async Task Test_OnAgentMessagePreviewEdit_RouteSelector_ActivityNotMatched()
        {
            var adapter = new SimpleAdapter();
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.FetchTask,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });
            var actionResponseMock = new Mock<Microsoft.Teams.Api.MessageExtensions.Response>();
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            RouteSelector routeSelector = (turnContext, cancellationToken) =>
            {
                // Return true even though the Activity is wrong to test that the handler properly validates the activity type and name.
                return Task.FromResult(true);
            };
            AgentMessagePreviewEditHandler handler = (turnContext, turnState, data, cancellationToken) =>
            {
                return Task.FromResult(actionResponseMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnAgentMessagePreviewEdit(routeSelector, handler);
            });
            // Act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await app.OnTurnAsync(turnContext, CancellationToken.None));

            // Assert
            Assert.Equal("Unexpected MessagePreviewEditRouteBuilder triggered for activity type: invoke, name: composeExtension/fetchTask", exception.Message);
        }

        [Fact]
        public async Task Test_OnAgentMessagePreviewSend_CommandId()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);
            var activity = new Activity()
            {
                Type = ActivityTypes.Message,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            };

            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.SubmitAction,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new Microsoft.Teams.Api.MessageExtensions.Action
                {
                    CommandId = "test-command",
                    CommandContext = Microsoft.Teams.Api.Commands.Context.Message,
                    BotMessagePreviewAction = Microsoft.Teams.Api.MessageExtensions.MessagePreviewAction.Send,
                    BotActivityPreview = [activity.ToTeamsActivity()]
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200,
                Body = new Microsoft.Teams.Api.MessageExtensions.Response()
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            AgentMessagePreviewSendHandler handler = (turnContext, turnState, activityPreview, cancellationToken) =>
            {
                Assert.Equivalent(activity, activityPreview);
                return Task.CompletedTask;
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnAgentMessagePreviewSend("test-command", handler);
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnAgentMessagePreviewSend_CommandId_NotHit()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);
            var activity = new Activity()
            {
                Type = ActivityTypes.Message,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            };

            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.SubmitAction,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    commandId = "test-command",
                    botMessagePreviewAction = "edit",
                    botActivityPreview = new List<Activity> { activity }
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });

            var extension = new TeamsAgentExtension(app);
            AgentMessagePreviewSendHandler handler = (turnContext, turnState, activityPreview, cancellationToken) =>
            {
                Assert.Equivalent(activity, activityPreview);
                return Task.CompletedTask;
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnAgentMessagePreviewSend("not-test-command", handler);
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Null(activitiesToSend);
        }

        [Fact]
        public async Task Test_OnAgentMessagePreviewSend_RouteSelector_ActivityNotMatched()
        {
            var adapter = new SimpleAdapter();
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.FetchTask,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            RouteSelector routeSelector = (turnContext, cancellationToken) =>
            {
                return Task.FromResult(true);
            };
            AgentMessagePreviewSendHandler handler = (turnContext, turnState, data, cancellationToken) =>
            {
                return Task.CompletedTask;
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnAgentMessagePreviewSend(routeSelector, handler);
            });
            // Act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await app.OnTurnAsync(turnContext, CancellationToken.None));

            // Assert
            Assert.Equal("Unexpected MessagePreviewSendRouteBuilder triggered for activity type: invoke, name: composeExtension/fetchTask", exception.Message);
        }

        [Fact]
        public async Task Test_OnFetchTask_CommandId()
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
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.FetchTask,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    commandId = "test-command",
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });
            var taskModuleResponseMock = new Mock<Microsoft.Teams.Api.MessageExtensions.ActionResponse>();
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200,
                Body = taskModuleResponseMock.Object
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            FetchTaskHandler handler = (turnContext, turnState, cancellationToken) =>
            {
                return Task.FromResult(taskModuleResponseMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnFetchTask("test-command", handler);
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnFetchTask_CommandId_NotHit()
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
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.FetchTask,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    commandId = "test-command",
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });
            var taskModuleResponseMock = new Mock<Microsoft.Teams.Api.MessageExtensions.ActionResponse>();
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            FetchTaskHandler handler = (turnContext, turnState, cancellationToken) =>
            {
                return Task.FromResult(taskModuleResponseMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnFetchTask("not-test-command", handler);
            });
            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Null(activitiesToSend);
        }

        [Fact]
        public async Task Test_OnFetchTask_RouteSelector_ActivityNotMatched()
        {
            var adapter = new SimpleAdapter();
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.SubmitAction,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });
            var taskModuleResponseMock = new Mock<Microsoft.Teams.Api.MessageExtensions.ActionResponse>();
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            RouteSelector routeSelector = (turnContext, cancellationToken) =>
            {
                return Task.FromResult(true);
            };
            FetchTaskHandler handler = (turnContext, turnState, cancellationToken) =>
            {
                return Task.FromResult(taskModuleResponseMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnFetchTask(routeSelector, handler);
            });
            // Act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await app.OnTurnAsync(turnContext, CancellationToken.None));

            // Assert
            Assert.Equal("Unexpected FetchTaskRouteBuilder triggered for activity type: invoke, name: composeExtension/submitAction", exception.Message);
        }

        [Fact]
        public async Task Test_OnQuery_CommandId()
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
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.Query,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    commandId = "test-command",
                    parameters = new List<Microsoft.Teams.Api.MessageExtensions.Parameter>
                    {
                        new() { Name = "test-name", Value = "test-value" }
                    },
                    queryOptions = new
                    {
                        count = 10,
                        skip = 0
                    }
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });
            var messagingExtensionResultMock = new Mock<Microsoft.Teams.Api.MessageExtensions.Response>();
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200,
                Body = messagingExtensionResultMock.Object
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            QueryHandler handler = (turnContext, turnState, query, cancellationToken) =>
            {
                Assert.Single(query.Parameters);
                Assert.Equal("test-value", query.Parameters.FirstOrDefault(p => p.Name == "test-name")?.Value?.ToString());
                Assert.Equal(10, query.QueryOptions.Count);
                Assert.Equal(0, query.QueryOptions.Skip);
                return Task.FromResult(messagingExtensionResultMock.Object);
            };
            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnQuery("test-command", handler);
            });
            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnQuery_CommandId_NotHit()
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
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.Query,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    commandId = "test-command",
                    parameters = new List<Microsoft.Teams.Api.MessageExtensions.Parameter>
                    {
                        new() { Name = "test-name", Value = "test-value" }
                    },
                    queryOptions = new
                    {
                        count = 10,
                        skip = 0
                    }
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });
            var messagingExtensionResultMock = new Mock<Microsoft.Teams.Api.MessageExtensions.Response>();
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            QueryHandler handler = (turnContext, turnState, query, cancellationToken) =>
            {
                Assert.Single(query.Parameters);
                Assert.Equal("test-value", query.Parameters.FirstOrDefault(p => p.Name == "test-name")?.Value?.ToString());
                Assert.Equal(10, query.QueryOptions.Count);
                Assert.Equal(0, query.QueryOptions.Skip);
                return Task.FromResult(messagingExtensionResultMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnQuery("not-test-command", handler);
            });
            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Null(activitiesToSend);
        }

        [Fact]
        public async Task Test_OnQuery_RouteSelector_NotMatched()
        {
            var adapter = new SimpleAdapter();
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.SelectItem,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });
            var messagingExtensionResultMock = new Mock<Microsoft.Teams.Api.MessageExtensions.Response>();
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            RouteSelector routeSelector = (turnContext, cancellationToken) =>
            {
                return Task.FromResult(true);
            };
            QueryHandler handler = (turnContext, turnState, data, cancellationToken) =>
            {
                return Task.FromResult(messagingExtensionResultMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnQuery(routeSelector, handler);
            });

            // Act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await app.OnTurnAsync(turnContext, CancellationToken.None));

            // Assert
            Assert.Equal("Unexpected QueryRouteBuilder triggered for activity type: invoke, name: composeExtension/selectItem", exception.Message);
        }

        [Fact]
        public async Task Test_SelectItem()
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
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new { }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });
            var messagingExtensionResultMock = new Mock<Microsoft.Teams.Api.MessageExtensions.Response>();
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200,
                Body = messagingExtensionResultMock.Object
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            SelectItemHandler handler = (turnContext, turnState, item, cancellationToken) =>
            {
                return Task.FromResult(messagingExtensionResultMock.Object);
            };
            app.RegisterExtension(extension, (ext) =>
            {
#pragma warning disable CS0618 // Type or member is obsolete
                ext.MessageExtensions.OnSelectItem(handler);
#pragma warning restore CS0618 // Type or member is obsolete
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_SelectItemTyped()
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
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new MessageExtensionActionData { }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });
            var messagingExtensionResultMock = new Mock<Microsoft.Teams.Api.MessageExtensions.Response>();
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200,
                Body = messagingExtensionResultMock.Object
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            SelectItemHandler<MessageExtensionActionData> handler = (turnContext, turnState, item, cancellationToken) =>
            {
                Assert.IsType<MessageExtensionActionData>(item);
                return Task.FromResult(messagingExtensionResultMock.Object);
            };
            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnSelectItem(handler);
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_SelectItem_NotHit()
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
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.Query,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new { }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });
            var messagingExtensionResultMock = new Mock<Microsoft.Teams.Api.MessageExtensions.Response>();
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200,
                Body = messagingExtensionResultMock.Object
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            SelectItemHandler handler = (turnContext, turnState, item, cancellationToken) =>
            {
                return Task.FromResult(messagingExtensionResultMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
#pragma warning disable CS0618 // Type or member is obsolete
                ext.MessageExtensions.OnSelectItem(handler);
#pragma warning restore CS0618 // Type or member is obsolete
            });
            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Null(activitiesToSend);
        }

        [Fact]
        public async Task Test_OnQueryLink()
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
                Value = new
                {
                    url = "test-url"
                },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });
            var messagingExtensionResultMock = new Mock<Microsoft.Teams.Api.MessageExtensions.Response>();
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200,
                Body = messagingExtensionResultMock.Object
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            QueryLinkHandler handler = (turnContext, turnState, url, cancellationToken) =>
            {
                Assert.Equal("test-url", url);
                return Task.FromResult(messagingExtensionResultMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnQueryLink(handler);
            });
            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnQueryLink_NotHit()
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
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.Query,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });
            var messagingExtensionResultMock = new Mock<Microsoft.Teams.Api.MessageExtensions.Response>();
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            QueryLinkHandler handler = (turnContext, turnState, url, cancellationToken) =>
            {
                return Task.FromResult(messagingExtensionResultMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnQueryLink(handler);
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Null(activitiesToSend);
        }

        [Fact]
        public async Task Test_OnAnonymousQueryLink()
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
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.AnonQueryLink,
                Value = new
                {
                    url = "test-url"
                },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams
            });
            var messagingExtensionResultMock = new Mock<Microsoft.Teams.Api.MessageExtensions.Response>();
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200,
                Body = messagingExtensionResultMock.Object
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            QueryLinkHandler handler = (turnContext, turnState, url, cancellationToken) =>
            {
                Assert.Equal("test-url", url);
                return Task.FromResult(messagingExtensionResultMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnAnonymousQueryLink(handler);
            });
            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnAnonymousQueryLink_NotHit()
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
                Name = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.Query,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });
            var messagingExtensionResultMock = new Mock<Microsoft.Teams.Api.MessageExtensions.Response>();
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            QueryLinkHandler handler = (turnContext, turnState, url, cancellationToken) =>
            {
                return Task.FromResult(messagingExtensionResultMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnAnonymousQueryLink(handler);
            });
            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Null(activitiesToSend);
        }

        [Fact]
        public async Task Test_OnQueryUrlSetting()
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
            var messagingExtensionResultMock = new Mock<Microsoft.Teams.Api.MessageExtensions.Response>();
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200,
                Body = messagingExtensionResultMock.Object
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            QueryUrlSettingHandler handler = (turnContext, turnState, cancellationToken) =>
            {
                return Task.FromResult(messagingExtensionResultMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnQueryUrlSetting(handler);
            });
            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnQueryUrlSetting_NotHit()
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
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });
            var messagingExtensionResultMock = new Mock<Microsoft.Teams.Api.MessageExtensions.Response>();
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            QueryLinkHandler handler = (turnContext, turnState, url, cancellationToken) =>
            {
                return Task.FromResult(messagingExtensionResultMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnAnonymousQueryLink(handler);
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Null(activitiesToSend);
        }

        [Fact]
        public async Task Test_OnConfigureSettings()
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
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    state = "test-state"
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            ConfigureSettingsHandler handler = (turnContext, turnState, settings, cancellationToken) =>
            {
                var obj = ProtocolJsonSerializer.ToJsonElements(settings);
                Assert.NotNull(obj);
                Assert.Equal("test-state", obj["state"].ToString());
                return Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
            };

            app.RegisterExtension(extension, (ext) =>
            {
#pragma warning disable CS0618 // Type or member is obsolete
                ext.MessageExtensions.OnConfigureSettings(handler);
#pragma warning restore CS0618 // Type or member is obsolete
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnConfigureSettingsTyped()
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
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new Dictionary<string, string>
                {
                    { "state", "test-state" }
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            ConfigureSettingsHandler handler = (turnContext, turnState, settings, cancellationToken) =>
            {
                Assert.Equal("test-state", settings.State);
                return Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.MessageExtensions.OnConfigureSettings(handler);
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnConfigureSettings_NotHit()
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
            var app = new AgentApplication(new(() => turnState.Result)
            {
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            ConfigureSettingsHandler handler = (turnContext, turnState, settings, cancellationToken) =>
            {
                return Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
            };

            app.RegisterExtension(extension, (ext) =>
            {
#pragma warning disable CS0618 // Type or member is obsolete
                ext.MessageExtensions.OnConfigureSettings(handler);
#pragma warning restore CS0618 // Type or member is obsolete
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Null(activitiesToSend);
        }

        [Fact]
        public async Task Test_OnCardButtonClicked()
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
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            CardButtonClickedHandler handler = (turnContext, turnState, cardData, cancellationToken) =>
            {
                return Task.CompletedTask;
            };

            app.RegisterExtension(extension, (ext) =>
            {
#pragma warning disable CS0618 // Type or member is obsolete
                ext.MessageExtensions.OnCardButtonClicked(handler);
#pragma warning restore CS0618 // Type or member is obsolete
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnCardButtonClicked_NotHit()
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
            var app = new AgentApplication(new(() => turnState.Result)
            {
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            CardButtonClickedHandler handler = (turnContext, turnState, cardData, cancellationToken) =>
            {
                return Task.CompletedTask;
            };

            app.RegisterExtension(extension, (ext) =>
            {
#pragma warning disable CS0618 // Type or member is obsolete
                ext.MessageExtensions.OnCardButtonClicked(handler);
#pragma warning restore CS0618 // Type or member is obsolete
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Null(activitiesToSend);
        }

        private static T Cast<T>(object data)
        {
            Assert.NotNull(data);
            T result = ProtocolJsonSerializer.ToObject<T>(data);
            Assert.NotNull(result);
            return result;
        }

        private sealed class MessageExtensionActionData
        {
            public string Title { get; set; }

            public string Content { get; set; }
        }
    }
}
