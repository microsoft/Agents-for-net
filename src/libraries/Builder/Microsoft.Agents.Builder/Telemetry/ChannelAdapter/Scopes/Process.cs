using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Telemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Agents.Builder.Telemetry.ChannelAdapter.Scopes
{
    internal class Process : TelemetryScope
    {
        private Core.Models.Activity _activity;

        public Process(Core.Models.Activity activity) : base(Constants.ScopeProcess)
        {
            _activity = activity;
        }

        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception error)
        {
            telemetryActivity.SetTag(TagNames.ActivityType, _activity.Type);
            telemetryActivity.SetTag(TagNames.ActivityChannelId, _activity.ChannelId);
            telemetryActivity.SetTag(TagNames.ActivityDeliveryMode, _activity.DeliveryMode);
            telemetryActivity.SetTag(TagNames.ConversationId, _activity.Conversation.Id);
            telemetryActivity.SetTag(TagNames.IsAgentic, _activity.IsAgenticRequest());

            var metricTagList = new TagList
            {
                new(TagNames.ActivityType, _activity.Type),
                new(TagNames.ActivityChannelId, _activity.ChannelId)
            };

            Metrics.AdapterProcessDuration.Record(duration, metricTagList);
            Metrics.ActivitiesReceived.Add(1, metricTagList);
        }
    }
}
