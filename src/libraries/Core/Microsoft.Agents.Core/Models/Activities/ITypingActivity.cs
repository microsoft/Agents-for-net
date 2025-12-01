// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models.Activities
{
    /// <summary>
    /// The From address is typing.
    /// </summary>
    public interface ITypingActivity : IActivity
    {
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
    }
}
