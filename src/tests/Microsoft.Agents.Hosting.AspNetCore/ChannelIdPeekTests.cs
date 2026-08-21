// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text;
using Xunit;

namespace Microsoft.Agents.Hosting.AspNetCore.Tests
{
    public class ChannelIdPeekTests
    {
        private static string Peek(string json)
            => AgentEndpointExtensions.TryReadChannelId(Encoding.UTF8.GetBytes(json));

        [Fact]
        public void TopLevelChannelId_IsExtracted()
        {
            Assert.Equal("msteams", Peek("""{"type":"message","channelId":"msteams","text":"hi"}"""));
        }

        [Fact]
        public void ChannelId_AsFirstProperty_IsExtracted()
        {
            Assert.Equal("slack", Peek("""{"channelId":"slack","type":"message"}"""));
        }

        [Fact]
        public void NestedChannelId_IsIgnored()
        {
            // Only the top-level channelId is honored; a nested one must not match.
            Assert.Equal("msteams", Peek("""{"from":{"channelId":"decoy"},"channelId":"msteams"}"""));
        }

        [Fact]
        public void NestedChannelId_WithNoTopLevel_ReturnsNull()
        {
            Assert.Null(Peek("""{"from":{"channelId":"decoy"},"type":"message"}"""));
        }

        [Fact]
        public void MissingChannelId_ReturnsNull()
        {
            Assert.Null(Peek("""{"type":"message","text":"hi"}"""));
        }

        [Fact]
        public void EmptyPayload_ReturnsNull()
        {
            Assert.Null(Peek(""));
        }

        [Fact]
        public void MalformedJson_ReturnsNull()
        {
            Assert.Null(Peek("""{"channelId":"""));
        }

        [Fact]
        public void ChannelId_WithLargeNestedArraysBefore_IsExtracted()
        {
            var json = """{"attachments":[{"a":1},{"b":2}],"entities":[{"x":[1,2,3]}],"channelId":"directline"}""";
            Assert.Equal("directline", Peek(json));
        }
    }
}
