// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
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
            string cacheKey = CreateCacheKey(authentication);
            if (_oauthTokenCache.TryGetValue(cacheKey, out OAuthTokenCacheEntry? cachedToken)
                && cachedToken.CanReuse(_timeProvider.GetUtcNow()))
            {
                return cachedToken.AccessToken;
            }

            OAuthConnectionOptions connection = _options.GetRequiredConnection(authentication.SecuritySchemeName);
            OAuthAccessToken token = cachedToken?.RefreshToken is string refreshToken
                ? await _oauth
                    .RefreshTokenAsync(authentication, connection, refreshToken, cancellationToken)
                    .ConfigureAwait(false)
                : await _oauth
                    .AcquireTokenAsync(authentication, connection, cancellationToken)
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

    private static string CreateCacheKey(A2AAgentCardAuthentication authentication)
        => $"{authentication.SecuritySchemeName}|{authentication.FlowType}|{string.Join("\u001f", authentication.Scopes)}";

    private sealed record OAuthTokenCacheEntry(
        string AccessToken,
        DateTimeOffset? ReuseUntil,
        string? RefreshToken)
    {
        public bool CanReuse(DateTimeOffset now) => ReuseUntil is null || now < ReuseUntil;
    }
}
