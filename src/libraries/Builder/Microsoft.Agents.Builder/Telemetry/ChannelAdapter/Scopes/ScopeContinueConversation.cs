using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Telemetry;
using System;

namespace Microsoft.Agents.Builder.Telemetry.ChannelAdapter.Scopes
{
    internal class ScopeContinueConversation : TelemetryScope
    {
        private IActivity _activity;

        public ScopeContinueConversation(IActivity activity) : base(Constants.ScopeContinueConversation)
        {
            _activity = activity;
        }

        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception error)
        {
            telemetryActivity.SetTag(TagNames.AppId, _activity.Recipient.Id);
            telemetryActivity.SetTag(TagNames.ConversationId, _activity.Conversation.Id);
            telemetryActivity.SetTag(TagNames.IsAgentic, _activity.IsAgenticRequest());
        }
    }
}
