// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.Tests;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Compat;
using Microsoft.Agents.Extensions.Teams.Models;
using Microsoft.Agents.Extensions.Teams.Tests.Model;
using Microsoft.Teams.Api;
using Microsoft.Teams.Api.Config;
using Moq;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.App
{
    public class ApplicationRouteTests
    {
        [Fact]
        public async Task Test_OnMessageEdit()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.MessageUpdate,
                ChannelId = Channels.Msteams,
                ChannelData = new ChannelData
                {
                    EventType = "editMessage"
                },
                Name = "1",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.MessageUpdate,
                ChannelId = Channels.Msteams,
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelData = new ChannelData
                {
                    EventType = "softDeleteMessage"
                }
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Message,
                ChannelId = Channels.Msteams,
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });

            var extension = new TeamsAgentExtension(app);
            var names = new List<string>();
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Messages.OnMessageEdit((turnContext, _, _) =>
                {
                    names.Add(turnContext.Activity.Name);
                    return Task.CompletedTask;
                });
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);

            // Assert
            Assert.Single(names);
            Assert.Equal("1", names[0]);
        }

        [Fact]
        public async Task Test_OnMessageUnDelete()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.MessageUpdate,
                ChannelId = Channels.Msteams,
                ChannelData = new ChannelData
                {
                    EventType = "undeleteMessage"
                },
                Name = "1",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.MessageUpdate,
                ChannelId = Channels.Msteams,
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelData = new ChannelData
                {
                    EventType = "softDeleteMessage"
                }
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Message,
                ChannelId = Channels.Msteams,
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });

            var extension = new TeamsAgentExtension(app);
            var names = new List<string>();
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Messages.OnMessageUndelete((turnContext, _, _) =>
                {
                    names.Add(turnContext.Activity.Name);
                    return Task.CompletedTask;
                });
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);

            // Assert
            Assert.Single(names);
            Assert.Equal("1", names[0]);
        }

        [Fact]
        public async Task Test_OnMessageDelete()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.MessageDelete,
                ChannelId = Channels.Msteams,
                ChannelData = new ChannelData
                {
                    EventType = "softDeleteMessage"
                },
                Name = "1",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.MessageDelete,
                ChannelId = Channels.Msteams,
                ChannelData = new ChannelData
                {
                    EventType = "unknown"
                },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Message,
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });

            var names = new List<string>();
            var extension = new TeamsAgentExtension(app);

            app.RegisterExtension(extension, (ext) =>
            {
                ext.Messages.OnMessageDelete((turnContext, _, _) =>
                {
                    names.Add(turnContext.Activity.Name);
                    return Task.CompletedTask;
                });
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);

            // Assert
            Assert.Single(names);
            Assert.Equal("1", names[0]);
        }

        [Fact]
        public async Task Test_OnConfigFetch()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);
            var activity1 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "config/fetch",
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "config/fetch",
                ChannelId = Channels.Outlook,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "config/submit",
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var activity4 = new Activity
            {
                Type = ActivityTypes.Message,
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var turnContext4 = new TurnContext(adapter, activity4);
            var configResponseMock = new Mock<ConfigResponse>();
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200,
                Body = configResponseMock.Object
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });

            var extension = new TeamsAgentExtension(app);
            var names = new List<string>();

            app.RegisterExtension(extension, (ext) =>
            {
                ext.Configuration.OnConfigFetch((turnContext, _, _, _) =>
                {
                    names.Add(turnContext.Activity.Name);
                    return Task.FromResult(configResponseMock.Object);
                });
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);
            await app.OnTurnAsync(turnContext4, CancellationToken.None);

            // Assert
            Assert.Single(names);
            Assert.Equal("config/fetch", names[0]);
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnConfigSubmit()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);
            object data = new
            {
                testKey = "testValue"
            };
            var activity1 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "config/submit",
                ChannelId = Channels.Msteams,
                Value = ProtocolJsonSerializer.ToJsonElements(data),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "config/submit",
                ChannelId = Channels.Outlook,
                Value = ProtocolJsonSerializer.ToJsonElements(data),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "config/fetch",
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var activity4 = new Activity
            {
                Type = ActivityTypes.Message,
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var turnContext4 = new TurnContext(adapter, activity4);
            var configResponseMock = new Mock<ConfigResponse>();
            var expectedInvokeResponse = new InvokeResponse()
            {
                Status = 200,
                Body = configResponseMock.Object
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var names = new List<string>();
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Configuration.OnConfigSubmit((turnContext, _, configData, _) =>
                {
                    Assert.NotNull(configData);
                    //Assert.Equal(configData, configData as JObject);
                    names.Add(turnContext.Activity.Name);
                    return Task.FromResult(configResponseMock.Object);
                });
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);
            await app.OnTurnAsync(turnContext4, CancellationToken.None);

            // Assert
            Assert.Single(names);
            Assert.Equal("config/submit", names[0]);
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnFileConsentAccept()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);
            var activity1 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "fileConsent/invoke",
                Value = new
                {
                    action = "accept"
                },
                Id = "test",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "fileConsent/invoke",
                Value = new
                {
                    action = "decline"
                },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/queryLink",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams
            };
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var expectedInvokeResponse = new InvokeResponse
            {
                Status = 200
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            var ids = new List<string>();
            app.RegisterExtension(extension, (ext) =>
            {
                ext.FileConsent.OnAccept((turnContext, _, _, _) =>
                {
                    ids.Add(turnContext.Activity.Id);
                    return Task.CompletedTask;
                });
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);

            // Assert
            Assert.Single(ids);
            Assert.Equal("test", ids[0]);
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnFileConsentDecline()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);
            var activity1 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "fileConsent/invoke",
                Value = new
                {
                    action = "decline"
                },
                Id = "test",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "fileConsent/invoke",
                Value = new
                {
                    action = "accept"
                },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/queryLink",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams
            };
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var expectedInvokeResponse = new InvokeResponse
            {
                Status = 200
            };
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var ids = new List<string>();
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                ext.FileConsent.OnDecline((turnContext, _, _, _) =>
                {
                    ids.Add(turnContext.Activity.Id);
                    return Task.CompletedTask;
                });
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);

            // Assert
            Assert.Single(ids);
            Assert.Equal("test", ids[0]);
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnO365ConnectorCardAction()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);
            var activity1 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "actionableMessage/executeAction",
                Value = new { },
                Id = "test",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.Event,
                Name = "actionableMessage/executeAction",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/queryLink",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Channels.Msteams
            };
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var expectedInvokeResponse = new InvokeResponse
            {
                Status = 200
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var ids = new List<string>();
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Messages.OnO365ConnectorCardAction((turnContext, _, _, _) =>
                {
                    ids.Add(turnContext.Activity.Id);
                    return Task.CompletedTask;
                });
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);

            // Assert
            Assert.Single(ids);
            Assert.Equal("test", ids[0]);
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }

        [Fact]
        public async Task Test_OnTeamsReadReceipt()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Event,
                ChannelId = Channels.Msteams,
                Name = Microsoft.Teams.Api.Activities.Events.Name.ReadReceipt,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new ReadReceiptInfo
                {
                    LastReadMessageId = "10101010",
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, activity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            var extension = new TeamsAgentExtension(app);
            var names = new List<string>();
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Messages.OnReadReceipt((context, _, _, _) =>
                {
                    names.Add(context.Activity.Name);
                    return Task.CompletedTask;
                });
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Single(names);
            Assert.Equal("application/vnd.microsoft.readReceipt", names[0]);
        }

        [Fact]
        public async Task Test_OnTeamsReadReceipt_IncorrectName()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Event,
                ChannelId = Channels.Msteams,
                Name = "application/vnd.microsoft.meetingStart",
                Value = ProtocolJsonSerializer.ToJsonElements(new
                {
                    lastReadMessageId = "10101010",
                }),
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, activity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });

            var extension = new TeamsAgentExtension(app);
            var names = new List<string>();
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Messages.OnReadReceipt((context, _, _, _) =>
                {
                    names.Add(context.Activity.Name);
                    return Task.CompletedTask;
                });
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Empty(names);
        }
    }
}
