
using System;
using System.Runtime.CompilerServices;

namespace Microsoft.Agents.Core.Models.Activities
{
    public class TraceActivity : Activity, ITraceActivity
    {
        public TraceActivity(IActivity activity, string name, object value = null, string valueType = null, [CallerMemberName] string label = null) : base(ActivityTypes.Trace) 
        {
            Timestamp = DateTime.UtcNow;
            From = new ChannelAccount(id: activity.Recipient?.Id, name: activity.Recipient?.Name);
            Recipient = new ChannelAccount(id: activity.From?.Id, name: activity.From?.Name);
            ReplyToId = !activity.IsType(ActivityTypes.ConversationUpdate) || activity.ChannelId != "directline" && activity.ChannelId != "webchat" ? activity.Id : null;
            ServiceUrl = activity.ServiceUrl;
            ChannelId = activity.ChannelId;
            Conversation = activity.Conversation;
            Name = name;
            Label = label;
            ValueType = valueType ?? value?.GetType().Name;
            Value = value;
        }

        public string Label { get; set; }
        public string Name { get; set; }
        //!!!public ConversationReference RelatesTo { get; set; }
        public object Value { get; set; }
        public string ValueType { get; set; }
    }
}
