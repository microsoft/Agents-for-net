// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models.Activities
{
    /// <summary>
    /// Conversation is ending, or a request to end the conversation.
    /// </summary>
    public interface IEndOfConversationActivity : IActivity
    {
        /// <summary>
        /// The Code field contains a programmatic value describing why or how the conversation was ended. The value of 
        /// the Code field is of type string and its meaning is defined by the channel sending the Activity.
        /// </summary>
        string Code { get; set; }

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
