// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Extensions.Teams;

/// <summary>
/// Provides extension methods for converting between Teams and core Activity Protocol models.
/// </summary>
/// <remarks>These extension methods simplify interoperability between Microsoft Teams APIs and core Activity
/// Protocol models by enabling direct conversion of common types. This facilitates integration scenarios where
/// objects need to be translated between Teams-specific and core representations, such as when building agents
/// that operate across both domains.</remarks>
public static class TeamsModelExtensions
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
    /// <summary>
    /// Converts a Teams <c>Activity</c> to its corresponding <c>Microsoft.Agents.Core.Models.IActivity</c>.
    /// </summary>
    /// <typeparam name="T">The type of the Teams activity to convert. Must derive from Microsoft.Teams.Api.Activities.Activity.</typeparam>
    /// <param name="teamsActivity">The Teams activity instance to convert.</param>
    /// <returns>An instance of <c>Microsoft.Agents.Core.Models.IActivity</c> that represents the converted activity.</returns>
    public static Core.Models.IActivity ToCoreActivity<T>(this T teamsActivity) where T : Microsoft.Teams.Api.Activities.Activity
    {
        var coreActivity = ProtocolJsonSerializer.ToObject<Core.Models.IActivity>(teamsActivity);
        if (teamsActivity is Microsoft.Teams.Api.Activities.MessageActivity messageActivity)
        {
            coreActivity.Text = (messageActivity.Text == "" ? null : messageActivity.Text);
        }
        return coreActivity;
    }

    /// <summary>
    /// Converts an <c>Microsoft.Agents.Core.Models.IActivity</c> to a Microsoft Teams <c>Activity</c> instance.
    /// </summary>
    /// <remarks>The returned activity may be of a specific subtype, such as <c>MessageActivity</c>, depending on the input.</remarks>
    /// <param name="activity">The activity to convert.</param>
    /// <returns>A Microsoft Teams <c>Activity</c> that represents the specified <c>Microsoft.Agents.Core.Models.IActivity</c>.</returns>
    public static Microsoft.Teams.Api.Activities.Activity ToTeamsActivity(this Core.Models.IActivity activity)
    {
        var teamsActivity = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.Activities.Activity>(activity);
        if (teamsActivity is Microsoft.Teams.Api.Activities.MessageActivity messageActivity)
        {
            messageActivity.Text = activity.Text;
        }
        return teamsActivity;
    }

    /// <summary>
    /// Converts a Microsoft Teams account to a <c>Microsoft.Agents.Core.Models.ChannelAccount</c> model.
    /// </summary>
    /// <param name="teamsAccount">The Microsoft Teams account to convert.</param>
    /// <returns>A <c>Microsoft.Agents.Core.Models.ChannelAccount</c> model representing the specified Teams <c>Account</c>.</returns>
    public static Core.Models.ChannelAccount ToCoreChannelAccount(this Microsoft.Teams.Api.Account teamsAccount)
    {
        return ProtocolJsonSerializer.ToObject<Core.Models.ChannelAccount>(teamsAccount);
    }

    /// <summary>
    /// Converts a <c>Microsoft.Agents.Core.Models.ChannelAccount</c> instance to a Teams <c>Account</c> object.
    /// </summary>
    /// <param name="channelAccount">The ChannelAccount to convert.</param>
    /// <returns>A Teams <c>Account</c> object representing the specified <c>Microsoft.Agents.Core.Models.ChannelAccount</c>.</returns>
    public static Microsoft.Teams.Api.Account ToTeamsAccount(this Core.Models.ChannelAccount channelAccount)
    {
        return ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.Account>(channelAccount);
    }

    /// <summary>
    /// Converts a Teams <c>Reaction</c> to its corresponding <c>Microsoft.Agents.Core.Models.MessageReaction</c> model.
    /// </summary>
    /// <param name="teamsReaction">The Teams message reaction to convert.</param>
    /// <returns>A <c>Microsoft.Agents.Core.Models.MessageReaction</c> model that represents the specified Teams <c>Reaction</c>.</returns>
    public static Core.Models.MessageReaction ToCoreMessageReaction(this Microsoft.Teams.Api.Messages.Reaction teamsReaction)
    {
        return ProtocolJsonSerializer.ToObject<Core.Models.MessageReaction>(teamsReaction);
    }

    /// <summary>
    /// Converts an <c>Microsoft.Agents.Core.Models.MessageReaction</c> to a Microsoft Teams <c>Reaction</c> object.
    /// </summary>
    /// <param name="messageReaction">The message reaction to convert.</param>
    /// <returns>A Microsoft Teams <c>Reaction</c> object that represents the specified <c>Microsoft.Agents.Core.Models.MessageReaction</c>.</returns>
    public static Microsoft.Teams.Api.Messages.Reaction ToTeamsReaction(this Core.Models.MessageReaction messageReaction)
    {
        return ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.Messages.Reaction>(messageReaction);
    }
    #endregion
}
