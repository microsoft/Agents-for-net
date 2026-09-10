// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class A2AClientAuthenticationOptions
{
    public required string TenantId { get; init; }

    public required string PublicClientId { get; init; }

    public required string ConfidentialClientId { get; init; }

    public required string ConfidentialClientSecret { get; init; }

    public required string AgentDelegatedScope { get; init; }

    public required string AgentApplicationScope { get; init; }

    public void Validate(A2AAuthMode mode)
    {
        switch (mode)
        {
            case A2AAuthMode.None:
                return;

            case A2AAuthMode.Delegated:
                ValidateRequired(TenantId, nameof(TenantId));
                ValidateRequired(PublicClientId, nameof(PublicClientId));
                ValidateRequired(AgentDelegatedScope, nameof(AgentDelegatedScope));
                return;

            case A2AAuthMode.App:
                ValidateRequired(TenantId, nameof(TenantId));
                ValidateRequired(ConfidentialClientId, nameof(ConfidentialClientId));
                ValidateRequired(ConfidentialClientSecret, nameof(ConfidentialClientSecret));
                ValidateRequired(AgentApplicationScope, nameof(AgentApplicationScope));
                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }

    private static void ValidateRequired(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
    }
}
