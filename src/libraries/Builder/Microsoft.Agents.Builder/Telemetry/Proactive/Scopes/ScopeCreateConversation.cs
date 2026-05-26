// Copyright (c) Microsoft Corporation. All rights reserved.
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Core.Telemetry;
using System;

namespace Microsoft.Agents.Builder.Telemetry.Proactive.Scopes
{
    /// <summary>
    /// A <see cref="TelemetryScope"/> that traces the creation of a new proactive
    /// conversation.
    /// </summary>
    /// <remarks>
    /// Records the target channel identifier and the number of members requested for the
    /// new conversation as span tags.
    /// </remarks>
    internal class ScopeCreateConversation : TelemetryScope
    {

        private readonly CreateConversationOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeCreateConversation"/> class.
        /// </summary>
        /// <param name="options">The options used to create the conversation.</param>
        public ScopeCreateConversation(CreateConversationOptions options) : base(Constants.ScopeCreateConversation)
        {
            _options = options;
        }

        /// <inheritdoc />
        protected override void Callback(System.Diagnostics.Activity activity, double duration, Exception? exception)
        {
            activity.SetTag(TagNames.ActivityChannelId, _options?.ChannelId);
            activity.SetTag(TagNames.MembersCount, _options?.Parameters?.Members?.Count ?? 0);
        }
    }
}