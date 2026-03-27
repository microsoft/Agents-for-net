using Microsoft.Agents.Core.Telemetry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.Telemetry.ChannelAdapter.Scopes
{
    internal class ContinueConversation : TelemetryScope
    {
        private Core.Models.Activity _activity;

        public ContinueConversation(Core.Models.Activity activity) : base(Constants.ScopeContinueConversation)
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
