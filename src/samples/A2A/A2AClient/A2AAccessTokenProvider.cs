// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class A2AAccessTokenProvider : IA2AAccessTokenProvider
{
    private static readonly TimeSpan s_expirationSafetySkew = TimeSpan.FromMinutes(1);
    private readonly A2AClientAuthenticationOptions _options;
    private readonly IOAuthTokenClient _oauth;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, OAuthTokenCacheEntry> _oauthTokenCache = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _oauthTokenCacheLock = new(1, 1);

    public A2AAccessTokenProvider(
        A2AClientAuthenticationOptions options,
        IOAuthTokenClient oauth)
        : this(options, oauth, TimeProvider.System)
    {
    }

    internal A2AAccessTokenProvider(
        A2AClientAuthenticationOptions options,
        IOAuthTokenClient oauth,
        TimeProvider timeProvider)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _oauth = oauth ?? throw new ArgumentNullException(nameof(oauth));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<string?> GetAccessTokenAsync(
        A2AAgentCardAuthentication? authentication,
        CancellationToken cancellationToken)
    {
        if (authentication is null)
        {
            return null;
        }

        await _oauthTokenCacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ResolvedClient resolvedClient = ResolveClient(authentication);
            string cacheKey = CreateCacheKey(authentication, resolvedClient);
            if (_oauthTokenCache.TryGetValue(cacheKey, out OAuthTokenCacheEntry? cachedToken)
                && cachedToken.CanReuse(_timeProvider.GetUtcNow()))
            {
                return cachedToken.AccessToken;
            }

            OAuthAccessToken token = cachedToken?.RefreshToken is string refreshToken
                ? await _oauth
                    .RefreshTokenAsync(
                        authentication,
                        resolvedClient.Provider,
                        resolvedClient.Registration,
                        refreshToken,
                        cancellationToken)
                    .ConfigureAwait(false)
                : await _oauth
                    .AcquireTokenAsync(
                        authentication,
                        resolvedClient.Provider,
                        resolvedClient.Registration,
                        cancellationToken)
                    .ConfigureAwait(false);
            _oauthTokenCache[cacheKey] = new OAuthTokenCacheEntry(
                token.AccessToken,
                token.ExpiresIn is TimeSpan expiresIn
                    ? _timeProvider.GetUtcNow().Add(expiresIn).Subtract(s_expirationSafetySkew)
                    : null,
                token.RefreshToken ?? cachedToken?.RefreshToken);
            return token.AccessToken;
        }
        finally
        {
            _oauthTokenCacheLock.Release();
        }
    }

    private ResolvedClient ResolveClient(
        A2AAgentCardAuthentication authentication)
    {
        var matches = new List<ResolvedClient>();

        foreach (OAuthCredentialProviderOptions provider in _options.Providers.Values)
        {
            try
            {
                _ = OAuthEndpointValidator.GetTrustedEndpoint(
                    authentication.TokenUrl,
                    provider,
                    "token endpoint");
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            if (authentication.FlowType == A2AOAuthFlowType.AuthorizationCode)
            {
                try
                {
                    _ = OAuthEndpointValidator.GetTrustedEndpoint(
                        authentication.AuthorizationUrl,
                        provider,
                        "authorization endpoint");
                }
                catch (InvalidOperationException)
                {
                    continue;
                }
            }
            else if (authentication.FlowType == A2AOAuthFlowType.DeviceCode)
            {
                try
                {
                    _ = OAuthEndpointValidator.GetTrustedEndpoint(
                        authentication.DeviceAuthorizationUrl,
                        provider,
                        "device authorization endpoint");
                }
                catch (InvalidOperationException)
                {
                    continue;
                }
            }

            foreach (OAuthClientRegistration registration in provider.Registrations.Values)
            {
                if (registration.GrantTypes.Contains(authentication.FlowType))
                {
                    matches.Add(new ResolvedClient(provider, registration));
                }
            }
        }

        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException(
                $"No configured OAuth provider can satisfy flow '{authentication.FlowType}' for token endpoint '{authentication.TokenUrl}'."),
            _ => throw new InvalidOperationException(
                $"Multiple configured OAuth providers can satisfy flow '{authentication.FlowType}': {string.Join(", ", matches.Select(match => $"{match.Provider.Id}/{match.Registration.Id}"))}."),
        };
    }

    private static string CreateCacheKey(
        A2AAgentCardAuthentication authentication,
        ResolvedClient resolvedClient)
        => string.Join(
            "\u001f",
            resolvedClient.Provider.Id,
            GetRegistrationCacheIdentity(resolvedClient.Registration),
            authentication.FlowType.ToString(),
            string.Join("\u001f", GetNormalizedScopes(authentication, resolvedClient.Provider)));

    private static string GetRegistrationCacheIdentity(OAuthClientRegistration registration)
        => string.IsNullOrWhiteSpace(registration.Id) ? registration.ClientId : registration.Id;

    private static IReadOnlyList<string> GetNormalizedScopes(
        A2AAgentCardAuthentication authentication,
        OAuthCredentialProviderOptions provider)
        => OAuthScopeResolver.GetScopes(authentication, provider)
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(scope => scope, StringComparer.Ordinal)
            .ToArray();

    private sealed record OAuthTokenCacheEntry(
        string AccessToken,
        DateTimeOffset? ReuseUntil,
        string? RefreshToken)
    {
        public bool CanReuse(DateTimeOffset now) => ReuseUntil is null || now < ReuseUntil;
    }

    private sealed record ResolvedClient(
        OAuthCredentialProviderOptions Provider,
        OAuthClientRegistration Registration);
}
