// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class OAuthCredentialProviderResolver : IOAuthCredentialProviderResolver
{
    private readonly IReadOnlyList<IOAuthCredentialProvider> _providers;

    public OAuthCredentialProviderResolver(IEnumerable<IOAuthCredentialProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _providers = providers.ToArray();
    }

    public Task<OAuthCredentialBinding> ResolveAsync(
        Uri agentOrigin,
        A2AAgentCardAuthentication authentication,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agentOrigin);
        ArgumentNullException.ThrowIfNull(authentication);

        var matches = new List<OAuthProviderMatch>();
        foreach (IOAuthCredentialProvider provider in _providers)
        {
            OAuthProviderMatch? match = provider.Match(authentication);
            if (match is not null)
            {
                matches.Add(match);
            }
        }

        OAuthProviderMatch[] bindableMatches = matches
            .Where(match => match.RegistrationSpecificity >= 0)
            .OrderByDescending(match => match.ProviderSpecificity)
            .ThenByDescending(match => match.AuthoritySpecificity)
            .ThenByDescending(match => match.RegistrationSpecificity)
            .ToArray();
        if (bindableMatches.Length > 0)
        {
            OAuthProviderMatch bestMatch = bindableMatches[0];
            OAuthProviderMatch[] ambiguousMatches = bindableMatches
                .Where(match =>
                    match != bestMatch
                    && match.ProviderSpecificity == bestMatch.ProviderSpecificity
                    && match.AuthoritySpecificity == bestMatch.AuthoritySpecificity
                    && match.RegistrationSpecificity == bestMatch.RegistrationSpecificity)
                .ToArray();
            if (ambiguousMatches.Length > 0)
            {
                string[] candidates = [FormatCandidate(bestMatch), .. ambiguousMatches.Select(FormatCandidate)];
                throw new InvalidOperationException(
                    $"Multiple configured OAuth providers can satisfy flow '{authentication.FlowType}': {string.Join(", ", candidates)}.");
            }

            return bestMatch.Provider.BindAsync(agentOrigin, authentication, bestMatch, cancellationToken);
        }

        if (matches.Count > 0)
        {
            OAuthProviderMatch[] diagnosticMatches = matches
                .OrderByDescending(match => match.ProviderSpecificity)
                .ThenByDescending(match => match.AuthoritySpecificity)
                .ThenByDescending(match => match.RegistrationSpecificity)
                .ToArray();
            OAuthProviderMatch diagnosticMatch = diagnosticMatches[0];
            OAuthProviderMatch[] ambiguousMatches = diagnosticMatches
                .Where(match =>
                    match != diagnosticMatch
                    && match.ProviderSpecificity == diagnosticMatch.ProviderSpecificity
                    && match.AuthoritySpecificity == diagnosticMatch.AuthoritySpecificity
                    && match.RegistrationSpecificity == diagnosticMatch.RegistrationSpecificity)
                .ToArray();
            if (ambiguousMatches.Length > 0)
            {
                string[] candidates = [FormatCandidate(diagnosticMatch), .. ambiguousMatches.Select(FormatCandidate)];
                throw new InvalidOperationException(
                    $"Multiple configured OAuth providers can satisfy flow '{authentication.FlowType}': {string.Join(", ", candidates)}.");
            }

            return diagnosticMatch.Provider.BindAsync(agentOrigin, authentication, diagnosticMatch, cancellationToken);
        }

        throw new InvalidOperationException(
            $"No configured OAuth provider can satisfy flow '{authentication.FlowType}' for {DescribeAdvertisedEndpoints(authentication)}.");
    }

    private static string DescribeAdvertisedEndpoints(A2AAgentCardAuthentication authentication)
    {
        var endpoints = new List<string>();
        if (!string.IsNullOrWhiteSpace(authentication.AuthorizationUrl))
        {
            endpoints.Add($"authorization endpoint '{authentication.AuthorizationUrl}'");
        }

        if (!string.IsNullOrWhiteSpace(authentication.DeviceAuthorizationUrl))
        {
            endpoints.Add($"device authorization endpoint '{authentication.DeviceAuthorizationUrl}'");
        }

        endpoints.Add($"token endpoint '{authentication.TokenUrl}'");

        if (!string.IsNullOrWhiteSpace(authentication.MetadataUrl))
        {
            endpoints.Add($"metadata URL '{authentication.MetadataUrl}'");
        }

        return string.Join(", ", endpoints);
    }

    private static string FormatCandidate(OAuthProviderMatch match)
        => match.RegistrationId is null
            ? match.Provider.Id
            : $"{match.Provider.Id}/{match.RegistrationId}";
}
