// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes.Tests
{
    public class PipeUriPredicateTests
    {
        [Theory]
        [InlineData("urn:botframework:namedpipe:/v3/conversations/abc")]
        [InlineData("urn:botframework:namedpipe:any-suffix")]
        [InlineData("URN:botframework:namedpipe:UPPER")]
        public void IsNamedPipeUri_ValidPipeUris_ReturnsTrue(string input)
        {
            var uri = new Uri(input, UriKind.Absolute);

            Assert.True(PipeUriPredicate.IsNamedPipeUri(uri));
        }

        [Theory]
        // Other urn schemes must not be routed to the pipe.
        [InlineData("urn:ietf:rfc:2648")]
        [InlineData("urn:botframework:other")]
        [InlineData("urn:botframework:namedpipes:typo")] // note the trailing 's'
        // HTTP URLs that merely contain the marker substring elsewhere.
        [InlineData("https://example.com/v3/?ref=botframework:namedpipe:foo")]
        [InlineData("http://attacker.com/botframework:namedpipe:/")]
        // Plain HTTPS service URLs.
        [InlineData("https://smba.trafficmanager.net/amer/v3/conversations/abc")]
        public void IsNamedPipeUri_NonPipeUris_ReturnsFalse(string input)
        {
            var uri = new Uri(input, UriKind.Absolute);

            Assert.False(PipeUriPredicate.IsNamedPipeUri(uri));
        }

        [Fact]
        public void IsNamedPipeUri_Null_ReturnsFalse()
        {
            Assert.False(PipeUriPredicate.IsNamedPipeUri(null));
        }
    }
}
