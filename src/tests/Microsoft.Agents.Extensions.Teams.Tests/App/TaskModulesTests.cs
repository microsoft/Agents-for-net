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
using Microsoft.Agents.Extensions.Teams.App.TaskModules;
using Moq;
using System;
using System.Net.Http;
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
                ChannelId = Channels.Msteams,
            });
            var taskModuleResponseMock = new Mock<Microsoft.Teams.Api.TaskModules.Response>();
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
            FetchHandler handler = (turnContext, turnState, data, cancellationToken) =>
            {
                return Task.FromResult(taskModuleResponseMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.TaskModules.OnFetch("test-verb", handler);
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
                ChannelId = Channels.Msteams,
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var taskModuleResponseMock = new Mock<Microsoft.Teams.Api.TaskModules.Response>();
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            FetchHandler handler = (turnContext, turnState, data, cancellationToken) =>
            {
                return Task.FromResult(taskModuleResponseMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.TaskModules.OnFetch("test-verb", handler);
            });

            // Act
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
                ChannelId = Channels.Msteams,
            });
            var taskModuleResponseMock = new Mock<Microsoft.Teams.Api.TaskModules.Response>();
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
            FetchHandler handler = (turnContext, turnState, data, cancellationToken) =>
            {
                return Task.FromResult(taskModuleResponseMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.AddRoute(app, FetchRouteBuilder.Create().WithSelector(routeSelector).WithHandler(handler).Build());
            });

            // Act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await app.OnTurnAsync(turnContext, CancellationToken.None));

            // Assert
            Assert.Equal("Unexpected FetchRouteBuilder triggered for activity type: invoke, name: task/fetch", exception.Message);
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
                ChannelId = Channels.Msteams,
            });
            var taskModuleResponseMock = new Mock<Microsoft.Teams.Api.TaskModules.Response>();
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
            SubmitHandler handler = (turnContext, turnState, data, cancellationToken) =>
            {
                return Task.FromResult(taskModuleResponseMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.TaskModules.OnSubmit("test-verb", handler);
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
                ChannelId = Channels.Msteams,
            });
            var taskModuleResponseMock = new Mock<Microsoft.Teams.Api.TaskModules.Response>();
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);

            SubmitHandler handler = (turnContext, turnState, data, cancellationToken) =>
            {
                return Task.FromResult(taskModuleResponseMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.TaskModules.OnSubmit("test-verb", handler);
            });

            // Act
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
                ChannelId = Channels.Msteams,
            });
            var taskModuleResponseMock = new Mock<Microsoft.Teams.Api.TaskModules.Response>();
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
            SubmitHandler handler = (turnContext, turnState, data, cancellationToken) =>
            {
                return Task.FromResult(taskModuleResponseMock.Object);
            };

            app.RegisterExtension(extension, (ext) =>
            {
                ext.AddRoute(app, SubmitRouteBuilder.Create().WithSelector(routeSelector).WithHandler(handler).Build());
            });

            // Act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await app.OnTurnAsync(turnContext, CancellationToken.None));

            // Assert
            Assert.Equal("Unexpected SubmitRouteBuilder triggered for activity type: invoke, name: task/submit", exception.Message);
        }

        [Fact]
        public async Task Test_OnFetch_TypedHandler_DeserializesData()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg) => activitiesToSend = arg;
            var adapter = new SimpleAdapter(CaptureSend);
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "task/fetch",
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    data = new
                    {
                        msteams = new { type = "task/fetch" },
                        verb = "test-verb",
                        title = "test-title",
                    }
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });
            var taskModuleResponseMock = new Mock<Microsoft.Teams.Api.TaskModules.Response>();
            var expectedInvokeResponse = new InvokeResponse() { Status = 200, Body = taskModuleResponseMock.Object };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            FetchHandler<TaskModuleData> handler = (ctx, ts, data, ct) =>
            {
                Assert.NotNull(data);
                Assert.Equal("test-verb", data.Verb);
                Assert.Equal("test-title", data.Title);
                return Task.FromResult(taskModuleResponseMock.Object);
            };
            app.RegisterExtension(extension, (ext) =>
            {
                ext.TaskModules.OnFetch("test-verb", handler);
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
        public async Task Test_OnSubmit_TypedHandler_DeserializesData()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg) => activitiesToSend = arg;
            var adapter = new SimpleAdapter(CaptureSend);
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "task/submit",
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    data = new
                    {
                        msteams = new { type = "task/submit" },
                        verb = "test-verb",
                        title = "test-title",
                    }
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams,
            });
            var taskModuleResponseMock = new Mock<Microsoft.Teams.Api.TaskModules.Response>();
            var expectedInvokeResponse = new InvokeResponse() { Status = 200, Body = taskModuleResponseMock.Object };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            SubmitHandler<TaskModuleData> handler = (ctx, ts, data, ct) =>
            {
                Assert.NotNull(data);
                Assert.Equal("test-verb", data.Verb);
                Assert.Equal("test-title", data.Title);
                return Task.FromResult(taskModuleResponseMock.Object);
            };
            app.RegisterExtension(extension, (ext) =>
            {
                ext.TaskModules.OnSubmit("test-verb", handler);
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        private sealed class TaskModuleData
        {
            public string Verb { get; set; }
            public string Title { get; set; }
        }
    }
}
