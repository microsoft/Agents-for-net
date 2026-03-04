// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Microsoft.Agents.Core.Telemetry
{
    public static class AgentsTelemetry
    {
        public static readonly Meter Meter = new(AgentsTelemetryConstants.SourceName, AgentsTelemetryConstants.SourceVersion);


        /* Metrics */

        public static Activity? StartActivity(string name)
        {
            return AgentsTelemetryConstants.ActivitySource.StartActivity(name);
        }

        public static TimedActivity StartTimedActivity(
            string operationName,
            Action<Activity?, long, Exception?>? callback = null
            )
        {
            var activity = AgentsTelemetryConstants.ActivitySource.StartActivity(operationName);
            return new TimedActivity(activity, callback);
        }
    }
}
