// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Collections.Generic;

namespace Microsoft.Agents.Core.Models.Activities
{
    /// <summary>
    /// The referenced conversation has been updated.
    /// </summary>
    public interface IConversationUpdateActivity : IActivity
    {
        /// <summary>
        /// The membersAdded field contains a list of channel participants (Agents or users) added to the conversation. The value 
        /// of the membersAdded field is an array of type channelAccount.
        /// </summary>
        IList<ChannelAccount> MembersAdded { get; set; }

        /// <summary>
        /// The membersRemoved field contains a list of channel participants (Agents or users) removed from the conversation. The 
        /// value of the membersRemoved field is an array of type channelAccount.
        /// </summary>
        IList<ChannelAccount> MembersRemoved { get; set; }

        /// <summary>
        /// The topicName field contains the text topic or description for the conversation. The value of the topicName field is of 
        /// type string.
        /// </summary>
        string TopicName { get; set; }
    }
}
