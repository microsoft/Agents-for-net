// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using System.Collections.Generic;

namespace Microsoft.Agents.Core.Models
{
    [ActivityType(ActivityTypes.MessageReaction)]
    public class MessageReactionActivity : Activity, IMessageReactionActivity
    {
        public MessageReactionActivity() : base(ActivityTypes.MessageReaction)
        { 
        }

        public IList<MessageReaction> ReactionsAdded { get; set; }
        public IList<MessageReaction> ReactionsRemoved { get; set; }
    }
}
