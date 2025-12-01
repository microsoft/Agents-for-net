using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Core.Models.Activities
{
    public class HandoffInitiationActivity : Activity, IHandoffInitiationActivity
    {
        public HandoffInitiationActivity(IActivity activity, object handoffContext, Transcript transcript = null) : base(ActivityTypes.Handoff)
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
