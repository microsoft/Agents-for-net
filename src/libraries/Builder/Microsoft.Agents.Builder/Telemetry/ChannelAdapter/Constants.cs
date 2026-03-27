using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.Telemetry.ChannelAdapter
{
    internal static class Constants
    {
        internal static readonly string ScopeProcess = "agents.adapter.process";
        internal static readonly string ScopeSendActivities = "agents.adapter.send_activities";
        internal static readonly string ScopeUpdateActivity = "agents.adpater.update_activity";
        internal static readonly string ScopeDeleteActivity = "agents.adapter.delete_activity";
        internal static readonly string ScopeContinueConversation = "agents.adapter.continue_conversation";
        internal static readonly string ScopeCreateConnectorClient = "agents.adapter.create_connector_client";
        internal static readonly string ScopeCreateUserTokenClient = "agents.adapter.create_user_token_client";

        internal static readonly string MetricAdapterProcessDuration = "agents.adapter.process.duration";

        internal static readonly string MetricActivitiesReceived = "agents.activities.received";
        internal static readonly string MetricActivitiesSent = "agents.activities.sent";
        internal static readonly string MetricActivitiesUpdated = "agents.activities.updated";
        internal static readonly string MetricActivitiesDeleted = "agents.activities.deleted";
    }
}
