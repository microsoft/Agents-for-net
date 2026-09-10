// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.MSTeams.Models;
using System.Collections.Generic;

namespace Microsoft.Agents.Extensions.MSTeams
{
    /// <summary>
    /// A Teams-specific <see cref="IActivity"/> that exposes the Teams channel data as a strongly-typed
    /// <see cref="Microsoft.Teams.Apps.Schema.TeamsChannelData"/> instead of the loosely-typed <see cref="IActivity.ChannelData"/>.
    /// </summary>
    public interface ITeamsActivity : IActivity
    {
        /// <summary>
        /// The Teams channel data carried on the Activity.
        /// </summary>
        new Microsoft.Teams.Apps.Schema.TeamsChannelData ChannelData { get; set; }

        /// <summary>
        /// Gets the quoted reply entities carried by this Teams activity.
        /// </summary>
        IEnumerable<QuotedReplyEntity> GetQuotedMessages();

        /// <summary>
        /// Adds a quoted reply entity and its inline text placeholder to this Teams activity.
        /// </summary>
        /// <param name="messageId">The ID of the message being quoted.</param>
        /// <param name="text">Optional text to append after the quoted message placeholder.</param>
        /// <returns>This activity.</returns>
        ITeamsActivity AddQuotedReply(string messageId, string? text = null);

        /// <summary>
        /// Gets the Prompt Preview metadata carried by this Teams activity.
        /// </summary>
        TargetedMessageInfoEntity? GetTargetedMessageInfo();

        /// <summary>
        /// Adds Prompt Preview metadata referencing an inbound targeted message.
        /// </summary>
        /// <param name="messageId">The ID of the inbound targeted message.</param>
        /// <returns>This activity.</returns>
        ITeamsActivity AddTargetedMessageInfo(string messageId);

        /// <summary>
        /// Determines whether the inbound Teams activity was targeted to its recipient.
        /// </summary>
        bool IsRecipientTargeted();
    }
}
