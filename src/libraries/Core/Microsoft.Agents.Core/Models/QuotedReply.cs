// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

namespace Microsoft.Agents.Core.Models
{

    public class QuotedReplyBody
    {
        public string messageId;

        public QuotedReplyBody(string messageId)
        {
            this.messageId = messageId;
        }
    }

    public class QuotedReply : Entity
    {

        public QuotedReply(string messageId, string type = default) : base(type ?? EntityTypes.Mention)
        {
            quotedReply = new QuotedReplyBody(messageId);
        }

        /// <summary> Channel account information needed to route a message. </summary>
        public QuotedReplyBody quotedReply;
    }
}
