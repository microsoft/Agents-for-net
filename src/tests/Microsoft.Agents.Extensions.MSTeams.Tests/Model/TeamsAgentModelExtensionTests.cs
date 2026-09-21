// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Teams.Apps.Schema;
using Microsoft.Teams.Apps;
using Microsoft.Agents.Core.Serialization;
using System.Collections.Generic;
using Xunit;

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
