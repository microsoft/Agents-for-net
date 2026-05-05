// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class RouteTests
    {
        // IsChannelIdMatch: null route ChannelId matches any incoming channel

        [Fact]
        public void IsChannelIdMatch_NullRouteChannelId_MatchesNullIncoming()
        {
            var route = new Route { ChannelId = null };
            Assert.True(route.IsChannelIdMatch(null));
        }

        [Fact]
        public void IsChannelIdMatch_NullRouteChannelId_MatchesSpecificChannel()
        {
            var route = new Route { ChannelId = null };
            Assert.True(route.IsChannelIdMatch(new ChannelId("msteams")));
        }

        // IsChannelIdMatch: wildcard "*" route ChannelId matches any incoming channel

        [Fact]
        public void IsChannelIdMatch_WildcardRouteChannelId_MatchesSpecificChannel()
        {
            var route = new Route { ChannelId = new ChannelId("*") };
            Assert.True(route.IsChannelIdMatch(new ChannelId("msteams")));
        }

        [Fact]
        public void IsChannelIdMatch_WildcardRouteChannelId_MatchesDifferentChannels()
        {
            var route = new Route { ChannelId = new ChannelId("*") };
            Assert.True(route.IsChannelIdMatch(new ChannelId("slack")));
            Assert.True(route.IsChannelIdMatch(new ChannelId("webchat")));
            Assert.True(route.IsChannelIdMatch(new ChannelId("directline")));
        }

        [Fact]
        public void IsChannelIdMatch_WildcardRouteChannelId_MatchesNullIncoming()
        {
            var route = new Route { ChannelId = new ChannelId("*") };
            Assert.True(route.IsChannelIdMatch(null));
        }

        [Fact]
        public void IsChannelIdMatch_WildcardRouteChannelId_MatchesChannelWithSubChannel()
        {
            var route = new Route { ChannelId = new ChannelId("*") };
            Assert.True(route.IsChannelIdMatch(new ChannelId("msteams:subchannel")));
        }

        // IsChannelIdMatch: wildcard with sub-channel is NOT treated as wildcard

        [Fact]
        public void IsChannelIdMatch_WildcardWithSubChannel_IsNotWildcard()
        {
            var route = new Route { ChannelId = new ChannelId("*:sub") };

            // Should NOT match a different channel
            Assert.False(route.IsChannelIdMatch(new ChannelId("msteams")));
        }

        [Fact]
        public void IsChannelIdMatch_WildcardWithSubChannel_MatchesExactChannelAndSubChannel()
        {
            var route = new Route { ChannelId = new ChannelId("*:sub") };
            Assert.True(route.IsChannelIdMatch(new ChannelId("*:sub")));
        }

        // IsChannelIdMatch: specific channel matching

        [Fact]
        public void IsChannelIdMatch_SpecificChannel_MatchesSameChannel()
        {
            var route = new Route { ChannelId = new ChannelId("msteams") };
            Assert.True(route.IsChannelIdMatch(new ChannelId("msteams")));
        }

        [Fact]
        public void IsChannelIdMatch_SpecificChannel_MatchesCaseInsensitive()
        {
            var route = new Route { ChannelId = new ChannelId("msteams") };
            Assert.True(route.IsChannelIdMatch(new ChannelId("MSTEAMS")));
        }

        [Fact]
        public void IsChannelIdMatch_SpecificChannel_DoesNotMatchDifferentChannel()
        {
            var route = new Route { ChannelId = new ChannelId("msteams") };
            Assert.False(route.IsChannelIdMatch(new ChannelId("slack")));
        }

        [Fact]
        public void IsChannelIdMatch_SpecificChannel_DoesNotMatchNullIncoming()
        {
            var route = new Route { ChannelId = new ChannelId("msteams") };
            Assert.False(route.IsChannelIdMatch(null));
        }

        // IsChannelIdMatch: specific channel with sub-channel

        [Fact]
        public void IsChannelIdMatch_SpecificChannelAndSubChannel_MatchesExact()
        {
            var route = new Route { ChannelId = new ChannelId("msteams:subchannel") };
            Assert.True(route.IsChannelIdMatch(new ChannelId("msteams:subchannel")));
        }

        [Fact]
        public void IsChannelIdMatch_SpecificChannelAndSubChannel_DoesNotMatchChannelOnly()
        {
            var route = new Route { ChannelId = new ChannelId("msteams:subchannel") };
            Assert.False(route.IsChannelIdMatch(new ChannelId("msteams")));
        }

        [Fact]
        public void IsChannelIdMatch_SpecificChannel_DoesNotMatchSameChannelDifferentSubChannel()
        {
            var route = new Route { ChannelId = new ChannelId("msteams:sub1") };
            Assert.False(route.IsChannelIdMatch(new ChannelId("msteams:sub2")));
        }
    }
}
