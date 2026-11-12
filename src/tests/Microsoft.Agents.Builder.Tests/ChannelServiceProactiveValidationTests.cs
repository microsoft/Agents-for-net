// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Connector.Types;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Moq;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests
{
    public class ChannelServiceProactiveValidationTests
    {
        [Fact]
        public async Task DirectContinuation_RejectsMissingAudienceForNonAnonymousIdentity()
        {
            var adapter = new ServiceAdapter(new Mock<IChannelServiceClientFactory>().Object);
            var identity = new ClaimsIdentity(new[] { new Claim("iss", "issuer") });
            bool invoked = false;

            await Assert.ThrowsAsync<ArgumentException>(() => adapter.ContinueConversationAsync(identity, CreateReference(), (_, _) =>
            {
                invoked = true;
                return Task.CompletedTask;
            }));

            Assert.False(invoked);
        }

        [Fact]
        public async Task DirectContinuation_RejectsAnonymousByDefault()
        {
            var adapter = new ServiceAdapter(new Mock<IChannelServiceClientFactory>().Object);

            await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.ContinueConversationAsync(new ClaimsIdentity(),
                CreateReference(), (_, _) => Task.CompletedTask));
        }

        [Fact]
        public async Task SendActivity_StillRequiresActivityProtocolServiceUrl()
        {
            var adapter = new ServiceAdapter(new Mock<IChannelServiceClientFactory>().Object);
            var reference = CreateReference();
            reference.ServiceUrl = null;

            await Assert.ThrowsAsync<ArgumentException>(() => Proactive.SendActivityAsync(adapter,
                new Conversation(new ClaimsIdentity(new[] { new Claim("aud", "agent") }), reference), MessageFactory.Text("notification")));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DirectContinuation_DoesNotDowngradeNonAnonymousIdentity(bool authenticated)
        {
            var adapter = new ServiceAdapter(new Mock<IChannelServiceClientFactory>().Object, CreateHostValidator())
            {
                AllowAnonymousProactive = true
            };
            var identity = authenticated
                ? new ClaimsIdentity("validated")
                : new ClaimsIdentity(new[] { new Claim("custom", "value") });

            await Assert.ThrowsAsync<ArgumentException>(() => adapter.ContinueConversationAsync(identity,
                CreateReference(), (_, _) => Task.CompletedTask));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task AnonymousContinuation_RequiresEnabledHostValidation(bool hasValidator)
        {
            var validator = hasValidator ? new OutboundHostValidator(new OutboundHostValidatorOptions()) : null;
            var adapter = new ServiceAdapter(new Mock<IChannelServiceClientFactory>().Object, validator)
            {
                AllowAnonymousProactive = true
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.ContinueConversationAsync(new ClaimsIdentity(),
                CreateReference(), (_, _) => Task.CompletedTask));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task AnonymousContinuation_RejectsDisallowedDestination(bool direct)
        {
            var adapter = new ServiceAdapter(new Mock<IChannelServiceClientFactory>().Object, CreateHostValidator())
            {
                AllowAnonymousProactive = true
            };
            var reference = CreateReference();
            reference.ServiceUrl = "https://untrusted.example";

            await Assert.ThrowsAsync<ArgumentException>(() => direct
                ? adapter.ContinueConversationAsync(new ClaimsIdentity(), reference, (_, _) => Task.CompletedTask)
                : Proactive.SendActivityAsync(adapter, new Conversation(new ClaimsIdentity(), reference), MessageFactory.Text("notification")));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task AnonymousSend_UsesAnonymousConnectorOnlyAfterOptIn(bool stored)
        {
            var factory = new Mock<IChannelServiceClientFactory>();
            var connector = new Mock<IConnectorClient>();
            connector.Setup(c => c.Conversations.SendToConversationAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceResponse("delivered"));
            connector.Setup(c => c.Conversations.ReplyToActivityAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceResponse("delivered"));
            bool? anonymous = null;
            factory.Setup(f => f.CreateConnectorClientAsync(It.IsAny<ITurnContext>(), It.IsAny<string>(),
                    It.IsAny<IList<string>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Callback<ITurnContext, string, IList<string>, bool, CancellationToken>(
                    (_, _, _, useAnonymous, _) => anonymous = useAnonymous)
                .ReturnsAsync(connector.Object);
            var adapter = new ServiceAdapter(factory.Object, CreateHostValidator()) { AllowAnonymousProactive = true };
            var conversation = new Conversation(new ClaimsIdentity(), CreateReference());
            ResourceResponse response;

            if (stored)
            {
                var storage = new MemoryStorage();
                var app = new AgentApplication(new AgentApplicationOptions(storage) { Proactive = new ProactiveOptions(storage) });
                var id = await app.Proactive.StoreConversationAsync(conversation);
                response = await app.Proactive.SendActivityAsync(adapter, id, MessageFactory.Text("notification"));
            }
            else
            {
                response = await Proactive.SendActivityAsync(adapter, conversation, MessageFactory.Text("notification"));
            }

            Assert.Equal("delivered", response.Id);
            Assert.True(anonymous);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DirectCreateConversation_PreservesAuthenticatedAndAnonymousAddressing(bool anonymousDelivery)
        {
            var factory = new Mock<IChannelServiceClientFactory>();
            var connector = new Mock<IConnectorClient>();
            connector.Setup(c => c.Conversations.CreateConversationAsync(It.IsAny<ConversationParameters>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ConversationResourceResponse(id: "created"));
            bool? anonymous = null;
            factory.Setup(f => f.CreateConnectorClientAsync(It.IsAny<ITurnContext>(), It.IsAny<string>(),
                    It.IsAny<IList<string>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Callback<ITurnContext, string, IList<string>, bool, CancellationToken>(
                    (_, _, _, useAnonymous, _) => anonymous = useAnonymous)
                .ReturnsAsync(connector.Object);
            var adapter = new ServiceAdapter(factory.Object, CreateHostValidator()) { AllowAnonymousProactive = anonymousDelivery };
            var identity = anonymousDelivery ? new ClaimsIdentity() : new ClaimsIdentity(new[] { new Claim("aud", "application") });

            var reference = await adapter.CreateConversationAsync(identity, Channels.Test, "http://localhost:3978", null,
                new ConversationParameters { Agent = new ChannelAccount("agent") }, null);

            Assert.Equal("created", reference.Conversation.Id);
            Assert.Equal(anonymousDelivery ? "agent" : "application", reference.Agent.Id);
            Assert.Equal(reference.Agent.Id, reference.User.Id);
            Assert.Equal(anonymousDelivery, anonymous);
        }

        [Fact]
        public async Task AuthenticatedContinuation_DoesNotRetryAnonymouslyWhenCredentialsFail()
        {
            var failure = new InvalidOperationException("Credentials are unavailable.");
            var attempts = new List<bool>();
            var factory = new Mock<IChannelServiceClientFactory>();
            factory.Setup(f => f.CreateConnectorClientAsync(It.IsAny<ITurnContext>(), It.IsAny<string>(),
                    It.IsAny<IList<string>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Callback<ITurnContext, string, IList<string>, bool, CancellationToken>(
                    (_, _, _, useAnonymous, _) => attempts.Add(useAnonymous))
                .ThrowsAsync(failure);
            var adapter = new ServiceAdapter(factory.Object, CreateHostValidator()) { AllowAnonymousProactive = true };
            bool invoked = false;

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.ContinueConversationAsync(
                new ClaimsIdentity(new[] { new Claim("aud", "agent") }), CreateReference(), (_, _) =>
                {
                    invoked = true;
                    return Task.CompletedTask;
                }));

            Assert.Same(failure, exception);
            Assert.Equal(new[] { false }, attempts);
            Assert.False(invoked);
        }

        private static IOutboundHostValidator CreateHostValidator() => new OutboundHostValidator(new OutboundHostValidatorOptions
        {
            Enabled = true,
            IncludeDefaultMicrosoftHosts = false,
            AllowPrivateNetworkAddresses = true,
            Hosts = new[] { "localhost" }
        });

        private static ConversationReference CreateReference() => new()
        {
            ChannelId = Channels.Test,
            Conversation = new ConversationAccount(id: "conversation"),
            Agent = new ChannelAccount("agent"),
            User = new ChannelAccount("user"),
            ServiceUrl = "http://localhost:3978"
        };

        private sealed class ServiceAdapter : ChannelServiceAdapterBase
        {
            public ServiceAdapter(IChannelServiceClientFactory factory, IOutboundHostValidator validator = null)
                : base(factory, null, validator) { }
        }
    }
}
