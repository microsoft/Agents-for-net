// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class OAuthAuthorizationCodeTokenClient
{
    private readonly IOAuthAuthorizationCodeReceiver _authorizationCodeReceiver;
    private readonly OAuthTokenEndpointClient _tokenEndpointClient;

    public OAuthAuthorizationCodeTokenClient(
        HttpClient httpClient,
        IOAuthAuthorizationCodeReceiver authorizationCodeReceiver)
    {
        _tokenEndpointClient = new OAuthTokenEndpointClient(httpClient);
        _authorizationCodeReceiver = authorizationCodeReceiver
            ?? throw new ArgumentNullException(nameof(authorizationCodeReceiver));
    }

    public async Task<OAuthAccessToken> AcquireTokenAsync(
        A2AAgentCardAuthentication authentication,
        OAuthConnectionOptions connection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(connection);
        if (authentication.FlowType != A2AOAuthFlowType.AuthorizationCode)
        {
            throw new InvalidOperationException(
                "Authorization Code token acquisition requires an Authorization Code OAuth flow.");
        }

        if (string.IsNullOrWhiteSpace(connection.ClientId))
        {
            throw new InvalidOperationException("The selected OAuth connection requires ClientId.");
        }

        if (connection.RedirectUri is null)
        {
            throw new InvalidOperationException(
                "The selected OAuth connection requires RedirectUri for Authorization Code.");
        }

        Uri authorizationEndpoint = OAuthEndpointValidator.GetTrustedEndpoint(
            authentication.AuthorizationUrl,
            connection,
            "authorization endpoint");
        Uri tokenEndpoint = OAuthEndpointValidator.GetTrustedEndpoint(
            authentication.TokenUrl,
            connection,
            "token endpoint");

        string state = CreateRandomValue();
        string? codeVerifier = connection.UsePkce ? CreateRandomValue() : null;
        Uri authorizationUri = CreateAuthorizationUri(
            authorizationEndpoint,
            authentication,
            connection,
            state,
            codeVerifier);
        string code = await _authorizationCodeReceiver
            .ReceiveCodeAsync(authorizationUri, connection.RedirectUri, state, cancellationToken)
            .ConfigureAwait(false);

        var fields = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "authorization_code"),
            new("code", code),
            new("redirect_uri", connection.RedirectUri.AbsoluteUri),
        };
        if (codeVerifier is not null)
        {
            fields.Add(new("code_verifier", codeVerifier));
        }

        return await _tokenEndpointClient
            .RequestTokenAsync(tokenEndpoint, connection, fields, cancellationToken)
            .ConfigureAwait(false);
    }

    private static Uri CreateAuthorizationUri(
        Uri authorizationEndpoint,
        A2AAgentCardAuthentication authentication,
        OAuthConnectionOptions connection,
        string state,
        string? codeVerifier)
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("response_type", "code"),
            new("client_id", connection.ClientId!),
            new("redirect_uri", connection.RedirectUri!.AbsoluteUri),
            new("scope", string.Join(' ', OAuthScopeResolver.GetScopes(authentication, connection))),
            new("state", state),
        };
        if (codeVerifier is not null)
        {
            fields.Add(new("code_challenge", CreateCodeChallenge(codeVerifier)));
            fields.Add(new("code_challenge_method", "S256"));
        }

        string query = string.Join(
            "&",
            fields.ConvertAll(field => $"{WebUtility.UrlEncode(field.Key)}={WebUtility.UrlEncode(field.Value)}"));
        var builder = new UriBuilder(authorizationEndpoint)
        {
            Query = string.IsNullOrEmpty(authorizationEndpoint.Query)
                ? query
                : $"{authorizationEndpoint.Query.TrimStart('?')}&{query}",
        };
        return builder.Uri;
    }

    private static string CreateRandomValue()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncode(bytes);
    }

    private static string CreateCodeChallenge(string codeVerifier)
        => Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
