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

        // IsChannelIdMatch: wildcard channel with specific sub-channel ("*:sub") matches any parent channel with that sub-channel

        [Fact]
        public void IsChannelIdMatch_WildcardChannelWithSubChannel_DoesNotMatchChannelWithoutSubChannel()
        {
            var route = new Route { ChannelId = new ChannelId("*:sub") };

            // Should NOT match a channel without the matching subchannel
            Assert.False(route.IsChannelIdMatch(new ChannelId("msteams")));
        }

        [Fact]
        public void IsChannelIdMatch_WildcardChannelWithSubChannel_MatchesAnyParentWithSubChannel()
        {
            var route = new Route { ChannelId = new ChannelId("*:email") };
            Assert.True(route.IsChannelIdMatch(new ChannelId("agents:email")));
            Assert.True(route.IsChannelIdMatch(new ChannelId("msteams:email")));
            Assert.True(route.IsChannelIdMatch(new ChannelId("slack:email")));
        }

        [Fact]
        public void IsChannelIdMatch_WildcardChannelWithSubChannel_DoesNotMatchDifferentSubChannel()
        {
            var route = new Route { ChannelId = new ChannelId("*:email") };
            Assert.False(route.IsChannelIdMatch(new ChannelId("agents:chat")));
        }

        // IsChannelIdMatch: specific channel with wildcard sub-channel ("agents:*") matches any subchannel

        [Fact]
        public void IsChannelIdMatch_SpecificChannelWildcardSubChannel_MatchesAnySubChannel()
        {
            var route = new Route { ChannelId = new ChannelId("agents:*") };
            Assert.True(route.IsChannelIdMatch(new ChannelId("agents:email")));
            Assert.True(route.IsChannelIdMatch(new ChannelId("agents:chat")));
            Assert.True(route.IsChannelIdMatch(new ChannelId("agents")));
        }

        [Fact]
        public void IsChannelIdMatch_SpecificChannelWildcardSubChannel_DoesNotMatchDifferentChannel()
        {
            var route = new Route { ChannelId = new ChannelId("agents:*") };
            Assert.False(route.IsChannelIdMatch(new ChannelId("msteams:email")));
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
