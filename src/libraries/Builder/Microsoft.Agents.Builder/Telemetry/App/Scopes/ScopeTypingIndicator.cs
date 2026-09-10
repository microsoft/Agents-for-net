// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Telemetry;
using System;

namespace Microsoft.Agents.Builder.Telemetry.App.Scopes
{
    /// <summary>
    /// A <see cref="TelemetryScope"/> that traces an automatic typing indicator send.
    /// </summary>
    internal class ScopeTypingIndicator : TelemetryScope
    {
        private readonly ITurnContext _turnContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeTypingIndicator"/> class.
        /// </summary>
        /// <param name="turnContext">The turn for which the typing indicator is being sent.</param>
        public ScopeTypingIndicator(ITurnContext turnContext) : base(Constants.ScopeTypingIndicator)
        {
            _turnContext = turnContext;
        }

        /// <inheritdoc />
        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception? error)
        {
            telemetryActivity.SetTag(TagNames.ActivityType, ActivityTypes.Typing);
            telemetryActivity.SetTag(TagNames.ActivityChannelId, _turnContext.Activity.ChannelId?.ToString());
            telemetryActivity.SetTag(TagNames.ConversationId, _turnContext.Activity.Conversation?.Id);
        }
    }
}
