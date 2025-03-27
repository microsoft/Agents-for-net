// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Microsoft.Agents.Core.Models
{
    /// <inhertdoc/>
    public class Activity :
        IActivity,
        IConversationUpdateActivity,
        IContactRelationUpdateActivity,
        IInstallationUpdateActivity,
        IMessageActivity,
        IMessageUpdateActivity,
        IMessageDeleteActivity,
        IMessageReactionActivity,
        ISuggestionActivity,
        ITypingActivity,
        IEndOfConversationActivity,
        IEventActivity,
        IInvokeActivity,
        ITraceActivity,
        IHandoffActivity,
        ICommandActivity,
        ICommandResultActivity
    {
        /// <summary> Initializes a new instance of Activity. </summary>
        public Activity()
        {
        }

        /// <summary> Initializes a new instance of Activity. </summary>
        /// <param name="type"> Types of Activities. </param>
        /// <param name="id"> Contains an ID that uniquely identifies the activity on the channel. </param>
        /// <param name="timestamp"> Contains the date and time that the message was sent, in UTC, expressed in ISO-8601 format. </param>
        /// <param name="localTimestamp">
        /// Contains the local date and time of the message, expressed in ISO-8601 format.
        /// For example, 2016-09-23T13:07:49.4714686-07:00.
        /// </param>
        /// <param name="localTimezone">
        /// Contains the name of the local timezone of the message, expressed in IANA Time Zone database format.
        /// For example, America/Los_Angeles.
        /// </param>
        /// <param name="callerId">
        /// A string containing an IRI identifying the caller of an Agent. This field is not intended to be transmitted
        /// over the wire, but is instead populated by Agents and clients based on cryptographically verifiable data
        /// that asserts the identity of the callers (e.g. tokens).
        /// </param>
        /// <param name="serviceUrl"> Contains the URL that specifies the channel's service endpoint. Set by the channel. </param>
        /// <param name="channelId"> Contains an ID that uniquely identifies the channel. Set by the channel. </param>
        /// <param name="from"> Identifies the sender of the message. </param>
        /// <param name="conversation"> Identifies the conversation to which the activity belongs. </param>
        /// <param name="recipient"> Identifies the recipient of the message. </param>
        /// <param name="textFormat"> Format of text fields Default:markdown. </param>
        /// <param name="attachmentLayout"> The layout hint for multiple attachments. Default: list. </param>
        /// <param name="membersAdded"> The collection of members added to the conversation. </param>
        /// <param name="membersRemoved"> The collection of members removed from the conversation. </param>
        /// <param name="reactionsAdded"> The collection of reactions added to the conversation. </param>
        /// <param name="reactionsRemoved"> The collection of reactions removed from the conversation. </param>
        /// <param name="topicName"> The updated topic name of the conversation. </param>
        /// <param name="locale"> A BCP-47 locale name for the contents of the text field. </param>
        /// <param name="text"> The text content of the message. </param>
        /// <param name="speak"> The text to speak. </param>
        /// <param name="inputHint"> Indicates whether the Agent is accepting, expecting, or ignoring input. </param>
        /// <param name="summary"> The text to display if the channel cannot render cards. </param>
        /// <param name="suggestedActions"> SuggestedActions that can be performed. </param>
        /// <param name="attachments"> Attachments. </param>
        /// <param name="entities"> Represents the entities that were mentioned in the message. </param>
        /// <param name="channelData"> Contains channel-specific content. </param>
        /// <param name="action"> Indicates whether the recipient of a contactRelationUpdate was added or removed from the sender's contact list. </param>
        /// <param name="replyToId"> Contains the ID of the message to which this message is a reply. </param>
        /// <param name="label"> A descriptive label for the activity. </param>
        /// <param name="valueType"> The type of the activity's value object. </param>
        /// <param name="value"> A value that is associated with the activity. </param>
        /// <param name="name"> The name of the operation associated with an invoke or event activity. </param>
        /// <param name="relatesTo"> An object relating to a particular point in a conversation. </param>
        /// <param name="code"> Codes indicating why a conversation has ended. </param>
        /// <param name="expiration"> The time at which the activity should be considered to be "expired" and should not be presented to the recipient. </param>
        /// <param name="importance"> Defines the importance of an Activity. </param>
        /// <param name="deliveryMode"> Values for deliveryMode field. </param>
        /// <param name="listenFor"> List of phrases and references that speech and language priming systems should listen for. </param>
        /// <param name="textHighlights"> The collection of text fragments to highlight when the activity contains a ReplyToId value. </param>
        /// <param name="semanticAction"> Represents a reference to a programmatic action. </param>
        public Activity(string type = default, string id = default, System.DateTimeOffset? timestamp = default, System.DateTimeOffset? localTimestamp = default, string serviceUrl = default, string channelId = default, ChannelAccount from = default, ConversationAccount conversation = default, ChannelAccount recipient = default, string textFormat = default, string attachmentLayout = default, IList<ChannelAccount> membersAdded = default, IList<ChannelAccount> membersRemoved = default, IList<MessageReaction> reactionsAdded = default, IList<MessageReaction> reactionsRemoved = default, string topicName = default, string locale = default, string text = default, string speak = default, string inputHint = default, string summary = default, SuggestedActions suggestedActions = default, IList<Attachment> attachments = default, IList<Entity> entities = default, object channelData = default, string action = default, string replyToId = default, string label = default, string valueType = default, object value = default, string name = default, ConversationReference relatesTo = default, string code = default, System.DateTimeOffset? expiration = default, string importance = default, string deliveryMode = default, IList<string> listenFor = default, IList<TextHighlight> textHighlights = default, SemanticAction semanticAction = default, string localTimezone = default, string callerId = default)
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
            TextFormat = textFormat;
            AttachmentLayout = attachmentLayout;
            MembersAdded = membersAdded;
            MembersRemoved = membersRemoved;
            ReactionsAdded = reactionsAdded;
            ReactionsRemoved = reactionsRemoved;
            TopicName = topicName;
            Locale = locale;
            Text = text;
            Speak = speak;
            InputHint = inputHint;
            Summary = summary;
            SuggestedActions = suggestedActions;
            Attachments = attachments;
            Entities = entities;
            ChannelData = channelData;
            Action = action;
            ReplyToId = replyToId;
            Label = label;
            ValueType = valueType;
            Value = value;
            Name = name;
            RelatesTo = relatesTo;
            Code = code;
            Expiration = expiration;
            Importance = importance;
            DeliveryMode = deliveryMode;
            ListenFor = listenFor;
            TextHighlights = textHighlights;
            SemanticAction = semanticAction;
        }

        public bool IsType(string type)
        {
            return string.Equals(type, Type, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public bool HasContent()
        {
            if (!string.IsNullOrWhiteSpace(Text))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(Summary))
            {
                return true;
            }

            if (Attachments != null && Attachments.Any())
            {
                return true;
            }

            if (ChannelData != null)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determine if the Activity was sent via an Http/Https connection or Streaming
        /// This can be determined by looking at the ServiceUrl property:
        /// (1) All channels that send messages via http/https are not streaming
        /// (2) Channels that send messages via streaming have a ServiceUrl that does not begin with http/https.
        /// </summary>
        /// <returns>True if the Activity originated from a streaming connection.</returns>
        public bool IsFromStreamingConnection()
        {
            var isHttp = ServiceUrl?.StartsWith("http", StringComparison.InvariantCultureIgnoreCase);
            return isHttp.HasValue && !isHttp.Value;
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
        public string TextFormat { get; set; }

        /// <inheritdoc/>
        public string AttachmentLayout { get; set; }

        /// <inheritdoc/>
        public IList<ChannelAccount> MembersAdded { get; set; }

        /// <inheritdoc/>
        public IList<ChannelAccount> MembersRemoved { get; set; }

        /// <inheritdoc/>
        public IList<MessageReaction> ReactionsAdded { get; set; }

        /// <inheritdoc/>
        public IList<MessageReaction> ReactionsRemoved { get; set; }

        /// <inheritdoc/>
        public string TopicName { get; set; }

        /// <inheritdoc/>
        public string Locale { get; set; }

        /// <inheritdoc/>
        public string Text { get; set; }

        /// <inheritdoc/>
        public string Speak { get; set; }

        /// <inheritdoc/>
        public string InputHint { get; set; }

        /// <inheritdoc/>
        public string Summary { get; set; }

        /// <inheritdoc/>
        public SuggestedActions SuggestedActions { get; set; }

        /// <inheritdoc/>
        public IList<Attachment> Attachments { get; set; }

        /// <inheritdoc/>
        public IList<Entity> Entities { get; set; }

        /// <inheritdoc/>
        public object ChannelData { get; set; }

        /// <inheritdoc/>
        public string Action { get; set; }

        /// <inheritdoc/>
        public string ReplyToId { get; set; }

        /// <inheritdoc/>
        public string Label { get; set; }

        /// <inheritdoc/>
        public string ValueType { get; set; }

        /// <inheritdoc/>
        public object Value { get; set; }

        /// <inheritdoc/>
        public string Name { get; set; }

        /// <inheritdoc/>
        public ConversationReference RelatesTo { get; set; }

        /// <inheritdoc/>
        public string Code { get; set; }

        /// <inheritdoc/>
        public DateTimeOffset? Expiration { get; set; }

        /// <inheritdoc/>
        public string Importance { get; set; }

        /// <inheritdoc/>
        public string DeliveryMode { get; set; }

        /// <inheritdoc/>
        public IList<string> ListenFor { get; set; }

        /// <inheritdoc/>
        public IList<TextHighlight> TextHighlights { get; set; }

        /// <inheritdoc/>
        public SemanticAction SemanticAction { get; set; }

        /// <inheritdoc/>
        public IDictionary<string, JsonElement> Properties { get; set; } = new Dictionary<string, JsonElement>();

        public static IEventActivity CreateEventActivity()
        {
            return new Activity
            {
                Type = ActivityTypes.Event,
                Attachments = [],
                Entities = [],
            };
        }

        public static IActivity CreateMessageActivity()
        {
            return new Activity(ActivityTypes.Message)
            {
                Attachments = new List<Attachment>(),
                Entities = new List<Entity>(),
            };
        }

        /// <summary>
        /// Creates an instance of the <see cref="IActivity"/>.
        /// </summary>
        /// <returns>The new typing activity.</returns>
        public static IActivity CreateTypingActivity()
        {
            return new Activity(ActivityTypes.Typing);
        }

        /// <summary>
        /// Creates an instance of the <see cref="IActivity"/> class as an EndOfConversationActivity object.
        /// </summary>
        /// <returns>The new end of conversation activity.</returns>
        public static IActivity CreateEndOfConversationActivity()
        {
            return new Activity()
            {
                Type = ActivityTypes.EndOfConversation
            };
        }

        public static IActivity CreateConversationUpdateActivity()
        {
            return new Activity()
            {
                Type = ActivityTypes.ConversationUpdate,
                MembersAdded = [],
                MembersRemoved = [],
            };
        }

        /// <summary>
        /// Creates an instance of the <see cref="IActivity"/>.
        /// </summary>
        /// <param name="name">The name of the trace operation to create.</param>
        /// <param name="valueType">Optional, identifier for the format of the <paramref name="value"/>.
        /// Default is the name of type of the <paramref name="value"/>.</param>
        /// <param name="value">Optional, the content for this trace operation.</param>
        /// <param name="label">Optional, a descriptive label for this trace operation.</param>
        /// <returns>The new trace activity.</returns>
        public static IActivity CreateTraceActivity(string name, string valueType = null, object value = null, [CallerMemberName] string label = null)
        {
            return new Activity()
            {
                Type = ActivityTypes.Trace,
                Name = name,
                Label = label,
                ValueType = valueType ?? value?.GetType().Name,
                Value = value,
            };
        }

        /// <summary>
        /// Creates an instance of the <see cref="IActivity"/>.
        /// </summary>
        /// <returns>The new handoff activity.</returns>
        public static IActivity CreateHandoffActivity()
        {
            return new Activity(ActivityTypes.Handoff);
        }

        /// <summary>
        /// Creates an instance of the <see cref="IActivity"/>.
        /// </summary>
        /// <returns>The new invoke activity.</returns>
        public static IActivity CreateInvokeActivity()
        {
            return new Activity(ActivityTypes.Invoke);
        }

        public static IActivity CreateInvokeResponseActivity(object body = default, int status = (int)HttpStatusCode.OK)
        {
            Activity activity = new()
            {
                Type = ActivityTypes.InvokeResponse,
                Value = new InvokeResponse { Status = status, Body = body }
            };
            return activity;
        }


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

            if (isIncoming)
            {
                From = reference.User;
                Recipient = reference.Agent;
                if (reference.ActivityId != null)
                {
                    Id = reference.ActivityId;
                }
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
            }

            return this;
        }

        /// <inheritdoc/>
        public IActivity CreateReply(string text = "", string locale = "")
        {
            var reply = new Activity
            {
                Type = ActivityTypes.Message,
                Timestamp = DateTime.UtcNow,
                From = new ChannelAccount(id: Recipient?.Id, name: Recipient?.Name),
                Recipient = new ChannelAccount(id: From?.Id, name: From?.Name),
                ReplyToId = !string.Equals(Type, ActivityTypes.ConversationUpdate, StringComparison.OrdinalIgnoreCase) || ChannelId != "directline" && ChannelId != "webchat" ? Id : null,
                ServiceUrl = ServiceUrl,
                ChannelId = ChannelId,
                Conversation = new ConversationAccount(isGroup: Conversation.IsGroup, id: Conversation.Id, name: Conversation.Name),
                Text = text ?? string.Empty,
                Locale = locale ?? Locale,
                Attachments = new List<Attachment>(),
                Entities = new List<Entity>(),
            };
            return reply;
        }

        /// <inheritdoc/>
        public IActivity CreateTrace(string name, object value = null, string valueType = "", [CallerMemberName] string label = "")
        {
            var trace = new Activity
            {
                Type = ActivityTypes.Trace,
                Timestamp = DateTime.UtcNow,
                From = new ChannelAccount { Id = Recipient?.Id, Name = Recipient?.Name },
                Recipient = new ChannelAccount { Id = From?.Id, Name = From?.Name },
                ReplyToId = !string.Equals(Type, ActivityTypes.ConversationUpdate, StringComparison.OrdinalIgnoreCase) || ChannelId != "directline" && ChannelId != "webchat" ? Id : null,
                ServiceUrl = ServiceUrl,
                ChannelId = ChannelId,
                Conversation = Conversation,
                Name = name,
                Label = string.IsNullOrWhiteSpace(label) ? null : label,
                ValueType = string.IsNullOrWhiteSpace(valueType) ? value?.GetType().Name : valueType,
                Value = value
            };

            return trace;
        }

        /// <summary>
        /// Indicates whether this activity is of a specified activity type.
        /// </summary>
        /// <param name="activityType">The activity type to check for.</param>
        /// <returns><c>true</c> if this activity is of the specified activity type; otherwise, <c>false</c>.</returns>
        internal bool IsActivity(string activityType)
        {
            /*
             * NOTE: While it is possible to come up with a fancy looking "one-liner" to solve
             * this problem, this code is purposefully more verbose due to optimizations.
             *
             * This main goal of the optimizations was to make zero allocations because it is called
             * by all of the .AsXXXActivity methods which are used in a pattern heavily upstream to
             * "pseudo-cast" the activity based on its type.
             */

            var type = Type;

            // If there's no type set then we can't tell if it's the type they're looking for
            if (type == null)
            {
                return false;
            }

            // Check if the full type value starts with the type they're looking for
            var result = type.StartsWith(activityType, StringComparison.OrdinalIgnoreCase);

            // If the full type value starts with the type they're looking for, then we need to check a little further to check if it's definitely the right type
            if (result)
            {
                // If the lengths are equal, then it's the exact type they're looking for
                result = type.Length == activityType.Length;

                if (!result)
                {
                    // Finally, if the type is longer than the type they're looking for then we need to check if there's a / separator right after the type they're looking for
                    result = type[activityType.Length] == '/';
                }
            }

            return result;
        }
    }
}
