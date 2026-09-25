// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Samples.A2AClient.A2A;
using Microsoft.Agents.Samples.A2AClient.OAuth;

namespace Microsoft.Agents.Samples.A2AClient.OAuth.Providers;

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

        if (matches.Count == 0)
        {
            string evaluatedProviders = string.Join(", ", _providers.Select(provider => provider.Id).Distinct(StringComparer.Ordinal));
            throw new InvalidOperationException(
                $"No OAuth provider can satisfy flow '{authentication.FlowType}' for {DescribeAdvertisedEndpoints(authentication)}. "
                + $"Evaluated providers: {evaluatedProviders}.");
        }

        // A provider only produces a match after its trust policy accepted every advertised endpoint, so the
        // highest provider specificity present is the most specific trusted authority match. Lower-specificity
        // providers - including dynamic client registration - are not considered even when the winning provider
        // has no compatible registration, so a configured provider reports its own missing-registration error
        // instead of silently falling through to a broader provider.
        int bestProviderSpecificity = matches.Max(match => match.ProviderSpecificity);
        OAuthProviderMatch[] rankedMatches = matches
            .Where(match => match.ProviderSpecificity == bestProviderSpecificity)
            .OrderByDescending(match => match.AuthoritySpecificity)
            .ThenByDescending(match => match.RegistrationSpecificity)
            .ToArray();
        OAuthProviderMatch bestMatch = rankedMatches[0];
        OAuthProviderMatch[] ambiguousMatches = rankedMatches
            .Where(match =>
                match != bestMatch
                && match.AuthoritySpecificity == bestMatch.AuthoritySpecificity
                && match.RegistrationSpecificity == bestMatch.RegistrationSpecificity)
            .ToArray();
        if (ambiguousMatches.Length > 0)
        {
            string[] candidates = [FormatCandidate(bestMatch), .. ambiguousMatches.Select(FormatCandidate)];
            throw new InvalidOperationException(
                $"Multiple OAuth providers can satisfy flow '{authentication.FlowType}': {string.Join(", ", candidates)}.");
        }

        return bestMatch.Provider.BindAsync(agentOrigin, authentication, bestMatch, cancellationToken);
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
