// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class OAuthClientCredentialsTokenClient
{
    private readonly OAuthTokenEndpointClient _tokenEndpointClient;

    public OAuthClientCredentialsTokenClient(HttpClient httpClient)
    {
        _tokenEndpointClient = new OAuthTokenEndpointClient(
            httpClient ?? throw new ArgumentNullException(nameof(httpClient)));
    }

    public Task<OAuthAccessToken> AcquireTokenAsync(
        A2AAgentCardAuthentication authentication,
        OAuthCredentialProviderOptions provider,
        OAuthClientRegistration registration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(registration);
        if (authentication.FlowType != A2AOAuthFlowType.ClientCredentials)
        {
            throw new InvalidOperationException(
                "Client Credentials token acquisition requires a Client Credentials OAuth flow.");
        }

        Uri tokenEndpoint = OAuthEndpointValidator.GetTrustedEndpoint(
            authentication.TokenUrl,
            provider,
            "token endpoint");
        return _tokenEndpointClient.RequestTokenAsync(
            tokenEndpoint,
            registration,
            [
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>(
                    "scope",
                    string.Join(' ', OAuthScopeResolver.GetScopes(authentication, provider))),
            ],
            cancellationToken);
    }
}
