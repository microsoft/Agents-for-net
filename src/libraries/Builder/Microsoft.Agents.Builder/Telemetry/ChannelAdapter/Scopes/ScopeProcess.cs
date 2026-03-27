using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Telemetry;
using System;
using System.Diagnostics;

namespace Microsoft.Agents.Builder.Telemetry.ChannelAdapter.Scopes
{
    internal class ScopeProcess : TelemetryScope
    {
        private IActivity? _activity = null;

        public ScopeProcess() : base(Constants.ScopeProcess)
        {
        }

        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception error)
        {
            TagList metricTagList = new TagList();
            if (_activity != null)
            {
                telemetryActivity.SetTag(TagNames.ActivityType, _activity.Type);
                telemetryActivity.SetTag(TagNames.ActivityChannelId, _activity.ChannelId);
                telemetryActivity.SetTag(TagNames.ActivityDeliveryMode, _activity.DeliveryMode);
                telemetryActivity.SetTag(TagNames.ConversationId, _activity.Conversation.Id);
                telemetryActivity.SetTag(TagNames.IsAgentic, _activity.IsAgenticRequest());

                metricTagList.Add(TagNames.ActivityType, _activity.Type);
                metricTagList.Add(TagNames.ActivityChannelId, _activity.ChannelId);
            }

            Metrics.AdapterProcessDuration.Record(duration, metricTagList);
            Metrics.ActivitiesReceived.Add(1, metricTagList);
        }

        public void Share(IActivity activity)
        {
            _activity = activity;
        }
    }
}
