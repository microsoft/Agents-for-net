// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Extensions.A2A.AgentCard;
using Microsoft.Agents.Extensions.A2A.ProtocolExtensions.InTaskAuthorization;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using A2AProtocolAgentCard = A2A.AgentCard;

namespace Microsoft.Agents.Extensions.A2A.Tests;

public class AgentCardComposerTests
{
    [Fact]
    public async Task ComposeAsync_MissingDeclaration_DoesNotMutateHandlerCard()
    {
        var card = CreateCard();

        var result = await ComposeAsync(card);

        Assert.Empty(card.Capabilities.Extensions);
        Assert.NotSame(card, result);
        Assert.Equal(InTaskAuthorizationExtension.Uri, Assert.Single(result.Capabilities.Extensions).Uri);
    }

    [Fact]
    public async Task ComposeAsync_SignedCardMissingDeclaration_RequiresResigning()
    {
        var card = CreateCard(signed: true);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ComposeAsync(card));

        Assert.Contains("re-sign", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(card.Capabilities.Extensions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ComposeAsync_MatchingDeclaration_PreservesUnrelatedDeclarations(bool signed)
    {
        var card = CreateCard(signed);
        var declaration = InTaskAuthorizationExtension.CreateAgentCardExtension();
        declaration.Required = null;
        card.Capabilities.Extensions.Add(declaration);
        card.Capabilities.Extensions.Add(new global::A2A.AgentExtension { Uri = "urn:example:other" });

        var result = await ComposeAsync(card);

        Assert.Equal(2, result.Capabilities.Extensions.Count);
        Assert.Null(result.Capabilities.Extensions[0].Required);
        Assert.Equal("urn:example:other", result.Capabilities.Extensions[1].Uri);
        Assert.Equal(signed ? 1 : 0, result.Signatures?.Count ?? 0);
        Assert.NotSame(card.Capabilities.Extensions, result.Capabilities.Extensions);
    }

    [Fact]
    public async Task ComposeAsync_DuplicateDeclaration_Throws()
    {
        var card = CreateCard();
        card.Capabilities.Extensions.Add(InTaskAuthorizationExtension.CreateAgentCardExtension());
        card.Capabilities.Extensions.Add(InTaskAuthorizationExtension.CreateAgentCardExtension());

        await Assert.ThrowsAsync<InvalidOperationException>(() => ComposeAsync(card));
    }

    private static A2AProtocolAgentCard CreateCard(bool signed = false) => new()
    {
        Capabilities = new AgentCapabilities { Extensions = [] },
        Skills = [],
        SecuritySchemes = [],
        Signatures = signed
            ? [new AgentCardSignature { Protected = "header", Signature = "signature" }]
            : null,
    };

    private static Task<A2AProtocolAgentCard> ComposeAsync(A2AProtocolAgentCard card)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["AgentApplication:UserAuthorization:AutoSignIn"] = "false",
            ["AgentApplication:UserAuthorization:Handlers:request:Type"] = "A2AUserAuthorization",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:Mode"] = "InTask",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:OAuthFlows:DeviceCode:DeviceAuthorizationUrl"] = "https://login.example.com/devicecode",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:OAuthFlows:DeviceCode:TokenUrl"] = "https://login.example.com/token",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:OAuthFlows:DeviceCode:Scopes:agent.read"] = "Access the agent",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:RequiredScopes:0"] = "agent.read",
        }).Build();
        var agent = new CardHandlerAgent(new AgentApplicationOptions(new MemoryStorage()), card);
        return new A2AAgentCardComposer(configuration).ComposeAsync(
            CreateCard(),
            agent,
            A2AServiceExtensions.Dispatch.InTaskAuthorization.Extension);
    }

    private sealed class CardHandlerAgent(AgentApplicationOptions options, A2AProtocolAgentCard card)
        : AgentApplication(options), IAgentCardHandler
    {
        public Task<A2AProtocolAgentCard> GetAgentCard(A2AProtocolAgentCard defaultCard) => Task.FromResult(card);
    }
}
