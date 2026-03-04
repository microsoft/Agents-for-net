using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Agents.Core.Telemetry
{
    public static class AgentsTelemetry
    {


        public static readonly Meter Meter = new(TelemetryConstants.SourceName, TelemetryConstants.SourceVersion);


        /* Metrics */

        public static Activity? StartActivity(string name)
        {
            return TelemetryConstants.ActivitySource.StartActivity(name);
        }

        public static TimedActivity StartTimedActivity(
            string operationName,
            Action<Activity?, long, Exception?>? callback = null
            )
        {
            var activity = TelemetryConstants.ActivitySource.StartActivity(operationName);
            return new TimedActivity(activity, callback);
        }
    }
}
