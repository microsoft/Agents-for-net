using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Telemetry;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Builder.Telemetry.ChannelAdapter.Scopes
{
    internal class ScopeUpdateActivity : TelemetryScope
    {
        private IActivity _activity;

        public ScopeUpdateActivity(IActivity activity) : base(Constants.ScopeUpdateActivity)
        {
            _activity = activity;
        }

        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception error)
        {
            telemetryActivity.SetTag(TagNames.ActivityId, _activity.Id);
            telemetryActivity.SetTag(TagNames.ConversationId, _activity.Conversation.Id);

            Metrics.ActivitiesUpdated.Add(
                1,
                new KeyValuePair<string, object?>(TagNames.ActivityChannelId, _activity.ChannelId)
            );
        }
    }
}
