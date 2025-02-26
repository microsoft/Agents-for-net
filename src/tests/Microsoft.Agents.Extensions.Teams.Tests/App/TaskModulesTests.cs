// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.BotBuilder.Testing;
using Microsoft.Agents.BotBuilder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.App;
using Microsoft.Agents.Extensions.Teams.App.TaskModules;
using Microsoft.Agents.Extensions.Teams.Models;
using Moq;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.App
{
    public class TaskModulesTests
    {
        [Fact]
        public async Task Test_OnFetch_Verb()
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
                Name = "task/fetch",
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    data = new
                    {
                        msteams = new
                        {
                            type = "task/fetch",
                        },
                        verb = "test-verb",
                    }
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var taskModuleResponseMock = new Mock<TaskModuleResponse>();
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200,
                Body = taskModuleResponseMock.Object
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TeamsApplication(new()
            {
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });
            FetchHandlerAsync handler = (turnContext, turnState, data, cancellationToken) =>
            {
                return Task.FromResult(taskModuleResponseMock.Object);
            };

            // Act
            app.TaskModules.OnFetch("test-verb", handler);
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnFetch_Verb_NotHit()
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
                Name = "task/fetch",
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    data = new
                    {
                        msteams = new
                        {
                            type = "task/fetch",
                        },
                        verb = "not-test-verb",
                    }
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var taskModuleResponseMock = new Mock<TaskModuleResponse>();
            var app = new TeamsApplication(new()
            {
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });
            FetchHandlerAsync handler = (turnContext, turnState, data, cancellationToken) =>
            {
                return Task.FromResult(taskModuleResponseMock.Object);
            };

            // Act
            app.TaskModules.OnFetch("test-verb", handler);
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Null(activitiesToSend);
        }

        [Fact]
        public async Task Test_OnFetch_RouteSelector_ActivityNotMatched()
        {
            var adapter = new SimpleAdapter();
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "task/fetch",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var taskModuleResponseMock = new Mock<TaskModuleResponse>();
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TeamsApplication(new()
            {
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });
            RouteSelectorAsync routeSelector = (turnContext, cancellationToken) =>
            {
                return Task.FromResult(true);
            };
            FetchHandlerAsync handler = (turnContext, turnState, data, cancellationToken) =>
            {
                return Task.FromResult(taskModuleResponseMock.Object);
            };

            // Act
            app.TaskModules.OnFetch(routeSelector, handler);
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await app.OnTurnAsync(turnContext, CancellationToken.None));

            // Assert
            Assert.Equal("Unexpected TaskModules.OnFetch() triggered for activity type: invoke", exception.Message);
        }

        [Fact]
        public async Task Test_OnSubmit_Verb()
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
                Name = "task/submit",
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    data = new
                    {
                        msteams = new
                        {
                            type = "task/submit",
                        },
                        verb = "test-verb",
                    }
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var taskModuleResponseMock = new Mock<TaskModuleResponse>();
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200,
                Body = taskModuleResponseMock.Object
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TeamsApplication(new()
            {
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });
            SubmitHandlerAsync handler = (turnContext, turnState, data, cancellationToken) =>
            {
                return Task.FromResult(taskModuleResponseMock.Object);
            };

            // Act
            app.TaskModules.OnSubmit("test-verb", handler);
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnSubmit_Verb_NotHit()
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
                Name = "task/submit",
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    data = new
                    {
                        msteams = new
                        {
                            type = "task/submit",
                        },
                        verb = "not-test-verb",
                    }
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var taskModuleResponseMock = new Mock<TaskModuleResponse>();
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TeamsApplication(new()
            {
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });

            SubmitHandlerAsync handler = (turnContext, turnState, data, cancellationToken) =>
            {
                return Task.FromResult(taskModuleResponseMock.Object);
            };

            // Act
            app.TaskModules.OnSubmit("test-verb", handler);
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Null(activitiesToSend);
        }

        [Fact]
        public async Task Test_OnSubmit_RouteSelector_ActivityNotMatched()
        {
            var adapter = new SimpleAdapter();
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "task/submit",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });
            var taskModuleResponseMock = new Mock<TaskModuleResponse>();
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TeamsApplication(new()
            {
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });
            RouteSelectorAsync routeSelector = (turnContext, cancellationToken) =>
            {
                return Task.FromResult(true);
            };
            SubmitHandlerAsync handler = (turnContext, turnState, data, cancellationToken) =>
            {
                return Task.FromResult(taskModuleResponseMock.Object);
            };

            // Act
            app.TaskModules.OnSubmit(routeSelector, handler);
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await app.OnTurnAsync(turnContext, CancellationToken.None));

            // Assert
            Assert.Equal("Unexpected TaskModules.OnSubmit() triggered for activity type: invoke", exception.Message);
        }
    }
}
