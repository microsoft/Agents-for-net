using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Telemetry;
using System;
using System.Linq;

namespace Microsoft.Agents.Builder.Telemetry.ChannelAdapter.Scopes
{
    internal class ScopeSendActivities : TelemetryScope
    {
        private IActivity[] _activities;

        public ScopeSendActivities(IActivity[] activities) : base(Constants.ScopeSendActivities)
        {
            _activities = activities;
        }

        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception error)
        {
            int count = _activities.Length;
            telemetryActivity.SetTag(TagNames.ActivityCount, count);
            if (count > 0)
            {
                telemetryActivity.SetTag(TagNames.ConversationId, _activities.First().Conversation.Id);
            } else
            {
                telemetryActivity.SetTag(TagNames.ConversationId, TelemetryUtils.Unknown);
            }

            foreach (var activity in _activities)
            {
                Metrics.ActivitiesSent.Add(
                    1,
                    new(TagNames.ActivityType, activity.Type),
                    new(TagNames.ActivityChannelId, activity.ChannelId)
                );
            }
        }
    }
}
