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
                    DeviceAuthorizationUrl = "https://login.example.com/devicecode",
                    TokenUrl = "https://login.example.com/token",
                },
            },
            "api://agent/access_as_user",
            skillRequirement: true);

        A2AAgentCardAuthentication selection = A2AAgentCardAuthentication.Select(card, A2AAuthMode.Delegated);

        Assert.Equal("delegated", selection.SecuritySchemeName);
        Assert.Equal("https://login.example.com/devicecode", selection.DeviceAuthorizationUrl);
        Assert.Equal("https://login.example.com/token", selection.TokenUrl);
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

    private static AgentCard CreateCard(string schemeName, OAuthFlows flows, string requiredScope, bool skillRequirement = false)
    {
        var requirement = new SecurityRequirement
        {
            Schemes = new Dictionary<string, StringList>
            {
                [schemeName] = new() { List = [requiredScope] },
            },
        };
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
            SecurityRequirements = skillRequirement ? null : [requirement],
            Skills = skillRequirement
                ? [new AgentSkill { SecurityRequirements = [requirement] }]
                : [],
        };
    }
}
