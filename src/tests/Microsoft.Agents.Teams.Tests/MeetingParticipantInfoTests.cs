// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Teams.Tests
{
    public class MeetingParticipantInfoTests
    {
        [Fact]
        public void MeetingParticipantInfoInits()
        {
            var role = "organizer";
            var inMeeting = true;

            var meetingParticipantInfo = new MeetingParticipantInfo(role, inMeeting);

            Assert.NotNull(meetingParticipantInfo);
            Assert.IsType<MeetingParticipantInfo>(meetingParticipantInfo);
            Assert.Equal(role, meetingParticipantInfo.Role);
            Assert.Equal(inMeeting, meetingParticipantInfo.InMeeting);
        }
        
        [Fact]
        public void MeetingParticipantInfoInitsWithNoArgs()
        {
            var meetingParticipantInfo = new MeetingParticipantInfo();

            Assert.NotNull(meetingParticipantInfo);
            Assert.IsType<MeetingParticipantInfo>(meetingParticipantInfo);
        }
    }
}
