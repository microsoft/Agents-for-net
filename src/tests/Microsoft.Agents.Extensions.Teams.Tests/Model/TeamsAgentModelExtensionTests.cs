// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Teams.Api.Activities;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class TeamsAgentModelExtensionTests
    {
        [Fact]
        public void TeamsActivity_ToCoreActivity_ReturnsCoreActivity()
        {
            // Arrange
            var teamsActivity = new Microsoft.Teams.Api.Activities.Activity(Microsoft.Teams.Api.Activities.ActivityType.Message)
            {
                Id = "12345",
            };
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
            var t = new Test();
            t.Properties["key1"] = "value1";

            // Arrange
            var teamsActivity = new Microsoft.Teams.Api.Activities.MessageActivity
            {
                Id = "12345",
                Text = "Hello, Teams!",
                Properties =
                {
                    ["customProperty"] = t
                }
            };
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
