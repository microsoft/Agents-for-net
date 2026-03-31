using Microsoft.Agents.Core.Telemetry;
using System.Diagnostics.Metrics;

namespace Microsoft.Agents.Builder.Telemetry.App
{
    internal static  class Metrics
    {
        internal static Counter<long> TurnCount = AgentsTelemetry.Meter.CreateCounter<long>(
            Constants.MetricTurnCount,
            unit: "turn",
            description: "Number of turns processed by the agent");

        internal static Counter<long> TurnErrorCount = AgentsTelemetry.Meter.CreateCounter<long>(
            Constants.MetricTurnErrorCount,
            unit: "turn",
            description: "Number of turns that resulted in an error");

        internal static Histogram<double> TurnDuration = AgentsTelemetry.Meter.CreateHistogram<double>(
            Constants.MetricTurnDuration,
            unit: "ms",
            description: "Duration of processing a turn in milliseconds");
    }
}
