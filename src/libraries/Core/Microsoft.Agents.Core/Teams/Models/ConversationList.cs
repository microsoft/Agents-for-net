﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Core.Teams.Models
{
    /// <summary>
    /// List of channels under a team.
    /// </summary>
    public class ConversationList
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConversationList"/> class.
        /// </summary>
        public ConversationList()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConversationList"/> class.
        /// </summary>
        /// <param name="conversations">The IList of conversations.</param>
        public ConversationList(IList<ChannelInfo> conversations = default)
        {
            Conversations = conversations;
        }

        /// <summary>
        /// Gets or sets the conversations.
        /// </summary>
        /// <value>The conversations.</value>
#pragma warning disable CA2227 // Collection properties should be read only (we can't change this without breaking compat)
        public IList<ChannelInfo> Conversations { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
    }
}
