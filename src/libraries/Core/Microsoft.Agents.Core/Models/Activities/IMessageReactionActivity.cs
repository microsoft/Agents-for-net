// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Collections.Generic;

namespace Microsoft.Agents.Core.Models.Activities
{
    /// <summary>
    /// A reaction to a Message Activity.
    /// </summary>
    public interface IMessageReactionActivity : IActivity
    {
        /// <summary>
        /// The reactionsAdded field contains a list of reactions added to this Activity. The value of the ReactionsAdded field 
        /// is an array of type MessageReaction.
        /// </summary>
        IList<MessageReaction> ReactionsAdded { get; set; }

        /// <summary>
        /// The reactionsRemoved field contains a list of reactions removed from this Activity. The value of the ReactionsRemoved 
        /// field is an array of type MessageReaction.
        /// </summary>
        IList<MessageReaction> ReactionsRemoved { get; set; }
    }
}
