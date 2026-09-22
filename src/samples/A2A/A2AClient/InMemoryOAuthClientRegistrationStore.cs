// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class InMemoryOAuthClientRegistrationStore : IOAuthClientRegistrationStore
{
    private readonly ConcurrentDictionary<string, OAuthClientRegistration> _registrations = new(StringComparer.Ordinal);

    public Task<OAuthClientRegistration?> GetAsync(
        string providerIdentity,
        Uri redirectUri,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string key = CreateKey(providerIdentity, redirectUri);
        _registrations.TryGetValue(key, out OAuthClientRegistration? registration);
        return Task.FromResult(registration);
    }

    public Task SaveAsync(
        string providerIdentity,
        Uri redirectUri,
        OAuthClientRegistration registration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);
        cancellationToken.ThrowIfCancellationRequested();

        string key = CreateKey(providerIdentity, redirectUri);
        _registrations[key] = registration;
        return Task.CompletedTask;
    }

    private static string CreateKey(string providerIdentity, Uri redirectUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerIdentity);
        ArgumentNullException.ThrowIfNull(redirectUri);

        if (!redirectUri.IsAbsoluteUri)
        {
            throw new InvalidOperationException("OAuth registration store requires an absolute redirect URI.");
        }

        string normalizedRedirectUri = redirectUri.AbsoluteUri;
        return $"{providerIdentity.Length}:{providerIdentity}:{normalizedRedirectUri}";
    }
}
