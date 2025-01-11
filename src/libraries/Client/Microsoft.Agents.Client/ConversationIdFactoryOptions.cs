// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// A class defining the parameters used in <see cref="IConversationIdFactory.CreateConversationIdAsync(ConversationIdFactoryOptions,System.Threading.CancellationToken)"/>.
    /// </summary>
    public class ConversationIdFactoryOptions
    {
        /// <summary>
        /// Gets or sets the oauth audience scope, used during token retrieval (either https://api.botframework.com or bot app id).
        /// </summary>
        /// <value>
        /// The oauth audience scope, used during token retrieval (either https://api.botframework.com or bot app id if this is a bot calling another bot).
        /// </value>
        public string FromBotOAuthScope { get; set; }

        /// <summary>
        /// Gets or sets the id of the parent bot that is messaging the bot.
        /// </summary>
        /// <value>
        /// The id of the parent bot that is messaging the bot.
        /// </value>
        public string FromBotId { get; set; }

        /// <summary>
        /// Gets or sets the activity which will be sent to the skill.
        /// </summary>
        /// <value>
        /// The activity which will be sent to the skill.
        /// </value>
        public IActivity Activity { get; set; }

        /// <summary>
        /// Gets or sets the skill to create the conversation Id for.
        /// </summary>
        /// <value>
        /// The skill to create the conversation Id for.
        /// </value>
        public IChannelInfo Bot { get; set; }
    }
}
