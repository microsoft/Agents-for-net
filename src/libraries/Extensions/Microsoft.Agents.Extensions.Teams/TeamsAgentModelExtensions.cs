// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Extensions.Teams
{
    /// <summary>
    /// Provides extension methods for converting between Teams and core agent models.
    /// </summary>
    /// <remarks>These extension methods simplify interoperability between Microsoft Teams APIs and core agent
    /// models by enabling direct conversion of common types. This facilitates integration scenarios where objects
    /// need to be translated between Teams-specific and core representations, such as when building agents that
    /// operate across both domains.</remarks>
    public static class TeamsAgentModelExtensions
    {
        #region Cards
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
        #endregion

        #region AP
        public static Core.Models.IActivity ToCoreActivity<T>(this T teamsActivity) where T : Microsoft.Teams.Api.Activities.Activity
        {
            return ProtocolJsonSerializer.ToObject<Core.Models.IActivity>(teamsActivity);
        }

        public static Microsoft.Teams.Api.Activities.Activity ToTeamsActivity(this Core.Models.IActivity activity)
        {
            return ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.Activities.Activity>(activity);
        }

        public static Core.Models.ChannelAccount ToCoreChannelAccount(this Microsoft.Teams.Api.Account teamsAccount)
        {
            return ProtocolJsonSerializer.ToObject<Core.Models.ChannelAccount>(teamsAccount);
        }

        public static Microsoft.Teams.Api.Account ToTeamsAccount(this Core.Models.ChannelAccount channelAccount)
        {
            return ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.Account>(channelAccount);
        }

        public static Core.Models.MessageReaction ToCoreMessageReaction(this Microsoft.Teams.Api.Messages.Reaction teamsReaction)
        {
            return ProtocolJsonSerializer.ToObject<Core.Models.MessageReaction>(teamsReaction);
        }

        public static Microsoft.Teams.Api.Messages.Reaction ToTeamsReaction(this Core.Models.MessageReaction messageReaction)
        {
            return ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.Messages.Reaction>(messageReaction);
        }

        #endregion
    }
}
