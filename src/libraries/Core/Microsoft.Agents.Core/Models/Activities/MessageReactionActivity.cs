using System.Collections.Generic;

namespace Microsoft.Agents.Core.Models.Activities
{
    public class MessageReactionActivity : Activity, IMessageReactionActivity
    {
        public MessageReactionActivity() : base(ActivityTypes.MessageReaction)
        { 
        }

        public IList<MessageReaction> ReactionsAdded { get; set; }
        public IList<MessageReaction> ReactionsRemoved { get; set; }
    }
}
