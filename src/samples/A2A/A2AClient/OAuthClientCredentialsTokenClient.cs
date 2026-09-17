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
        OAuthConnectionOptions connection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(connection);
        if (authentication.FlowType != A2AOAuthFlowType.ClientCredentials)
        {
            throw new InvalidOperationException(
                "Client Credentials token acquisition requires a Client Credentials OAuth flow.");
        }

        Uri tokenEndpoint = OAuthEndpointValidator.GetTrustedEndpoint(
            authentication.TokenUrl,
            connection,
            "token endpoint");
        return _tokenEndpointClient.RequestTokenAsync(
            tokenEndpoint,
            connection,
            [
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>(
                    "scope",
                    string.Join(' ', OAuthScopeResolver.GetScopes(authentication, connection))),
            ],
            cancellationToken);
    }
}
