// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace InMeetingNotificationsBot.Models
{
    public class SubmitFeedbackAction : PushAgendaAction
    {
        public string Topic { get; set; }
        public string Feedback { get; set; }
    }
}
