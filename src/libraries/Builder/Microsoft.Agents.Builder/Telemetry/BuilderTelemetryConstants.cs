using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.Telemetry
{
    public static class BuilderTelemetryConstants
    {

        /* Activities */

        public static readonly string AdapterProcessOperationName = "adapter process";
        public static readonly string AgentTurnOperationName = "agent turn";
        public static readonly string ConnectorRequestOperationNameFormat = "connector {}";

        /* Metrics */

        /* ChannelAdapter */
        public static readonly string AdapterProcessDurationMetricName = "agents.adapter.process.duration";
        public static readonly string AdapterProcessTotalMetricName = "agents.adapter.process.total";

        /* AgentApplication */
        public static readonly string AgentTurnDurationMetricName = "agents.turn.duration";
        public static readonly string AgentTurnTotalMetricName = "agents.turn.total";
        public static readonly string AgentTurnErrorsMetricName = "agents.turn.errors";

        /* ConnectorClient */
        public static readonly string ConnectorRequestTotalMetricName = "agents.connector.request.total";
        public static readonly string ConnectorRequestDurationMetricName = "agents.connector.request.duration";
            
    }
}
