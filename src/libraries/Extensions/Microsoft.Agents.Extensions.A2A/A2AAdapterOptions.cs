// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Extensions.A2A;

/// <summary>
/// Configuration options for <see cref="A2AAdapter"/> runtime behavior.
/// </summary>
public sealed class A2AAdapterOptions
{
    /// <summary>
    /// Gets or sets how long clients and shared caches may reuse the Agent Card.
    /// </summary>
    public TimeSpan AgentCardCacheMaxAge { get; set; } = TimeSpan.FromHours(1);
}
