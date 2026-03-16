
using System.Net;

namespace Microsoft.Agents.Core.Models
{
    public partial class Activity
    {
        public static IEventActivity CreateEventActivity(string name)
        {
            return new EventActivity(name)
            {
                Entities = [],
            };
        }

        public static IActivity CreateMessageActivity()
        {
            return new MessageActivity()
            {
                Attachments = [],
                Entities = [],
            };
        }

        /// <summary>
        /// Creates an instance of the <see cref="IActivity"/>.
        /// </summary>
        /// <returns>The new typing activity.</returns>
        public static IActivity CreateTypingActivity()
        {
            return new TypingActivity();
        }

        /// <summary>
        /// Creates an instance of the <see cref="IActivity"/> class as an EndOfConversationActivity object.
        /// </summary>
        /// <returns>The new end of conversation activity.</returns>
        public static IActivity CreateEndOfConversationActivity()
        {
            return new EndOfConversationActivity();
        }

        public static IActivity CreateConversationUpdateActivity()
        {
            return new ConversationUpdateActivity()
            {
                MembersAdded = [],
                MembersRemoved = [],
            };
        }

        /// <summary>
        /// Creates an instance of the <see cref="IActivity"/>.
        /// </summary>
        /// <returns>The new handoff activity.</returns>
        public static IActivity CreateHandoffActivity()
        {
            return new Activity(ActivityTypes.Handoff);
        }

        /// <summary>
        /// Creates an instance of the <see cref="IActivity"/>.
        /// </summary>
        /// <returns>The new invoke activity.</returns>
        public static IActivity CreateInvokeActivity()
        {
            return new Activity(ActivityTypes.Invoke);
        }

        public static IActivity CreateInvokeResponseActivity(object body = default, int status = (int)HttpStatusCode.OK)
        {
            return new InvokeResponseActivity
            {
                Value = new InvokeResponse { Status = status, Body = body }
            };
        }
    }
}
