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
using Microsoft.Agents.Extensions.Teams.FileConsents;
using Moq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.App
{
    public class FileConsentAttributeTests
    {
        [Fact]
        public async Task FileConsentAcceptRouteAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext("accept");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.AcceptHandlerCalled);
            Assert.False(app.DeclineHandlerCalled);
        }

        [Fact]
        public async Task FileConsentDeclineRouteAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext("decline");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.DeclineHandlerCalled);
            Assert.False(app.AcceptHandlerCalled);
        }

        [Fact]
        public async Task FileConsentAcceptRouteAttribute_DoesNotFire_ForDeclineAction()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext("decline");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.False(app.AcceptHandlerCalled);
        }

        [Fact]
        public async Task FileConsentDeclineRouteAttribute_DoesNotFire_ForAcceptAction()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext("accept");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.False(app.DeclineHandlerCalled);
        }

        [Fact]
        public async Task FileConsentAcceptRouteAttribute_DoesNotFire_ForWrongInvokeName()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext("accept", invokeName: "task/fetch");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.False(app.AcceptHandlerCalled);
        }

        [Fact]
        public async Task FileConsentDeclineRouteAttribute_DoesNotFire_ForWrongInvokeName()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext("decline", invokeName: "task/fetch");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.False(app.DeclineHandlerCalled);
        }

        [Fact]
        public async Task FileConsentAcceptRouteAttribute_HandlerReceivesDeserializedResponse()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext("accept", uploadUrl: "https://upload.example.com/file", fileName: "report.txt");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.AcceptHandlerCalled);
            Assert.NotNull(app.ReceivedResponse);
            Assert.Equal("https://upload.example.com/file", app.ReceivedResponse.UploadInfo.UploadUrl);
            Assert.Equal("report.txt", app.ReceivedResponse.UploadInfo.Name);
        }

        [Fact]
        public async Task FileConsentDeclineRouteAttribute_HandlerReceivesDeserializedResponse()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext("decline", fileName: "report.txt");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.DeclineHandlerCalled);
            Assert.NotNull(app.ReceivedResponse);
            Assert.Equal("report.txt", app.ReceivedResponse.UploadInfo.Name);
        }

        [Fact]
        public async Task FileConsentAcceptRouteAttribute_DoesNotFire_ForNonInvokeActivity()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            var adapter = new SimpleAdapter((activities) => activitiesToSend = activities);
            var turnContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello",
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestFileConsentAttributeApp(new AgentApplicationOptions(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.False(app.AcceptHandlerCalled);
        }

        private static (TestFileConsentAttributeApp app, ITurnContext turnContext) CreateAppAndContext(
            string action,
            string invokeName = "fileConsent/invoke",
            string uploadUrl = "https://upload.example.com",
            string fileName = "file.txt")
        {
            IActivity[] activitiesToSend = null;
            var adapter = new SimpleAdapter((activities) => activitiesToSend = activities);
            var turnContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = invokeName,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    action,
                    uploadInfo = new
                    {
                        name = fileName,
                        uniqueId = "unique-123",
                        fileType = "txt",
                        uploadUrl,
                        contentUrl = "https://content.example.com/file"
                    }
                }),
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestFileConsentAttributeApp(new AgentApplicationOptions(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            return (app, turnContext);
        }
    }

    [TeamsExtension]
    partial class TestFileConsentAttributeApp : AgentApplication
    {
        public bool AcceptHandlerCalled { get; private set; }
        public bool DeclineHandlerCalled { get; private set; }
        public Microsoft.Teams.Api.FileConsentCardResponse ReceivedResponse { get; private set; }

        public TestFileConsentAttributeApp(AgentApplicationOptions options) : base(options) { }

        [FileConsentAcceptRoute]
        public Task OnFileConsentAcceptAsync(
            ITurnContext turnContext,
            ITurnState turnState,
            Microsoft.Teams.Api.FileConsentCardResponse response,
            CancellationToken cancellationToken)
        {
            AcceptHandlerCalled = true;
            ReceivedResponse = response;
            return Task.CompletedTask;
        }

        [FileConsentDeclineRoute]
        public Task OnFileConsentDeclineAsync(
            ITurnContext turnContext,
            ITurnState turnState,
            Microsoft.Teams.Api.FileConsentCardResponse response,
            CancellationToken cancellationToken)
        {
            DeclineHandlerCalled = true;
            ReceivedResponse = response;
            return Task.CompletedTask;
        }
    }
}
