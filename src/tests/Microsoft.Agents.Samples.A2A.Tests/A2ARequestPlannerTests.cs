// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using A2A;
using Microsoft.Agents.Samples.A2AClient;
using Xunit;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class A2ARequestPlannerTests
{
    [Fact]
    public void Plan_NoModeOverride_UsesSelectedSkillsAdvertisedOAuthFlow()
    {
        AgentCard card = CreateTwoProviderCard();
        var session = new A2AAuthenticationSession();
        var planner = new A2ARequestPlanner(card, session);

        A2AAgentCardSkillSelection selection = planner.Plan("-me");

        Assert.Equal("Microsoft Graph profile", selection.Skill!.Id);
        Assert.Equal(A2AAuthMode.Delegated, session.Mode);
        Assert.Equal("delegated", session.SelectedAuthentication!.SecuritySchemeName);
        Assert.Equal(["api://agent/access_as_user"], session.SelectedAuthentication.Scopes);
    }

    [Theory]
    [InlineData("-me", "delegated", "api://agent/access_as_user")]
    [InlineData("-issues", "github", "repo")]
    public void Plan_NoModeOverride_MatchedSkillOnTwoProviderCard_SelectsItsScheme(
        string input,
        string expectedScheme,
        string expectedScope)
    {
        AgentCard card = CreateTwoProviderCard();
        var session = new A2AAuthenticationSession();
        var planner = new A2ARequestPlanner(card, session);

        A2AAgentCardSkillSelection selection = planner.Plan(input);

        Assert.Equal(A2AAuthMode.Delegated, session.Mode);
        Assert.NotNull(selection.Skill);
        Assert.Equal(expectedScheme, session.SelectedAuthentication!.SecuritySchemeName);
        Assert.Equal([expectedScope], session.SelectedAuthentication.Scopes);
    }

    [Fact]
    public void Plan_NoModeOverride_UnmatchedInputOnTwoProviderCard_RemainsAnonymous()
    {
        AgentCard card = CreateTwoProviderCard();
        var session = new A2AAuthenticationSession();
        var planner = new A2ARequestPlanner(card, session);

        A2AAgentCardSkillSelection selection = planner.Plan("Tell me a joke.");

        Assert.Null(selection.Skill);
        Assert.False(selection.IsAmbiguous);
        Assert.Equal(A2AAuthMode.None, session.Mode);
        Assert.Null(session.SelectedAuthentication);
    }

    [Fact]
    public void Plan_ExplicitModeOverride_ForcesMatchingCardAuthentication()
    {
        AgentCard card = CreateCardWithPublicSkillAndSingleProtectedFallback();
        AgentSkill publicSkill = Assert.Single(card.Skills!, skill => skill.Id == "Public echo");
        const string input = "-echo";

        var automaticSession = new A2AAuthenticationSession();
        var automaticPlanner = new A2ARequestPlanner(card, automaticSession);

        A2AAgentCardSkillSelection automaticSelection = automaticPlanner.Plan(input);

        Assert.Same(publicSkill, automaticSelection.Skill);
        Assert.False(automaticSelection.IsAmbiguous);
        Assert.Equal(A2AAuthMode.None, automaticSession.Mode);
        Assert.Null(automaticSession.SelectedAuthentication);

        var overrideSession = new A2AAuthenticationSession();
        overrideSession.SetMode(A2AAuthMode.Delegated);
        var overridePlanner = new A2ARequestPlanner(card, overrideSession);

        A2AAgentCardSkillSelection overrideSelection = overridePlanner.Plan(input);

        Assert.Same(publicSkill, overrideSelection.Skill);
        Assert.False(overrideSelection.IsAmbiguous);
        Assert.Equal(A2AAuthMode.Delegated, overrideSession.Mode);
        A2AAgentCardAuthentication authentication = Assert.IsType<A2AAgentCardAuthentication>(overrideSession.SelectedAuthentication);
        Assert.Equal("delegated", authentication.SecuritySchemeName);
        Assert.Equal("https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode", authentication.DeviceAuthorizationUrl);
        Assert.Equal("https://login.microsoftonline.com/organizations/oauth2/v2.0/token", authentication.TokenUrl);
        Assert.Equal(["api://agent/access_as_user"], authentication.Scopes);
    }

    private static SecurityRequirement CreateRequirement(string schemeName, string scope)
        => new()
        {
            Schemes = new Dictionary<string, StringList>
            {
                [schemeName] = new() { List = [scope] },
            },
        };

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

    private static AgentCard CreateCardWithPublicSkillAndSingleProtectedFallback()
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
            },
            Skills =
            [
                new AgentSkill
                {
                    Id = "Public echo",
                    Name = "Public echo",
                    Description = "Echoes public text without authentication.",
                    Examples = ["-echo"],
                },
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
            ],
        };
    }
}
