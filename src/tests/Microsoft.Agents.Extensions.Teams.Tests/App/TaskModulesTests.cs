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
using System.Text.RegularExpressions;
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

        [Fact]
        public async Task Test_OnFetch_CustomKeyName_Matches()
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
                        action = "test-verb",
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
            FetchHandler handler = (turnContext, turnState, data, cancellationToken) =>
                Task.FromResult(taskModuleResponseMock.Object);

            app.RegisterExtension(extension, (ext) =>
            {
                ext.TaskModules.OnFetch("test-verb", handler, keyName: "action");
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
        public async Task Test_OnFetch_CustomKeyName_DefaultKeyNotMatched()
        {
            // Arrange — activity uses default "verb" field but route is registered with keyName "action"
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
                        verb = "test-verb",
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
            FetchHandler handler = (turnContext, turnState, data, cancellationToken) =>
                Task.FromResult(taskModuleResponseMock.Object);

            app.RegisterExtension(extension, (ext) =>
            {
                ext.TaskModules.OnFetch("test-verb", handler, keyName: "action");
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert — should not match because the activity has "verb" but the route looks for "action"
            Assert.Null(activitiesToSend);
        }

        [Fact]
        public async Task Test_OnFetch_NullKeyValue_MatchesAnyFetch()
        {
            // Arrange — null keyValue should match any fetch regardless of data content
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
                        someOtherField = "whatever",
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
            FetchHandler handler = (turnContext, turnState, data, cancellationToken) =>
                Task.FromResult(taskModuleResponseMock.Object);

            app.RegisterExtension(extension, (ext) =>
            {
                ext.TaskModules.OnFetch((string)null, handler);
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
        public async Task Test_OnSubmit_CustomKeyName_Matches()
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
                        action = "test-verb",
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
            SubmitHandler handler = (turnContext, turnState, data, cancellationToken) =>
                Task.FromResult(taskModuleResponseMock.Object);

            app.RegisterExtension(extension, (ext) =>
            {
                ext.TaskModules.OnSubmit("test-verb", handler, keyName: "action");
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
        public async Task Test_OnSubmit_CustomKeyName_DefaultKeyNotMatched()
        {
            // Arrange — activity uses default "verb" field but route expects "action"
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
                        verb = "test-verb",
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
                Task.FromResult(taskModuleResponseMock.Object);

            app.RegisterExtension(extension, (ext) =>
            {
                ext.TaskModules.OnSubmit("test-verb", handler, keyName: "action");
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Null(activitiesToSend);
        }

        [Fact]
        public async Task Test_OnSubmit_NullKeyValue_MatchesAnySubmit()
        {
            // Arrange — null keyValue should match any submit regardless of data content
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
                        someOtherField = "whatever",
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
            SubmitHandler handler = (turnContext, turnState, data, cancellationToken) =>
                Task.FromResult(taskModuleResponseMock.Object);

            app.RegisterExtension(extension, (ext) =>
            {
                ext.TaskModules.OnSubmit((string)null, handler);
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        // ── Regex routing ──────────────────────────────────────────────────────

        [Fact]
        public async Task Test_OnFetch_Regex_Matches()
        {
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg) => activitiesToSend = arg;
            var adapter = new SimpleAdapter(CaptureSend);
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "task/fetch",
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new { data = new { verb = "fetch-foo" } }),
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
            FetchHandler handler = (ctx, ts, data, ct) => Task.FromResult(taskModuleResponseMock.Object);

            app.RegisterExtension(extension, (ext) =>
            {
                ext.TaskModules.OnFetch(new Regex("^fetch-"), handler);
            });

            await app.OnTurnAsync(turnContext, CancellationToken.None);

            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnFetch_Regex_NoMatch()
        {
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg) => activitiesToSend = arg;
            var adapter = new SimpleAdapter(CaptureSend);
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "task/fetch",
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new { data = new { verb = "other-action" } }),
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
            FetchHandler handler = (ctx, ts, data, ct) => Task.FromResult(new Mock<Microsoft.Teams.Api.TaskModules.Response>().Object);

            app.RegisterExtension(extension, (ext) =>
            {
                ext.TaskModules.OnFetch(new Regex("^fetch-"), handler);
            });

            await app.OnTurnAsync(turnContext, CancellationToken.None);

            Assert.Null(activitiesToSend);
        }

        [Fact]
        public async Task Test_OnSubmit_Regex_Matches()
        {
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg) => activitiesToSend = arg;
            var adapter = new SimpleAdapter(CaptureSend);
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "task/submit",
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new { data = new { verb = "submit-bar" } }),
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
            SubmitHandler handler = (ctx, ts, data, ct) => Task.FromResult(taskModuleResponseMock.Object);

            app.RegisterExtension(extension, (ext) =>
            {
                ext.TaskModules.OnSubmit(new Regex("^submit-"), handler);
            });

            await app.OnTurnAsync(turnContext, CancellationToken.None);

            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnSubmit_Regex_NoMatch()
        {
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg) => activitiesToSend = arg;
            var adapter = new SimpleAdapter(CaptureSend);
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "task/submit",
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new { data = new { verb = "fetch-something" } }),
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
            SubmitHandler handler = (ctx, ts, data, ct) => Task.FromResult(new Mock<Microsoft.Teams.Api.TaskModules.Response>().Object);

            app.RegisterExtension(extension, (ext) =>
            {
                ext.TaskModules.OnSubmit(new Regex("^submit-"), handler);
            });

            await app.OnTurnAsync(turnContext, CancellationToken.None);

            Assert.Null(activitiesToSend);
        }

        // ── Cross-invoke-name isolation ────────────────────────────────────────

        [Fact]
        public async Task Test_OnFetch_DoesNotFireForSubmitActivity()
        {
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg) => activitiesToSend = arg;
            var adapter = new SimpleAdapter(CaptureSend);
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "task/submit",           // submit, not fetch
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new { data = new { verb = "test-verb" } }),
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
            FetchHandler handler = (ctx, ts, data, ct) => Task.FromResult(new Mock<Microsoft.Teams.Api.TaskModules.Response>().Object);

            app.RegisterExtension(extension, (ext) =>
            {
                ext.TaskModules.OnFetch("test-verb", handler);
            });

            await app.OnTurnAsync(turnContext, CancellationToken.None);

            Assert.Null(activitiesToSend);
        }

        [Fact]
        public async Task Test_OnSubmit_DoesNotFireForFetchActivity()
        {
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg) => activitiesToSend = arg;
            var adapter = new SimpleAdapter(CaptureSend);
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "task/fetch",            // fetch, not submit
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new { data = new { verb = "test-verb" } }),
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
            SubmitHandler handler = (ctx, ts, data, ct) => Task.FromResult(new Mock<Microsoft.Teams.Api.TaskModules.Response>().Object);

            app.RegisterExtension(extension, (ext) =>
            {
                ext.TaskModules.OnSubmit("test-verb", handler);
            });

            await app.OnTurnAsync(turnContext, CancellationToken.None);

            Assert.Null(activitiesToSend);
        }

        // ── Channel isolation ──────────────────────────────────────────────────

        [Fact]
        public async Task Test_OnFetch_NonTeamsChannel_NotMatched()
        {
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg) => activitiesToSend = arg;
            var adapter = new SimpleAdapter(CaptureSend);
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "task/fetch",
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new { data = new { verb = "test-verb" } }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "webchat",          // not msteams
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            FetchHandler handler = (ctx, ts, data, ct) => Task.FromResult(new Mock<Microsoft.Teams.Api.TaskModules.Response>().Object);

            app.RegisterExtension(extension, (ext) =>
            {
                ext.TaskModules.OnFetch("test-verb", handler);
            });

            await app.OnTurnAsync(turnContext, CancellationToken.None);

            Assert.Null(activitiesToSend);
        }

        // ── Null Activity.Value ────────────────────────────────────────────────

        [Fact]
        public async Task Test_OnFetch_NullActivityValue_NullKeyRoute_NotMatched()
        {
            // "Match any" route should still require Activity.Value to be non-null
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg) => activitiesToSend = arg;
            var adapter = new SimpleAdapter(CaptureSend);
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "task/fetch",
                Value = null,                   // no value
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
            FetchHandler handler = (ctx, ts, data, ct) => Task.FromResult(new Mock<Microsoft.Teams.Api.TaskModules.Response>().Object);

            app.RegisterExtension(extension, (ext) =>
            {
                ext.TaskModules.OnFetch((string)null, handler);
            });

            await app.OnTurnAsync(turnContext, CancellationToken.None);

            Assert.Null(activitiesToSend);
        }

        [Fact]
        public async Task Test_OnSubmit_NullActivityValue_NullKeyRoute_NotMatched()
        {
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg) => activitiesToSend = arg;
            var adapter = new SimpleAdapter(CaptureSend);
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "task/submit",
                Value = null,
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
            SubmitHandler handler = (ctx, ts, data, ct) => Task.FromResult(new Mock<Microsoft.Teams.Api.TaskModules.Response>().Object);

            app.RegisterExtension(extension, (ext) =>
            {
                ext.TaskModules.OnSubmit((string)null, handler);
            });

            await app.OnTurnAsync(turnContext, CancellationToken.None);

            Assert.Null(activitiesToSend);
        }

        // ── Key value case sensitivity ─────────────────────────────────────────

        [Fact]
        public async Task Test_OnFetch_KeyValueMatchIsCaseSensitive()
        {
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg) => activitiesToSend = arg;
            var adapter = new SimpleAdapter(CaptureSend);
            var turnContext = new TurnContext(adapter, new Activity()
            {
                Type = ActivityTypes.Invoke,
                Name = "task/fetch",
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new { data = new { verb = "Test-Verb" } }),  // different case
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
            FetchHandler handler = (ctx, ts, data, ct) => Task.FromResult(new Mock<Microsoft.Teams.Api.TaskModules.Response>().Object);

            app.RegisterExtension(extension, (ext) =>
            {
                ext.TaskModules.OnFetch("test-verb", handler);  // lower-case registration
            });

            await app.OnTurnAsync(turnContext, CancellationToken.None);

            Assert.Null(activitiesToSend);
        }

        // ── Builder duplicate-registration guard ──────────────────────────────

        [Fact]
        public void WithKeyValue_DuplicateString_ThrowsInvalidOperationException()
        {
            var builder = FetchRouteBuilder.Create().WithKeyValue("first");

            Assert.Throws<InvalidOperationException>(() => builder.WithKeyValue("second"));
        }

        [Fact]
        public void WithKeyValue_DuplicateRegex_ThrowsInvalidOperationException()
        {
            var builder = FetchRouteBuilder.Create().WithKeyValue(new Regex("^first"));

            Assert.Throws<InvalidOperationException>(() => builder.WithKeyValue(new Regex("^second")));
        }

        [Fact]
        public void WithKeyValue_StringThenRegex_ThrowsInvalidOperationException()
        {
            var builder = FetchRouteBuilder.Create().WithKeyValue("first");

            Assert.Throws<InvalidOperationException>(() => builder.WithKeyValue(new Regex("^second")));
        }

        [Fact]
        public void WithKeyValue_RegexThenString_ThrowsInvalidOperationException()
        {
            var builder = FetchRouteBuilder.Create().WithKeyValue(new Regex("^first"));

            Assert.Throws<InvalidOperationException>(() => builder.WithKeyValue("second"));
        }

        private sealed class TaskModuleData
        {
            public string Verb { get; set; }
            public string Title { get; set; }
        }
    }
}
