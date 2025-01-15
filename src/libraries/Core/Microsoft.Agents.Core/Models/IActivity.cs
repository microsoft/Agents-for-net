﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Microsoft.Agents.Core.Models
{
    /// <summary> An Activity is the basic communication type for the Azure Bot Service 3.0 protocol. </summary>
    public interface IActivity
    {
        /// <summary>
        /// The action field describes the meaning of the contact relation update activity. The value of the Action field
        /// is a string. Only values of add and remove are defined, which denote a relationship between the users/bots in 
        /// the from and recipient fields.
        /// </summary>
        string Action { get; set; }

        /// <summary>
        /// The AttachmentLayout field instructs user interface renderers how to present content included in the attachments 
        /// field. The value of the attachmentLayout field is of type string, with defined values of list and carousel.
        /// </summary>
        string AttachmentLayout { get; set; }

        /// <summary>
        /// The Attachments field contains a flat list of objects to be displayed as part of this activity. The value of 
        /// each attachments list element is a complex object of the Attachment type.
        /// </summary>
        IList<Attachment> Attachments { get; set; }

        /// <summary>
        /// In some cases, it's important to record where an activity was sent. The CallerId field is a string containing 
        /// an IRI identifying the caller of a bot. This field is not intended to be transmitted over the wire, but is 
        /// instead populated by bots and clients based on cryptographically verifiable 
        /// data that asserts the identity of the callers (e.g. tokens).
        /// </summary>
        string CallerId { get; set; }

        /// <summary>
        /// Extensibility data in the activity schema is organized principally within the ChannelData field. This 
        /// simplifies plumbing in SDKs that implement the protocol. The format of the ChannelData object is defined 
        /// by the channel sending or receiving the activity.
        /// </summary>
        object ChannelData { get; set; }

        /// <summary>
        /// The ChannelId field establishes the channel and authoritative store for the activity.
        /// </summary>
        string ChannelId { get; set; }

        /// <summary>
        /// The Code field contains a programmatic value describing why or how the conversation was ended. The value of 
        /// the Code field is of type string and its meaning is defined by the channel sending the activity.
        /// </summary>
        string Code { get; set; }

        /// <summary>
        /// The Conversation field describes the conversation in which the activity exists. The value of the 
        /// conversation field is a complex object of the Conversation account type.
        /// </summary>
        ConversationAccount Conversation { get; set; }

        /// <summary>
        /// The DeliveryMode field contains any one of an enumerated set of values to signal to the recipient 
        /// alternate delivery paths for the activity or response. The value of the deliveryMode field is of type 
        /// string, with defined values of 'normal', 'notification' and 'expectReplies'. The default value is normal.
        /// </summary>
        string DeliveryMode { get; set; }

        /// <summary>
        /// The Entities field contains a flat list of metadata objects pertaining to this activity. Unlike 
        /// attachments (see the attachments field), entities do not necessarily manifest as user-interactable 
        /// content elements, and are intended to be ignored if not understood. Senders may include entities 
        /// they think may be useful to a receiver even if they are not certain the receiver can accept them. 
        /// The value of each entities list element is a complex object of the Entity type.
        /// </summary>
        IList<Entity> Entities { get; set; }

        /// <summary>
        /// The Expiration field contains a time at which the activity should be considered to be "expired" and 
        /// should not be presented to the recipient. The value of the expiration field is an ISO 8601 date time 
        /// format encoded datetime within a string.
        /// </summary>
        DateTimeOffset? Expiration { get; set; }

        /// <summary>
        /// The From field describes which client, bot, or channel generated an activity. The value of the From 
        /// field is a complex object of the ChannelAccount type.
        /// </summary>
        ChannelAccount From { get; set; }

        /// <summary>
        /// The Id field establishes the identity for the activity once it has been recorded in the channel. Activities 
        /// in-flight that have not yet been recorded do not have identities. Not all activities are assigned identities 
        /// (for example, a typing activity may never be assigned an id.) 
        /// </summary>
        string Id { get; set; }

        /// <summary>
        /// The importance field contains an enumerated set of values to signal to the recipient the relative importance 
        /// of the activity. It is up to the receiver to map these importance hints to the user experience. The value of 
        /// the importance field is of type string, with defined values of low, normal and high. The default value is normal.
        /// </summary>
        string Importance { get; set; }

        /// <summary>
        /// The inputHint field indicates whether or not the generator of the activity is anticipating a response. This 
        /// field is used predominantly within channels that have modal user interfaces, and is typically not used in channels 
        /// with continuous chat feeds. The value of the inputHint field is of type string, with defined values of accepting, 
        /// expecting, and ignoring. The default value is accepting.
        /// </summary>
        string InputHint { get; set; }

        /// <summary>
        /// The label field contains optional a label which can provide contextual information about the trace. The value of 
        /// the label field is of type string.
        /// </summary>
        string Label { get; set; }

        /// <summary>
        /// The listenFor field contains a list of terms or references to term sources that speech and language processing systems 
        /// can listen for.
        /// </summary>
        IList<string> ListenFor { get; set; }

        /// <summary>
        /// The locale field communicates the language code of the text field. The value of the locale field is an IETF BCP-47
        /// language tag within a string.
        /// </summary>
        string Locale { get; set; }

        /// <summary>
        /// The localTimestamp field expresses the datetime and timezone offset where the activity was generated. This may be 
        /// different from the UTC timestamp where the activity was recorded. The value of the localTimestamp field is an 
        /// ISO 8601 encoded datetime within a string.
        /// </summary>
        DateTimeOffset? LocalTimestamp { get; set; }

        /// <summary>
        /// The LocalTimezone field expresses the timezone where the activity was generated. The value of the localTimezone 
        /// field is a time zone name (zone entry) per the IANA Time Zone database.
        /// </summary>
        string LocalTimezone { get; set; }

        /// <summary>
        /// The membersAdded field contains a list of channel participants (bots or users) added to the conversation. The value 
        /// of the membersAdded field is an array of type channelAccount.
        /// </summary>
        IList<ChannelAccount> MembersAdded { get; set; }

        /// <summary>
        /// The membersRemoved field contains a list of channel participants (bots or users) removed from the conversation. The 
        /// value of the membersRemoved field is an array of type channelAccount.
        /// </summary>
        IList<ChannelAccount> MembersRemoved { get; set; }


        /// <summary>
        /// The name field controls the meaning of the event and the schema of the value field.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// The reactionsAdded field contains a list of reactions added to this activity. The value of the ReactionsAdded field 
        /// is an array of type MessageReaction.
        /// </summary>
        IList<MessageReaction> ReactionsAdded { get; set; }

        /// <summary>
        /// The reactionsRemoved field contains a list of reactions removed from this activity. The value of the ReactionsRemoved 
        /// field is an array of type MessageReaction.
        /// </summary>
        IList<MessageReaction> ReactionsRemoved { get; set; }

        /// <summary>
        /// The recipient field describes which client or bot is receiving this activity. This field is only meaningful when 
        /// an activity is transmitted to exactly one recipient; it is not meaningful when it is broadcast to multiple recipients 
        /// (as happens when an activity is sent to a channel). The purpose of the field is to allow the recipient to identify 
        /// themselves. This is helpful when a client or bot has more than one identity within the channel. The value of the 
        /// recipient field is a complex object of the Channel account type.
        /// </summary>
        ChannelAccount Recipient { get; set; }

        /// <summary>
        /// The relatesTo field references another conversation, and optionally a specific activity within that conversation. 
        /// The value of the relatesTo field is a complex object of the Conversation reference type.
        /// </summary>
        ConversationReference RelatesTo { get; set; }

        /// <summary>
        /// The replyToId field identifies the prior activity to which the current activity is a reply. This field allows threaded 
        /// conversation and comment nesting to be communicated between participants. replyToId is valid only within the current 
        /// conversation. (See relatesTo for references to other conversations.)
        /// </summary>
        string ReplyToId { get; set; }

        /// <summary>
        /// The semanticAction field contains an optional programmatic action accompanying the user request. The semantic action 
        /// field is populated by the channel and bot based on some understanding of what the user is trying to accomplish; this 
        /// understanding may be achieved with natural language processing, additional user interface elements tied specifically 
        /// to these actions, through a process of conversational refinement, or contextually via other means. The meaning and 
        /// structure of the semantic action is agreed ahead of time between the channel and the bot.
        /// </summary>
        SemanticAction SemanticAction { get; set; }

        /// <summary>
        /// Activities are frequently sent asynchronously, with separate transport connections for sending and receiving traffic. 
        /// The serviceUrl field is used by channels to denote the URL where replies to the current activity may be sent. 
        /// </summary>
        string ServiceUrl { get; set; }

        /// <summary>
        /// The speak field indicates how the activity should be spoken via a text-to-speech system. The field is only used to customize 
        /// speech rendering when the default is deemed inadequate. It replaces speech synthesis for any content within the activity, 
        /// including text, attachments, and summaries. The value of the speak field is either plain text or SSML encoded within a string.
        /// </summary>
        string Speak { get; set; }

        /// <summary>
        /// The suggestedActions field contains a payload of interactive actions that may be displayed to the user. Support for 
        /// suggestedActions and their manifestation depends heavily on the channel. The value of the suggestedActions field is a 
        /// complex object of the Suggested actions type.
        /// </summary>
        SuggestedActions SuggestedActions { get; set; }

        /// <summary>
        /// The summary field contains text used to replace attachments on channels that do not support them.
        /// </summary>
        string Summary { get; set; }

        /// <summary>
        /// The text field contains text content, either in the Markdown format, XML, or as plain text. The format is controlled 
        /// by the textFormat field as is plain if unspecified or ambiguous. 
        /// </summary>
        string Text { get; set; }

        /// <summary>
        /// The textFormat field denotes whether the text field should be interpreted as Markdown [3], plain text, or XML. The value 
        /// of the textFormat field is of type string, with defined values of markdown, plain, and xml. The default value is plain. 
        /// This field is not designed to be extended with arbitrary values.
        /// </summary>
        string TextFormat { get; set; }

        /// <summary>
        /// The textHighlights field contains a list of text to highlight in the text field of the activity referred to by ReplyToId. 
        /// The value of the TextHighlights field is an array of type TextHighlight.
        /// </summary>
        IList<TextHighlight> TextHighlights { get; set; }

        /// <summary>
        /// The timestamp field records the exact UTC time when the activity occurred. Due to the distributed nature of computing 
        /// systems, the important time is when the channel (the authoritative store) records the activity. The time when a client 
        /// or bot initiated an activity may be transmitted separately in the localTimestamp field. The value of the timestamp field 
        /// is an ISO 8601 date time format encoded datetime within a string.
        /// </summary>
        DateTimeOffset? Timestamp { get; set; }

        /// <summary>
        /// The topicName field contains the text topic or description for the conversation. The value of the topicName field is of 
        /// type string.
        /// </summary>
        string TopicName { get; set; }

        /// <summary>
        /// The type field controls the meaning of each activity, and are by convention short strings (e.g. "message"). 
        /// Senders may define their own application-layer types, although they are encouraged to choose values that are 
        /// unlikely to collide with future well-defined values. If senders use URIs as type values, they SHOULD NOT 
        /// implement URI ladder comparisons to establish equivalence.
        /// </summary>
        string Type { get; set; }

        /// <summary>
        /// The value field contains a programmatic payload specific to the activity being sent. Its meaning and format 
        /// are defined in other sections of this document that describe its use.
        /// </summary>
        object Value { get; set; }

        /// <summary>
        /// The valueType field is a string type which contains a unique value which identifies the shape of the value.
        /// </summary>
        string ValueType { get; set; }

        /// <summary>
        /// Updates this activity with the delivery information from an existing <see cref="ConversationReference"/>.
        /// </summary>
        /// <param name="reference">The existing conversation reference.</param>
        /// <param name="isIncoming">Optional, <c>true</c> to treat the activity as an
        /// incoming activity, where the bot is the recipient; otherwise, <c>false</c>.
        /// Default is <c>false</c>, and the activity will show the bot as the sender.</param>
        /// <remarks>Call <see cref="GetConversationReference()"/> on an incoming
        /// activity to get a conversation reference that you can then use to update an
        /// outgoing activity with the correct delivery information.
        /// </remarks>
        /// <returns>This activity, updated with the delivery information.</returns>
        IActivity ApplyConversationReference(ConversationReference reference, bool isIncoming = false);

        /// <summary>
        /// Creates a <see cref="ConversationReference"/> based on this activity.
        /// </summary>
        /// <returns>A conversation reference for the conversation that contains this activity.</returns>
        ConversationReference GetConversationReference();

        /*
        /// <summary>
        /// Create a ConversationReference based on this Activity's Conversation info and the ResourceResponse from sending an activity.
        /// </summary>
        /// <param name="reply">ResourceResponse returned from sendActivity.</param>
        /// <returns>A ConversationReference that can be stored and used later to delete or update the activity.</returns>
        ConversationReference GetReplyConversationReference(ResourceResponse reply);
        */

        /// <summary>
        /// Creates a new message activity as a response to this activity.
        /// </summary>
        /// <param name="text">The text of the reply.</param>
        /// <param name="locale">The language code for the <paramref name="text"/>.</param>
        /// <returns>The new message activity.</returns>
        /// <remarks>The new activity sets up routing information based on this activity.</remarks>
        IActivity CreateReply(string text = "", string locale = "");

        /// <summary>
        /// Creates a new trace activity based on this activity.
        /// </summary>
        /// <param name="name">The name of the trace operation to create.</param>
        /// <param name="value">Optional, the content for this trace operation.</param>
        /// <param name="valueType">Optional, identifier for the format of the <paramref name="value"/>.
        /// Default is the name of type of the <paramref name="value"/>.</param>
        /// <param name="label">Optional, a descriptive label for this trace operation.</param>
        /// <returns>The new trace activity.</returns>
        IActivity CreateTrace(string name, object value = null, string valueType = "", [CallerMemberName] string label = "");

        /// <summary>
        /// Indicates whether this activity has content.
        /// </summary>
        /// <returns>True, if this activity has any content to send; otherwise, false.</returns>
        /// <remarks>This method is defined on the <see cref="Activity"/> class, but is only intended
        /// for use with a message activity, where the activity <see cref="Activity.Type"/> is set to
        /// <see cref="ActivityTypes.Message"/>.</remarks>
        bool HasContent();

        bool IsFromStreamingConnection();

        /// <summary>
        /// Gets properties that are not otherwise defined by the <see cref="Activity"/> type but that
        /// might appear in the serialized REST JSON object.
        /// </summary>
        /// <value>The extended properties for the object.</value>
        /// <remarks>With this, properties not represented in the defined type are not dropped when
        /// the JSON object is deserialized, but are instead stored in this property. Such properties
        /// will be written to a JSON object when the instance is serialized.</remarks>
        IDictionary<string, JsonElement> Properties { get; set; }
    }
}