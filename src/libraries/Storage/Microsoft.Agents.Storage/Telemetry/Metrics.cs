using Microsoft.Agents.Core.Telemetry;
using System.Diagnostics.Metrics;

namespace Microsoft.Agents.Storage.Telemetry
{
    internal static class Metrics
    {
        internal static readonly Counter<long> OperationTotal = AgentsTelemetry.Meter.CreateCounter<long>(
            Constants.OperationTotal,
            description: "Total number of storage operations performed.",
            unit: "operation");

        internal static readonly Histogram<double> OperationDuration = AgentsTelemetry.Meter.CreateHistogram<double>(
            Constants.OperationDuration,
            description: "Duration of storage operations in milliseconds.",
            unit: "ms");
    }
}
