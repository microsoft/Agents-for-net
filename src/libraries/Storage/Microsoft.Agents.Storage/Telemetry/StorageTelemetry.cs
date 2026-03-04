// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics.Metrics;
using Microsoft.Agents.Core.Telemetry;

namespace Microsoft.Agents.Storage.Telemetry
{

    public static class StorageTelemetry
    {

        /* Metrics */

        private static readonly Counter<long> OperationsTotal = AgentsTelemetry.Meter.CreateCounter<long>(
            StorageTelemetryConstants.OperationTotalMetricName,
            "operation"
        );
        private static readonly Histogram<long> OperationsDuration = AgentsTelemetry.Meter.CreateHistogram<long>(
            StorageTelemetryConstants.OperationDurationMetricName,
            "ms"
        );

        public static TimedActivity StartStorageOp(string operationType)
        {
            string operationName = String.Format(
                StorageTelemetryConstants.OperationNameFormat,
                operationType);

            return AgentsTelemetry.StartTimedActivity(
                operationName,
                (activity, duration, error) =>
                {
                    OperationsTotal.Add(1);
                    OperationsDuration.Record(duration);
                }
            );
        }
    }
}
