// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using A2A;
using Microsoft.Agents.Samples.A2AClient.OAuth;

namespace Microsoft.Agents.Samples.A2AClient;

/// <summary>
/// Selects the OAuth metadata needed to acquire an Agent API token from an A2A Agent Card.
/// </summary>
internal sealed class A2AAgentCardAuthentication
{
    private A2AAgentCardAuthentication(
        A2AAuthMode mode,
        A2AOAuthFlowType flowType,
        string? securitySchemeName,
        string? metadataUrl,
        string? authorizationUrl,
        string tokenUrl,
        string? deviceAuthorizationUrl,
        IReadOnlyList<string> scopes)
    {
        Mode = mode;
        FlowType = flowType;
        SecuritySchemeName = securitySchemeName;
        MetadataUrl = metadataUrl;
        AuthorizationUrl = authorizationUrl;
        TokenUrl = tokenUrl;
        DeviceAuthorizationUrl = deviceAuthorizationUrl;
        Scopes = scopes;
    }

    public A2AAuthMode Mode { get; }

    public A2AOAuthFlowType FlowType { get; }

    public string? SecuritySchemeName { get; }

    public string? MetadataUrl { get; }

    public string? AuthorizationUrl { get; }

    public string TokenUrl { get; }

    public string? DeviceAuthorizationUrl { get; }

    public IReadOnlyList<string> Scopes { get; }

    internal static A2AAgentCardAuthentication CreateInTask(
        OAuthFlows flows,
        IReadOnlyList<string> scopes)
    {
        ArgumentNullException.ThrowIfNull(flows);

        if (flows.DeviceCode is not null)
        {
            return new A2AAgentCardAuthentication(
                A2AAuthMode.Delegated,
                A2AOAuthFlowType.DeviceCode,
                securitySchemeName: null,
                metadataUrl: null,
                authorizationUrl: null,
                tokenUrl: flows.DeviceCode.TokenUrl,
                deviceAuthorizationUrl: flows.DeviceCode.DeviceAuthorizationUrl,
                scopes: scopes);
        }
        if (flows.AuthorizationCode is not null)
        {
            return new A2AAgentCardAuthentication(
                A2AAuthMode.Delegated,
                A2AOAuthFlowType.AuthorizationCode,
                securitySchemeName: null,
                metadataUrl: null,
                authorizationUrl: flows.AuthorizationCode.AuthorizationUrl,
                tokenUrl: flows.AuthorizationCode.TokenUrl,
                deviceAuthorizationUrl: null,
                scopes: scopes);
        }

        throw new InvalidOperationException(
            "In-task authorization requires a delegated Device Code or Authorization Code OAuth flow.");
    }

    public static A2AAgentCardAuthentication Select(AgentCard card, A2AAuthMode mode)
        => Select(card, skill: null, mode);

    public static A2AAgentCardAuthentication Select(AgentCard card, AgentSkill? skill, A2AAuthMode mode)
    {
        ArgumentNullException.ThrowIfNull(card);

        IEnumerable<SecurityRequirement> requirements = skill is null
            ? GetRequirements(card)
            : GetEffectiveRequirements(card, skill);
        SelectionAttempt attempt = EvaluateAlternatives(card, requirements, mode);
        if (skill is not null && attempt.RequirementCount == 0)
        {
            attempt = EvaluateAlternatives(card, GetRequirements(card), mode);
        }

        return ResolveSelection(attempt, mode);
    }

    public static A2AAgentCardAuthentication? Select(
        AgentCard card,
        AgentSkill? skill,
        A2AAuthMode? modeOverride)
    {
        if (modeOverride == A2AAuthMode.None)
        {
            return null;
        }

        if (modeOverride is A2AAuthMode.Delegated or A2AAuthMode.App)
        {
            return Select(card, skill, modeOverride.Value);
        }

        SecurityRequirement[] requirements = GetEffectiveRequirements(card, skill).ToArray();
        if (requirements.Length == 0)
        {
            return null;
        }

        A2AAuthMode[] advertisedModes = requirements
            .Where(requirement => requirement.Schemes?.Count == 1)
            .SelectMany(requirement => requirement.Schemes is null
                ? Enumerable.Empty<string>()
                : requirement.Schemes.Keys)
            .Select(schemeName => card.SecuritySchemes is not null
                && card.SecuritySchemes.TryGetValue(schemeName, out SecurityScheme? scheme)
                    ? scheme?.OAuth2SecurityScheme?.Flows
                    : null)
            .SelectMany(GetSupportedModes)
            .Distinct()
            .ToArray();

        return advertisedModes.Length switch
        {
            1 => ResolveSelection(EvaluateAlternatives(card, requirements, advertisedModes[0]), advertisedModes[0]),
            > 1 => throw new InvalidOperationException(
                "The selected Agent Card requirements advertise both delegated and application authentication. "
                + "Choose one with --auth-mode or :auth."),
            _ => throw new InvalidOperationException(
                "The selected Agent Card requirements do not advertise a supported Authorization Code, Device Code, or Client Credentials OAuth flow."),
        };
    }

    private static SelectionAttempt EvaluateAlternatives(
        AgentCard card,
        IEnumerable<SecurityRequirement> requirements,
        A2AAuthMode mode)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (mode is not A2AAuthMode.Delegated and not A2AAuthMode.App)
        {
            throw new InvalidOperationException(
                $"Authentication mode '{mode}' does not acquire an Agent API access token.");
        }

        int requirementCount = 0;
        var candidates = new List<A2AAgentCardAuthentication>();
        var distinctCandidates = new HashSet<string>(StringComparer.Ordinal);
        var rejectedAlternatives = new List<string>();

        foreach (SecurityRequirement requirement in requirements)
        {
            requirementCount++;
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

                AddCandidate(
                    candidates,
                    distinctCandidates,
                    new A2AAgentCardAuthentication(
                        mode,
                        A2AOAuthFlowType.DeviceCode,
                        securitySchemeName: schemeRequirement.Key,
                        metadataUrl: scheme.OAuth2SecurityScheme?.OAuth2MetadataUrl,
                        authorizationUrl: null,
                        tokenUrl: tokenUrl,
                        deviceAuthorizationUrl: deviceAuthorizationUrl,
                        scopes: scopes));
                continue;
            }

            if (mode == A2AAuthMode.Delegated && flows.AuthorizationCode is not null)
            {
                if (!TryGetRequiredEndpoint(schemeRequirement.Key, "authorization", flows.AuthorizationCode.AuthorizationUrl, out string? authorizationUrl, out string? failure)
                    || !TryGetRequiredEndpoint(schemeRequirement.Key, "token", flows.AuthorizationCode.TokenUrl, out string? tokenUrl, out failure))
                {
                    rejectedAlternatives.Add(failure!);
                    continue;
                }

                AddCandidate(
                    candidates,
                    distinctCandidates,
                    new A2AAgentCardAuthentication(
                        mode,
                        A2AOAuthFlowType.AuthorizationCode,
                        securitySchemeName: schemeRequirement.Key,
                        metadataUrl: scheme.OAuth2SecurityScheme?.OAuth2MetadataUrl,
                        authorizationUrl: authorizationUrl,
                        tokenUrl: tokenUrl,
                        deviceAuthorizationUrl: null,
                        scopes: scopes));
                continue;
            }

            if (mode == A2AAuthMode.App && flows.ClientCredentials is not null)
            {
                if (!TryGetRequiredEndpoint(schemeRequirement.Key, "token", flows.ClientCredentials.TokenUrl, out string? tokenUrl, out string? failure))
                {
                    rejectedAlternatives.Add(failure!);
                    continue;
                }

                AddCandidate(
                    candidates,
                    distinctCandidates,
                    new A2AAgentCardAuthentication(
                        mode,
                        A2AOAuthFlowType.ClientCredentials,
                        securitySchemeName: schemeRequirement.Key,
                        metadataUrl: scheme.OAuth2SecurityScheme?.OAuth2MetadataUrl,
                        authorizationUrl: null,
                        tokenUrl: tokenUrl,
                        deviceAuthorizationUrl: null,
                        scopes: scopes));
                continue;
            }

            rejectedAlternatives.Add(
                $"scheme '{schemeRequirement.Key}' does not advertise a supported {(mode == A2AAuthMode.Delegated ? "Authorization Code or Device Code" : "Client Credentials")} OAuth flow");
        }

        return new SelectionAttempt(requirementCount, candidates, rejectedAlternatives);
    }

    private static A2AAgentCardAuthentication ResolveSelection(SelectionAttempt attempt, A2AAuthMode mode)
    {
        if (attempt.Candidates.Count == 1)
        {
            return attempt.Candidates[0];
        }

        if (attempt.Candidates.Count > 1)
        {
            string modeName = mode == A2AAuthMode.Delegated ? "delegated" : "application";
            throw new InvalidOperationException(
                $"The Agent Card exposes multiple {modeName} security schemes for this request. Select a skill or security requirement that disambiguates the provider.");
        }

        string expectedFlow = mode == A2AAuthMode.Delegated ? "Authorization Code or Device Code" : "Client Credentials";
        string reason = attempt.RequirementCount == 0
            ? "does not declare security requirements"
            : $"has no satisfiable alternative: {string.Join("; ", attempt.RejectedAlternatives.Distinct(StringComparer.Ordinal))}";
        throw new InvalidOperationException(
            $"The Agent Card {reason} for {expectedFlow} authentication.");
    }

    private static void AddCandidate(
        List<A2AAgentCardAuthentication> candidates,
        HashSet<string> distinctCandidates,
        A2AAgentCardAuthentication candidate)
    {
        string key = $"{candidate.SecuritySchemeName}|{candidate.FlowType}|{string.Join("\u001f", candidate.Scopes)}";
        if (distinctCandidates.Add(key))
        {
            candidates.Add(candidate);
        }
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

    private static IEnumerable<SecurityRequirement> GetEffectiveRequirements(AgentCard card, AgentSkill? skill)
    {
        if (skill is null)
        {
            return card.SecurityRequirements ?? [];
        }

        SecurityRequirement[] agentRequirements = (card.SecurityRequirements ?? []).ToArray();
        SecurityRequirement[] skillRequirements = (skill.SecurityRequirements ?? []).ToArray();

        if (agentRequirements.Length == 0)
        {
            return skillRequirements;
        }

        if (skillRequirements.Length == 0)
        {
            return agentRequirements;
        }

        return agentRequirements.SelectMany(
            agentRequirement => skillRequirements,
            CombineRequirements);
    }

    private static SecurityRequirement CombineRequirements(
        SecurityRequirement agentRequirement,
        SecurityRequirement skillRequirement)
    {
        var schemes = new Dictionary<string, StringList>(StringComparer.Ordinal);
        AddSchemes(schemes, agentRequirement.Schemes);
        AddSchemes(schemes, skillRequirement.Schemes);
        return new SecurityRequirement { Schemes = schemes };
    }

    private static void AddSchemes(
        Dictionary<string, StringList> destination,
        IDictionary<string, StringList>? source)
    {
        if (source is null)
        {
            return;
        }

        foreach (KeyValuePair<string, StringList> scheme in source)
        {
            if (!destination.TryGetValue(scheme.Key, out StringList? scopes))
            {
                scopes = new StringList { List = [] };
                destination.Add(scheme.Key, scopes);
            }

            foreach (string scope in scheme.Value?.List ?? [])
            {
                if (!string.IsNullOrWhiteSpace(scope) && !scopes.List.Contains(scope, StringComparer.Ordinal))
                {
                    scopes.List.Add(scope);
                }
            }
        }
    }

    private static IEnumerable<A2AAuthMode> GetSupportedModes(OAuthFlows? flows)
    {
        if (flows?.DeviceCode is not null)
        {
            yield return A2AAuthMode.Delegated;
        }

        if (flows?.AuthorizationCode is not null)
        {
            yield return A2AAuthMode.Delegated;
        }

        if (flows?.ClientCredentials is not null)
        {
            yield return A2AAuthMode.App;
        }
    }

    private static bool TryGetRequiredEndpoint(
        string schemeName,
        string endpointName,
        string? value,
        out string endpointValue,
        out string? failure)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? endpoint)
            || !endpoint.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            endpointValue = string.Empty;
            failure = $"scheme '{schemeName}' must provide an absolute HTTPS {endpointName} endpoint";
            return false;
        }

        endpointValue = endpoint.AbsoluteUri;
        failure = null;
        return true;
    }

    private sealed class SelectionAttempt(
        int requirementCount,
        IReadOnlyList<A2AAgentCardAuthentication> candidates,
        IReadOnlyList<string> rejectedAlternatives)
    {
        public int RequirementCount { get; } = requirementCount;

        public IReadOnlyList<A2AAgentCardAuthentication> Candidates { get; } = candidates;

        public IReadOnlyList<string> RejectedAlternatives { get; } = rejectedAlternatives;
    }
}
