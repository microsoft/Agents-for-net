// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Collections.Generic;

namespace Microsoft.Agents.Core.Models.Activities
{
    /// <summary>
    /// Represents a private suggestion to the <see cref="Activity.Recipient"/> about another activity.
    /// </summary>
    /// <remarks>
    /// The activity's <see cref="Activity.ReplyToId"/> property identifies the activity being referenced.
    /// The activity's <see cref="Activity.Recipient"/> property indicates which user the suggestion is for.
    /// </remarks>
    public interface ISuggestionActivity : IMessageActivity
    {
        /// <summary>
        /// The textHighlights field contains a list of text to highlight in the text field of the Activity referred to by ReplyToId. 
        /// The value of the TextHighlights field is an array of type TextHighlight.
        /// </summary>
        IList<TextHighlight> TextHighlights { get; set; }
    }
}
