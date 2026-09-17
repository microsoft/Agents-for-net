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
    private static readonly IGitHubDeviceFlowTokenClient s_unsupportedGitHubDeviceFlow = new UnsupportedGitHubDeviceFlowTokenClient();
    private readonly IMsalTokenClient _msal;
    private readonly IGitHubDeviceFlowTokenClient _gitHub;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, GitHubTokenCacheEntry> _gitHubTokenCache = new(StringComparer.Ordinal);

    public A2AAccessTokenProvider(IMsalTokenClient msal)
        : this(msal, s_unsupportedGitHubDeviceFlow, TimeProvider.System)
    {
    }

    public A2AAccessTokenProvider(IMsalTokenClient msal, IGitHubDeviceFlowTokenClient gitHub)
        : this(msal, gitHub, TimeProvider.System)
    {
    }

    internal A2AAccessTokenProvider(
        IMsalTokenClient msal,
        IGitHubDeviceFlowTokenClient gitHub,
        TimeProvider timeProvider)
    {
        _msal = msal ?? throw new ArgumentNullException(nameof(msal));
        _gitHub = gitHub ?? throw new ArgumentNullException(nameof(gitHub));
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

        bool isGitHubDeviceCode = IsGitHubDeviceCode(authentication);
        if (isGitHubDeviceCode
            && _gitHubTokenCache.TryGetValue(authentication.SecuritySchemeName, out GitHubTokenCacheEntry? cachedToken)
            && cachedToken.CanReuse(_timeProvider.GetUtcNow()))
        {
            return cachedToken.AccessToken;
        }

        if (isGitHubDeviceCode)
        {
            GitHubDeviceFlowAccessToken token = await _gitHub
                .AcquireTokenAsync(authentication, cancellationToken)
                .ConfigureAwait(false);
            _gitHubTokenCache[authentication.SecuritySchemeName] = new GitHubTokenCacheEntry(
                token.AccessToken,
                token.ExpiresIn is TimeSpan expiresIn
                    ? _timeProvider.GetUtcNow().Add(expiresIn).Subtract(s_expirationSafetySkew)
                    : null);
            return token.AccessToken;
        }

        return authentication.Mode switch
        {
            A2AAuthMode.Delegated => await _msal.AcquireDelegatedTokenAsync(authentication, cancellationToken).ConfigureAwait(false),
            A2AAuthMode.App => await _msal.AcquireApplicationTokenAsync(authentication, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(authentication), authentication.Mode, "Unsupported authentication mode."),
        };
    }

    private static bool IsGitHubDeviceCode(A2AAgentCardAuthentication authentication)
        => GitHubDeviceFlowAuthentication.IsSupported(authentication);

    private sealed class UnsupportedGitHubDeviceFlowTokenClient : IGitHubDeviceFlowTokenClient
    {
        public Task<GitHubDeviceFlowAccessToken> AcquireTokenAsync(
            A2AAgentCardAuthentication authentication,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException(
                $"Security scheme '{authentication.SecuritySchemeName}' requires GitHub device-code authentication, which is not configured in this client yet.");
    }

    private sealed record GitHubTokenCacheEntry(string AccessToken, DateTimeOffset? ReuseUntil)
    {
        public bool CanReuse(DateTimeOffset now) => ReuseUntil is null || now < ReuseUntil;
    }
}
