// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.Tests;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.MSTeams.Tests.Model;
using Moq;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.MSTeams.Tests.App
{
    public class ApplicationRouteTests
    {
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
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams
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
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/queryLink",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams
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
                HttpClientFactory = new TestHttpClientFactory(),
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
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams
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
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/queryLink",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams
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
                HttpClientFactory = new TestHttpClientFactory(),
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

    }
}
