// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Storage.Telemetry
{
    public static class StorageTelemetryConstants
    {
        public static readonly string OperationNameFormat = "storage {}";

        public static readonly string OperationDurationMetricName = "storage.operation.duration";
        public static readonly string OperationTotalMetricName = "storage.operations.total";
    }
}
