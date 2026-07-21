// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Base class for rich cards that can be sent to a user as an <see cref="Microsoft.Agents.Core.Models.Attachment"/>.
    /// </summary>
    /// <remarks>
    /// Each concrete card implements <see cref="ToAttachment"/> to wrap itself in an attachment using its own
    /// content type. <see cref="ToMessage"/> builds on that to produce a ready-to-send message activity.
    /// </remarks>
    public abstract class Card
    {
        /// <summary>
        /// Creates a new <see cref="Microsoft.Agents.Core.Models.Attachment"/> that wraps this card.
        /// </summary>
        /// <returns> The generated attachment.</returns>
        public abstract Attachment ToAttachment();

        /// <summary>
        /// Creates a new message activity that includes this card as an attachment.
        /// </summary>
        /// <remarks>Use this method to generate a message activity suitable for sending to a user, with
        /// the current card included as an attachment. The returned activity has its type set to <see
        /// langword="ActivityTypes.Message"/>.</remarks>
        /// <returns>An <see cref="IActivity"/> representing a message activity with the card attached.</returns>
        public virtual IActivity ToMessage()
        {
            return new Activity
            {
                Type = ActivityTypes.Message,
                Attachments = [ToAttachment()]
            };
        }
    }
}
