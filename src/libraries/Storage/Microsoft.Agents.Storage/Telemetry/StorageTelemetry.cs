// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Agents.Core.Telemetry;

namespace Microsoft.Agents.Storage.Telemetry
{

    public static class StorageTelemetry
    {

        /* Metrics */

        private static readonly Counter<long> OperationsTotal = AgentsTelemetry.Meter.CreateCounter<long>(
            Constants.MetricStorageOperationTotal,
            "operation"
        );
        private static readonly Histogram<long> OperationsDuration = AgentsTelemetry.Meter.CreateHistogram<long>(
            Constants.MetricStorageOperationDuration,
            "ms"
        );

        /* Activity helpers */

        public static Activity? StartStorageOp(string activityName, int numKeys)
        {
            TimedActivity timedActivity = AgentsTelemetry.StartTimedActivity(
                activityName,
                (activity, duration, error) =>
                {
                    OperationsTotal.Add(1);
                    OperationsDuration.Record(duration);
                }
            );
            Activity? activity = timedActivity.Activity;
            activity?.SetTag(Core.Telemetry.Constants.AttrNumKeys, numKeys);
            return activity;
        }

        public static IDisposable StartStorageRead(int numKeys)
        {
            return StartStorageOp(Constants.ActivityStorageRead, numKeys);
        }

        public static IDisposable StartStorageWrite(int numKeys)
        {
            return StartStorageOp(Constants.ActivityStorageWrite, numKeys);
        }

        public static IDisposable StartStorageDelete(int numKeys)
        {
            return StartStorageOp(Constants.ActivityStorageDelete, numKeys);
        }
    }
}
