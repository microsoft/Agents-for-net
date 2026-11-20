// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.A2A.ProtocolExtensions;

/// <summary>
/// Contributes support for A2A protocol extensions without replacing SDK-owned extensions.
/// </summary>
/// <remarks>
/// Contributors are singletons and run once for each agent to which they are registered.
/// Register support during startup; resolve request-scoped services from the operation or
/// availability-policy context, not from the contributor's constructor.
/// </remarks>
public interface IA2AProtocolExtensionContributor
{
    /// <summary>
    /// Adds extension declarations, owned operations, and transport bindings before finalization.
    /// </summary>
    /// <param name="builder">The additive builder for this agent's protocol extensions.</param>
    void Register(A2AProtocolExtensionBuilder builder);
}
