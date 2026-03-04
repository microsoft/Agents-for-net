using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Agents.Storage.Telemetry
{
    public static class StorageTelemetryConstants
    {
        public static readonly string OperationNameFormat = "storage {}";

        public static readonly string OperationDurationMetricName = "storage.operation.duration";
        public static readonly string OperationTotalMetricName = "storage.operations.total";
    }
}
