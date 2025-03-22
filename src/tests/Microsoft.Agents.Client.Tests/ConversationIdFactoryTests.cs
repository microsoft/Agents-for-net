// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Client.Tests
{
    public class ConversationIdFactoryTests
    {
        private const string ServiceUrl = "http://testbot.com/api/messages";
        private const string SkillId = "skill";

        private readonly IConversationIdFactory _conversationIdFactory = new ConversationIdFactory(new MemoryStorage());
        private readonly string _applicationId = Guid.NewGuid().ToString(format: "N");
        private readonly string _botId = Guid.NewGuid().ToString(format: "N");

        [Fact]
        public async Task SkillConversationIdFactoryHappyPath()
        {
            var conversationReference = BuildConversationReference();

            // Create skill conversation
            var skillConversationId = await _conversationIdFactory.CreateConversationIdAsync(
                options: new ConversationIdFactoryOptions
                {
                    Activity = BuildMessageActivity(conversationReference),
                    Agent = this.BuildBotFrameworkSkill(),
                    FromClientId = _botId,
                    FromOAuthScope = _botId,
                },
                cancellationToken: CancellationToken.None);
            
            Assert.False(string.IsNullOrEmpty(skillConversationId), "Expected a valid skill conversation ID to be created");

            // Retrieve skill conversation
            var retrievedConversationReference = await _conversationIdFactory.GetAgentConversationReferenceAsync(skillConversationId, CancellationToken.None);

            // Delete
            await _conversationIdFactory.DeleteConversationReferenceAsync(skillConversationId, CancellationToken.None);

            // Retrieve again
            var deletedConversationReference = await _conversationIdFactory.GetAgentConversationReferenceAsync(skillConversationId, CancellationToken.None);

            Assert.NotNull(retrievedConversationReference);
            Assert.NotNull(retrievedConversationReference.ConversationReference);
            Assert.Equal(conversationReference, retrievedConversationReference.ConversationReference, new ConversationReferenceEqualityComparer());
            Assert.Null(deletedConversationReference);
        }

        [Fact]
        public async Task IdIsUniqueEachTime()
        {
            var conversationReference = BuildConversationReference();

            // Create skill conversation
            var firstId = await _conversationIdFactory.CreateConversationIdAsync(
                options: new ConversationIdFactoryOptions
                {
                    Activity = BuildMessageActivity(conversationReference),
                    Agent = this.BuildBotFrameworkSkill(),
                    FromClientId = _botId,
                    FromOAuthScope = _botId,
                },
                cancellationToken: CancellationToken.None);
            
            var secondId = await _conversationIdFactory.CreateConversationIdAsync(
                options: new ConversationIdFactoryOptions
                {
                    Activity = BuildMessageActivity(conversationReference),
                    Agent = this.BuildBotFrameworkSkill(),
                    FromClientId = _botId,
                    FromOAuthScope = _botId,
                },
                cancellationToken: CancellationToken.None);

            // Ensure that we get a different conversationId each time we call CreateSkillConversationIdAsync
            Assert.NotEqual(firstId, secondId);
        }

        private static ConversationReference BuildConversationReference()
        {
            return new ConversationReference
            {
                Conversation = new ConversationAccount(id: Guid.NewGuid().ToString("N")),
                ServiceUrl = ServiceUrl
            };
        }

        private static Activity BuildMessageActivity(ConversationReference conversationReference)
        {
            if (conversationReference == null)
            {
                throw new ArgumentNullException(nameof(conversationReference));
            }

            var activity = (Activity)Activity.CreateMessageActivity();
            activity.ApplyConversationReference(conversationReference);

            return activity;
        }

        private IAgentInfo BuildBotFrameworkSkill()
        {
            return new BotFrameworkSkill
            {
                Name = SkillId
            };
        }

		private class BotFrameworkSkill : IAgentInfo
		{
			public string Alias { get; set; }
			public string DisplayName { get; set; }
            public string Name { get; set; }
        }

		private class ConversationReferenceEqualityComparer : EqualityComparer<ConversationReference>
        {
            public override bool Equals(ConversationReference x, ConversationReference y)
            {
                if (x == null && y == null)
                {
                    return true;
                }

                if (x == null || y == null)
                {
                    return false;
                }

                return x.Conversation.Id.Equals(y.Conversation?.Id, StringComparison.Ordinal) && x.ServiceUrl.Equals(y.ServiceUrl, StringComparison.Ordinal);
            }

            public override int GetHashCode(ConversationReference obj)
            {
                return (obj.ServiceUrl.GetHashCode() ^ obj.Conversation.Id.GetHashCode()).GetHashCode();
            }
        }
    }
}
