// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class A2AAccessTokenProvider : IA2AAccessTokenProvider
{
    private static readonly IGitHubDeviceFlowTokenClient s_unsupportedGitHubDeviceFlow = new UnsupportedGitHubDeviceFlowTokenClient();
    private readonly IMsalTokenClient _msal;
    private readonly IGitHubDeviceFlowTokenClient _gitHub;
    private readonly Dictionary<string, string> _tokenCache = new(StringComparer.Ordinal);

    public A2AAccessTokenProvider(IMsalTokenClient msal)
        : this(msal, s_unsupportedGitHubDeviceFlow)
    {
    }

    public A2AAccessTokenProvider(IMsalTokenClient msal, IGitHubDeviceFlowTokenClient gitHub)
    {
        _msal = msal ?? throw new ArgumentNullException(nameof(msal));
        _gitHub = gitHub ?? throw new ArgumentNullException(nameof(gitHub));
    }

    public async Task<string?> GetAccessTokenAsync(
        A2AAgentCardAuthentication? authentication,
        CancellationToken cancellationToken)
    {
        if (authentication is null)
        {
            return null;
        }

        if (_tokenCache.TryGetValue(authentication.SecuritySchemeName, out string? cachedToken))
        {
            return cachedToken;
        }

        string token = IsGitHubDeviceCode(authentication)
            ? await _gitHub.AcquireTokenAsync(authentication, cancellationToken).ConfigureAwait(false)
            : authentication.Mode switch
            {
                A2AAuthMode.Delegated => await _msal.AcquireDelegatedTokenAsync(authentication, cancellationToken).ConfigureAwait(false),
                A2AAuthMode.App => await _msal.AcquireApplicationTokenAsync(authentication, cancellationToken).ConfigureAwait(false),
                _ => throw new ArgumentOutOfRangeException(nameof(authentication), authentication.Mode, "Unsupported authentication mode."),
            };

        _tokenCache[authentication.SecuritySchemeName] = token;
        return token;
    }

    private static bool IsGitHubDeviceCode(A2AAgentCardAuthentication authentication)
        => GitHubDeviceFlowAuthentication.IsSupported(authentication);

    private sealed class UnsupportedGitHubDeviceFlowTokenClient : IGitHubDeviceFlowTokenClient
    {
        public Task<string> AcquireTokenAsync(A2AAgentCardAuthentication authentication, CancellationToken cancellationToken)
            => throw new InvalidOperationException(
                $"Security scheme '{authentication.SecuritySchemeName}' requires GitHub device-code authentication, which is not configured in this client yet.");
    }
}
