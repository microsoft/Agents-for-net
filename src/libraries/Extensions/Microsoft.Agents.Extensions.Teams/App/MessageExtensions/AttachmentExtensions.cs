// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Extensions.Teams.App.MessageExtensions
{
    /// <summary>
    /// Attachment extensions.
    /// </summary>
    public static class AttachmentExtensions
    {
        /// <summary>
        /// Converts normal attachment into the messaging extension attachment.
        /// </summary>
        /// <param name="attachment">The attachment.</param>
        /// <param name="previewAttachment">The preview attachment.</param>
        /// <returns>Messaging extension attachment.</returns>
        public static Microsoft.Teams.Api.MessageExtensions.Attachment ToMessagingExtensionAttachment(this Attachment attachment, Attachment previewAttachment = null)
        {
            // We are recreating the attachment so that JsonSerializerSettings with ReferenceLoopHandling set to Error does not generate error
            // while serializing. Refer to issue - https://github.com/OfficeDev/BotBuilder-MicrosoftTeams/issues/52.
            return new Microsoft.Teams.Api.MessageExtensions.Attachment
            {
                Content = attachment.Content,
                ContentType = new Microsoft.Teams.Api.ContentType(attachment.ContentType),
                ContentUrl = attachment.ContentUrl,
                Name = attachment.Name,
                ThumbnailUrl = attachment.ThumbnailUrl,
                Preview = previewAttachment != null ? ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.Attachment>(ProtocolJsonSerializer.ToJson(attachment)) : null
            };
        }
    }
}
