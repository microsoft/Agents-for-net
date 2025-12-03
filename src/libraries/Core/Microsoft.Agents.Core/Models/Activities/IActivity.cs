// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models.Entities;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Microsoft.Agents.Core.Models.Activities
{
    /// <summary> An Activity is the basic communication type for the Activity Protocol used with Agents. </summary>
    public interface IActivity
    {
        bool IsType(string type);

        /// <summary>
        /// In some cases, it's important to record where an Activity was sent. The CallerId field is a string containing 
        /// an IRI identifying the caller of a Agent. This field is not intended to be transmitted over the wire, but is 
        /// instead populated by Agents and clients based on cryptographically verifiable 
        /// data that asserts the identity of the callers (e.g. tokens).
        /// </summary>
        string CallerId { get; set; }

        /// <summary>
        /// Extensibility data in the Activity schema is organized principally within the ChannelData field. This 
        /// simplifies plumbing in SDKs that implement the protocol. The format of the ChannelData object is defined 
        /// by the channel sending or receiving the Activity.
        /// </summary>
        object ChannelData { get; set; }

        /// <summary>
        /// The ChannelId field establishes the channel and authoritative store for the Activity. This can be a combination id for a base channel and sub-channel, such as "msteams:Copilot" or "webchat:Sharepoint".
        /// </summary>
        ChannelId ChannelId { get; set; }

        /// <summary>
        /// The Conversation field describes the conversation in which the Activity exists. The value of the 
        /// conversation field is a complex object of the Conversation account type.
        /// </summary>
        ConversationAccount Conversation { get; set; }

        /// <summary>
        /// The DeliveryMode field contains any one of an enumerated set of values to signal to the recipient 
        /// alternate delivery paths for the Activity or response. The value of the deliveryMode field is of type 
        /// string, with defined values of 'normal', 'notification' and 'expectReplies'. The default value is normal.
        /// </summary>
        string DeliveryMode { get; set; }

        /// <summary>
        /// The Entities field contains a flat list of metadata objects pertaining to this Activity. Unlike 
        /// attachments (see the attachments field), entities do not necessarily manifest as user-interactable 
        /// content elements, and are intended to be ignored if not understood. Senders may include entities 
        /// they think may be useful to a receiver even if they are not certain the receiver can accept them. 
        /// The value of each entities list element is a complex object of the Entity type.
        /// </summary>
        IList<Entity> Entities { get; set; }

        /// <summary>
        /// The From field describes which client, Agent, or channel generated an Activity. The value of the From 
        /// field is a complex object of the ChannelAccount type.
        /// </summary>
        ChannelAccount From { get; set; }

        /// <summary>
        /// The Id field establishes the identity for the Activity once it has been recorded in the channel. Activities 
        /// in-flight that have not yet been recorded do not have identities. Not all activities are assigned identities 
        /// (for example, a typing Activity may never be assigned an id.) 
        /// </summary>
        string Id { get; set; }

        /// <summary>
        /// The locale field communicates the language code of the text field. The value of the locale field is an IETF BCP-47
        /// language tag within a string.
        /// </summary>
        string Locale { get; set; }

        /// <summary>
        /// The localTimestamp field expresses the datetime and timezone offset where the Activity was generated. This may be 
        /// different from the UTC timestamp where the Activity was recorded. The value of the localTimestamp field is an 
        /// ISO 8601 encoded datetime within a string.
        /// </summary>
        DateTimeOffset? LocalTimestamp { get; set; }

        /// <summary>
        /// The LocalTimezone field expresses the timezone where the Activity was generated. The value of the localTimezone 
        /// field is a time zone name (zone entry) per the IANA Time Zone database.
        /// </summary>
        string LocalTimezone { get; set; }

        /// <summary>
        /// The recipient field describes which client or Agent is receiving this Activity. This field is only meaningful when 
        /// an Activity is transmitted to exactly one recipient; it is not meaningful when it is broadcast to multiple recipients 
        /// (as happens when an Activity is sent to a channel). The purpose of the field is to allow the recipient to identify 
        /// themselves. This is helpful when a client or Agent has more than one identity within the channel. The value of the 
        /// recipient field is a complex object of the Channel account type.
        /// </summary>
        ChannelAccount Recipient { get; set; }

        /// <summary>
        /// The relatesTo field references another conversation, and optionally a specific Activity within that conversation. 
        /// The value of the relatesTo field is a complex object of the Conversation reference type.
        /// </summary>
        /// !!! In the spec this isn't core Activity.  But, there is some places where it seems to be expected (CreateTokenExchangeState)
        ConversationReference RelatesTo { get; set; }

        /// <summary>
        /// The replyToId field identifies the prior Activity to which the current Activity is a reply. This field allows threaded 
        /// conversation and comment nesting to be communicated between participants. replyToId is valid only within the current 
        /// conversation. (See relatesTo for references to other conversations.)
        /// </summary>
        string ReplyToId { get; set; }

        /// <summary>
        /// Activities are frequently sent asynchronously, with separate transport connections for sending and receiving traffic. 
        /// The serviceUrl field is used by channels to denote the URL where replies to the current Activity may be sent. 
        /// </summary>
        string ServiceUrl { get; set; }

        /// <summary>
        /// The timestamp field records the exact UTC time when the Activity occurred. Due to the distributed nature of computing 
        /// systems, the important time is when the channel (the authoritative store) records the Activity. The time when a client 
        /// or Agent initiated an Activity may be transmitted separately in the localTimestamp field. The value of the timestamp field 
        /// is an ISO 8601 date time format encoded datetime within a string.
        /// </summary>
        DateTimeOffset? Timestamp { get; set; }

        /// <summary>
        /// The type field controls the meaning of each Activity, and are by convention short strings (e.g. "message"). 
        /// Senders may define their own application-layer types, although they are encouraged to choose values that are 
        /// unlikely to collide with future well-defined values. If senders use URIs as type values, they SHOULD NOT 
        /// implement URI ladder comparisons to establish equivalence.
        /// </summary>
        string Type { get; set; }

        string RequestId { get; set; }

        IDictionary<string, JsonElement> Properties { get; set; }

        /// <summary>
        /// Updates this Activity with the delivery information from an existing <see cref="ConversationReference"/>.
        /// </summary>
        /// <param name="reference">The existing conversation reference.</param>
        /// <param name="isIncoming">Optional, <c>true</c> to treat the Activity as an
        /// incoming Activity, where the Agent is the recipient; otherwise, <c>false</c>.
        /// Default is <c>false</c>, and the Activity will show the Agent as the sender.</param>
        /// <remarks>Call <see cref="GetConversationReference()"/> on an incoming
        /// Activity to get a conversation reference that you can then use to update an
        /// outgoing Activity with the correct delivery information.
        /// </remarks>
        /// <returns>This Activity, updated with the delivery information.</returns>
        IActivity ApplyConversationReference(ConversationReference reference, bool isIncoming = false);

        /// <summary>
        /// Creates a <see cref="ConversationReference"/> based on this Activity.
        /// </summary>
        /// <returns>A conversation reference for the conversation that contains this Activity.</returns>
        ConversationReference GetConversationReference();
    }
}