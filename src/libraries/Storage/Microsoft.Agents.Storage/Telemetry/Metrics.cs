using Microsoft.Agents.Core.Telemetry;
using System.Diagnostics.Metrics;

namespace Microsoft.Agents.Storage.Telemetry
{
    internal static class Metrics
    {
        internal static readonly Counter<long> OperationTotal = AgentsTelemetry.Meter.CreateCounter<long>(
            Constants.MetricOperationTotal,
            description: "Total number of storage operations performed.",
            unit: "operation");

        internal static readonly Histogram<double> OperationDuration = AgentsTelemetry.Meter.CreateHistogram<double>(
            Constants.MetricOperationDuration,
            description: "Duration of storage operations in milliseconds.",
            unit: "ms");
    }
}
