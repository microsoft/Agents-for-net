// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;

namespace Microsoft.Agents.Core.Telemetry
{
    public static class AgentsTelemetryConstants
    {
        public static readonly string SourceName = "Microsoft.Agents.Builder";
        public static readonly string SourceVersion = "1.0.0";
        public static readonly ActivitySource ActivitySource = new(SourceName, SourceVersion);
    }
}
