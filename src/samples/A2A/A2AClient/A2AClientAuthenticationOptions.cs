// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class A2AClientAuthenticationOptions
{
    public string? TenantId { get; init; }

    public string? PublicClientId { get; init; }

    public string? ConfidentialClientId { get; init; }

    public string? ConfidentialClientSecret { get; init; }

    public string? AgentDelegatedScope { get; init; }

    public string? AgentApplicationScope { get; init; }
}
