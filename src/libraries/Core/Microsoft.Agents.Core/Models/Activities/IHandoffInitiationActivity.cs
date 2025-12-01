// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Collections.Generic;

namespace Microsoft.Agents.Core.Models.Activities
{
    /// <summary>
    /// Control of the conversation has been transferred, or a request to transfer control of the conversation.
    /// </summary>
    public interface IHandoffInitiationActivity : IEventActivity
    {
        /// <summary>
        /// The Attachments field contains a flat list of objects to be displayed as part of this Activity. The value of 
        /// each attachments list element is a complex object of the Attachment type.
        /// </summary>
        IList<Attachment> Attachments { get; set; }
    }
}
