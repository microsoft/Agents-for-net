// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Teams.Apps.Schema;
using Microsoft.Teams.Apps;
using Microsoft.Agents.Core.Serialization;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;
using CoreModels = Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Extensions.MSTeams.Tests.Model
{
    public class TeamsAgentModelExtensionTests
    {
        [Fact]
        public void TeamsActivity_ToCoreActivity_ReturnsCoreActivity()
        {
            // Arrange
            var teamsActivity = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Apps.Schema.TeamsActivity>(
                """{"type":"message","id":"12345"}""");
            // Act
            var coreActivity = teamsActivity.ToCoreActivity();
            // Assert
            Assert.NotNull(coreActivity);
            Assert.Equal(teamsActivity.Id, coreActivity.Id);
        }

        [Fact]
        public void CoreActivity_ToTeamsActivity_ReturnsTeamsActivity()
        {
            // Arrange
            var coreActivity = new Microsoft.Agents.Core.Models.Activity()
            {
                Type = "message",
                Id = "67890",
                Text = "Hello, Core!"
            };
            // Act
            var teamsActivity = coreActivity.ToTeamsActivity();
            // Assert
            Assert.NotNull(teamsActivity);
            Assert.Equal(coreActivity.Id, teamsActivity.Id);

            Assert.IsAssignableFrom<MessageActivity>(teamsActivity);
            var messageActivity = teamsActivity as MessageActivity;
            Assert.Equal(coreActivity.Text, messageActivity.Text);
        }

        [Theory]
        [InlineData("message", null)]
        [InlineData("message", "")]
        [InlineData("message", "Hello, Core!")]
        [InlineData("event", null)]
        public void CoreActivity_ToTeamsActivity_PreservesAddressing(string activityType, string text)
        {
            var source = new CoreModels.Activity
            {
                Type = activityType,
                Id = "activity-id",
                ServiceUrl = "https://smba.trafficmanager.net/amer/",
                ChannelId = CoreModels.Channels.Msteams,
                Conversation = new CoreModels.ConversationAccount
                {
                    Id = "conversation-id",
                    Name = "Chat",
                    IsGroup = true,
                    ConversationType = "groupChat",
                    TenantId = "tenant-id"
                },
                From = new CoreModels.ChannelAccount
                {
                    Id = "user-id",
                    Name = "User",
                    AadObjectId = "user-object-id",
                    Role = "user"
                },
                Recipient = new CoreModels.ChannelAccount { Id = "agent-id", Name = "Agent", Role = "bot" },
                Text = text,
                Properties = new Dictionary<string, JsonElement>
                {
                    ["customProperty"] = JsonSerializer.SerializeToElement(new { key = "value" })
                }
            };

            var converted = source.ToTeamsActivity();

            Assert.Equal("conversation-id", converted.Conversation?.Id);
            Assert.Equal("user-id", converted.From?.Id);
            Assert.Equal("agent-id", converted.Recipient?.Id);
            Assert.Equal(source.ServiceUrl, converted.ServiceUrl?.AbsoluteUri);
            if (activityType == "message")
            {
                Assert.Equal(text, Assert.IsType<MessageActivity>(converted).Text);
            }
            else
            {
                Assert.IsNotType<MessageActivity>(converted);
            }

            var restored = converted.ToCoreActivity();
            Assert.Equal("activity-id", restored.Id);
            Assert.Equal("Chat", restored.Conversation.Name);
            Assert.True(restored.Conversation.IsGroup);
            Assert.Equal("groupChat", restored.Conversation.ConversationType);
            Assert.Equal("tenant-id", restored.Conversation.TenantId);
            Assert.Equal("User", restored.From.Name);
            Assert.Equal("user-object-id", restored.From.AadObjectId);
            Assert.Equal("user", restored.From.Role);
            Assert.Equal("Agent", restored.Recipient.Name);
            Assert.Equal("bot", restored.Recipient.Role);
            using var json = JsonDocument.Parse(ProtocolJsonSerializer.ToJson(converted));
            Assert.Equal("value", json.RootElement.GetProperty("customProperty").GetProperty("key").GetString());
        }

        [Fact]
        public void TeamsMessageActivity_ToCoreActivity_ReturnsCoreActivity()
        {
            // Arrange
            var teamsActivity = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Apps.MessageActivity>(
                """{"type":"message","id":"12345","text":"Hello, Teams!","customProperty":{"key1":"value1"}}""");
            // Act
            var coreActivity = teamsActivity.ToCoreActivity();
            // Assert
            Assert.NotNull(coreActivity);
            Assert.Equal(teamsActivity.Id, coreActivity.Id);
            Assert.Equal(teamsActivity.Text, coreActivity.Text);
            Assert.True(coreActivity.Properties.ContainsKey("customProperty"));
        }

        class Test
        {
            public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
        }
    }
}
