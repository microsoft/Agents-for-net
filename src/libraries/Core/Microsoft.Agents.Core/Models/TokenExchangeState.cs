// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

namespace Microsoft.Agents.Core.Models
{
    /// <summary> State object passed to the bot token service. </summary>
    public class TokenExchangeState
    {
        /// <summary> Initializes a new instance of TokenExchangeState. </summary>
        public TokenExchangeState()
        {
        }

        /// <summary> Initializes a new instance of TokenExchangeState. </summary>
        /// <param name="connectionName"> The connection name that was used. </param>
        /// <param name="conversation"> An object relating to a particular point in a conversation. </param>
        /// <param name="relatesTo"> An object relating to a particular point in a conversation. </param>
        /// <param name="msAppId"> The bot's registered application ID. </param>
        internal TokenExchangeState(string connectionName, ConversationReference conversation, ConversationReference relatesTo, string msAppId)
        {
            ConnectionName = connectionName;
            Conversation = conversation;
            RelatesTo = relatesTo;
            MsAppId = msAppId;
        }

        /// <summary> The connection name that was used. </summary>
        public string ConnectionName { get; set; }
        /// <summary> An object relating to a particular point in a conversation. </summary>
        public ConversationReference Conversation { get; set; }
        /// <summary> An object relating to a particular point in a conversation. </summary>
        public ConversationReference RelatesTo { get; set; }
        /// <summary> The bot's registered application ID. </summary>
        public string MsAppId { get; set; }

        /// <summary>
        /// Gets or sets the URL of the bot messaging endpoint.
        /// </summary>
        /// <value>
        /// The URL of the bot messaging endpoint.
        /// </value>
#pragma warning disable CA1056 // Uri properties should not be strings (we can't change this without breaking binary compat)
        public string BotUrl { get; set; }
#pragma warning restore CA1056 // Uri properties should not be strings
    }
}
