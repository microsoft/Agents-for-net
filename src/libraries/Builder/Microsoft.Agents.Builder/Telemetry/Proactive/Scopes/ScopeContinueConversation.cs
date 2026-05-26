// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Telemetry;
using System;

namespace Microsoft.Agents.Builder.Telemetry.Proactive.Scopes
{
    /// <summary>
    /// A <see cref="TelemetryScope"/> that traces the proactive continuation of an
    /// existing conversation.
    /// </summary>
    /// <remarks>
    /// Records the target conversation identifier, activity type, and activity channel
    /// as span tags.
    /// </remarks>
    internal class ScopeContinueConversation : TelemetryScope
    {

        private readonly string _conversationId;
        private readonly IActivity _activity;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeContinueConversation"/> class.
        /// </summary>
        /// <param name="conversationId">The identifier of the conversation being continued.</param>
        /// <param name="activity">The continuation activity used to resume the conversation.</param>
        public ScopeContinueConversation(string conversationId, IActivity activity) : base(Constants.ScopeContinueConversation)
        {
            _conversationId = conversationId;
            _activity = activity;
        }

        /// <inheritdoc />
        protected override void Callback(System.Diagnostics.Activity activity, double duration, Exception? exception)
        {
            activity.SetTag(TagNames.ConversationId, _conversationId);
            activity.SetTag(TagNames.ActivityType, _activity.Type);
            activity.SetTag(TagNames.ActivityChannelId, _activity.ChannelId?.ToString());
        }
    }
}