// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using Microsoft.Agents.Core.Models.Entities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Models.Activities
{
    /// <inhertdoc/>
    public class Activity : IActivity
    {
        /// <summary> Initializes a new instance of Activity. </summary>
        public Activity()
        {
            Entities = [];
        }

        /// <summary> Initializes a new instance of Activity. </summary>
        /// <param name="type"> Types of Activities. </param>
        /// <param name="id"> Contains an ID that uniquely identifies the activity on the channel. </param>
        /// <param name="timestamp">The `timestamp` field records the exact UTC time when the activity occurred. Due to the distributed nature of computing systems, the important time is when the channel (the authoritative store) records the activity. The time when a client or Agent initiated an activity may be transmitted separately in the `localTimestamp` field. The value of the `timestamp` field is an `ISO 8601 date time format`.</param>
        /// <param name="localTimestamp">The `localTimestamp` field expresses the datetime and timezone offset where the activity was generated. This may be different from the UTC `timestamp` where the activity was recorded. The value of the `localTimestamp` field is an ISO 8601</param>
        /// <param name="localTimezone">The `localTimezone` field expresses the timezone where the activity was generated. The value of the `localTimezone` field is a time zone name (zone entry) per the IANA Time Zone database.</param>
        /// <param name="callerId">
        /// A string containing an IRI identifying the caller of an Agent. This field is not intended to be transmitted
        /// over the wire, but is instead populated by Agents and clients based on cryptographically verifiable data
        /// that asserts the identity of the callers (e.g. tokens).
        /// </param>
        /// <param name="serviceUrl">The `serviceUrl` field is used by channels to denote the URL where replies to the current activity may be sent.</param>
        /// <param name="channelId">Contains an ID that uniquely identifies the channel. Set by the channel. </param>
        /// <param name="from">The `from` field describes which client, Agent, or channel generated an Activity.</param>
        /// <param name="conversation">The `conversation` field describes the conversation in which the activity exists. The value of the `conversation` field is a complex object of the ConversationAccount type.</param>
        /// <param name="recipient">The `recipient` field describes which client or Agent is receiving this activity. This field is only meaningful when an activity is transmitted to exactly one recipient; it is not meaningful when it is broadcast to multiple recipients (as happens when an activity is sent to a channel). The purpose of the field is to allow the recipient to identify themselves. This is helpful when a client or Agent has more than one identity within the channel. The value of the `recipient` field is a complex object of the ChannelAccount</param>
        /// <param name="locale"> A BCP-47 locale name for the contents of the text field. </param>
        /// <param name="entities"> Represents the entities that were mentioned in the message. </param>
        /// <param name="channelData"> Contains channel-specific content. </param>
        /// <param name="replyToId">The `replyToId` field identifies the prior activity to which the current activity is a reply. This field allows threaded conversation and comment nesting to be communicated between participants. `replyToId` is valid only within the current conversation.</param>
        /// <param name="deliveryMode"> Values for deliveryMode field. </param>
        public Activity(string type, string id = default, DateTimeOffset? timestamp = default, DateTimeOffset? localTimestamp = default, string serviceUrl = default, string channelId = default, ChannelAccount from = default, ConversationAccount conversation = default, ChannelAccount recipient = default, string locale = default, IList<Entity> entities = default, object channelData = default, string replyToId = default, string deliveryMode = default, string localTimezone = default, string callerId = default)
        {
            Type = type;
            Id = id;
            Timestamp = timestamp;
            LocalTimestamp = localTimestamp;
            LocalTimezone = localTimezone;
            CallerId = callerId;
            ServiceUrl = serviceUrl;
            ChannelId = new ChannelId(channelId);
            From = @from;
            Conversation = conversation;
            Recipient = recipient;
            Locale = locale;
            Entities = entities ?? [];
            ChannelData = channelData;
            ReplyToId = replyToId;
            DeliveryMode = deliveryMode;
        }

        public bool IsType(string type)
        {
            return string.Equals(type, Type, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public string Type { get; set; }

        /// <inheritdoc/>
        public string Id { get; set; }

        /// <inheritdoc/>
        public DateTimeOffset? Timestamp { get; set; }

        /// <inheritdoc/>
        public DateTimeOffset? LocalTimestamp { get; set; }

        /// <inheritdoc/>
        public string LocalTimezone { get; set; }

        /// <inheritdoc/>
        public string CallerId { get; set; }

        /// <inheritdoc/>
        public string ServiceUrl { get; set; }

        /// <inheritdoc/>
        public ChannelId ChannelId { get; set; }

        /// <inheritdoc/>
        public ChannelAccount From { get; set; }

        /// <inheritdoc/>
        public ConversationAccount Conversation { get; set; }

        /// <inheritdoc/>
        public ChannelAccount Recipient { get; set; }

        /// <inheritdoc/>
        public IList<Entity> Entities { get; set; }

        /// <inheritdoc/>
        public object ChannelData { get; set; }

        /// <inheritdoc/>
        public string ReplyToId { get; set; }

        /// <inheritdoc/>
        public string DeliveryMode { get; set; }

        /// <inheritdoc/>
        public string Locale { get; set; }

        [JsonIgnore]
        public string RequestId { get; set; }

        //[JsonExtensionData]
        public IDictionary<string, JsonElement> Properties { get; set; } = new Dictionary<string, JsonElement>();

        public ConversationReference RelatesTo { get; set; }

        /// <inheritdoc/>
        public ConversationReference GetConversationReference()
        {
            var reference = new ConversationReference
            {
                ActivityId = !string.Equals(Type, ActivityTypes.ConversationUpdate.ToString(), StringComparison.OrdinalIgnoreCase) || ChannelId != "directline" && ChannelId != "webchat" ? Id : null,
                User = From,
                Agent = Recipient,
                Conversation = Conversation,
                ChannelId = ChannelId?.ToString(),
                Locale = Locale,
                ServiceUrl = ServiceUrl,
                DeliveryMode = DeliveryMode,
                RequestId = RequestId,
            };

            return reference;
        }

        /// <inheritdoc/>
        public ConversationReference GetReplyConversationReference(ResourceResponse reply)
        {
            var reference = GetConversationReference();

            // Update the reference with the new outgoing Activity's id.
            reference.ActivityId = reply.Id;
            return reference;
        }

        /// <inheritdoc/>
        public IActivity ApplyConversationReference(ConversationReference reference, bool isIncoming = false)
        {
            ChannelId = reference.ChannelId;
            ServiceUrl = reference.ServiceUrl;
            Conversation = reference.Conversation;
            Locale = reference.Locale ?? Locale;
            RequestId = reference.RequestId;

            if (isIncoming)
            {
                From = reference.User;
                Recipient = reference.Agent;
                if (reference.ActivityId != null)
                {
                    Id ??= reference.ActivityId;
                }
                DeliveryMode = reference.DeliveryMode;
            }
            else
            {
                // Outgoing
                From = reference.Agent;
                Recipient = reference.User;
                if (reference.ActivityId != null)
                {
                    ReplyToId = reference.ActivityId;
                }

                // `A3116`: Agents SHOULD NOT send activities with `deliveryMode` of `expectReplies` to channels. 
                // So we won't send DeliveryMode at all.  Not really needed for outgoing anyway.
                DeliveryMode = null;
            }

            return this;
        }

        public static T CreateReply<T>(IActivity activity, Func<T> factory) where T : class, IActivity
        {
            var reply = factory();
            reply.Timestamp = DateTime.UtcNow;
            reply.From = new ChannelAccount(id: activity?.Recipient?.Id, name: activity?.Recipient?.Name);
            reply.Recipient = new ChannelAccount(id: activity?.From?.Id, name: activity?.From?.Name);
            reply.ReplyToId = !string.Equals(activity.Type, ActivityTypes.ConversationUpdate, StringComparison.OrdinalIgnoreCase) || activity.ChannelId != "directline" && activity.ChannelId != "webchat" ? activity.Id : null;
            reply.ServiceUrl = activity.ServiceUrl;
            reply.ChannelId = activity.ChannelId;
            reply.Conversation = new ConversationAccount(isGroup: activity.Conversation.IsGroup, id: activity.Conversation.Id, name: activity.Conversation.Name);
            reply.Entities = [];
            return reply;
        }
    }
}
