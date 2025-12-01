// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Core.Models.Activities
{
    /// <summary>
    /// Message activities represent content intended to be shown within a conversational interface. Message activities may contain 
    /// text, speech, interactive cards, and binary or unknown attachments; typically channels require at most one of these for the 
    /// message activity to be well-formed.
    /// </summary>
    public interface IMessageActivity : IActivity
    {
        /// <summary>
        /// The AttachmentLayout field instructs user interface renderers how to present content included in the attachments 
        /// field. The value of the attachmentLayout field is of type string, with defined values of list and carousel.
        /// </summary>
        string AttachmentLayout { get; set; }

        /// <summary>
        /// The Attachments field contains a flat list of objects to be displayed as part of this Activity. The value of 
        /// each attachments list element is a complex object of the Attachment type.
        /// </summary>
        IList<Attachment> Attachments { get; set; }

        /// <summary>
        /// The Expiration field contains a time at which the Activity should be considered to be "expired" and 
        /// should not be presented to the recipient. The value of the expiration field is an ISO 8601 date time 
        /// format encoded datetime within a string.
        /// </summary>
        DateTimeOffset? Expiration { get; set; }

        /// <summary>
        /// The importance field contains an enumerated set of values to signal to the recipient the relative importance 
        /// of the Activity. It is up to the receiver to map these importance hints to the user experience. The value of 
        /// the importance field is of type string, with defined values of low, normal and high. The default value is normal.
        /// </summary>
        string Importance { get; set; }

        /// <summary>
        /// The inputHint field indicates whether or not the generator of the Activity is anticipating a response. This 
        /// field is used predominantly within channels that have modal user interfaces, and is typically not used in channels 
        /// with continuous chat feeds. The value of the inputHint field is of type string, with defined values of accepting, 
        /// expecting, and ignoring. The default value is accepting.
        /// </summary>
        string InputHint { get; set; }

        /// <summary>
        /// The listenFor field contains a list of terms or references to term sources that speech and language processing systems 
        /// can listen for.
        /// </summary>
        IList<string> ListenFor { get; set; }

        /// <summary>
        /// The semanticAction field contains an optional programmatic action accompanying the user request. The semantic action 
        /// field is populated by the channel and Agent based on some understanding of what the user is trying to accomplish; this 
        /// understanding may be achieved with natural language processing, additional user interface elements tied specifically 
        /// to these actions, through a process of conversational refinement, or contextually via other means. The meaning and 
        /// structure of the semantic action is agreed ahead of time between the channel and the Agent.
        /// </summary>
        SemanticAction SemanticAction { get; set; }

        /// <summary>
        /// The speak field indicates how the Activity should be spoken via a text-to-speech system. The field is only used to customize 
        /// speech rendering when the default is deemed inadequate. It replaces speech synthesis for any content within the Activity, 
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
        /// The value field contains a programmatic payload specific to the Activity being sent. Its meaning and format 
        /// are defined in other sections of this document that describe its use.
        /// </summary>
        object Value { get; set; }

        /// <summary>
        /// The valueType field is a string type which contains a unique value which identifies the shape of the value.
        /// </summary>
        string ValueType { get; set; }
    }
}
