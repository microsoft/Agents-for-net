// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Samples.A2AClient.OAuth;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class A2AAccessTokenProvider : IA2AAccessTokenProvider
{
    private static readonly TimeSpan s_expirationSafetySkew = TimeSpan.FromMinutes(1);
    private readonly Uri _agentOrigin;
    private readonly IOAuthTokenClient _oauth;
    private readonly IOAuthCredentialProviderResolver _resolver;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, OAuthTokenCacheEntry> _oauthTokenCache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _oauthTokenCacheLocks = new(StringComparer.Ordinal);

    public A2AAccessTokenProvider(
        IOAuthCredentialProviderResolver resolver,
        IOAuthTokenClient oauth,
        Uri agentOrigin)
        : this(resolver, oauth, agentOrigin, TimeProvider.System)
    {
    }

    internal A2AAccessTokenProvider(
        IOAuthCredentialProviderResolver resolver,
        IOAuthTokenClient oauth,
        Uri agentOrigin,
        TimeProvider timeProvider)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _oauth = oauth ?? throw new ArgumentNullException(nameof(oauth));
        _agentOrigin = agentOrigin ?? throw new ArgumentNullException(nameof(agentOrigin));
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

        OAuthCredentialBinding binding = await _resolver
            .ResolveAsync(_agentOrigin, authentication, cancellationToken)
            .ConfigureAwait(false);
        string cacheKey = CreateCacheKey(binding);
        SemaphoreSlim cacheLock = _oauthTokenCacheLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));

        await cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_oauthTokenCache.TryGetValue(cacheKey, out OAuthTokenCacheEntry? cachedToken)
                && cachedToken.CanReuse(_timeProvider.GetUtcNow()))
            {
                return cachedToken.AccessToken;
            }

            OAuthAccessToken token;
            if (cachedToken?.RefreshToken is string refreshToken)
            {
                try
                {
                    token = await _oauth
                        .RefreshTokenAsync(cachedToken.Binding, refreshToken, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is not OperationCanceledException
                    && !cancellationToken.IsCancellationRequested)
                {
                    // The refresh token is no longer usable. Drop the poisoned entry so no later request can
                    // replay it, then run the advertised flow once. A failure here propagates and leaves the
                    // cache empty rather than retrying in a loop.
                    _oauthTokenCache.TryRemove(cacheKey, out _);
                    cachedToken = null;
                    token = await _oauth.AcquireTokenAsync(binding, cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                token = await _oauth.AcquireTokenAsync(binding, cancellationToken).ConfigureAwait(false);
            }

            _oauthTokenCache[cacheKey] = new OAuthTokenCacheEntry(
                token.AccessToken,
                token.ExpiresIn is TimeSpan expiresIn
                    ? _timeProvider.GetUtcNow().Add(expiresIn).Subtract(s_expirationSafetySkew)
                    : null,
                token.RefreshToken ?? cachedToken?.RefreshToken,
                cachedToken?.Binding ?? binding);
            return token.AccessToken;
        }
        finally
        {
            cacheLock.Release();
        }
    }

    private static string CreateCacheKey(
        OAuthCredentialBinding binding)
        => string.Join(
            "\u001f",
            binding.ProviderIdentity,
            binding.ProviderId,
            binding.RegistrationId,
            binding.Registration.ClientId,
            binding.FlowType.ToString(),
            string.Join("\u001f", OAuthScopeResolver.GetCacheIdentityScopes(binding.EffectiveScopes)));

    private sealed record OAuthTokenCacheEntry(
        string AccessToken,
        DateTimeOffset? ReuseUntil,
        string? RefreshToken,
        OAuthCredentialBinding Binding)
    {
        public bool CanReuse(DateTimeOffset now) => ReuseUntil is null || now < ReuseUntil;
    }
}
