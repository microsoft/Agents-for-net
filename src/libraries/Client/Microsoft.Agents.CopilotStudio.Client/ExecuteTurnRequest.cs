// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.CopilotStudio.Client
{
    /// <summary>
    /// Turn request wrapper for communicating with Copilot Studio Engine.
    /// </summary>
    public record ExecuteTurnRequest
    {
        [JsonPropertyName("activity")]
        public Activity? Activity { get; init; }
    }
}
