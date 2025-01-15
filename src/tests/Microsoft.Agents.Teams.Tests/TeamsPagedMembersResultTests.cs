﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Teams.Tests
{
    public class TeamsPagedMembersResultTests
    {
        [Fact]
        public void TeamsPagedMemberResultInits()
        {
            var continuationToken = "myContinuationToken";
            var channel1Id = "Channel1";
            var channel2Id = "Channel2";
            var members = new List<ChannelAccount>()
            {
                new ChannelAccount(channel1Id),
                new ChannelAccount(channel2Id),
            };

            var result = new TeamsPagedMembersResult(continuationToken, members);

            Assert.NotNull(result);
            Assert.IsType<TeamsPagedMembersResult>(result);
            Assert.Equal(continuationToken, result.ContinuationToken);
            var resultMembers = result.Members;
            Assert.IsType<List<TeamsChannelAccount>>(resultMembers);
            Assert.Equal(2, resultMembers.Count);
            Assert.Equal(channel1Id, resultMembers[0].Id);
            Assert.Equal(channel2Id, resultMembers[1].Id);
        }
        
        [Fact]
        public void TeamsPagedMemberResultInitsWithNoArgs()
        {
            var result = new TeamsPagedMembersResult();

            Assert.NotNull(result);
            Assert.IsType<TeamsPagedMembersResult>(result);
        }
    }
}
