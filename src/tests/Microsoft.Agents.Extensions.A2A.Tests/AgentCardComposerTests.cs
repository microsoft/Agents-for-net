// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Extensions.A2A.AgentCard;
using Microsoft.Agents.Extensions.A2A.ProtocolExtensions;
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

    [Fact]
    public async Task ComposeAsync_SigningHandler_ReceivesAuthorizationDeclaration()
    {
        var result = await ComposeAsync(card =>
        {
            var extension = Assert.Single(card.Capabilities.Extensions);
            Assert.Equal(InTaskAuthorizationExtension.Uri, extension.Uri);
            card.Signatures = [new AgentCardSignature { Protected = "header", Signature = "signature" }];
            return card;
        });

        Assert.Equal(InTaskAuthorizationExtension.Uri, Assert.Single(result.Capabilities.Extensions).Uri);
        var signature = Assert.Single(result.Signatures);
        Assert.Equal("header", signature.Protected);
        Assert.Equal("signature", signature.Signature);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ComposeAsync_MatchingDeclaration_PreservesRegisteredDeclarations(bool signed)
    {
        var card = CreateCard(signed);
        var declaration = InTaskAuthorizationExtension.CreateAgentCardExtension();
        declaration.Required = null;
        card.Capabilities.Extensions.Add(declaration);
        card.Capabilities.Extensions.Add(new global::A2A.AgentExtension { Uri = "urn:example:other" });

        var result = await ComposeAsync(_ => card, builder =>
            builder.RegisterExtension(new global::A2A.AgentExtension { Uri = "urn:example:other" }));

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

    [Fact]
    public async Task ComposeAsync_CustomDeclaration_IsComposedBeforeSigning()
    {
        var result = await ComposeAsync(card =>
        {
            Assert.Contains(card.Capabilities.Extensions, extension => extension.Uri == "urn:example:custom");
            card.Signatures = [new AgentCardSignature { Protected = "header", Signature = "signature" }];
            return card;
        }, builder => builder.RegisterExtension(new global::A2A.AgentExtension { Uri = "urn:example:custom" }));

        Assert.Single(result.Signatures);
        Assert.Contains(result.Capabilities.Extensions, extension => extension.Uri == "urn:example:custom");
    }

    [Fact]
    public async Task ComposeAsync_UnregisteredDeclaration_Throws()
    {
        var card = CreateCard();
        card.Capabilities.Extensions.Add(new global::A2A.AgentExtension { Uri = "urn:example:unsupported" });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ComposeAsync(card));

        Assert.Contains("urn:example:unsupported", exception.Message);
    }

    [Fact]
    public async Task ComposeAsync_HandlerChangesRegisteredDeclaration_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => ComposeAsync(card =>
        {
            foreach (var extension in card.Capabilities.Extensions)
            {
                if (extension.Uri == "urn:example:custom")
                {
                    extension.Description = "Conflicting description";
                }
            }
            return card;
        }, builder => builder.RegisterExtension(new global::A2A.AgentExtension { Uri = "urn:example:custom" })));
    }

    [Fact]
    public async Task ComposeAsync_SignedHandlerCardMissingCustomDeclaration_RequiresResigning()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ComposeAsync(card =>
        {
            card.Capabilities.Extensions.RemoveAll(extension => extension.Uri == "urn:example:custom");
            card.Signatures = [new AgentCardSignature { Protected = "header", Signature = "signature" }];
            return card;
        }, builder => builder.RegisterExtension(new global::A2A.AgentExtension { Uri = "urn:example:custom" })));

        Assert.Contains("re-sign", exception.Message, StringComparison.OrdinalIgnoreCase);
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

    private static Task<A2AProtocolAgentCard> ComposeAsync(A2AProtocolAgentCard card) => ComposeAsync(_ => card);

    private static Task<A2AProtocolAgentCard> ComposeAsync(
        Func<A2AProtocolAgentCard, A2AProtocolAgentCard> cardHandler,
        Action<A2AProtocolExtensionBuilder> configure = null)
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
        var agent = new CardHandlerAgent(new AgentApplicationOptions(new MemoryStorage()), cardHandler);
        var builder = new A2AProtocolExtensionBuilder();
        new InTaskAuthorizationOperation().Register(builder);
        configure?.Invoke(builder);
        return new A2AAgentCardComposer(configuration).ComposeAsync(
            CreateCard(),
            agent,
            builder.Build().Registry);
    }

    private sealed class CardHandlerAgent(
        AgentApplicationOptions options,
        Func<A2AProtocolAgentCard, A2AProtocolAgentCard> cardHandler)
        : AgentApplication(options), IAgentCardHandler
    {
        public Task<A2AProtocolAgentCard> GetAgentCard(A2AProtocolAgentCard defaultCard) => Task.FromResult(cardHandler(defaultCard));
    }
}
