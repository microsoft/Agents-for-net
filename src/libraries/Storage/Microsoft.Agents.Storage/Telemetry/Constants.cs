// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Storage.Telemetry
{
    internal static class Constants
    {
        public static readonly string ActivityStorageRead = "agents.storage.read";
        public static readonly string ActivityStorageWrite = "agents.storage.write";
        public static readonly string ActivityStorageDelete = "agents.storage.delete";

        public static readonly string MetricStorageOperationTotal = "agents.storage.operation.total";
        public static readonly string MetricStorageOperationDuration = "agents.storage.operation.duration";
    }
}
