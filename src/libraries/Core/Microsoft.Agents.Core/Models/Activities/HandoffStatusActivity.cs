using System;

namespace Microsoft.Agents.Core.Models.Activities
{
    public class HandoffStatusActivity : Activity, IHandoffStatusActivity
    {
        public HandoffStatusActivity(ConversationAccount conversation, string state, string message = null) : base(ActivityTypes.Handoff)
        {
            Id = Guid.NewGuid().ToString();
            Timestamp = DateTime.UtcNow;
            Conversation = conversation;
            Value = new { state, message }; ;
        }

        public string Name { get; set; } = HandoffEventNames.HandoffStatus;
        //!!!public ConversationReference RelatesTo { get; set; }
        public object Value { get; set; }
        public string ValueType { get; set; }
    }
}
