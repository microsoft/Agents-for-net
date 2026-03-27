// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Microsoft.Agents.Core.Telemetry
{
    public static class AgentsTelemetry
    {
        public static readonly string SourceName = "Microsoft.Agents.Core";
        public static readonly string SourceVersion = ThisAssembly.AssemblyFileVersion;

        public static readonly ActivitySource ActivitySource = new(SourceName, SourceVersion);
        public static readonly Meter Meter = new(SourceName, SourceVersion);
    }
}