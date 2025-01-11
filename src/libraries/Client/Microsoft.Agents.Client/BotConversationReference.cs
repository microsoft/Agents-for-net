// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// A conversation reference type for bot-to-bot.
    /// </summary>
    public class BotConversationReference
    {
        /// <summary>
        /// Gets or sets the conversation reference.
        /// </summary>
        /// <value>
        /// The conversation reference.
        /// </value>
        public ConversationReference ConversationReference { get; set; }

        /// <summary>
        /// Gets or sets the OAuth scope.
        /// </summary>
        /// <value>
        /// The OAuth scope.
        /// </value>
        public string OAuthScope { get; set; }
    }
}
