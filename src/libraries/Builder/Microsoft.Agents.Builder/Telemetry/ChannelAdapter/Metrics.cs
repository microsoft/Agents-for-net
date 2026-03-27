using Microsoft.Agents.Core.Telemetry;
using System.Diagnostics.Metrics;

namespace Microsoft.Agents.Builder.Telemetry.ChannelAdapter
{
    internal static class Metrics
    {
        internal static Histogram<double> AdapterProcessDuration = AgentsTelemetry.Meter.CreateHistogram<double>(
            Constants.MetricAdapterProcessDuration,
            unit: "ms",
            description: "Duration of processing an activity in the adapter");
        
        internal static Counter<long> ActivitiesReceived = AgentsTelemetry.Meter.CreateCounter<long>(
            Constants.MetricActivitiesReceived,
            unit: "activity",
            description: "Number of activities received by the adapter");

        internal static Counter<long> ActivitiesSent = AgentsTelemetry.Meter.CreateCounter<long>(
            Constants.MetricActivitiesSent,
            unit: "activity",
            description: "Number of activities sent by the adapter");

        internal static Counter<long> ActivitiesUpdated = AgentsTelemetry.Meter.CreateCounter<long>(
            Constants.MetricActivitiesUpdated,
            unit: "activity",
            description: "Number of activities updated by the adapter");
        
        internal static Counter<long> ActivitiesDeleted = AgentsTelemetry.Meter.CreateCounter<long>(
            Constants.MetricActivitiesDeleted,
            unit: "activity",
            description: "Number of activities deleted by the adapter");
    }
}
