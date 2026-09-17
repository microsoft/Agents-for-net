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
        A2AAgentCardAuthentication? configuredAuthentication = null;
        var planner = new A2ARequestPlanner(card, session, authentication => configuredAuthentication = authentication);

        A2AAgentCardSkillSelection selection = planner.Plan("-me");

        Assert.Equal("Microsoft Graph profile", selection.Skill!.Id);
        Assert.Equal(A2AAuthMode.Delegated, session.Mode);
        Assert.Equal("delegated", configuredAuthentication!.SecuritySchemeName);
        Assert.Equal(["api://agent/access_as_user"], configuredAuthentication.Scopes);
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
        A2AAgentCardAuthentication? configuredAuthentication = null;
        var planner = new A2ARequestPlanner(card, session, authentication => configuredAuthentication = authentication);

        A2AAgentCardSkillSelection selection = planner.Plan("Tell me a joke.");

        Assert.Null(selection.Skill);
        Assert.False(selection.IsAmbiguous);
        Assert.Equal(A2AAuthMode.None, session.Mode);
        Assert.Null(session.SelectedAuthentication);
        Assert.Null(configuredAuthentication);
    }

    [Fact]
    public void Plan_ExplicitModeOverride_ForcesMatchingCardAuthentication()
    {
        AgentCard card = CreateTwoProviderCard();
        var session = new A2AAuthenticationSession();
        session.SetMode(A2AAuthMode.Delegated);
        A2AAgentCardAuthentication? configuredAuthentication = null;
        var planner = new A2ARequestPlanner(card, session, authentication => configuredAuthentication = authentication);

        A2AAgentCardSkillSelection selection = planner.Plan("-issues");

        Assert.Equal("GitHub assigned issues", selection.Skill!.Id);
        Assert.Equal(A2AAuthMode.Delegated, session.Mode);
        Assert.Null(configuredAuthentication);
        Assert.Equal("github", session.SelectedAuthentication!.SecuritySchemeName);
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
}
