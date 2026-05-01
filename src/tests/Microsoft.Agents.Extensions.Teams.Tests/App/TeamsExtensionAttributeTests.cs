// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Extensions.Teams.Tests.Model;
using Moq;
using System.Net.Http;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.App
{
    public class TeamsExtensionAttributeTests
    {
        private static AgentApplicationOptions CreateOptions()
        {
            var adapter = new NotImplementedAdapter();
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(new Builder.TurnContext(adapter, new Core.Models.Activity()));
            return new AgentApplicationOptions(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            };
        }

        [Fact]
        public void TeamExtensionAttribute_HasCorrectAttributeUsage()
        {
            var attr = System.Attribute.GetCustomAttribute(
                typeof(TeamsExtensionAttribute),
                typeof(System.AttributeUsageAttribute)) as System.AttributeUsageAttribute;

            Assert.NotNull(attr);
            Assert.Equal(System.AttributeTargets.Class, attr.ValidOn);
            Assert.False(attr.Inherited);
        }

        [Fact]
        public void Teams_Property_ReturnsTeamsAgentExtension()
        {
            var app = new TestTeamsExtensionAgent(CreateOptions());

            Assert.NotNull(app.TeamsExtension);
            Assert.IsType<TeamsAgentExtension>(app.TeamsExtension);
        }

        [Fact]
        public void Teams_Extension_IsRegistered_EagerlyAtConstruction()
        {
            // Extension must be registered during construction — before the Teams property
            // is ever accessed — so that its OnBeforeTurn handler runs on every turn even
            // when the agent uses attribute-based routing and never touches Teams directly.
            var app = new TestTeamsExtensionAgent(CreateOptions());

            Assert.Single(app.RegisteredExtensions);
            Assert.IsType<TeamsAgentExtension>(app.RegisteredExtensions[0]);
        }

        [Fact]
        public void Teams_Property_RegistersExtensionWithApplication()
        {
            var app = new TestTeamsExtensionAgent(CreateOptions());

            Assert.Single(app.RegisteredExtensions);
            Assert.IsType<TeamsAgentExtension>(app.RegisteredExtensions[0]);
        }

        [Fact]
        public void Teams_Property_ReturnsSameInstanceOnMultipleAccesses()
        {
            var app = new TestTeamsExtensionAgent(CreateOptions());

            var first = app.TeamsExtension;
            var second = app.TeamsExtension;

            Assert.Same(first, second);
        }
    }

    [TeamsExtension]
    public partial class TestTeamsExtensionAgent(AgentApplicationOptions options) : AgentApplication(options)
    {
    }
}
