using System.Collections.Generic;

namespace Microsoft.Agents.Core.Models.Activities
{
    public class ConversationUpdateActivity : Activity, IConversationUpdateActivity
    {
        public IList<ChannelAccount> MembersAdded { get; set; }
        public IList<ChannelAccount> MembersRemoved { get; set; }
        public string TopicName { get; set; }
    }
}
