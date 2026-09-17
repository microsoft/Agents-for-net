// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using A2A;
using Microsoft.Agents.Samples.A2AClient;
using Xunit;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class A2AAgentCardAuthenticationTests
{
    [Fact]
    public void Select_DelegatedMode_UsesDeviceCodeFlowAndRequirementScopes()
    {
        AgentCard card = CreateCard(
            "delegated",
            new OAuthFlows
            {
                DeviceCode = new()
                {
                    DeviceAuthorizationUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode",
                    TokenUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/token",
                },
            },
            "api://agent/access_as_user",
            skillRequirement: true);

        A2AAgentCardAuthentication selection = A2AAgentCardAuthentication.Select(card, A2AAuthMode.Delegated);

        Assert.Equal("delegated", selection.SecuritySchemeName);
        Assert.Equal("https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode", selection.DeviceAuthorizationUrl);
        Assert.Equal("https://login.microsoftonline.com/organizations/oauth2/v2.0/token", selection.TokenUrl);
        Assert.Equal(["api://agent/access_as_user"], selection.Scopes);
    }

    [Fact]
    public void Select_AppMode_UsesClientCredentialsFlowAndRequirementScopes()
    {
        AgentCard card = CreateCard(
            "application",
            new OAuthFlows
            {
                ClientCredentials = new()
                {
                    TokenUrl = "https://login.example.com/token",
                },
            },
            "api://agent/.default");

        A2AAgentCardAuthentication selection = A2AAgentCardAuthentication.Select(card, A2AAuthMode.App);

        Assert.Equal("application", selection.SecuritySchemeName);
        Assert.Null(selection.DeviceAuthorizationUrl);
        Assert.Equal("https://login.example.com/token", selection.TokenUrl);
        Assert.Equal(["api://agent/.default"], selection.Scopes);
    }

    [Fact]
    public void Select_RequirementUsesUnsupportedFlow_Throws()
    {
        AgentCard card = CreateCard(
            "authorizationCode",
            new OAuthFlows
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = "https://login.example.com/authorize",
                    TokenUrl = "https://login.example.com/token",
                },
            },
            "api://agent/access_as_user");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => A2AAgentCardAuthentication.Select(card, A2AAuthMode.Delegated));

        Assert.Contains("Device Code", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Select_RequirementReferencesMissingScheme_Throws()
    {
        var card = new AgentCard
        {
            SecurityRequirements =
            [
                new SecurityRequirement
                {
                    Schemes = new Dictionary<string, StringList>
                    {
                        ["missing"] = new() { List = ["api://agent/access_as_user"] },
                    },
                },
            ],
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => A2AAgentCardAuthentication.Select(card, A2AAuthMode.Delegated));

        Assert.Contains("missing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Select_SkipsUnsatisfiedAlternativeAndUsesLaterSatisfiableAlternative()
    {
        AgentCard card = CreateCard(
            "delegated",
            CreateDeviceCodeFlows(),
            ["api://agent/access_as_user"],
            new SecurityRequirement
            {
                Schemes = new Dictionary<string, StringList>
                {
                    ["missing"] = new() { List = ["api://agent/access_as_user"] },
                },
            });

        A2AAgentCardAuthentication selection = A2AAgentCardAuthentication.Select(card, A2AAuthMode.Delegated);

        Assert.Equal("delegated", selection.SecuritySchemeName);
    }

    [Fact]
    public void Select_SkipsCompoundAlternativeAndUsesLaterSatisfiableAlternative()
    {
        AgentCard card = CreateCard(
            "delegated",
            CreateDeviceCodeFlows(),
            ["api://agent/access_as_user"],
            new SecurityRequirement
            {
                Schemes = new Dictionary<string, StringList>
                {
                    ["delegated"] = new() { List = ["api://agent/access_as_user"] },
                    ["second"] = new() { List = ["api://second/access_as_user"] },
                },
            });

        card.SecuritySchemes!["second"] = new()
        {
            OAuth2SecurityScheme = new OAuth2SecurityScheme { Flows = CreateDeviceCodeFlows() },
        };

        A2AAgentCardAuthentication selection = A2AAgentCardAuthentication.Select(card, A2AAuthMode.Delegated);

        Assert.Equal("delegated", selection.SecuritySchemeName);
    }

    [Fact]
    public void Select_ForSkill_IgnoresRequirementsFromOtherSkills()
    {
        SecurityRequirement delegatedRequirement = CreateRequirement(
            "delegated",
            "api://agent/access_as_user");
        AgentSkill selectedSkill = new()
        {
            Id = "profile",
            SecurityRequirements = [delegatedRequirement],
        };
        var card = new AgentCard
        {
            SecuritySchemes = new Dictionary<string, SecurityScheme>
            {
                ["delegated"] = new()
                {
                    OAuth2SecurityScheme = new OAuth2SecurityScheme { Flows = CreateDeviceCodeFlows() },
                },
                ["application"] = new()
                {
                    OAuth2SecurityScheme = new OAuth2SecurityScheme
                    {
                        Flows = new OAuthFlows
                        {
                            ClientCredentials = new()
                            {
                                TokenUrl = "https://login.example.com/token",
                            },
                        },
                    },
                },
            },
            Skills =
            [
                new AgentSkill
                {
                    Id = "application-only",
                    SecurityRequirements = [CreateRequirement("application", "api://agent/.default")],
                },
                selectedSkill,
            ],
        };

        A2AAgentCardAuthentication selection = A2AAgentCardAuthentication.Select(
            card,
            selectedSkill,
            A2AAuthMode.Delegated);

        Assert.Equal("delegated", selection.SecuritySchemeName);
        Assert.Equal(["api://agent/access_as_user"], selection.Scopes);
    }

    [Fact]
    public void Select_ForSkill_CombinesAgentAndSkillScopesForSameScheme()
    {
        AgentSkill skill = new()
        {
            Id = "profile",
            SecurityRequirements = [CreateRequirement("delegated", "profile.read")],
        };
        var card = new AgentCard
        {
            SecuritySchemes = new Dictionary<string, SecurityScheme>
            {
                ["delegated"] = new()
                {
                    OAuth2SecurityScheme = new OAuth2SecurityScheme { Flows = CreateDeviceCodeFlows() },
                },
            },
            SecurityRequirements = [CreateRequirement("delegated", "agent.read")],
            Skills = [skill],
        };

        A2AAgentCardAuthentication selection = A2AAgentCardAuthentication.Select(
            card,
            skill,
            A2AAuthMode.Delegated);

        Assert.Equal(["agent.read", "profile.read"], selection.Scopes);
    }

    [Fact]
    public void Select_AutomaticMode_IgnoresUnsupportedCompoundAlternative()
    {
        AgentSkill skill = new()
        {
            Id = "profile",
            SecurityRequirements =
            [
                new SecurityRequirement
                {
                    Schemes = new Dictionary<string, StringList>
                    {
                        ["delegated"] = new() { List = ["agent.read"] },
                        ["application"] = new() { List = ["api://agent/.default"] },
                    },
                },
                CreateRequirement("delegated", "agent.read"),
            ],
        };
        var card = new AgentCard
        {
            SecuritySchemes = new Dictionary<string, SecurityScheme>
            {
                ["delegated"] = new()
                {
                    OAuth2SecurityScheme = new OAuth2SecurityScheme { Flows = CreateDeviceCodeFlows() },
                },
                ["application"] = new()
                {
                    OAuth2SecurityScheme = new OAuth2SecurityScheme
                    {
                        Flows = new OAuthFlows
                        {
                            ClientCredentials = new()
                            {
                                TokenUrl = "https://login.example.com/token",
                            },
                        },
                    },
                },
            },
            Skills = [skill],
        };

        A2AAgentCardAuthentication? selection = A2AAgentCardAuthentication.Select(card, skill, modeOverride: null);

        Assert.NotNull(selection);
        Assert.Equal(A2AAuthMode.Delegated, selection.Mode);
    }

    [Fact]
    public void Select_ExplicitDelegatedModeWithTwoProvidersAndNoSkill_ThrowsHelpfulError()
    {
        AgentCard card = CreateTwoProviderCard();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => A2AAgentCardAuthentication.Select(card, skill: null, modeOverride: A2AAuthMode.Delegated));

        Assert.Contains("multiple delegated security schemes", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Select_EmptyAcquisitionScopes_ThrowsAfterRejectingAlternative()
    {
        AgentCard card = CreateCard("delegated", CreateDeviceCodeFlows(), []);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => A2AAgentCardAuthentication.Select(card, A2AAuthMode.Delegated));

        Assert.Contains("acquisition scopes", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://login.microsoftonline.com/organizations/oauth2/v2.0/token", "https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode")]
    [InlineData("not an endpoint", "https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode")]
    [InlineData("https://login.microsoftonline.com/organizations/oauth2/v2.0/token", "http://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode")]
    [InlineData("https://login.microsoftonline.com/organizations/oauth2/v2.0/token", "not an endpoint")]
    public void Select_NonHttpsOrMalformedEndpoints_ThrowsAfterRejectingAlternative(string tokenUrl, string deviceAuthorizationUrl)
    {
        AgentCard card = CreateCard(
            "delegated",
            new OAuthFlows
            {
                DeviceCode = new()
                {
                    TokenUrl = tokenUrl,
                    DeviceAuthorizationUrl = deviceAuthorizationUrl,
                },
            },
            ["api://agent/access_as_user"]);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => A2AAgentCardAuthentication.Select(card, A2AAuthMode.Delegated));

        Assert.Contains("HTTPS", exception.Message, StringComparison.Ordinal);
    }

    private static OAuthFlows CreateDeviceCodeFlows()
    {
        return new OAuthFlows
        {
            DeviceCode = new()
            {
                DeviceAuthorizationUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode",
                TokenUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/token",
            },
        };
    }

    private static SecurityRequirement CreateRequirement(string schemeName, string requiredScope)
        => new()
        {
            Schemes = new Dictionary<string, StringList>
            {
                [schemeName] = new() { List = [requiredScope] },
            },
        };

    private static AgentCard CreateCard(string schemeName, OAuthFlows flows, string requiredScope, bool skillRequirement = false)
    {
        return CreateCard(
            schemeName,
            flows,
            [requiredScope],
            additionalRequirement: null,
            skillRequirement);
    }

    private static AgentCard CreateCard(
        string schemeName,
        OAuthFlows flows,
        IReadOnlyList<string> requiredScopes,
        SecurityRequirement? additionalRequirement = null,
        bool skillRequirement = false)
    {
        var requirement = new SecurityRequirement
        {
            Schemes = new Dictionary<string, StringList>
            {
                [schemeName] = new() { List = [.. requiredScopes] },
            },
        };
        var requirements = new List<SecurityRequirement>();
        if (additionalRequirement is not null)
        {
            requirements.Add(additionalRequirement);
        }

        requirements.Add(requirement);
        return new AgentCard
        {
            SecuritySchemes = new Dictionary<string, SecurityScheme>
            {
                [schemeName] = new()
                {
                    OAuth2SecurityScheme = new OAuth2SecurityScheme
                    {
                        Flows = flows,
                    },
                },
            },
            SecurityRequirements = skillRequirement ? null : requirements,
            Skills = skillRequirement
                ? [new AgentSkill { SecurityRequirements = [requirement] }]
                : [],
        };
    }

    private static AgentCard CreateTwoProviderCard()
    {
        return new AgentCard
        {
            SecuritySchemes = new Dictionary<string, SecurityScheme>
            {
                ["delegated"] = new()
                {
                    OAuth2SecurityScheme = new OAuth2SecurityScheme
                    {
                        Flows = new OAuthFlows
                        {
                            DeviceCode = new()
                            {
                                DeviceAuthorizationUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode",
                                TokenUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/token",
                            },
                        },
                    },
                },
                ["github"] = new()
                {
                    OAuth2SecurityScheme = new OAuth2SecurityScheme
                    {
                        Flows = new OAuthFlows
                        {
                            DeviceCode = new()
                            {
                                DeviceAuthorizationUrl = "https://github.com/login/device/code",
                                TokenUrl = "https://github.com/login/oauth/access_token",
                            },
                        },
                    },
                },
            },
            Skills =
            [
                new AgentSkill
                {
                    Id = "Microsoft Graph profile",
                    Name = "Microsoft Graph profile",
                    Examples = ["-me"],
                    SecurityRequirements =
                    [
                        new SecurityRequirement
                        {
                            Schemes = new Dictionary<string, StringList>
                            {
                                ["delegated"] = new() { List = ["api://agent/access_as_user"] },
                            },
                        },
                    ],
                },
                new AgentSkill
                {
                    Id = "GitHub assigned issues",
                    Name = "GitHub assigned issues",
                    Examples = ["-issues"],
                    SecurityRequirements =
                    [
                        new SecurityRequirement
                        {
                            Schemes = new Dictionary<string, StringList>
                            {
                                ["github"] = new() { List = ["repo"] },
                            },
                        },
                    ],
                },
            ],
        };
    }
}
