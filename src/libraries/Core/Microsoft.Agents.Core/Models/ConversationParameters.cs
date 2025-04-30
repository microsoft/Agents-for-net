// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Models
{
    /// <summary> Parameters for creating a new conversation. </summary>
    public class ConversationParameters
    {
        /// <summary> Initializes a new instance of ConversationParameters. </summary>
        internal ConversationParameters()
        {
            Members = [];
        }

        /// <summary>Initializes a new instance of the <see cref="ConversationParameters"/> class.</summary>
        /// <param name="isGroup">IsGroup.</param>
        /// <param name="agent">The Agent address for this conversation.</param>
        /// <param name="members">Members to add to the conversation.</param>
        /// <param name="topicName">(Optional) Topic of the conversation (if supported by the channel).</param>
        /// <param name="activity">(Optional) When creating a new conversation, use this activity as the initial message to the conversation.</param>
        /// <param name="channelData">Channel specific payload for creating the conversation.</param>
        /// <param name="tenantId">(Optional) The tenant ID in which the conversation should be created.</param>
        public ConversationParameters(bool? isGroup = default, ChannelAccount agent = default, IReadOnlyList<ChannelAccount> members = default, string topicName = default, Activity activity = default, object channelData = default, string tenantId = default)
        {
            IsGroup = isGroup;
            Agent = agent;
            Members = members ?? [];
            TopicName = topicName;
            Activity = activity;
            ChannelData = channelData;
            TenantId = tenantId;
        }

        /// <summary> IsGroup. </summary>
        public bool? IsGroup { get; set; }

        /// <summary> Channel account information needed to route a message. </summary>
        [JsonPropertyName("bot")]
        public ChannelAccount Agent { get; set; }

        /// <summary> Members to add to the conversation. </summary>
        public IReadOnlyList<ChannelAccount> Members { get; set; }

        /// <summary> (Optional) Topic of the conversation (if supported by the channel). </summary>
        public string TopicName { get; set; }

        /// <summary> (Optional) The tenant ID in which the conversation should be created. </summary>
        public string TenantId { get; set; }

        /// <summary> An Activity is the basic communication type for the Activity Protocol. </summary>
        public Activity Activity { get; set; }

        /// <summary> Channel specific payload for creating the conversation. </summary>
        public object ChannelData { get; set; }
    }
}
