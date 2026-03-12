// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Microsoft.Agents.Core.Telemetry
{
    public static class AgentsTelemetry
    {
        public static readonly string SourceName = "Microsoft.Agents.Builder";
        public static readonly string SourceVersion = "1.0.0";

        public static readonly ActivitySource ActivitySource = new(SourceName, SourceVersion);
        public static readonly Meter Meter = new(SourceName, SourceVersion);

        /* Metrics */

        public static Activity? StartActivity(string name)
        {
            return ActivitySource.StartActivity(name);
        }

        public static TimedActivity StartTimedActivity(
            string operationName,
            Action<Activity?, long, Exception?>? callback = null
            )
        {
            var activity = ActivitySource.StartActivity(operationName);
            return new TimedActivity(activity, callback);
        }
    }
}
