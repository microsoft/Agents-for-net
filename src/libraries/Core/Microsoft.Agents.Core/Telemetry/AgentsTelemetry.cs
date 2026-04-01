// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Microsoft.Agents.Core.Telemetry
{
    /// <summary>
    /// Provides the shared <see cref="ActivitySource"/> and <see cref="Meter"/> used by the
    /// Microsoft Agents SDK to emit distributed traces and metrics.
    /// </summary>
    /// <remarks>
    /// All SDK telemetry is published under a single source name so that consumers can
    /// subscribe to <c>"Microsoft.Agents.Core"</c> to capture every <see cref="Activity"/>
    /// and metric produced by the Agents SDK.
    /// </remarks>
    public static class AgentsTelemetry
    {
        /// <summary>
        /// The name used by <see cref="ActivitySource"/> and <see cref="Meter"/> to identify
        /// telemetry originating from the Microsoft Agents SDK.
        /// </summary>
        public static readonly string SourceName = "Microsoft.Agents.Core";

        /// <summary>
        /// The version of the telemetry source, derived from the assembly file version at build time.
        /// </summary>
        public static readonly string SourceVersion = ThisAssembly.AssemblyFileVersion;

        /// <summary>
        /// The <see cref="System.Diagnostics.ActivitySource"/> used to create distributed-tracing
        /// <see cref="Activity"/> instances throughout the SDK.
        /// </summary>
        public static readonly ActivitySource ActivitySource = new(SourceName, SourceVersion);

        /// <summary>
        /// The <see cref="System.Diagnostics.Metrics.Meter"/> used to create counters,
        /// histograms, and other metric instruments throughout the SDK.
        /// </summary>
        public static readonly Meter Meter = new(SourceName, SourceVersion);
    }
}