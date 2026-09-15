// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using A2A;

namespace Microsoft.Agents.Samples.A2AClient;

/// <summary>
/// Selects the OAuth metadata needed to acquire an Agent API token from an A2A Agent Card.
/// </summary>
internal sealed class A2AAgentCardAuthentication
{
    private A2AAgentCardAuthentication(
        A2AAuthMode mode,
        string securitySchemeName,
        string tokenUrl,
        string? deviceAuthorizationUrl,
        IReadOnlyList<string> scopes)
    {
        Mode = mode;
        SecuritySchemeName = securitySchemeName;
        TokenUrl = tokenUrl;
        DeviceAuthorizationUrl = deviceAuthorizationUrl;
        Scopes = scopes;
    }

    public A2AAuthMode Mode { get; }

    public string SecuritySchemeName { get; }

    public string TokenUrl { get; }

    public string? DeviceAuthorizationUrl { get; }

    public IReadOnlyList<string> Scopes { get; }

    public static A2AAgentCardAuthentication Select(AgentCard card, A2AAuthMode mode)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (mode is not A2AAuthMode.Delegated and not A2AAuthMode.App)
        {
            throw new InvalidOperationException(
                $"Authentication mode '{mode}' does not acquire an Agent API access token.");
        }

        var rejectedAlternatives = new List<string>();
        foreach (SecurityRequirement requirement in GetRequirements(card))
        {
            if (requirement.Schemes is null || requirement.Schemes.Count == 0)
            {
                rejectedAlternatives.Add("it does not name a security scheme");
                continue;
            }

            if (requirement.Schemes.Count != 1)
            {
                rejectedAlternatives.Add("it combines multiple security schemes");
                continue;
            }

            KeyValuePair<string, StringList> schemeRequirement = requirement.Schemes.Single();
            if (card.SecuritySchemes is null
                || !card.SecuritySchemes.TryGetValue(schemeRequirement.Key, out SecurityScheme? scheme))
            {
                rejectedAlternatives.Add($"it references missing scheme '{schemeRequirement.Key}'");
                continue;
            }

            OAuthFlows? flows = scheme.OAuth2SecurityScheme?.Flows;
            if (flows is null)
            {
                rejectedAlternatives.Add($"scheme '{schemeRequirement.Key}' is not an OAuth security scheme");
                continue;
            }

            IReadOnlyList<string> scopes = (schemeRequirement.Value?.List ?? [])
                .Where(scope => !string.IsNullOrWhiteSpace(scope))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (scopes.Count == 0)
            {
                rejectedAlternatives.Add($"scheme '{schemeRequirement.Key}' does not specify acquisition scopes");
                continue;
            }

            if (mode == A2AAuthMode.Delegated && flows.DeviceCode is not null)
            {
                if (!TryGetRequiredEndpoint(schemeRequirement.Key, "token", flows.DeviceCode.TokenUrl, out string? tokenUrl, out string? failure)
                    || !TryGetRequiredEndpoint(schemeRequirement.Key, "device authorization", flows.DeviceCode.DeviceAuthorizationUrl, out string? deviceAuthorizationUrl, out failure))
                {
                    rejectedAlternatives.Add(failure!);
                    continue;
                }

                return new A2AAgentCardAuthentication(
                    mode,
                    schemeRequirement.Key,
                    tokenUrl,
                    deviceAuthorizationUrl,
                    scopes);
            }

            if (mode == A2AAuthMode.App && flows.ClientCredentials is not null)
            {
                if (!TryGetRequiredEndpoint(schemeRequirement.Key, "token", flows.ClientCredentials.TokenUrl, out string? tokenUrl, out string? failure))
                {
                    rejectedAlternatives.Add(failure!);
                    continue;
                }

                return new A2AAgentCardAuthentication(
                    mode,
                    schemeRequirement.Key,
                    tokenUrl,
                    deviceAuthorizationUrl: null,
                    scopes);
            }

            rejectedAlternatives.Add(
                $"scheme '{schemeRequirement.Key}' does not advertise a supported {(mode == A2AAuthMode.Delegated ? "Device Code" : "Client Credentials")} OAuth flow");
        }

        string expectedFlow = mode == A2AAuthMode.Delegated ? "Device Code" : "Client Credentials";
        string reason = rejectedAlternatives.Count == 0
            ? "does not declare security requirements"
            : $"has no satisfiable alternative: {string.Join("; ", rejectedAlternatives.Distinct(StringComparer.Ordinal))}";
        throw new InvalidOperationException(
            $"The Agent Card {reason} for {expectedFlow} authentication. This client POC supports only Device Code and Client Credentials flows.");
    }

    private static IEnumerable<SecurityRequirement> GetRequirements(AgentCard card)
    {
        foreach (SecurityRequirement requirement in card.SecurityRequirements ?? [])
        {
            yield return requirement;
        }

        foreach (AgentSkill skill in card.Skills ?? [])
        {
            foreach (SecurityRequirement requirement in skill.SecurityRequirements ?? [])
            {
                yield return requirement;
            }
        }
    }

    private static bool TryGetRequiredEndpoint(
        string schemeName,
        string endpointName,
        string? value,
        out string endpointValue,
        out string? failure)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? endpoint) || !endpoint.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            endpointValue = string.Empty;
            failure = $"scheme '{schemeName}' must provide an absolute HTTPS {endpointName} endpoint";
            return false;
        }

        endpointValue = endpoint.AbsoluteUri;
        failure = null;
        return true;
    }
}
