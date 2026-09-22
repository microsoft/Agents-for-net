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
using Microsoft.Agents.Samples.A2AClient.OAuth;
using Microsoft.Agents.Samples.A2AClient.OAuth.Configuration;
using Microsoft.Agents.Samples.A2AClient.OAuth.Registration;

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
        OAuthCredentialBinding binding,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(binding);

        OAuthClientRegistration registration = binding.Registration;
        if (binding.FlowType != A2AOAuthFlowType.AuthorizationCode)
        {
            throw new InvalidOperationException(
                "Authorization Code token acquisition requires an Authorization Code OAuth flow.");
        }

        OAuthClientRegistrationValidator.EnsureUsableClientId(registration);

        if (registration.RedirectUri is null)
        {
            throw new InvalidOperationException(
                "The selected OAuth client registration requires RedirectUri for Authorization Code.");
        }

        if ((binding.ProviderType is OAuthCredentialProviderType.GenericOAuth2Pkce
            or OAuthCredentialProviderType.OAuth21PkceDcr)
            && !registration.UsePkce)
        {
            throw new InvalidOperationException(
                "The selected OAuth credential binding requires PKCE for Authorization Code.");
        }

        OAuthEndpointValidator.EnsureSupportedLoopbackRedirectUri(registration.RedirectUri);

        Uri authorizationEndpoint = binding.AuthorizationEndpoint
            ?? throw new InvalidOperationException(
                "Authorization Code token acquisition requires an authorization endpoint.");
        Uri tokenEndpoint = binding.TokenEndpoint;

        string state = CreateRandomValue();
        string? codeVerifier = registration.UsePkce ? CreateRandomValue() : null;
        Uri authorizationUri = CreateAuthorizationUri(
            authorizationEndpoint,
            binding,
            state,
            codeVerifier);
        string code = await _authorizationCodeReceiver
            .ReceiveCodeAsync(authorizationUri, registration.RedirectUri, state, cancellationToken)
            .ConfigureAwait(false);

        var fields = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "authorization_code"),
            new("code", code),
            new("redirect_uri", registration.RedirectUri.AbsoluteUri),
        };
        if (codeVerifier is not null)
        {
            fields.Add(new("code_verifier", codeVerifier));
        }

        return await _tokenEndpointClient
            .RequestTokenAsync(tokenEndpoint, registration, fields, cancellationToken)
            .ConfigureAwait(false);
    }

    private static Uri CreateAuthorizationUri(
        Uri authorizationEndpoint,
        OAuthCredentialBinding binding,
        string state,
        string? codeVerifier)
    {
        OAuthClientRegistration registration = binding.Registration;
        var fields = new List<KeyValuePair<string, string>>
        {
            new("response_type", "code"),
            new("client_id", registration.ClientId),
            new("redirect_uri", registration.RedirectUri!.AbsoluteUri),
            new("scope", string.Join(' ', binding.EffectiveScopes)),
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
