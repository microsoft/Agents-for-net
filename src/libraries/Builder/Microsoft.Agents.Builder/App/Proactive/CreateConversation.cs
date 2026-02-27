// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Builder.App.Proactive
{
    /// <summary>
    /// Represents the data required to create a new conversation, including authentication scope, configuration
    /// parameters, and conversation reference information.
    /// </summary>
    public class CreateConversation
    {
        public const string AzureBotScope = $"{AuthenticationConstants.BotFrameworkScope}/.default";

        /// <summary>
        /// Gets or sets the OAuth scope to use when requesting authentication tokens.
        /// </summary>
        public string Scope { get; set; } = AzureBotScope;

        /// <summary>
        /// Gets or sets the parameters used to configure the conversation.
        /// </summary>
        public ConversationParameters Parameters { get; set; }

        /// <summary>
        /// Gets or sets the information for the conversation associated with this instance.
        /// </summary>
        public Conversation Conversation { get; set; }
    }
}
