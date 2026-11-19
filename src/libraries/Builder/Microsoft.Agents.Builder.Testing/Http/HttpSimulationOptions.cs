// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Builder.Testing.Http
{
    /// <summary>Bounds and optional request-body capture for one HTTP simulation.</summary>
    public sealed class HttpSimulationOptions
    {
        /// <summary>Gets or sets the maximum number of journaled attempts. Defaults to 1,000.</summary>
        public int MaxRequests { get; set; } = 1000;

        /// <summary>Gets or sets the maximum body size used for matching or capture. Defaults to 64 KiB.</summary>
        public int MaxRequestBodyBytes { get; set; } = 65536;

        /// <summary>Gets or sets whether request bodies are included in exchange snapshots. Defaults to false.</summary>
        public bool CaptureRequestBody { get; set; }
    }
}
