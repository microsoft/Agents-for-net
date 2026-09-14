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

        bool foundOAuthScheme = false;
        foreach (SecurityRequirement requirement in GetRequirements(card))
        {
            if (requirement.Schemes is null || requirement.Schemes.Count == 0)
            {
                continue;
            }

            if (requirement.Schemes.Count != 1)
            {
                throw new InvalidOperationException(
                    "This A2A client POC cannot satisfy a security requirement that combines multiple schemes.");
            }

            KeyValuePair<string, StringList> schemeRequirement = requirement.Schemes.Single();
            if (card.SecuritySchemes is null
                || !card.SecuritySchemes.TryGetValue(schemeRequirement.Key, out SecurityScheme? scheme))
            {
                throw new InvalidOperationException(
                    $"The Agent Card security requirement references missing scheme '{schemeRequirement.Key}'.");
            }

            OAuthFlows? flows = scheme.OAuth2SecurityScheme?.Flows;
            if (flows is null)
            {
                continue;
            }

            foundOAuthScheme = true;
            IReadOnlyList<string> scopes = (schemeRequirement.Value?.List ?? [])
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (scopes.Count == 0)
            {
                throw new InvalidOperationException(
                    $"The Agent Card requirement for scheme '{schemeRequirement.Key}' does not specify acquisition scopes.");
            }

            if (mode == A2AAuthMode.Delegated && flows.DeviceCode is not null)
            {
                return new A2AAgentCardAuthentication(
                    mode,
                    schemeRequirement.Key,
                    GetRequiredEndpoint(schemeRequirement.Key, "token", flows.DeviceCode.TokenUrl),
                    GetRequiredEndpoint(schemeRequirement.Key, "device authorization", flows.DeviceCode.DeviceAuthorizationUrl),
                    scopes);
            }

            if (mode == A2AAuthMode.App && flows.ClientCredentials is not null)
            {
                return new A2AAgentCardAuthentication(
                    mode,
                    schemeRequirement.Key,
                    GetRequiredEndpoint(schemeRequirement.Key, "token", flows.ClientCredentials.TokenUrl),
                    deviceAuthorizationUrl: null,
                    scopes);
            }
        }

        string expectedFlow = mode == A2AAuthMode.Delegated ? "Device Code" : "Client Credentials";
        string reason = foundOAuthScheme
            ? $"does not advertise a supported {expectedFlow} OAuth flow"
            : "does not advertise an OAuth security scheme";
        throw new InvalidOperationException(
            $"The Agent Card {reason}. This client POC supports only Device Code and Client Credentials flows.");
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

    private static string GetRequiredEndpoint(string schemeName, string endpointName, string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? endpoint) || !endpoint.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The Agent Card scheme '{schemeName}' must provide an absolute HTTPS {endpointName} endpoint.");
        }

        return endpoint.AbsoluteUri;
    }
}
