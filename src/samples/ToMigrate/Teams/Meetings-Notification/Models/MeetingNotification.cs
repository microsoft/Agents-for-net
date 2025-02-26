// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace InMeetingNotificationsBot.Models
{
    public class MeetingNotification
    {   
        /// <summary>
        /// List of participants that are currently part of the meeting.
        /// </summary>
        public List<ParticipantDetail> ParticipantDetails { get; set; }
    }
}