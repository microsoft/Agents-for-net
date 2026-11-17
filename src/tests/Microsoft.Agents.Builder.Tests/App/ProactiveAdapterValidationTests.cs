// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Moq;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class ProactiveAdapterValidationTests
    {
        [Fact]
        public async Task SendActivity_DoesNotRequireActivityProtocolCredentialsOrServiceUrl()
        {
            var adapter = new LocalAdapter();
            var conversation = CreateConversation();

            var response = await Proactive.SendActivityAsync(adapter, conversation, MessageFactory.Text("notification"));

            Assert.Equal("notification", adapter.SentActivity.Text);
            Assert.Equal("local-task", adapter.SentActivity.Conversation.Id);
            Assert.Equal("sent-id", response.Id);
        }

        [Fact]
        public async Task ContinueConversation_DoesNotRequireActivityProtocolCredentialsOrServiceUrl()
        {
            var adapter = new LocalAdapter();
            var app = new AgentApplication(new AgentApplicationOptions(new MemoryStorage()));
            bool handled = false;

            await app.Proactive.ContinueConversationAsync(adapter, CreateConversation(), (context, state, ct) =>
            {
                Assert.Equal("local-task", context.Activity.Conversation.Id);
                Assert.Empty(context.Identity.Claims);
                handled = true;
                return Task.CompletedTask;
            });

            Assert.True(handled);
        }

        [Fact]
        public async Task CreateConversation_DoesNotRequireActivityProtocolServiceUrlOrScope()
        {
            var adapter = new LocalAdapter();
            var app = new AgentApplication(new AgentApplicationOptions(new MemoryStorage()));

            var conversation = await app.Proactive.CreateConversationAsync(adapter, new CreateConversationOptions
            {
                Identity = new ClaimsIdentity(),
                ChannelId = "local",
                Scope = null,
                Audience = null
            });

            Assert.Equal("local-task", conversation.Reference.Conversation.Id);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task AdapterValidation_CannotBeBypassedByDirectContinuation(bool direct)
        {
            var adapter = new RejectingAdapter();
            var conversation = CreateConversation();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => direct
                ? adapter.ContinueConversationAsync(conversation.Identity, conversation.Reference, (_, _) => Task.CompletedTask)
                : Proactive.SendActivityAsync(adapter, conversation, MessageFactory.Text("notification")));

            Assert.Equal("Continuation is not allowed.", exception.Message);
            Assert.Null(adapter.SentActivity);
        }

        [Fact]
        public async Task CreateConversation_UsesAdapterValidation()
        {
            var app = new AgentApplication(new AgentApplicationOptions(new MemoryStorage()));

            await Assert.ThrowsAsync<NotSupportedException>(() => app.Proactive.CreateConversationAsync(new RejectingAdapter(),
                new CreateConversationOptions { Identity = new ClaimsIdentity(), ChannelId = "local" }));
        }

        [Fact]
        public async Task LegacyAdapter_RetainsActivityProtocolValidation()
        {
            var adapter = new Mock<IChannelAdapter>().Object;

            await Assert.ThrowsAsync<ArgumentException>(() =>
                Proactive.SendActivityAsync(adapter, CreateConversation(), MessageFactory.Text("notification")));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task MissingConversationId_IsRejectedBeforeContinuation(bool direct)
        {
            var adapter = new LocalAdapter();
            var conversation = CreateConversation();
            conversation.Reference.Conversation.Id = null;

            await Assert.ThrowsAsync<ArgumentException>(() => direct
                ? adapter.ContinueConversationAsync(conversation.Identity, conversation.Reference, (_, _) => Task.CompletedTask)
                : Proactive.SendActivityAsync(adapter, conversation, MessageFactory.Text("notification")));

            Assert.Null(adapter.SentActivity);
        }

        private static Conversation CreateConversation() => new(new ClaimsIdentity(), new ConversationReference
        {
            ChannelId = "local",
            Conversation = new ConversationAccount(id: "local-task"),
            Agent = new ChannelAccount("agent"),
            User = new ChannelAccount("user")
        });

        private class LocalAdapter : ChannelAdapter
        {
            public IActivity SentActivity { get; private set; }

            public override Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext context, IActivity[] activities, CancellationToken cancellationToken)
            {
                SentActivity = activities[0];
                return Task.FromResult(new[] { new ResourceResponse("sent-id") });
            }

            public override Task<ConversationReference> CreateConversationAsync(ClaimsIdentity identity, string channelId, string serviceUrl,
                string audience, ConversationParameters parameters, AgentCallbackHandler callback, CancellationToken cancellationToken = default)
                => Task.FromResult(CreateConversation().Reference);
        }

        private sealed class RejectingAdapter : LocalAdapter
        {
            public override void ValidateConversation(ClaimsIdentity identity, ConversationReference reference)
                => throw new InvalidOperationException("Continuation is not allowed.");

            public override void ValidateCreateConversation(CreateConversationOptions options)
                => throw new NotSupportedException();
        }
    }
}
