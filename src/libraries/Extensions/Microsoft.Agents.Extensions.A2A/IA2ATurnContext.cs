// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;

namespace Microsoft.Agents.Extensions.A2A;

/// <summary>
/// Provides A2A-specific helpers for working with the current <see cref="ITurnContext"/>.
/// </summary>
/// <remarks>
/// Exposes the current turn's <see cref="Activity"/> as a strongly-typed <see cref="IA2AActivity"/>
/// and provides direct access to the underlying A2A primitives via <see cref="Client"/>.
/// </remarks>
public interface IA2ATurnContext : ITurnContext
{
    /// <summary>
    /// Gets the current turn's activity as a strongly-typed <see cref="IA2AActivity"/>.
    /// </summary>
    new IA2AActivity Activity { get; }

    /// <summary>
    /// Gets the A2A client for the current turn, providing direct access to the A2A event queue,
    /// request context, and task store.
    /// </summary>
    A2AClient Client { get; }
}
