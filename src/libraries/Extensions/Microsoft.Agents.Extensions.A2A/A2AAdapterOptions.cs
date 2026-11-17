// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Extensions.A2A;

/// <summary>
/// Configuration options for <see cref="Microsoft.Agents.Extensions.A2A.Pipeline.A2AAdapter"/> runtime behavior.
/// </summary>
public sealed class A2AAdapterOptions
{
    /// <summary>
    /// Gets or sets how long clients and shared caches may reuse the Agent Card.
    /// </summary>
    public TimeSpan AgentCardCacheMaxAge { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the absolute HTTP or HTTPS root address advertised for declared gRPC interfaces.
    /// When omitted, the Agent Card request's scheme and authority are used.
    /// </summary>
    /// <remarks>
    /// Set this when native HTTP/2 uses a different listener from HTTP Agent Card discovery.
    /// Path prefixes, credentials, query strings, and fragments are not supported.
    /// </remarks>
    public Uri? GrpcUrl { get; set; }
}
