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

        public static Microsoft.Teams.Api.Attachment ToTeamsAttachment(this Microsoft.Agents.Core.Models.HeroCard card)
        {
            return new Microsoft.Teams.Api.Attachment()
            {
                ContentType = Microsoft.Teams.Api.ContentType.HeroCard,
                Content = card,
            };
        }

        public static Microsoft.Teams.Api.Attachment ToTeamsAttachment(this Microsoft.Agents.Core.Models.AudioCard card)
        {
            return new Microsoft.Teams.Api.Attachment()
            {
                ContentType = Microsoft.Teams.Api.ContentType.AudioCard,
                Content = card,
            };
        }

        public static Microsoft.Teams.Api.Attachment ToTeamsAttachment(this Microsoft.Agents.Core.Models.AnimationCard card)
        {
            return new Microsoft.Teams.Api.Attachment()
            {
                ContentType = Microsoft.Teams.Api.ContentType.AnimationCard,
                Content = card,
            };
        }
    }
}
