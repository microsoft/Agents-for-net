// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
#pragma warning disable CS0618 // Exercise the retained public compatibility APIs.
    public class ProactiveValidationCompatibilityTests
    {
        [Fact]
        public void ConversationValidation_PreservesLegacyAudienceClaimPresenceCheck()
        {
            var reference = CreateReference();

            new Conversation(new Dictionary<string, string> { ["aud"] = "" }, reference).Validate();

            Assert.Throws<ArgumentException>(() => new Conversation(new Dictionary<string, string>(), reference).Validate());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ConversationValidation_PreservesOptionalConversationId(bool validateConversation)
        {
            var reference = CreateReference();
            reference.Conversation = null;
            var conversation = new Conversation(new Dictionary<string, string> { ["aud"] = "agent" }, reference);

            if (validateConversation)
            {
                Assert.Throws<ArgumentException>(() => reference.Validate());
                Assert.Throws<ArgumentException>(() => conversation.Validate());
            }
            else
            {
                reference.Validate(validateConversation: false);
                conversation.Validate(validateConversation: false);
            }
        }

        [Fact]
        public void CreateValidation_PreservesLegacyScopeRequirementWithoutRequiringIdentity()
        {
            var options = new CreateConversationOptions
            {
                ChannelId = Channels.Test,
                ServiceUrl = "https://channel.example",
                Parameters = new ConversationParameters { Agent = new ChannelAccount("agent") }
            };

            options.Validate();

            options.Scope = null;
            Assert.Throws<ArgumentException>(() => options.Validate());
        }

        [Fact]
        public void ParametersValidation_StillRequiresAgentId()
        {
            var parameters = new ConversationParameters { Agent = new ChannelAccount("agent") };

            parameters.Validate();

            parameters.Agent.Id = null;
            Assert.Throws<ArgumentException>(() => parameters.Validate());
        }

        private static ConversationReference CreateReference() => new()
        {
            ChannelId = Channels.Test,
            ServiceUrl = "https://channel.example",
            Agent = new ChannelAccount("agent"),
            User = new ChannelAccount("user"),
            Conversation = new ConversationAccount(id: "conversation")
        };
    }
#pragma warning restore CS0618
}
