// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.MSTeams.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;

namespace Microsoft.Agents.Extensions.MSTeams
{
    /// <summary>
    /// A Teams-specific <see cref="Activity"/> that surfaces the Teams channel payload as a
    /// strongly-typed <see cref="Microsoft.Teams.Apps.Schema.TeamsChannelData"/>.
    /// </summary>
    /// <remarks>
    /// The <c>[ActivityType(ChannelId = "msteams")]</c> annotation auto-registers this type (via the
    /// generated <see cref="ActivityTypeInitAssemblyAttribute"/>), so any inbound Activity whose
    /// <see cref="Activity.ChannelId"/> is <c>"msteams"</c> deserializes to <see cref="TeamsActivity"/>.
    /// The typed <see cref="ChannelData"/> shadow reads through the base <see cref="Activity.ChannelData"/>
    /// (which the deserializer populates as raw JSON), so both the base and typed views stay in sync.
    /// </remarks>
    [ActivityType(ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams)]
    public class TeamsActivity : Activity, ITeamsActivity
    {
        /// <summary>
        /// The Teams channel data carried on the Activity.
        /// </summary>
        public new Microsoft.Teams.Apps.Schema.TeamsChannelData ChannelData
        {
            get => this.GetChannelData<Microsoft.Teams.Apps.Schema.TeamsChannelData>();
            set => base.ChannelData = value;
        }

        /// <inheritdoc/>
        public IEnumerable<QuotedReplyEntity> GetQuotedMessages()
        {
            return Entities?.OfType<QuotedReplyEntity>() ?? [];
        }

        /// <inheritdoc/>
        public ITeamsActivity AddQuotedReply(string messageId, string? text = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

            Entities ??= [];
            Entities.Add(new QuotedReplyEntity
            {
                QuotedReply = new QuotedReplyData { MessageId = messageId }
            });

            var placeholder = $"<quoted messageId=\"{SecurityElement.Escape(messageId)}\"/>";
            Text = $"{Text}{placeholder}";
            if (text != null)
            {
                Text += $" {text}";
            }

            return this;
        }

        /// <inheritdoc/>
        public TargetedMessageInfoEntity? GetTargetedMessageInfo()
        {
            return Entities?.OfType<TargetedMessageInfoEntity>().FirstOrDefault();
        }

        /// <inheritdoc/>
        public ITeamsActivity AddTargetedMessageInfo(string messageId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

            if (Entities?.Any(entity => entity.Type == TargetedMessageInfoEntity.EntityName) != true)
            {
                Entities ??= [];
                Entities.Add(new TargetedMessageInfoEntity { MessageId = messageId });
            }

            return this;
        }

        /// <inheritdoc/>
        public bool IsRecipientTargeted()
        {
            return Recipient?.Properties.TryGetValue("isTargeted", out var isTargeted) == true
                && isTargeted.ValueKind == System.Text.Json.JsonValueKind.True;
        }
    }
}
