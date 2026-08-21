// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Primitives;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Xunit;

namespace Microsoft.Agents.Core.HeaderPropagation.Tests
{
    [Collection("Non-Parallel Collection")] // Ensure this test runs in a single-threaded context to avoid issues with static dictionary.
    public class HeaderPropagationTests
    {
        public HeaderPropagationTests()
        {
            HeaderPropagationContext.HeadersToPropagate = new HeaderPropagationEntryCollection();
            HeaderPropagationContext.HeaderProviders = new List<IHeaderValueProvider>();
            HeaderPropagationContext.HeadersFromRequest = new Dictionary<string, StringValues>();
        }

        [Fact]
        public void HeaderPropagationContext_ShouldFilterHeaders()
        {
            // Arrange
            HeaderPropagationContext.HeadersToPropagate.Add("x-custom-header", "custom-value");
            HeaderPropagationContext.HeadersToPropagate.Propagate("x-custom-header-1");
            HeaderPropagationContext.HeadersToPropagate.Override("x-custom-header-2", "new-value");
            HeaderPropagationContext.HeadersToPropagate.Append("x-custom-header-3", "extra-value");

            HeaderPropagationContext.HeadersFromRequest = new Dictionary<string, StringValues>
            {
                { "x-ms-correlation-id", new StringValues("1234") },
                { "x-custom-header-1", new StringValues("Value-1") },
                { "x-custom-header-2", new StringValues("Value-2") },
                { "x-custom-header-3", new StringValues("Value-3") }
            };

            // Act
            var filteredHeaders = HeaderPropagationContext.HeadersFromRequest;

            // Assert
            Assert.Equal(5, filteredHeaders.Count);
            Assert.Equal("1234", filteredHeaders["x-ms-correlation-id"]);
            Assert.Equal("custom-value", filteredHeaders["x-custom-header"]);
            Assert.Equal("Value-1", filteredHeaders["x-custom-header-1"]);
            Assert.Equal("new-value", filteredHeaders["x-custom-header-2"]);
            Assert.Equal("Value-3,extra-value", filteredHeaders["x-custom-header-3"]);
        }

        [Fact]
        public void HeaderPropagationContext_ShouldAppendMultipleValues()
        {
            // Arrange
            HeaderPropagationContext.HeadersToPropagate.Append("User-Agent", "extra-value-1");
            HeaderPropagationContext.HeadersToPropagate.Append("User-Agent", "extra-value-2");
            HeaderPropagationContext.HeadersToPropagate.Append("Accept", "text/html");

            HeaderPropagationContext.HeadersFromRequest = new Dictionary<string, StringValues>
            {
                { "x-ms-correlation-id", new StringValues("1234") },
                { "User-Agent", new StringValues("Value-1") },
                { "Accept", new StringValues("text/plain") }
            };

            // Act
            var filteredHeaders = HeaderPropagationContext.HeadersFromRequest;

            // Assert
            Assert.Equal(3, filteredHeaders.Count);
            Assert.Equal("1234", filteredHeaders["x-ms-correlation-id"]);
            Assert.Equal("Value-1 extra-value-1 extra-value-2", filteredHeaders["User-Agent"]);
            Assert.Equal("text/plain,text/html", filteredHeaders["Accept"]);
        }

        [Fact]
        public void HeaderPropagationContext_MultipleAdd_ShouldKeepLastValue()
        {
            // Arrange
            HeaderPropagationContext.HeadersToPropagate.Add("x-custom-header-1", "value-1");
            HeaderPropagationContext.HeadersToPropagate.Add("x-custom-header-1", "value-2");

            HeaderPropagationContext.HeadersFromRequest = new Dictionary<string, StringValues>
            {
                { "x-ms-correlation-id", new StringValues("1234") },
            };

            // Act
            var filteredHeaders = HeaderPropagationContext.HeadersFromRequest;

            // Assert
            Assert.Equal(2, filteredHeaders.Count);
            Assert.Equal("1234", filteredHeaders["x-ms-correlation-id"]);
            Assert.Equal("value-2", filteredHeaders["x-custom-header-1"]);
        }

        [Fact]
        public void HeaderPropagationContext_MultipleOverride_ShouldKeepLastValue()
        {
            // Arrange
            HeaderPropagationContext.HeadersToPropagate.Override("x-custom-header-1", "new-value-1");
            HeaderPropagationContext.HeadersToPropagate.Override("x-custom-header-1", "new-value-2");

            HeaderPropagationContext.HeadersFromRequest = new Dictionary<string, StringValues>
            {
                { "x-ms-correlation-id", new StringValues("1234") },
                { "x-custom-header-1", new StringValues("Value-1") }
            };

            // Act
            var filteredHeaders = HeaderPropagationContext.HeadersFromRequest;

            // Assert
            Assert.Equal(2, filteredHeaders.Count);
            Assert.Equal("1234", filteredHeaders["x-ms-correlation-id"]);
            Assert.Equal("new-value-2", filteredHeaders["x-custom-header-1"]);
        }
    }

    [Collection("Non-Parallel Collection")]
    public class AgenticHeaderProviderTests
    {
        public AgenticHeaderProviderTests()
        {
            HeaderPropagationContext.HeadersToPropagate = new HeaderPropagationEntryCollection();
            HeaderPropagationContext.HeaderProviders = new List<IHeaderValueProvider>();
            HeaderPropagationContext.HeadersFromRequest = new Dictionary<string, StringValues>();
        }

        [Fact]
        public void AgenticHeaderProvider_AgenticRequest_ShouldEmitAllHeaders()
        {
            // Arrange
            var activity = new Activity
            {
                Recipient = new ChannelAccount
                {
                    Role = RoleTypes.AgenticUser,
                    AgenticAppId = "Entra:test-guid-1234"
                },
                ChannelId = new ChannelId("msteams")
            };

            var provider = new AgenticHeaderProvider(activity, "MyTestAgent");

            // Act
            var headers = provider.GetHeaders().ToList();

            // Assert
            Assert.Equal(4, headers.Count);
            Assert.Equal("AgentRegistrar", headers[0].Key);
            Assert.Equal("A365", headers[0].Value);
            Assert.Equal("AgentID", headers[1].Key);
            Assert.Equal("Entra:test-guid-1234", headers[1].Value);
            Assert.Equal("AgentName", headers[2].Key);
            Assert.Equal("MyTestAgent", headers[2].Value);
            Assert.Equal("Agent-Referrer", headers[3].Key);
            Assert.Equal("msteams", headers[3].Value);
        }

        [Fact]
        public void AgenticHeaderProvider_AgenticIdentityRole_ShouldEmitHeaders()
        {
            // Arrange
            var activity = new Activity
            {
                Recipient = new ChannelAccount
                {
                    Role = RoleTypes.AgenticIdentity,
                    AgenticAppId = "Entra:identity-guid"
                },
                ChannelId = new ChannelId("webchat")
            };

            var provider = new AgenticHeaderProvider(activity, "IdentityAgent");

            // Act
            var headers = provider.GetHeaders().ToList();

            // Assert
            Assert.Equal(4, headers.Count);
            Assert.Equal("A365", headers[0].Value);
            Assert.Equal("Entra:identity-guid", headers[1].Value);
            Assert.Equal("IdentityAgent", headers[2].Value);
            Assert.Equal("webchat", headers[3].Value);
        }

        [Fact]
        public void AgenticHeaderProvider_NonAgenticRequest_ShouldEmitNoHeaders()
        {
            // Arrange
            var activity = new Activity
            {
                Recipient = new ChannelAccount
                {
                    Role = RoleTypes.User
                },
                ChannelId = new ChannelId("msteams")
            };

            var provider = new AgenticHeaderProvider(activity, "MyAgent");

            // Act
            var headers = provider.GetHeaders().ToList();

            // Assert
            Assert.Empty(headers);
        }

        [Fact]
        public void AgenticHeaderProvider_NullRole_ShouldEmitNoHeaders()
        {
            // Arrange
            var activity = new Activity
            {
                Recipient = new ChannelAccount(),
                ChannelId = new ChannelId("msteams")
            };

            var provider = new AgenticHeaderProvider(activity, "MyAgent");

            // Act
            var headers = provider.GetHeaders().ToList();

            // Assert
            Assert.Empty(headers);
        }

        [Fact]
        public void AddHeaderPropagation_ShouldApplyProviderHeaders()
        {
            // Arrange
            var activity = new Activity
            {
                Recipient = new ChannelAccount
                {
                    Role = RoleTypes.AgenticUser,
                    AgenticAppId = "Entra:app-id-123"
                },
                ChannelId = new ChannelId("msteams:Copilot")
            };

            HeaderPropagationContext.HeaderProviders.Add(new AgenticHeaderProvider(activity, "TestAgent"));

            using var httpClient = new HttpClient();

            // Act
            httpClient.AddHeaderPropagation();

            // Assert
            Assert.Equal("A365", httpClient.DefaultRequestHeaders.GetValues("AgentRegistrar").First());
            Assert.Equal("Entra:app-id-123", httpClient.DefaultRequestHeaders.GetValues("AgentID").First());
            Assert.Equal("TestAgent", httpClient.DefaultRequestHeaders.GetValues("AgentName").First());
            Assert.Equal("msteams:Copilot", httpClient.DefaultRequestHeaders.GetValues("Agent-Referrer").First());
        }

        [Fact]
        public void AddHeaderPropagation_NonAgentic_ShouldNotAddProviderHeaders()
        {
            // Arrange
            var activity = new Activity
            {
                Recipient = new ChannelAccount
                {
                    Role = RoleTypes.User
                },
                ChannelId = new ChannelId("msteams")
            };

            HeaderPropagationContext.HeaderProviders.Add(new AgenticHeaderProvider(activity, "TestAgent"));

            using var httpClient = new HttpClient();

            // Act
            httpClient.AddHeaderPropagation();

            // Assert
            Assert.False(httpClient.DefaultRequestHeaders.Contains("AgentRegistrar"));
            Assert.False(httpClient.DefaultRequestHeaders.Contains("AgentID"));
            Assert.False(httpClient.DefaultRequestHeaders.Contains("AgentName"));
            Assert.False(httpClient.DefaultRequestHeaders.Contains("Agent-Referrer"));
        }
    }

    [CollectionDefinition("Non-Parallel Collection", DisableParallelization = true)]
    public class NonParallelCollectionDefinition { }
}
