// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Samples.A2AClient.OAuth;

namespace Microsoft.Agents.Samples.A2AClient.OAuth.Tokens;

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
        OAuthCredentialBinding binding,
        CancellationToken cancellationToken)
        => binding.FlowType switch
        {
            A2AOAuthFlowType.DeviceCode => _deviceCode.AcquireTokenAsync(binding, cancellationToken),
            A2AOAuthFlowType.AuthorizationCode => _authorizationCode.AcquireTokenAsync(binding, cancellationToken),
            A2AOAuthFlowType.ClientCredentials => _clientCredentials.AcquireTokenAsync(binding, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(binding), binding.FlowType, "Unsupported OAuth flow."),
        };

    public Task<OAuthAccessToken> RefreshTokenAsync(
        OAuthCredentialBinding binding,
        string refreshToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ArgumentException("Refresh token is required.", nameof(refreshToken));
        }

        return _tokenEndpoint.RequestTokenAsync(
            binding.TokenEndpoint,
            binding.Registration,
            [
                new("grant_type", "refresh_token"),
                new("refresh_token", refreshToken),
                new("scope", string.Join(' ', binding.EffectiveScopes)),
            ],
            cancellationToken);
    }
}
