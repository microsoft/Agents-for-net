// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Core.Models.Activities
{
    public class HandoffActivity : Activity, IHandoffActivity
    {
        /// <summary>
        /// Create Handoff Initiation Activity
        /// </summary>
        /// <param name="activity"></param>
        /// <param name="handoffContext"></param>
        /// <param name="transcript"></param>
        public HandoffActivity(IActivity activity, object handoffContext, Transcript transcript = null) : base(ActivityTypes.Handoff)
        {
            Id = Guid.NewGuid().ToString();
            Timestamp = DateTime.UtcNow;
            Conversation = activity.Conversation;
            RelatesTo = activity.GetConversationReference();
            ReplyToId = activity.Id;
            ServiceUrl = activity.ServiceUrl;
            ChannelId = activity.ChannelId;
            Value = handoffContext;

            if (transcript != null)
            {
                var attachment = new Attachment
                {
                    Content = transcript,
                    ContentType = "application/json",
                    Name = "Transcript",
                };
                Attachments.Add(attachment);
            }
        }

        public IList<Attachment> Attachments { get; set; } = [];
        public string Name { get; set; } = HandoffEventNames.InitiateHandoff;
        //!!!public ConversationReference RelatesTo { get; set; }
        public object Value { get; set; }
        public string ValueType { get; set; }
    }
}
