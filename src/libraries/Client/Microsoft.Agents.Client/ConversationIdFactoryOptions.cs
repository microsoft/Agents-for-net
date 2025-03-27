// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// A class defining the parameters used in <see cref="IConversationIdFactory.CreateConversationIdAsync(ConversationIdFactoryOptions,System.Threading.CancellationToken)"/>.
    /// </summary>
    internal class ConversationIdFactoryOptions
    {
        /// <summary>
        /// Gets or sets the oauth audience scope, used during token retrieval (either https://api.botframework.com or Agent AppId).
        /// </summary>
        /// <value>
        /// The oauth audience scope, used during token retrieval (either https://api.botframework.com or Agent AppId id if this is a Agent calling another Agent).
        /// </value>
        public string FromOAuthScope { get; set; }

        /// <summary>
        /// Gets or sets the ClientId of the parent Agent that is sending.
        /// </summary>
        /// <value>
        /// The ClientId of the Agent that is sending.
        /// </value>
        public string FromClientId { get; set; }

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
        public IAgentInfo Agent { get; set; }
    }
}
