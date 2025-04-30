﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Represents the attachment in a message.
    /// </summary>
    public class MessageActionsPayloadAttachment
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MessageActionsPayloadAttachment"/> class.
        /// </summary>
        public MessageActionsPayloadAttachment()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageActionsPayloadAttachment"/> class.
        /// </summary>
        /// <param name="id">The id of the attachment.</param>
        /// <param name="contentType">The type of the attachment.</param>
        /// <param name="contentUrl">The url of the attachment, in case of an
        /// external link.</param>
        /// <param name="content">The content of the attachment, in case of a
        /// code snippet, email, or file.</param>
        /// <param name="name">The plaintext display name of the
        /// attachment.</param>
        /// <param name="thumbnailUrl">The url of a thumbnail image that might
        /// be embedded in the attachment, in case of a card.</param>
        public MessageActionsPayloadAttachment(string id = default, string contentType = default, string contentUrl = default, object content = default, string name = default, string thumbnailUrl = default)
        {
            Id = id;
            ContentType = contentType;
            ContentUrl = contentUrl;
            Content = content;
            Name = name;
            ThumbnailUrl = thumbnailUrl;
        }

        /// <summary>
        /// Gets or sets the id of the attachment.
        /// </summary>
        /// <value>The attachment ID.</value>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the type of the attachment.
        /// </summary>
        /// <value>The type of the attachment.</value>
        public string ContentType { get; set; }

        /// <summary>
        /// Gets or sets the url of the attachment, in case of an external link.
        /// </summary>
        /// <value>The URL of the attachment, in case of an external link.</value>
        public string ContentUrl { get; set; }

        /// <summary>
        /// Gets or sets the content of the attachment, in case of a code
        /// snippet, email, or file.
        /// </summary>
        /// <value>The content of the attachment.</value>
        public object Content { get; set; }

        /// <summary>
        /// Gets or sets the plaintext display name of the attachment.
        /// </summary>
        /// <value>The plaintext display name of the attachment.</value>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the url of a thumbnail image that might be embedded in
        /// the attachment, in case of a card.
        /// </summary>
        /// <value>The URL of the thumbnail image that might be embedded in the attachment.</value>
        public string ThumbnailUrl { get; set; }
    }
}
