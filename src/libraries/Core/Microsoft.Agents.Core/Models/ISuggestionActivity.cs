// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models
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
    }
}
