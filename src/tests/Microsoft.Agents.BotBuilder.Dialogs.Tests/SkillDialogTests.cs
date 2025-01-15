// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.BotBuilder.Testing;
using Moq;
using Xunit;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Client;
using Microsoft.Agents.Storage;
using System.Text.Json;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.State;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core;

namespace Microsoft.Agents.BotBuilder.Dialogs.Tests
{
    public class SkillDialogTests
    {
        [Fact]
        public void ConstructorValidationTests()
        {
            Assert.Throws<ArgumentNullException>(() => { new SkillDialog(null); });
        }

        [Fact]
        public async Task BeginDialogOptionsValidation()
        {
            var dialogOptions = new SkillDialogOptions();
            var sut = new SkillDialog(dialogOptions);
            var client = new DialogTestClient(Channels.Test, sut);
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await client.SendActivityAsync<Activity>("irrelevant"));

            client = new DialogTestClient(Channels.Test, sut, new Dictionary<string, string>());
            await Assert.ThrowsAsync<ArgumentException>(async () => await client.SendActivityAsync<Activity>("irrelevant"));

            client = new DialogTestClient(Channels.Test, sut, new BeginSkillDialogOptions());
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await client.SendActivityAsync<Activity>("irrelevant"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData(DeliveryModes.ExpectReplies)]
        public async Task BeginDialogCallsSkill(string deliveryMode)
        {
            IActivity activitySent = null;
            string toBotIdSent = null;
            Uri toUriSent = null;

            // Callback to capture the parameters sent to the skill
            void CaptureAction(string toBotId, string toBotResource, Uri toUri, Uri serviceUrl, string conversationId, IActivity activity, CancellationToken cancellationToken)
            {
                // Capture values sent to the skill so we can assert the right parameters were used.
                toBotIdSent = toBotId;
                toUriSent = toUri;
                activitySent = activity;
            }

            // Create a mock skill client to intercept calls and capture what is sent.
            var mockSkillClient = CreateMockSkillClient(CaptureAction);

            // Use Memory for conversation state
            var conversationState = new ConversationState(new MemoryStorage());
            var dialogOptions = CreateSkillDialogOptions(conversationState, mockSkillClient);

            // Create the SkillDialogInstance and the activity to send.
            var sut = new SkillDialog(dialogOptions);
            var activityToSend = (Activity)Activity.CreateMessageActivity();
            activityToSend.DeliveryMode = deliveryMode;
            activityToSend.Text = Guid.NewGuid().ToString();
            var client = new DialogTestClient(Channels.Test, sut, new BeginSkillDialogOptions { Activity = activityToSend }, conversationState: conversationState);

            Assert.Equal(0, ((SimpleConversationIdFactory)dialogOptions.ConversationIdFactory).CreateCount);
            
            // Send something to the dialog to start it
            await client.SendActivityAsync<Activity>("irrelevant");

            // Assert results and data sent to the SkillClient for fist turn
            Assert.Equal(1, ((SimpleConversationIdFactory)dialogOptions.ConversationIdFactory).CreateCount);
            Assert.Equal(dialogOptions.Skill.AppId, toBotIdSent);
            Assert.Equal(dialogOptions.Skill.Endpoint.ToString(), toUriSent.ToString());
            Assert.Equal(activityToSend.Text, activitySent.Text);
            Assert.Equal(DialogTurnStatus.Waiting, client.DialogTurnResult.Status);

            // Send a second message to continue the dialog
            await client.SendActivityAsync<Activity>("Second message");
            Assert.Equal(1, ((SimpleConversationIdFactory)dialogOptions.ConversationIdFactory).CreateCount);

            // Assert results for second turn
            Assert.Equal("Second message", activitySent.Text);
            Assert.Equal(DialogTurnStatus.Waiting, client.DialogTurnResult.Status);

            // Send EndOfConversation to the dialog
            await client.SendActivityAsync<Activity>((Activity)Activity.CreateEndOfConversationActivity());

            // Assert we are done.
            Assert.Equal(DialogTurnStatus.Complete, client.DialogTurnResult.Status);
        }

        [Fact]
        public async Task ShouldHandleInvokeActivities()
        {
            IActivity activitySent = null;
            string toBotIdSent = null;
            Uri toUriSent = null;

            // Callback to capture the parameters sent to the skill
            void CaptureAction(string toBotId, string toBotResource, Uri toUri, Uri serviceUrl, string conversationId, IActivity activity, CancellationToken cancellationToken)
            {
                // Capture values sent to the skill so we can assert the right parameters were used.
                toBotIdSent = toBotId;
                toUriSent = toUri;
                activitySent = activity;
            }

            // Create a mock skill client to intercept calls and capture what is sent.
            var mockSkillClient = CreateMockSkillClient(CaptureAction);

            // Use Memory for conversation state
            var conversationState = new ConversationState(new MemoryStorage());
            var dialogOptions = CreateSkillDialogOptions(conversationState, mockSkillClient);

            // Create the SkillDialogInstance and the activity to send.
            var activityToSend = Activity.CreateInvokeActivity();
            activityToSend.Name = Guid.NewGuid().ToString();
            var sut = new SkillDialog(dialogOptions);
            var client = new DialogTestClient(Channels.Test, sut, new BeginSkillDialogOptions { Activity = activityToSend }, conversationState: conversationState);

            // Send something to the dialog to start it
            await client.SendActivityAsync<Activity>("irrelevant");

            // Assert results and data sent to the SkillClient for fist turn
            Assert.Equal(dialogOptions.Skill.AppId, toBotIdSent);
            Assert.Equal(dialogOptions.Skill.Endpoint.ToString(), toUriSent.ToString());
            Assert.Equal(activityToSend.Name, activitySent.Name);
            Assert.Equal(DeliveryModes.ExpectReplies, activitySent.DeliveryMode);
            Assert.Equal(DialogTurnStatus.Waiting, client.DialogTurnResult.Status);

            // Send a second message to continue the dialog
            await client.SendActivityAsync<Activity>("Second message");

            // Assert results for second turn
            Assert.Equal("Second message", activitySent.Text);
            Assert.Equal(DialogTurnStatus.Waiting, client.DialogTurnResult.Status);

            // Send EndOfConversation to the dialog
            await client.SendActivityAsync<Activity>((Activity)Activity.CreateEndOfConversationActivity());

            // Assert we are done.
            Assert.Equal(DialogTurnStatus.Complete, client.DialogTurnResult.Status);
        }

        [Fact]
        public async Task CancelDialogSendsEoC()
        {
            IActivity activitySent = null;

            // Callback to capture the parameters sent to the skill
            void CaptureAction(string toBotId, string toBotResource, Uri toUri, Uri serviceUrl, string conversationId, IActivity activity, CancellationToken cancellationToken)
            {
                // Capture values sent to the skill so we can assert the right parameters were used.
                activitySent = activity;
            }

            // Create a mock skill client to intercept calls and capture what is sent.
            var mockSkillClient = CreateMockSkillClient(CaptureAction);

            // Use Memory for conversation state
            var conversationState = new ConversationState(new MemoryStorage());
            var dialogOptions = CreateSkillDialogOptions(conversationState, mockSkillClient);

            // Create the SkillDialogInstance and the activity to send.
            var sut = new SkillDialog(dialogOptions);
            var activityToSend = (Activity)Activity.CreateMessageActivity();
            activityToSend.Text = Guid.NewGuid().ToString();
            var client = new DialogTestClient(Channels.Test, sut, new BeginSkillDialogOptions { Activity = activityToSend }, conversationState: conversationState);

            // Send something to the dialog to start it
            await client.SendActivityAsync<Activity>("irrelevant");

            // Cancel the dialog so it sends an EoC to the skill
            await client.DialogContext.CancelAllDialogsAsync(CancellationToken.None);

            Assert.Equal(ActivityTypes.EndOfConversation, activitySent.Type);
        }

        [Fact]
        public async Task ShouldThrowHttpExceptionOnPostFailure()
        {
            // Create a mock skill client to intercept calls and capture what is sent.
            var mockSkillClient = CreateMockSkillClient(null, 500);

            // Use Memory for conversation state
            var conversationState = new ConversationState(new MemoryStorage());
            var dialogOptions = CreateSkillDialogOptions(conversationState, mockSkillClient);

            // Create the SkillDialogInstance and the activity to send.
            var sut = new SkillDialog(dialogOptions);
            var activityToSend = (Activity)Activity.CreateMessageActivity();
            activityToSend.Text = Guid.NewGuid().ToString();
            var client = new DialogTestClient(Channels.Test, sut, new BeginSkillDialogOptions { Activity = activityToSend }, conversationState: conversationState);

            // Send something to the dialog 
            await Assert.ThrowsAsync<HttpRequestException>(async () => await client.SendActivityAsync<Activity>("irrelevant"));
        }

        [Fact]
        public async Task ShouldInterceptOAuthCardsForSso()
        {
            var connectionName = "connectionName";
            var firstResponse = new ExpectedReplies(new List<IActivity> { CreateOAuthCardAttachmentActivity("https://test") });
            var mockSkillClient = new Mock<IChannel>();
            mockSkillClient
                .SetupSequence(x => x.PostActivityAsync<ExpectedReplies>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<Activity>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new InvokeResponse<ExpectedReplies>
                {
                    Status = 200,
                    Body = firstResponse
                }))
                .Returns(Task.FromResult(new InvokeResponse<ExpectedReplies> { Status = 200 }));

            var mockUserTokenClient = new Mock<IUserTokenClient>();
            mockUserTokenClient
                .SetupSequence(x => x.ExchangeTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TokenExchangeRequest>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new TokenResponse()
                {
                    ChannelId = Channels.Test,
                    ConnectionName = connectionName,
                    Token = "https://test1"
                }));

            var conversationState = new ConversationState(new MemoryStorage());
            var testAdapter = new TestAdapter(Channels.Test)
                .Use(new AutoSaveStateMiddleware(conversationState));

            var dialogOptions = CreateSkillDialogOptions(conversationState, mockSkillClient, connectionName);
            var sut = new SkillDialog(dialogOptions);
            var activityToSend = CreateSendActivity();
            var client = new DialogTestClient(testAdapter, sut, new BeginSkillDialogOptions { Activity = activityToSend }, conversationState: conversationState);
            testAdapter.AddExchangeableToken(connectionName, Channels.Test, "user1", "https://test", "https://test1");
            var finalActivity = await client.SendActivityAsync<Activity>("irrelevant");
            Assert.Null(finalActivity);
        }

        [Fact]
        public async Task ShouldNotInterceptOAuthCardsForEmptyConnectionName()
        {
            var connectionName = "connectionName";
            var firstResponse = new ExpectedReplies(new List<IActivity> { CreateOAuthCardAttachmentActivity("https://test") });
            var mockSkillClient = new Mock<IChannel>();
            mockSkillClient
                .Setup(x => x.PostActivityAsync<ExpectedReplies>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<Activity>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new InvokeResponse<ExpectedReplies>
                {
                    Status = 200,
                    Body = firstResponse
                }));

            var conversationState = new ConversationState(new MemoryStorage());
            var dialogOptions = CreateSkillDialogOptions(conversationState, mockSkillClient);

            var sut = new SkillDialog(dialogOptions);
            var activityToSend = CreateSendActivity();
            var testAdapter = new TestAdapter(Channels.Test)
                .Use(new AutoSaveStateMiddleware(conversationState));
            var client = new DialogTestClient(testAdapter, sut, new BeginSkillDialogOptions { Activity = activityToSend }, conversationState: conversationState);
            testAdapter.AddExchangeableToken(connectionName, Channels.Test, "user1", "https://test", "https://test1");
            var finalActivity = await client.SendActivityAsync<Activity>("irrelevant");
            Assert.NotNull(finalActivity);
            Assert.Single(finalActivity.Attachments);
        }

        [Fact]
        public async Task ShouldNotInterceptOAuthCardsForEmptyToken()
        {
            var firstResponse = new ExpectedReplies(new List<IActivity> { CreateOAuthCardAttachmentActivity("https://test") });
            var mockSkillClient = new Mock<IChannel>();
            mockSkillClient
                .Setup(x => x.PostActivityAsync<ExpectedReplies>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<Activity>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new InvokeResponse<ExpectedReplies>
                {
                    Status = 200,
                    Body = firstResponse
                }));

            var conversationState = new ConversationState(new MemoryStorage());
            var dialogOptions = CreateSkillDialogOptions(conversationState, mockSkillClient);

            var sut = new SkillDialog(dialogOptions);
            var activityToSend = CreateSendActivity();
            var testAdapter = new TestAdapter(Channels.Test)
                .Use(new AutoSaveStateMiddleware(conversationState));
            var client = new DialogTestClient(testAdapter, sut, new BeginSkillDialogOptions { Activity = activityToSend }, conversationState: conversationState);

            // Don't add exchangeable token to test adapter
            var finalActivity = await client.SendActivityAsync<Activity>("irrelevant");
            Assert.NotNull(finalActivity);
            Assert.Single(finalActivity.Attachments);
        }

        [Fact]
        public async Task ShouldNotInterceptOAuthCardsForTokenException()
        {
            var connectionName = "connectionName";
            var firstResponse = new ExpectedReplies(new List<IActivity> { CreateOAuthCardAttachmentActivity("https://test") });
            var mockSkillClient = new Mock<IChannel>();
            mockSkillClient
                .Setup(x => x.PostActivityAsync<ExpectedReplies>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<Activity>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new InvokeResponse<ExpectedReplies>
                {
                    Status = 200,
                    Body = firstResponse
                }));

            var conversationState = new ConversationState(new MemoryStorage());
            var dialogOptions = CreateSkillDialogOptions(conversationState, mockSkillClient, connectionName);

            var sut = new SkillDialog(dialogOptions);
            var activityToSend = CreateSendActivity();
            var testAdapter = new TestAdapter(Channels.Test)
                .Use(new AutoSaveStateMiddleware(conversationState));
            var initialDialogOptions = new BeginSkillDialogOptions { Activity = activityToSend };
            var client = new DialogTestClient(testAdapter, sut, initialDialogOptions, conversationState: conversationState);
            testAdapter.ThrowOnExchangeRequest(connectionName, Channels.Test, "user1", "https://test");
            var finalActivity = await client.SendActivityAsync<Activity>("irrelevant");
            Assert.NotNull(finalActivity);
            Assert.Single(finalActivity.Attachments);
        }

        [Fact]
        public async Task ShouldNotInterceptOAuthCardsForBadRequest()
        {
            var connectionName = "connectionName";
            var firstResponse = new ExpectedReplies(new List<IActivity> { CreateOAuthCardAttachmentActivity("https://test") });
            var mockSkillClient = new Mock<IChannel>();
            mockSkillClient
                .SetupSequence(x => x.PostActivityAsync<ExpectedReplies>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<Activity>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new InvokeResponse<ExpectedReplies>
                {
                    Status = 200,
                    Body = firstResponse
                }))
                .Returns(Task.FromResult(new InvokeResponse<ExpectedReplies> { Status = 409 }));

            var conversationState = new ConversationState(new MemoryStorage());
            var dialogOptions = CreateSkillDialogOptions(conversationState, mockSkillClient, connectionName);

            var sut = new SkillDialog(dialogOptions);
            var activityToSend = CreateSendActivity();
            var testAdapter = new TestAdapter(Channels.Test)
                .Use(new AutoSaveStateMiddleware(conversationState));
            var client = new DialogTestClient(testAdapter, sut, new BeginSkillDialogOptions { Activity = activityToSend }, conversationState: conversationState);
            testAdapter.AddExchangeableToken(connectionName, Channels.Test, "user1", "https://test", "https://test1");
            var finalActivity = await client.SendActivityAsync<Activity>("irrelevant");
            Assert.NotNull(finalActivity);
            Assert.Single(finalActivity.Attachments);
        }

        [Fact]
        public async Task EndOfConversationFromExpectRepliesCallsDeleteConversationReferenceAsync()
        {
            IActivity activitySent = null;

            // Callback to capture the parameters sent to the skill
            void CaptureAction(string toBotId, string toBotResource, Uri toUri, Uri serviceUrl, string conversationId, IActivity activity, CancellationToken cancellationToken)
            {
                // Capture values sent to the skill so we can assert the right parameters were used.
                activitySent = activity;
            }

            // Create a mock skill client to intercept calls and capture what is sent.
            var mockSkillClientx = CreateMockSkillClient(CaptureAction);

            var eoc = Activity.CreateEndOfConversationActivity() as Activity;
            var expectedReplies = new List<IActivity>();
            expectedReplies.Add(eoc);

            // Create a mock skill client to intercept calls and capture what is sent.
            var mockSkillClient = CreateMockSkillClient(CaptureAction, expectedReplies: expectedReplies);

            // Use Memory for conversation state
            var conversationState = new ConversationState(new MemoryStorage());
            var dialogOptions = CreateSkillDialogOptions(conversationState, mockSkillClient);

            // Create the SkillDialogInstance and the activity to send.
            var sut = new SkillDialog(dialogOptions);
            var activityToSend = (Activity)Activity.CreateMessageActivity();
            activityToSend.DeliveryMode = DeliveryModes.ExpectReplies;
            activityToSend.Text = Guid.NewGuid().ToString();
            var client = new DialogTestClient(Channels.Test, sut, new BeginSkillDialogOptions { Activity = activityToSend }, conversationState: conversationState);

            // Send something to the dialog to start it
            await client.SendActivityAsync<Activity>("hello");

            Assert.Empty((dialogOptions.ConversationIdFactory as SimpleConversationIdFactory).ConversationRefs);
            Assert.Equal(1, (dialogOptions.ConversationIdFactory as SimpleConversationIdFactory).CreateCount);
        }

        private static IActivity CreateOAuthCardAttachmentActivity(string uri)
        {
            var oauthCard = new OAuthCard { TokenExchangeResource = new TokenExchangeResource { Uri = uri } };
            var attachment = new Attachment
            {
                ContentType = OAuthCard.ContentType,
                Content = JsonSerializer.SerializeToNode(oauthCard, ProtocolJsonSerializer.SerializationOptions)
            };

            var attachmentActivity = MessageFactory.Attachment(attachment);
            attachmentActivity.Conversation = new ConversationAccount { Id = Guid.NewGuid().ToString() };
            attachmentActivity.From = new ChannelAccount("blah", "name");

            return attachmentActivity;
        }

        /// <summary>
        /// Helper to create a <see cref="SkillDialogOptions"/> for the skillDialog.
        /// </summary>
        /// <param name="conversationState"> The conversation state object.</param>
        /// <param name="mockSkillClient"> The skill client mock.</param>
        /// <returns> A Skill Dialog Options object.</returns>
        private static SkillDialogOptions CreateSkillDialogOptions(ConversationState conversationState, Mock<IChannel> mockSkillClient, string connectionName = null)
        {
            var dialogOptions = new SkillDialogOptions
            {
                BotId = Guid.NewGuid().ToString(),
                SkillHostEndpoint = new Uri("http://test.contoso.com/skill/messages"),
                ConversationIdFactory = new SimpleConversationIdFactory(),
                ConversationState = conversationState,
                SkillClient = mockSkillClient.Object,
                Skill = new TestBotInfo
                {
                    AppId = Guid.NewGuid().ToString(),
                    Endpoint = new Uri("http://testskill.contoso.com/api/messages")
                },
                ConnectionName = connectionName
            };
            return dialogOptions;
        }

        private class TestBotInfo : IChannelInfo
        {
            public string Id { get; set; }
            public string AppId { get; set; }
            public string ResourceUrl { get; set; }
            public Uri Endpoint { get; set; }
            public string TokenProvider { get; set; }
            public string ChannelFactory { get; set; }
        }

        private static Mock<IChannel> CreateMockSkillClient(Action<string, string, Uri, Uri, string, IActivity, CancellationToken> captureAction, int returnStatus = 200, IList<IActivity> expectedReplies = null)
        {
            var mockSkillClient = new Mock<IChannel>();
            var activityList = new ExpectedReplies(expectedReplies ?? new List<IActivity> { MessageFactory.Text("dummy activity") });

            if (captureAction != null)
            {
                mockSkillClient
                    .Setup(x => x.PostActivityAsync<ExpectedReplies>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(new InvokeResponse<ExpectedReplies>
                    {
                        Status = returnStatus,
                        Body = activityList
                    }))
                    .Callback(captureAction);
            }
            else
            {
                mockSkillClient
                    .Setup(x => x.PostActivityAsync<ExpectedReplies>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(new InvokeResponse<ExpectedReplies>
                    {
                        Status = returnStatus,
                        Body = activityList
                    }));
            }

            return mockSkillClient;
        }

        private Activity CreateSendActivity()
        {
            var activityToSend = (Activity)Activity.CreateMessageActivity();
            activityToSend.DeliveryMode = DeliveryModes.ExpectReplies;
            activityToSend.Text = Guid.NewGuid().ToString();
            return activityToSend;
        }

        // Simple conversation ID factory for testing.
        private class SimpleConversationIdFactory : IConversationIdFactory
        {
            public SimpleConversationIdFactory()
            {
                ConversationRefs = new ConcurrentDictionary<string, BotConversationReference>();
            }

            public ConcurrentDictionary<string, BotConversationReference> ConversationRefs { get; private set; }

            // Helper property to assert how many times is CreateSkillConversationIdAsync called.
            public int CreateCount { get; private set; }

            public Task<string> CreateConversationIdAsync(ConversationIdFactoryOptions options, CancellationToken cancellationToken)
            {
                CreateCount++;

                var key = (options.Activity.Conversation.Id + options.Activity.ServiceUrl).GetHashCode().ToString(CultureInfo.InvariantCulture);
                ConversationRefs.GetOrAdd(key, new BotConversationReference
                {
                    ConversationReference = options.Activity.GetConversationReference(),
                    OAuthScope = options.FromBotOAuthScope
                });
                return Task.FromResult(key);
            }

            public Task<BotConversationReference> GetBotConversationReferenceAsync(string skillConversationId, CancellationToken cancellationToken)
            {
                return Task.FromResult(ConversationRefs[skillConversationId]);
            }

            public Task DeleteConversationReferenceAsync(string skillConversationId, CancellationToken cancellationToken)
            {
                ConversationRefs.TryRemove(skillConversationId, out _);
                return Task.CompletedTask;
            }
        }
    }
}
