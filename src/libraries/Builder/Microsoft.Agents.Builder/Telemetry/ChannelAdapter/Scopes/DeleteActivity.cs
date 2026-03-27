using Microsoft.Agents.Core.Telemetry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.Telemetry.ChannelAdapter.Scopes
{
    internal class DeleteActivity : TelemetryScope
    {
        private Core.Models.Activity _activity;

        public DeleteActivity(Core.Models.Activity activity) : base(Constants.ScopeUpdateActivity)
        {
            _activity = activity;
        }

        protected override void Callback(System.Diagnostics.Activity telemetryActivity, double duration, Exception error)
        {
            telemetryActivity.SetTag(TagNames.ActivityType, _activity.Type);
            telemetryActivity.SetTag(TagNames.ConversationId, _activity.Conversation.Id);

            Metrics.ActivitiesUpdated.Add(
                1,
                new KeyValuePair<string, object?>(TagNames.ActivityChannelId, _activity.ChannelId)
            );
        }
    }
}
