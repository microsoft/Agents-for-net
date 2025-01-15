// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text.Json.Serialization;
using Microsoft.Agents.State;
using Microsoft.Agents.Client;

namespace Microsoft.Agents.BotBuilder.Dialogs
{
    /// <summary>
    /// Defines the options that will be used to execute a <see cref="SkillDialog"/>.
    /// </summary>
    public class SkillDialogOptions
    {
        /// <summary>
        /// Gets or sets the Microsoft app ID of the bot calling the skill.
        /// </summary>
        /// <value>
        /// The the Microsoft app ID of the bot calling the skill.
        /// </value>
        [JsonPropertyName("botId")]
        public string BotId { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="BotFrameworkClient"/> used to call the remote skill.
        /// </summary>
        /// <value>
        /// The <see cref="BotFrameworkClient"/> used to call the remote skill.
        /// </value>
        [JsonIgnore]
        public IChannel SkillClient { get; set; }

        /// <summary>
        /// Gets or sets the callback Url for the skill host.
        /// </summary>
        /// <value>
        /// The callback Url for the skill host.
        /// </value>
        [JsonPropertyName("skillHostEndpoint")]
        public Uri SkillHostEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="BotFrameworkSkill"/> that the dialog will call.
        /// </summary>
        /// <value>
        /// The <see cref="BotFrameworkSkill"/> that the dialog will call.
        /// </value>
        [JsonPropertyName("skill")]
        public IChannelInfo Skill { get; set; }

        /// <summary>
        /// Gets or sets an instance of a <see cref="SkillConversationIdFactoryBase"/> used to generate conversation IDs for interacting with the skill.
        /// </summary>
        /// <value>
        /// An instance of a <see cref="SkillConversationIdFactoryBase"/> used to generate conversation IDs for interacting with the skill.
        /// </value>
        [JsonIgnore]
        public IConversationIdFactory ConversationIdFactory { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="ConversationState"/> to be used by the dialog.
        /// </summary>
        /// <value>
        /// The <see cref="ConversationState"/> to be used by the dialog.
        /// </value>
        [JsonIgnore]
        public ConversationState ConversationState { get; set; }

        /// <summary>
        /// Gets or sets the OAuth Connection Name, that would be used to perform Single SignOn with a skill.
        /// </summary>
        /// <value>
        /// The OAuth Connection Name for the Parent Bot.
        /// </value>
        [JsonPropertyName("connectionName")]
        public string ConnectionName { get; set; }
    }
}
