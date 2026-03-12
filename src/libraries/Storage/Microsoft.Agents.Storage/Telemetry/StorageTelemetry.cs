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
            Metrics.OperationTotal,
            "operation"
        );
        private static readonly Histogram<long> OperationsDuration = AgentsTelemetry.Meter.CreateHistogram<long>(
            Metrics.OperationDuration,
            "ms"
        );

        /* Activity helpers */

        public static TimedActivity StartStorageOp(string activityName, int numKeys)
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
            activity?.SetTag(Attributes.NumKeys, numKeys);
            return timedActivity;
        }

        public static IDisposable StartStorageRead(int numKeys)
        {
            return StartStorageOp(Scopes.Read, numKeys);
        }

        public static IDisposable StartStorageWrite(int numKeys)
        {
            return StartStorageOp(Scopes.Write, numKeys);
        }

        public static IDisposable StartStorageDelete(int numKeys)
        {
            return StartStorageOp(Scopes.Delete, numKeys);
        }
    }
}
