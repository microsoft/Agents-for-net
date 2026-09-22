// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class OAuthTokenClient(
    OAuthDeviceCodeTokenClient deviceCode,
    OAuthAuthorizationCodeTokenClient authorizationCode,
    OAuthClientCredentialsTokenClient clientCredentials,
    OAuthTokenEndpointClient tokenEndpoint)
    : IOAuthTokenClient
{
    private readonly OAuthDeviceCodeTokenClient _deviceCode = deviceCode
        ?? throw new ArgumentNullException(nameof(deviceCode));
    private readonly OAuthAuthorizationCodeTokenClient _authorizationCode = authorizationCode
        ?? throw new ArgumentNullException(nameof(authorizationCode));
    private readonly OAuthClientCredentialsTokenClient _clientCredentials = clientCredentials
        ?? throw new ArgumentNullException(nameof(clientCredentials));
    private readonly OAuthTokenEndpointClient _tokenEndpoint = tokenEndpoint
        ?? throw new ArgumentNullException(nameof(tokenEndpoint));

    public Task<OAuthAccessToken> AcquireTokenAsync(
        A2AAgentCardAuthentication authentication,
        OAuthCredentialProviderOptions provider,
        OAuthClientRegistration registration,
        CancellationToken cancellationToken)
        => authentication.FlowType switch
        {
            A2AOAuthFlowType.DeviceCode => _deviceCode.AcquireTokenAsync(authentication, provider, registration, cancellationToken),
            A2AOAuthFlowType.AuthorizationCode => _authorizationCode.AcquireTokenAsync(authentication, provider, registration, cancellationToken),
            A2AOAuthFlowType.ClientCredentials => _clientCredentials.AcquireTokenAsync(authentication, provider, registration, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(authentication), authentication.FlowType, "Unsupported OAuth flow."),
        };

    public Task<OAuthAccessToken> RefreshTokenAsync(
        A2AAgentCardAuthentication authentication,
        OAuthCredentialProviderOptions provider,
        OAuthClientRegistration registration,
        string refreshToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ArgumentException("Refresh token is required.", nameof(refreshToken));
        }

        Uri tokenEndpoint = OAuthEndpointValidator.GetTrustedEndpoint(
            authentication.TokenUrl,
            provider,
            "token endpoint");
        return _tokenEndpoint.RequestTokenAsync(
            tokenEndpoint,
            registration,
            [
                new("grant_type", "refresh_token"),
                new("refresh_token", refreshToken),
                new("scope", string.Join(' ', OAuthScopeResolver.GetScopes(authentication, provider))),
            ],
            cancellationToken);
    }
}
