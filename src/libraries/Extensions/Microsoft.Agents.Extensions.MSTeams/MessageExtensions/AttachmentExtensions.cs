// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Teams.Core.Schema;
using System;

namespace Microsoft.Agents.Extensions.MSTeams.MessageExtensions;

/// <summary>
/// Extension methods for converting <see cref="Microsoft.Agents.Core.Models.Attachment"/> instances into Teams message extension attachments.
/// </summary>
public static class AttachmentExtensions
{
    /// <summary>
    /// Converts normal attachment into the messaging extension attachment.
    /// </summary>
    /// <param name="attachment">The attachment.</param>
    /// <param name="previewAttachment">The preview attachment.</param>
    /// <returns>Messaging extension attachment.</returns>
    public static Microsoft.Teams.Apps.Schema.TeamsAttachment ToMessagingExtensionAttachment(this Attachment attachment, Attachment previewAttachment = null)
    {
        // We are recreating the attachment so that JsonSerializerSettings with ReferenceLoopHandling set to Error does not generate error
        // while serializing. Refer to issue - https://github.com/OfficeDev/BotBuilder-MicrosoftTeams/issues/52.
        var result = new Microsoft.Teams.Apps.Schema.TeamsAttachment
        {
            Content = attachment.Content,
            ContentType = new Microsoft.Teams.Apps.Schema.AttachmentContentType(attachment.ContentType),
            ContentUrl = string.IsNullOrEmpty(attachment.ContentUrl) ? null : new Uri(attachment.ContentUrl, UriKind.RelativeOrAbsolute),
            Name = attachment.Name,
            ThumbnailUrl = string.IsNullOrEmpty(attachment.ThumbnailUrl) ? null : new Uri(attachment.ThumbnailUrl, UriKind.RelativeOrAbsolute),
        };

        if (previewAttachment != null)
        {
            result.Properties = new ExtendedPropertiesDictionary
            {
                ["preview"] = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Apps.Schema.TeamsAttachment>(previewAttachment)
            };
        }

        return result;
    }
}
