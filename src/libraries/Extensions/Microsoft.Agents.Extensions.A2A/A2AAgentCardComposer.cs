// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.A2A;

internal sealed class A2AAgentCardComposer
{
    private readonly IConfiguration _configuration;

    public A2AAgentCardComposer(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<AgentCard> ComposeAsync(AgentCard hostDefaults, IAgent agent)
    {
        ArgumentNullException.ThrowIfNull(hostDefaults);
        ArgumentNullException.ThrowIfNull(agent);

        var optionsSection = _configuration?.GetSection("AgentApplication:A2A:AgentCard");
        ValidateProtectedConfiguration(optionsSection);
        var options = optionsSection?.Get<A2AAgentCardOptions>() ?? new A2AAgentCardOptions();

        ApplySafeOptions(hostDefaults, options);

        var authorizations = _configuration == null
            ? []
            : A2AAuthorizationMetadata.Resolve(_configuration);
        var authorizationsByHandler = authorizations.ToDictionary(metadata => metadata.HandlerName, StringComparer.OrdinalIgnoreCase);

        AddInlineSchemes(hostDefaults, authorizations);
        if (_configuration != null)
        {
            AddGlobalRequirement(hostDefaults, authorizationsByHandler);
        }
        AddSkills(hostDefaults, agent, authorizationsByHandler);

        if (agent is IAgentCardHandler agentCardHandler)
        {
            hostDefaults = await agentCardHandler.GetAgentCard(hostDefaults).ConfigureAwait(false);
        }

        ValidateCard(hostDefaults);
        return hostDefaults;
    }

    private static void ApplySafeOptions(AgentCard agentCard, A2AAgentCardOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Name))
        {
            agentCard.Name = options.Name;
        }

        if (!string.IsNullOrWhiteSpace(options.Description))
        {
            agentCard.Description = options.Description;
        }

        if (!string.IsNullOrWhiteSpace(options.DocumentationUrl))
        {
            agentCard.DocumentationUrl = options.DocumentationUrl;
        }

        if (!string.IsNullOrWhiteSpace(options.IconUrl))
        {
            agentCard.IconUrl = options.IconUrl;
        }

        if (options.Provider != null)
        {
            agentCard.Provider = options.Provider;
        }

        foreach (var scheme in options.SecuritySchemes)
        {
            if (scheme.Value == null)
            {
                throw new InvalidOperationException($"A2A Agent Card security scheme '{scheme.Key}' cannot be null.");
            }

            agentCard.SecuritySchemes[scheme.Key] = scheme.Value;
        }
    }

    private static void AddInlineSchemes(AgentCard agentCard, IReadOnlyList<A2AAuthorizationMetadata> authorizations)
    {
        foreach (var authorization in authorizations.Where(metadata => metadata.SecurityScheme != null))
        {
            if (agentCard.SecuritySchemes.ContainsKey(authorization.SecuritySchemeName))
            {
                throw new InvalidOperationException($"A2A Agent Card security scheme '{authorization.SecuritySchemeName}' is configured more than once.");
            }

            agentCard.SecuritySchemes.Add(authorization.SecuritySchemeName, authorization.SecurityScheme);
        }
    }

    private void AddGlobalRequirement(AgentCard agentCard, Dictionary<string, A2AAuthorizationMetadata> authorizations)
    {
        var userAuthorization = _configuration.GetSection("AgentApplication:UserAuthorization");
        if (!userAuthorization.GetValue(nameof(UserAuthorizationOptions.AutoSignIn), true))
        {
            return;
        }

        var handlerName = userAuthorization.GetValue<string>(nameof(UserAuthorizationOptions.DefaultHandlerName));
        if (string.IsNullOrWhiteSpace(handlerName))
        {
            handlerName = userAuthorization.GetSection("Handlers").GetChildren().FirstOrDefault()?.Key;
        }

        if (!string.IsNullOrWhiteSpace(handlerName) && authorizations.TryGetValue(handlerName, out var authorization))
        {
            agentCard.SecurityRequirements ??= [];
            agentCard.SecurityRequirements.Add(CreateRequirement(authorization));
        }
    }

    private static void AddSkills(
        AgentCard agentCard,
        IAgent agent,
        Dictionary<string, A2AAuthorizationMetadata> authorizations)
    {
        if (agent is not AgentApplication application)
        {
            return;
        }

        var registrations = application.RegisteredExtensions
            .OfType<A2AAgentExtension>()
            .SelectMany(extension => extension.SkillRegistrations)
            .GroupBy(registration => registration.Id, StringComparer.Ordinal);

        foreach (var registrationsForSkill in registrations)
        {
            var registration = registrationsForSkill.First();
            if (registrationsForSkill.Any(candidate => !HasCompatibleMetadata(registration, candidate)))
            {
                throw new InvalidOperationException($"A2A skill '{registration.Id}' has conflicting registrations.");
            }

            var skill = new AgentSkill
            {
                Id = registration.Id,
                Name = registration.Name,
                Description = registration.Description,
                Tags = registration.Tags.ToList(),
                Examples = registration.Examples.ToList(),
                InputModes = registration.InputModes.ToList(),
                OutputModes = registration.OutputModes.ToList(),
            };

            SecurityRequirement requirement = null;
            foreach (var handlerName in registration.AutoSignInHandlers)
            {
                if (authorizations.TryGetValue(handlerName, out var authorization))
                {
                    requirement ??= new SecurityRequirement
                    {
                        Schemes = new Dictionary<string, StringList>(),
                    };

                    if (!requirement.Schemes.TryGetValue(authorization.SecuritySchemeName, out var requiredScopes))
                    {
                        requiredScopes = new StringList { List = [] };
                        requirement.Schemes.Add(authorization.SecuritySchemeName, requiredScopes);
                    }

                    foreach (var scope in authorization.RequiredScopes ?? [])
                    {
                        if (!requiredScopes.List.Contains(scope, StringComparer.Ordinal))
                        {
                            requiredScopes.List.Add(scope);
                        }
                    }
                }
            }

            if (requirement != null)
            {
                skill.SecurityRequirements = [requirement];
            }

            agentCard.Skills.Add(skill);
        }
    }

    private static SecurityRequirement CreateRequirement(A2AAuthorizationMetadata authorization)
    {
        return new SecurityRequirement
        {
            Schemes = new Dictionary<string, StringList>
            {
                [authorization.SecuritySchemeName] = new StringList { List = authorization.RequiredScopes?.ToList() ?? [] },
            },
        };
    }

    private static bool HasCompatibleMetadata(A2ASkillRegistration left, A2ASkillRegistration right)
    {
        return string.Equals(left.Name, right.Name, StringComparison.Ordinal)
            && string.Equals(left.Description, right.Description, StringComparison.Ordinal)
            && left.Tags.SequenceEqual(right.Tags, StringComparer.Ordinal)
            && left.Examples.SequenceEqual(right.Examples, StringComparer.Ordinal)
            && left.InputModes.SequenceEqual(right.InputModes, StringComparer.Ordinal)
            && left.OutputModes.SequenceEqual(right.OutputModes, StringComparer.Ordinal)
            && left.AutoSignInHandlers.SequenceEqual(right.AutoSignInHandlers, StringComparer.Ordinal);
    }

    private static void ValidateProtectedConfiguration(IConfigurationSection section)
    {
        if (section == null)
        {
            return;
        }

        foreach (var property in new[] { "Version", "SupportedInterfaces", "Endpoint", "Endpoints", "Url", "ProtocolVersion" })
        {
            if (section.GetSection(property).Exists())
            {
                throw new InvalidOperationException($"A2A Agent Card configuration cannot set '{property}'.");
            }
        }
    }

    private static void ValidateCard(AgentCard agentCard)
    {
        ArgumentNullException.ThrowIfNull(agentCard);
        ValidateRequirements(agentCard.SecurityRequirements, agentCard.SecuritySchemes);

        foreach (var skill in agentCard.Skills)
        {
            ValidateRequirements(skill.SecurityRequirements, agentCard.SecuritySchemes);
        }
    }

    private static void ValidateRequirements(
        IEnumerable<SecurityRequirement> requirements,
        Dictionary<string, SecurityScheme> securitySchemes)
    {
        foreach (var requirement in requirements ?? [])
        {
            foreach (var schemeRequirement in requirement.Schemes)
            {
                if (!securitySchemes.TryGetValue(schemeRequirement.Key, out var scheme))
                {
                    throw new InvalidOperationException($"A2A Agent Card requirement references missing security scheme '{schemeRequirement.Key}'.");
                }

                var requiredScopes = schemeRequirement.Value?.List ?? [];
                var availableScopes = GetOAuthScopes(scheme);
                if (availableScopes != null && requiredScopes.Any(scope => !availableScopes.Contains(scope)))
                {
                    throw new InvalidOperationException($"A2A Agent Card requirement for scheme '{schemeRequirement.Key}' includes a scope that the scheme does not define.");
                }
            }
        }
    }

    private static HashSet<string> GetOAuthScopes(SecurityScheme scheme)
    {
        var flows = scheme?.OAuth2SecurityScheme?.Flows;
        if (flows == null)
        {
            return null;
        }

        var scopes = new HashSet<string>(StringComparer.Ordinal);
        AddScopes(scopes, flows.AuthorizationCode?.Scopes);
        AddScopes(scopes, flows.ClientCredentials?.Scopes);
        AddScopes(scopes, flows.DeviceCode?.Scopes);
#pragma warning disable CS0618 // Deprecated flows are validated when provided by external configuration.
        AddScopes(scopes, flows.Implicit?.Scopes);
        AddScopes(scopes, flows.Password?.Scopes);
#pragma warning restore CS0618
        return scopes;
    }

    private static void AddScopes(HashSet<string> destination, IDictionary<string, string> scopes)
    {
        if (scopes == null)
        {
            return;
        }

        destination.UnionWith(scopes.Keys);
    }
}
