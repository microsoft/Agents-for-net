// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams
{
    public static class ModelExtensions
    {
        public static Microsoft.Teams.Api.Attachment ToTeamsAttachment(this Microsoft.Agents.Core.Models.ThumbnailCard card)
        {
            return new Microsoft.Teams.Api.Attachment()
            {
                ContentType = Microsoft.Teams.Api.ContentType.ThumbnailCard,
                Content = card,
            };
        }
    }
}
