using Microsoft.Agents.Core.Telemetry;
using System.Diagnostics.Metrics;

namespace Microsoft.Agents.Authentication.Telemetry
{
    internal static class Metrics
    {
        internal static Counter<long> TokenRequestCount = AgentsTelemetry.Meter.CreateCounter<long>(
            Constants.MetricTokenRequestCount,
            unit: "request",
            description: "Number of token requests made to the authentication service");

        internal static Histogram<double> TokenRequestDuration = AgentsTelemetry.Meter.CreateHistogram<double>(
            Constants.MetricTokenRequestDuration,
            unit: "ms",
            description: "Duration of token requests to the authentication service in milliseconds");
    }
}
