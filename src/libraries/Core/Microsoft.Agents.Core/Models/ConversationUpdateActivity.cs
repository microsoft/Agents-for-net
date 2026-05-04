// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using System.Collections.Generic;

namespace Microsoft.Agents.Core.Models
{
    [ActivityType(ActivityTypes.ConversationUpdate)]
    public class ConversationUpdateActivity : Activity, IConversationUpdateActivity
    {
        public IList<ChannelAccount> MembersAdded { get; set; }
        public IList<ChannelAccount> MembersRemoved { get; set; }
        public string TopicName { get; set; }
    }
}
