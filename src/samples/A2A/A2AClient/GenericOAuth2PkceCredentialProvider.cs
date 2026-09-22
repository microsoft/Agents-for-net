// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class GenericOAuth2PkceCredentialProvider : GenericOAuth2CredentialProvider
{
    public GenericOAuth2PkceCredentialProvider(OAuthCredentialProviderOptions options)
        : base(options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (options.Type != OAuthCredentialProviderType.GenericOAuth2Pkce)
        {
            throw new InvalidOperationException(
                $"Provider '{options.Id}' must declare Type '{OAuthCredentialProviderType.GenericOAuth2Pkce}'.");
        }
    }

    protected override bool IsRegistrationCompatible(
        OAuthClientRegistration registration,
        A2AAgentCardAuthentication authentication)
        => authentication.FlowType != A2AOAuthFlowType.AuthorizationCode || registration.UsePkce;

    protected override string GetMissingRegistrationMessage(A2AAgentCardAuthentication authentication)
        => authentication.FlowType == A2AOAuthFlowType.AuthorizationCode
            ? $"OAuth provider '{Id}' requires PKCE for flow '{authentication.FlowType}'."
            : base.GetMissingRegistrationMessage(authentication);
}
