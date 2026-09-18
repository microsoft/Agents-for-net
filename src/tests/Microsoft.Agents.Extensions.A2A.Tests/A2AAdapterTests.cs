// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using A2AProtocolAgentCard = A2A.AgentCard;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.A2A.AgentCard;
using Microsoft.Agents.Extensions.A2A.Authorization;
using Microsoft.Agents.Extensions.A2A.Pipeline;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.A2A.Tests;

public class A2AAdapterTests
{
    private readonly Mock<ITaskStore> _mockTaskStore;
    private readonly ILoggerFactory _mockLogger;
    private readonly Mock<IStorage> _mockStorage;

    public A2AAdapterTests()
    {
        _mockTaskStore = new Mock<ITaskStore>();
        _mockStorage = new Mock<IStorage>();
        _mockLogger = NullLoggerFactory.Instance;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithTaskStore_ShouldInitializeAdapter()
    {
        // Act
        var adapter = new A2AAdapter(_mockTaskStore.Object, _mockLogger);

        // Assert
        Assert.NotNull(adapter);
        Assert.NotNull(adapter.OnTurnError);
    }

    [Fact]
    public void Constructor_WithStorage_ShouldInitializeAdapter()
    {
        // Act
        var adapter = new A2AAdapter(_mockStorage.Object, _mockLogger);

        // Assert
        Assert.NotNull(adapter);
        Assert.NotNull(adapter.OnTurnError);
    }

    [Fact]
    public void Constructor_WithNullTaskStore_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new A2AAdapter((ITaskStore)null, _mockLogger));
    }

    #endregion

    #region SendActivitiesAsync Tests

    [Fact]
    public async Task SendActivitiesAsync_WithoutRequestContext_UsesA2AErrorMetadata()
    {
        var adapter = new A2AAdapter(new InMemoryTaskStore(), NullLoggerFactory.Instance);
        var activity = new Activity { RequestId = Guid.NewGuid().ToString("N") };
        var turnContext = new TurnContext(adapter, activity);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => adapter.SendActivitiesAsync(turnContext, [], CancellationToken.None));

        Assert.IsType<InvalidOperationException>(exception);
        A2AErrorMetadataAssertions.AssertErrorMetadata(exception, -100018);
        Assert.Equal(
            $"No A2A request context was found for request '{activity.RequestId}'.",
            exception.Message);
    }

    #endregion

    #region ProcessAgentCardAsync Tests

    [Fact]
    public async Task ProcessAgentCard_WithConfiguredScheme_EmitsOAuth2Scheme()
    {
        var adapter = CreateAdapter(CreateConfiguration(new Dictionary<string, string>
        {
            ["AgentApplication:UserAuthorization:Handlers:request:Type"] = "A2AUserAuthorization",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:SecuritySchemeName"] = "deviceCode",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:OAuthFlows:DeviceCode:DeviceAuthorizationUrl"] = "https://login.example.com/devicecode",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:OAuthFlows:DeviceCode:TokenUrl"] = "https://login.example.com/token",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:OAuthFlows:DeviceCode:Scopes:agent.read"] = "Access the agent",
        }));

        var agentCard = await ProcessAgentCardAsync(adapter, new AgentApplication(new AgentApplicationOptions(_mockStorage.Object)));

        var scheme = agentCard.SecuritySchemes["deviceCode"].OAuth2SecurityScheme;
        Assert.NotNull(scheme);
        Assert.Equal("https://login.example.com/devicecode", scheme.Flows.DeviceCode.DeviceAuthorizationUrl);
        Assert.Equal("Access the agent", scheme.Flows.DeviceCode.Scopes["agent.read"]);
    }

    [Fact]
    public async Task ProcessAgentCard_WithGlobalAutoSignin_EmitsAgentRequirement()
    {
        var adapter = CreateAdapter(CreateConfiguration(new Dictionary<string, string>
        {
            ["AgentApplication:A2A:AgentCard:SecuritySchemes:agentBearer:HttpAuthSecurityScheme:Scheme"] = "bearer",
            ["AgentApplication:UserAuthorization:AutoSignIn"] = "true",
            ["AgentApplication:UserAuthorization:DefaultHandlerName"] = "request",
            ["AgentApplication:UserAuthorization:Handlers:request:Type"] = "A2AUserAuthorization",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:SecuritySchemeName"] = "agentBearer",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:RequiredScopes:0"] = "api://agent/access_as_user",
        }));

        var agentCard = await ProcessAgentCardAsync(adapter, new AgentApplication(new AgentApplicationOptions(_mockStorage.Object)));

        var requirement = Assert.Single(agentCard.SecurityRequirements);
        Assert.Equal(["api://agent/access_as_user"], requirement.Schemes["agentBearer"].List);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public async Task ProcessAgentCard_WithSchemeLessGlobalHandler_ThrowsNamedMetadataError(string schemeName)
    {
        var adapter = CreateAdapter(CreateConfiguration(new Dictionary<string, string>
        {
            ["AgentApplication:UserAuthorization:AutoSignIn"] = "true",
            ["AgentApplication:UserAuthorization:DefaultHandlerName"] = "legacy-request",
            ["AgentApplication:UserAuthorization:Handlers:legacy-request:Type"] = "A2AUserAuthorization",
            ["AgentApplication:UserAuthorization:Handlers:legacy-request:Settings:SecuritySchemeName"] = schemeName,
        }));
        var agent = new AgentApplication(new AgentApplicationOptions(_mockStorage.Object));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ProcessAgentCardAsync(adapter, agent));

        A2AErrorMetadataAssertions.AssertErrorMetadata(exception, -100014);
        Assert.Equal(
            "A2A authorization handler 'legacy-request' used by global AutoSignIn requires Agent Card security metadata.",
            exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public async Task ProcessAgentCard_WithSchemeLessSkillHandler_ThrowsNamedMetadataError(string schemeName)
    {
        var adapter = CreateAdapter(CreateConfiguration(new Dictionary<string, string>
        {
            ["AgentApplication:UserAuthorization:AutoSignIn"] = "false",
            ["AgentApplication:UserAuthorization:Handlers:legacy-request:Type"] = "A2AUserAuthorization",
            ["AgentApplication:UserAuthorization:Handlers:legacy-request:Settings:SecuritySchemeName"] = schemeName,
        }));
        var agent = CreateSkillAgent("legacy-request");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ProcessAgentCardAsync(adapter, agent));

        A2AErrorMetadataAssertions.AssertErrorMetadata(exception, -100014);
        Assert.Equal(
            "A2A authorization handler 'legacy-request' used by skill 'weather' autoSignInHandlers requires Agent Card security metadata.",
            exception.Message);
    }

    [Theory]
    [InlineData("A2AUserAuthorization")]
    [InlineData("AzureBotUserAuthorization")]
    public async Task ProcessAgentCard_WithUnreferencedSchemeLessHandler_DoesNotRequireItsMetadata(string handlerType)
    {
        var adapter = CreateAdapter(CreateConfiguration(new Dictionary<string, string>
        {
            ["AgentApplication:A2A:AgentCard:SecuritySchemes:agentBearer:HttpAuthSecurityScheme:Scheme"] = "bearer",
            ["AgentApplication:UserAuthorization:AutoSignIn"] = "true",
            ["AgentApplication:UserAuthorization:DefaultHandlerName"] = "request",
            ["AgentApplication:UserAuthorization:Handlers:request:Type"] = "A2AUserAuthorization",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:SecuritySchemeName"] = "agentBearer",
            ["AgentApplication:UserAuthorization:Handlers:unused:Type"] = handlerType,
        }));

        var card = await ProcessAgentCardAsync(adapter, CreateSkillAgent("request"));

        Assert.Equal("agentBearer", Assert.Single(Assert.Single(card.SecurityRequirements).Schemes).Key);
        Assert.Equal("agentBearer", Assert.Single(Assert.Single(Assert.Single(card.Skills).SecurityRequirements).Schemes).Key);
        Assert.DoesNotContain("unused", card.SecuritySchemes.Keys);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessAgentCard_WithUriQualifiedOAuthScopes_PreservesScopeCatalog(bool inlineScheme)
    {
        var values = new Dictionary<string, string>
        {
            ["AgentApplication:UserAuthorization:AutoSignIn"] = "true",
            ["AgentApplication:UserAuthorization:DefaultHandlerName"] = "request",
            ["AgentApplication:UserAuthorization:Handlers:request:Type"] = "A2AUserAuthorization",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:RequiredScopes:0"] = "api://agent/access_as_user",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:RequiredScopes:1"] = "urn:agent:read",
        };
        string flowsPath;
        if (inlineScheme)
        {
            values["AgentApplication:UserAuthorization:Handlers:request:Settings:SecuritySchemeName"] = "delegated";
            flowsPath = "AgentApplication:UserAuthorization:Handlers:request:Settings:OAuthFlows";
        }
        else
        {
            values["AgentApplication:UserAuthorization:Handlers:request:Settings:SecuritySchemeName"] = "delegated";
            flowsPath = "AgentApplication:A2A:AgentCard:SecuritySchemes:delegated:OAuth2SecurityScheme:Flows";
        }
        values[$"{flowsPath}:DeviceCode:TokenUrl"] = "https://login.example.com/token";
        values[$"{flowsPath}:DeviceCode:DeviceAuthorizationUrl"] = "https://login.example.com/devicecode";
        values[$"{flowsPath}:DeviceCode:Scopes:api://agent/access_as_user"] = "Access the agent";
        values[$"{flowsPath}:DeviceCode:Scopes:urn:agent:read"] = "Read the agent";
        values[$"{flowsPath}:DeviceCode:Scopes:agent.profile"] = "Read the profile";

        var card = await ProcessAgentCardAsync(
            CreateAdapter(CreateConfiguration(values)),
            new AgentApplication(new AgentApplicationOptions(_mockStorage.Object)));

        var scopes = card.SecuritySchemes["delegated"].OAuth2SecurityScheme.Flows.DeviceCode.Scopes;
        Assert.Equal(3, scopes.Count);
        Assert.Equal("Access the agent", scopes["api://agent/access_as_user"]);
        Assert.Equal("Read the agent", scopes["urn:agent:read"]);
        Assert.Equal("Read the profile", scopes["agent.profile"]);
        Assert.Equal(["api://agent/access_as_user", "urn:agent:read"], Assert.Single(card.SecurityRequirements).Schemes["delegated"].List);
    }

    [Theory]
    [InlineData("missing", null, true)]
    [InlineData("requset", null, true)]
    [InlineData("external", "AzureBotUserAuthorization", true)]
    [InlineData("external", "AzureBotUserAuthorization", false)]
    public async Task ProcessAgentCard_WithUnresolvedGlobalHandler_ThrowsNamedContextError(
        string handlerName,
        string handlerType,
        bool explicitDefault)
    {
        var values = new Dictionary<string, string>
        {
            ["AgentApplication:UserAuthorization:AutoSignIn"] = "true",
        };
        if (explicitDefault)
        {
            values["AgentApplication:UserAuthorization:DefaultHandlerName"] = handlerName;
        }
        if (handlerType != null)
        {
            values[$"AgentApplication:UserAuthorization:Handlers:{handlerName}:Type"] = handlerType;
        }
        var adapter = CreateAdapter(CreateConfiguration(values));
        var agent = new AgentApplication(new AgentApplicationOptions(_mockStorage.Object));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ProcessAgentCardAsync(adapter, agent));

        A2AErrorMetadataAssertions.AssertErrorMetadata(exception, -100013);
        Assert.Equal(
            $"A2A global AutoSignIn references authorization handler '{handlerName}', which must be configured as an A2AUserAuthorization handler.",
            exception.Message);
    }

    [Theory]
    [InlineData("missing", null)]
    [InlineData("requset", null)]
    [InlineData("external", "AzureBotUserAuthorization")]
    public async Task ProcessAgentCard_WithUnresolvedSkillHandler_ThrowsNamedContextError(string handlerName, string handlerType)
    {
        var values = new Dictionary<string, string>
        {
            ["AgentApplication:A2A:AgentCard:SecuritySchemes:agentBearer:HttpAuthSecurityScheme:Scheme"] = "bearer",
            ["AgentApplication:UserAuthorization:AutoSignIn"] = "false",
            ["AgentApplication:UserAuthorization:Handlers:request:Type"] = "A2AUserAuthorization",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:SecuritySchemeName"] = "agentBearer",
        };
        if (handlerType != null)
        {
            values[$"AgentApplication:UserAuthorization:Handlers:{handlerName}:Type"] = handlerType;
        }
        var adapter = CreateAdapter(CreateConfiguration(values));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ProcessAgentCardAsync(adapter, CreateSkillAgent("request", handlerName)));

        A2AErrorMetadataAssertions.AssertErrorMetadata(exception, -100013);
        Assert.Equal(
            $"A2A skill 'weather' autoSignInHandlers references authorization handler '{handlerName}', which must be configured as an A2AUserAuthorization handler.",
            exception.Message);
    }

    [Theory]
    [InlineData(" request ")]
    [InlineData("\tReQuEsT\r\n")]
    public async Task ProcessAgentCard_WithWhitespacePaddedGlobalHandler_EmitsAgentRequirement(string handlerName)
    {
        var adapter = CreateAdapter(CreateConfiguration(new Dictionary<string, string>
        {
            ["AgentApplication:A2A:AgentCard:SecuritySchemes:agentBearer:HttpAuthSecurityScheme:Scheme"] = "bearer",
            ["AgentApplication:UserAuthorization:AutoSignIn"] = "true",
            ["AgentApplication:UserAuthorization:DefaultHandlerName"] = handlerName,
            ["AgentApplication:UserAuthorization:Handlers:request:Type"] = "A2AUserAuthorization",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:SecuritySchemeName"] = "agentBearer",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:RequiredScopes:0"] = "api://agent/access_as_user",
        }));

        var card = await ProcessAgentCardAsync(adapter, new AgentApplication(new AgentApplicationOptions(_mockStorage.Object)));

        Assert.Equal(["api://agent/access_as_user"], Assert.Single(card.SecurityRequirements).Schemes["agentBearer"].List);
    }

    [Theory]
    [InlineData(" request ")]
    [InlineData("\tReQuEsT\r\n")]
    public async Task ProcessAgentCard_WithWhitespacePaddedSkillHandler_EmitsSkillRequirement(string handlerName)
    {
        var adapter = CreateAdapter(CreateConfiguration(new Dictionary<string, string>
        {
            ["AgentApplication:A2A:AgentCard:SecuritySchemes:agentBearer:HttpAuthSecurityScheme:Scheme"] = "bearer",
            ["AgentApplication:UserAuthorization:AutoSignIn"] = "false",
            ["AgentApplication:UserAuthorization:Handlers:request:Type"] = "A2AUserAuthorization",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:SecuritySchemeName"] = "agentBearer",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:RequiredScopes:0"] = "api://agent/access_as_user",
        }));

        var card = await ProcessAgentCardAsync(adapter, CreateSkillAgent(handlerName));

        Assert.Null(card.SecurityRequirements);
        Assert.Equal(["api://agent/access_as_user"], Assert.Single(Assert.Single(card.Skills).SecurityRequirements).Schemes["agentBearer"].List);
    }

    [Fact]
    public async Task ProcessAgentCard_WithImplicitDefaultHandler_EmitsAgentRequirement()
    {
        var adapter = CreateAdapter(CreateConfiguration(new Dictionary<string, string>
        {
            ["AgentApplication:A2A:AgentCard:SecuritySchemes:agentBearer:HttpAuthSecurityScheme:Scheme"] = "bearer",
            ["AgentApplication:UserAuthorization:AutoSignIn"] = "true",
            ["AgentApplication:UserAuthorization:Handlers:request:Type"] = "A2AUserAuthorization",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:SecuritySchemeName"] = "agentBearer",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:RequiredScopes:0"] = "api://agent/access_as_user",
        }));

        var agentCard = await ProcessAgentCardAsync(adapter, new AgentApplication(new AgentApplicationOptions(_mockStorage.Object)));

        var requirement = Assert.Single(agentCard.SecurityRequirements);
        Assert.Equal(["api://agent/access_as_user"], requirement.Schemes["agentBearer"].List);
    }

    [Fact]
    public async Task ProcessAgentCard_WithCaseInsensitiveGlobalHandlerName_EmitsAgentRequirement()
    {
        var adapter = CreateAdapter(CreateConfiguration(new Dictionary<string, string>
        {
            ["AgentApplication:A2A:AgentCard:SecuritySchemes:agentBearer:HttpAuthSecurityScheme:Scheme"] = "bearer",
            ["AgentApplication:UserAuthorization:AutoSignIn"] = "true",
            ["AgentApplication:UserAuthorization:DefaultHandlerName"] = "request",
            ["AgentApplication:UserAuthorization:Handlers:Request:Type"] = "A2AUserAuthorization",
            ["AgentApplication:UserAuthorization:Handlers:Request:Settings:SecuritySchemeName"] = "agentBearer",
            ["AgentApplication:UserAuthorization:Handlers:Request:Settings:RequiredScopes:0"] = "api://agent/access_as_user",
        }));

        var agentCard = await ProcessAgentCardAsync(adapter, new AgentApplication(new AgentApplicationOptions(_mockStorage.Object)));

        var requirement = Assert.Single(agentCard.SecurityRequirements);
        Assert.Equal(["api://agent/access_as_user"], requirement.Schemes["agentBearer"].List);
    }

    [Fact]
    public async Task ProcessAgentCard_WithCaseInsensitiveSkillHandlerName_EmitsSkillRequirement()
    {
        var adapter = CreateAdapter(CreateConfiguration(new Dictionary<string, string>
        {
            ["AgentApplication:A2A:AgentCard:SecuritySchemes:agentBearer:HttpAuthSecurityScheme:Scheme"] = "bearer",
            ["AgentApplication:UserAuthorization:AutoSignIn"] = "false",
            ["AgentApplication:UserAuthorization:Handlers:Request:Type"] = "A2AUserAuthorization",
            ["AgentApplication:UserAuthorization:Handlers:Request:Settings:SecuritySchemeName"] = "agentBearer",
            ["AgentApplication:UserAuthorization:Handlers:Request:Settings:RequiredScopes:0"] = "api://agent/access_as_user",
        }));
        var agent = new AgentApplication(new AgentApplicationOptions(_mockStorage.Object));
        var extension = new A2AAgentExtension(agent);
        agent.RegisteredExtensions.Add(extension);
        extension.Skill("weather", skill => skill
            .WithName("Weather")
            .WithDescription("Gets weather.")
            .OnMessage((_, _, _) => Task.CompletedTask, autoSigninHandlers: ["request"]));

        var agentCard = await ProcessAgentCardAsync(adapter, agent);

        var skill = Assert.Single(agentCard.Skills);
        var requirement = Assert.Single(skill.SecurityRequirements);
        Assert.Equal(["api://agent/access_as_user"], requirement.Schemes["agentBearer"].List);
    }

    [Fact]
    public async Task ProcessAgentCard_WithProtectedSkill_EmitsSkillRequirement()
    {
        var adapter = CreateAdapter(CreateConfiguration(new Dictionary<string, string>
        {
            ["AgentApplication:A2A:AgentCard:SecuritySchemes:agentBearer:HttpAuthSecurityScheme:Scheme"] = "bearer",
            ["AgentApplication:UserAuthorization:Handlers:request:Type"] = "A2AUserAuthorization",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:SecuritySchemeName"] = "agentBearer",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:RequiredScopes:0"] = "api://agent/access_as_user",
        }));
        var agent = new AgentApplication(new AgentApplicationOptions(_mockStorage.Object));
        var extension = new A2AAgentExtension(agent);
        agent.RegisteredExtensions.Add(extension);
        extension.Skill("weather", skill => skill
            .WithName("Weather")
            .WithDescription("Gets weather.")
            .WithTags("weather")
            .OnMessage((_, _, _) => Task.CompletedTask, autoSigninHandlers: ["request"]));

        var agentCard = await ProcessAgentCardAsync(adapter, agent);

        var skill = Assert.Single(agentCard.Skills);
        var requirement = Assert.Single(skill.SecurityRequirements);
        Assert.Equal(["api://agent/access_as_user"], requirement.Schemes["agentBearer"].List);
    }

    [Fact]
    public async Task ProcessAgentCard_WithMultipleProtectedSkillHandlers_EmitsCombinedRequirement()
    {
        var adapter = CreateAdapter(CreateConfiguration(new Dictionary<string, string>
        {
            ["AgentApplication:A2A:AgentCard:SecuritySchemes:agentBearer:HttpAuthSecurityScheme:Scheme"] = "bearer",
            ["AgentApplication:A2A:AgentCard:SecuritySchemes:profileBearer:HttpAuthSecurityScheme:Scheme"] = "bearer",
            ["AgentApplication:UserAuthorization:AutoSignIn"] = "false",
            ["AgentApplication:UserAuthorization:Handlers:request:Type"] = "A2AUserAuthorization",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:SecuritySchemeName"] = "agentBearer",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:RequiredScopes:0"] = "api://agent/access_as_user",
            ["AgentApplication:UserAuthorization:Handlers:profile:Type"] = "A2AUserAuthorization",
            ["AgentApplication:UserAuthorization:Handlers:profile:Settings:SecuritySchemeName"] = "profileBearer",
            ["AgentApplication:UserAuthorization:Handlers:profile:Settings:RequiredScopes:0"] = "api://agent/profile",
        }));
        var agent = new AgentApplication(new AgentApplicationOptions(_mockStorage.Object));
        var extension = new A2AAgentExtension(agent);
        agent.RegisteredExtensions.Add(extension);
        extension.Skill("weather", skill => skill
            .WithName("Weather")
            .WithDescription("Gets weather.")
            .OnMessage((_, _, _) => Task.CompletedTask, autoSigninHandlers: ["request", "profile"]));

        var agentCard = await ProcessAgentCardAsync(adapter, agent);

        var skill = Assert.Single(agentCard.Skills);
        var requirement = Assert.Single(skill.SecurityRequirements);
        Assert.Equal(["api://agent/access_as_user"], requirement.Schemes["agentBearer"].List);
        Assert.Equal(["api://agent/profile"], requirement.Schemes["profileBearer"].List);
    }

    [Fact]
    public async Task ProcessAgentCard_WithHandlersSharingScheme_CombinesRequiredScopes()
    {
        var adapter = CreateAdapter(CreateConfiguration(new Dictionary<string, string>
        {
            ["AgentApplication:A2A:AgentCard:SecuritySchemes:agentBearer:HttpAuthSecurityScheme:Scheme"] = "bearer",
            ["AgentApplication:UserAuthorization:AutoSignIn"] = "false",
            ["AgentApplication:UserAuthorization:Handlers:request:Type"] = "A2AUserAuthorization",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:SecuritySchemeName"] = "agentBearer",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:RequiredScopes:0"] = "api://agent/access_as_user",
            ["AgentApplication:UserAuthorization:Handlers:profile:Type"] = "A2AUserAuthorization",
            ["AgentApplication:UserAuthorization:Handlers:profile:Settings:SecuritySchemeName"] = "agentBearer",
            ["AgentApplication:UserAuthorization:Handlers:profile:Settings:RequiredScopes:0"] = "api://agent/profile",
        }));
        var agent = new AgentApplication(new AgentApplicationOptions(_mockStorage.Object));
        var extension = new A2AAgentExtension(agent);
        agent.RegisteredExtensions.Add(extension);
        extension.Skill("weather", skill => skill
            .WithName("Weather")
            .WithDescription("Gets weather.")
            .OnMessage((_, _, _) => Task.CompletedTask, autoSigninHandlers: ["request", "profile"]));

        var agentCard = await ProcessAgentCardAsync(adapter, agent);

        var skill = Assert.Single(agentCard.Skills);
        var requirement = Assert.Single(skill.SecurityRequirements);
        Assert.Equal(["api://agent/access_as_user", "api://agent/profile"], requirement.Schemes["agentBearer"].List);
    }

    [Fact]
    public async Task ProcessAgentCard_WithGraphOBO_DoesNotEmitGraphScopes()
    {
        var adapter = CreateAdapter(CreateConfiguration(new Dictionary<string, string>
        {
            ["AgentApplication:A2A:AgentCard:SecuritySchemes:agentBearer:HttpAuthSecurityScheme:Scheme"] = "bearer",
            ["AgentApplication:UserAuthorization:AutoSignIn"] = "true",
            ["AgentApplication:UserAuthorization:DefaultHandlerName"] = "request",
            ["AgentApplication:UserAuthorization:Handlers:request:Type"] = "A2AUserAuthorization",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:SecuritySchemeName"] = "agentBearer",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:RequiredScopes:0"] = "api://agent/access_as_user",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:OBOConnectionName"] = "graphConnection",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:OBOScopes:0"] = "User.Read",
        }));

        var agentCard = await ProcessAgentCardAsync(adapter, new AgentApplication(new AgentApplicationOptions(_mockStorage.Object)));
        var json = ProtocolJsonSerializer.ToJson(agentCard);

        Assert.DoesNotContain("User.Read", json, StringComparison.Ordinal);
        Assert.DoesNotContain("graphConnection", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAgentCard_WithMissingSchemeReference_ThrowsMetadataError()
    {
        var adapter = CreateAdapter(CreateConfiguration(new Dictionary<string, string>
        {
            ["AgentApplication:UserAuthorization:AutoSignIn"] = "true",
            ["AgentApplication:UserAuthorization:DefaultHandlerName"] = "request",
            ["AgentApplication:UserAuthorization:Handlers:request:Type"] = "A2AUserAuthorization",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:SecuritySchemeName"] = "missing",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:RequiredScopes:0"] = "agent.read",
        }));
        var request = CreateMockHttpRequest("https", "localhost:3978");
        var response = CreateMockHttpResponse();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.ProcessAgentCardAsync(
            request.Object,
            response.Object,
            new AgentApplication(new AgentApplicationOptions(_mockStorage.Object)),
            "/a2a",
            CancellationToken.None));

        A2AErrorMetadataAssertions.AssertErrorMetadata(exception, -100016);
        Assert.Equal("A2A Agent Card requirement references missing security scheme 'missing'.", exception.Message);
    }

    [Fact]
    public void ApplySafeOptions_WithNullSecurityScheme_ThrowsMetadataError()
    {
        var options = new A2AAgentCardOptions();
        options.SecuritySchemes["empty"] = null;

        var exception = Assert.Throws<InvalidOperationException>(
            () => ApplySafeOptions(new A2AProtocolAgentCard(), options));

        A2AErrorMetadataAssertions.AssertErrorMetadata(exception, -100010);
        Assert.Equal("A2A Agent Card security scheme 'empty' cannot be null.", exception.Message);
    }

    [Fact]
    public async Task ProcessAgentCard_WithDuplicateConfiguredAndInlineScheme_ThrowsMetadataError()
    {
        var adapter = CreateAdapter(CreateConfiguration(new Dictionary<string, string>
        {
            ["AgentApplication:A2A:AgentCard:SecuritySchemes:deviceCode:HttpAuthSecurityScheme:Scheme"] = "bearer",
            ["AgentApplication:UserAuthorization:Handlers:request:Type"] = "A2AUserAuthorization",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:SecuritySchemeName"] = "deviceCode",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:OAuthFlows:DeviceCode:DeviceAuthorizationUrl"] = "https://login.example.com/devicecode",
            ["AgentApplication:UserAuthorization:Handlers:request:Settings:OAuthFlows:DeviceCode:TokenUrl"] = "https://login.example.com/token",
        }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ProcessAgentCardAsync(adapter, new AgentApplication(new AgentApplicationOptions(_mockStorage.Object))));

        A2AErrorMetadataAssertions.AssertErrorMetadata(exception, -100011);
        Assert.Equal("A2A Agent Card security scheme 'deviceCode' is configured more than once.", exception.Message);
    }

    [Fact]
    public async Task ProcessAgentCard_WithConflictingSkillRegistrations_ThrowsMetadataError()
    {
        var agent = new AgentApplication(new AgentApplicationOptions(_mockStorage.Object));
        var firstExtension = new A2AAgentExtension(agent);
        var secondExtension = new A2AAgentExtension(agent);
        agent.RegisteredExtensions.Add(firstExtension);
        agent.RegisteredExtensions.Add(secondExtension);
        firstExtension.Skill("weather", skill => skill
            .WithName("Weather")
            .WithDescription("Gets weather.")
            .OnMessage((_, _, _) => Task.CompletedTask));
        secondExtension.Skill("weather", skill => skill
            .WithName("Weather")
            .WithDescription("Gets different weather.")
            .OnMessage((_, _, _) => Task.CompletedTask));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ProcessAgentCardAsync(CreateAdapter(CreateConfiguration(new Dictionary<string, string>())), agent));

        A2AErrorMetadataAssertions.AssertErrorMetadata(exception, -100012);
        Assert.Equal("A2A skill 'weather' has conflicting registrations.", exception.Message);
    }

    [Fact]
    public async Task ProcessAgentCard_WithProtectedConfiguredProperty_ThrowsMetadataError()
    {
        var adapter = CreateAdapter(CreateConfiguration(new Dictionary<string, string>
        {
            ["AgentApplication:A2A:AgentCard:Version"] = "1.0.0",
        }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ProcessAgentCardAsync(adapter, new AgentApplication(new AgentApplicationOptions(_mockStorage.Object))));

        A2AErrorMetadataAssertions.AssertErrorMetadata(exception, -100015);
        Assert.Equal("A2A Agent Card configuration cannot set protected property 'Version'.", exception.Message);
    }

    [Fact]
    public async Task ProcessAgentCard_WithUndefinedOAuthScope_ThrowsMetadataError()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ProcessAgentCardAsync(
                CreateAdapter(CreateConfiguration(new Dictionary<string, string>())),
                new TestAgentApplicationWithInvalidSecurityRequirement(new AgentApplicationOptions(_mockStorage.Object))));

        A2AErrorMetadataAssertions.AssertErrorMetadata(exception, -100017);
        Assert.Equal(
            "A2A Agent Card requirement for scheme 'deviceCode' includes a scope that the scheme does not define.",
            exception.Message);
    }

    [Fact]
    public async Task ProcessAgentCard_WithHandlerOverride_PreservesFinalCustomization()
    {
        var adapter = CreateAdapter(CreateConfiguration(new Dictionary<string, string>
        {
            ["AgentApplication:A2A:AgentCard:Name"] = "Configured agent",
            ["AgentApplication:A2A:AgentCard:SecuritySchemes:agentBearer:HttpAuthSecurityScheme:Scheme"] = "bearer",
        }));
        var agent = new TestAgentApplicationWithCardHandler(new AgentApplicationOptions(_mockStorage.Object));

        var agentCard = await ProcessAgentCardAsync(adapter, agent);

        Assert.True(agent.CardHandlerCalled);
        Assert.Equal("Customized agent", agentCard.Name);
        Assert.True(agentCard.SecuritySchemes.ContainsKey("handlerBearer"));
    }

    [Fact]
    public async Task ProcessAgentCardAsync_ShouldReturnDefaultAgentCard()
    {
        // Arrange
        var adapter = new A2AAdapter(_mockTaskStore.Object, _mockLogger);
        var mockHttpRequest = CreateMockHttpRequest("https", "localhost:3978");
        var mockHttpResponse = CreateMockHttpResponse();
        var mockAgent = new Mock<IAgent>();

        // Act
        await adapter.ProcessAgentCardAsync(mockHttpRequest.Object, mockHttpResponse.Object, mockAgent.Object, "/a2a", CancellationToken.None);

        // Assert
        mockHttpResponse.VerifySet(r => r.ContentType = "application/json", Times.Once);
        mockHttpResponse.Object.Body.Position = 0;
        var agentCard = await JsonSerializer.DeserializeAsync<A2AProtocolAgentCard>(
            mockHttpResponse.Object.Body, A2AJsonUtilities.DefaultOptions);
        Assert.False(agentCard!.Capabilities.ExtendedAgentCard);
        Assert.Null(agentCard.SecurityRequirements);
    }

    [Fact]
    public async Task ProcessAgentCardAsync_ShouldIncludeCacheControlMaxAge()
    {
        var adapter = new A2AAdapter(_mockTaskStore.Object, _mockLogger);
        var mockHttpRequest = CreateMockHttpRequest("https", "localhost:3978");
        var mockHttpResponse = CreateMockHttpResponse();

        await adapter.ProcessAgentCardAsync(mockHttpRequest.Object, mockHttpResponse.Object, new Mock<IAgent>().Object, "/a2a", CancellationToken.None);

        Assert.Equal("public, max-age=3600", mockHttpResponse.Object.Headers.CacheControl);
    }

    [Fact]
    public async Task ProcessAgentCardAsync_WithOptions_ShouldUseConfiguredCacheMaxAge()
    {
        var adapter = new A2AAdapter(
            _mockTaskStore.Object,
            _mockLogger,
            options: new A2AAdapterOptions { AgentCardCacheMaxAge = TimeSpan.FromMinutes(15) });
        var mockHttpRequest = CreateMockHttpRequest("https", "localhost:3978");
        var mockHttpResponse = CreateMockHttpResponse();

        await adapter.ProcessAgentCardAsync(mockHttpRequest.Object, mockHttpResponse.Object, new Mock<IAgent>().Object, "/a2a", CancellationToken.None);

        Assert.Equal("public, max-age=900", mockHttpResponse.Object.Headers.CacheControl);
    }

    [Fact]
    public async Task ProcessAgentCardAsync_WithConfiguration_ShouldUseConfiguredCacheMaxAge()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["A2AAdapterOptions:AgentCardCacheMaxAge"] = "00:10:00",
            })
            .Build();
        var adapter = new A2AAdapter(_mockTaskStore.Object, _mockLogger, configuration: configuration);
        var mockHttpRequest = CreateMockHttpRequest("https", "localhost:3978");
        var mockHttpResponse = CreateMockHttpResponse();

        await adapter.ProcessAgentCardAsync(mockHttpRequest.Object, mockHttpResponse.Object, new Mock<IAgent>().Object, "/a2a", CancellationToken.None);

        Assert.Equal("public, max-age=600", mockHttpResponse.Object.Headers.CacheControl);
    }

    [Fact]
    public void Constructor_WithNegativeAgentCardCacheMaxAge_ShouldThrow()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new A2AAdapter(
            _mockTaskStore.Object,
            _mockLogger,
            options: new A2AAdapterOptions { AgentCardCacheMaxAge = TimeSpan.FromSeconds(-1) }));

        Assert.Equal("options", exception.ParamName);
    }

    [Fact]
    public async Task ProcessAgentCardAsync_ShouldIncludeBodyDerivedETag()
    {
        var adapter = new A2AAdapter(_mockTaskStore.Object, _mockLogger);
        var mockHttpRequest = CreateMockHttpRequest("https", "localhost:3978");
        var defaultResponse = CreateMockHttpResponse();
        var attributedResponse = CreateMockHttpResponse();

        await adapter.ProcessAgentCardAsync(mockHttpRequest.Object, defaultResponse.Object, new Mock<IAgent>().Object, "/a2a", CancellationToken.None);
        await adapter.ProcessAgentCardAsync(mockHttpRequest.Object, attributedResponse.Object, new TestAgentWithAttributes(), "/a2a", CancellationToken.None);

        Assert.False(string.IsNullOrEmpty(defaultResponse.Object.Headers.ETag));
        Assert.NotEqual(defaultResponse.Object.Headers.ETag, attributedResponse.Object.Headers.ETag);
        Assert.Equal(
            $"\"{Convert.ToHexString(SHA256.HashData(((MemoryStream)defaultResponse.Object.Body).ToArray()))}\"",
            defaultResponse.Object.Headers.ETag);
    }

    [Fact]
    public async Task ProcessAgentCardAsync_ShouldIncludeStableLastModified()
    {
        var adapter = new A2AAdapter(_mockTaskStore.Object, _mockLogger);
        var mockHttpRequest = CreateMockHttpRequest("https", "localhost:3978");
        var firstResponse = CreateMockHttpResponse();
        var secondResponse = CreateMockHttpResponse();
        var mockAgent = new Mock<IAgent>();

        await adapter.ProcessAgentCardAsync(mockHttpRequest.Object, firstResponse.Object, mockAgent.Object, "/a2a", CancellationToken.None);
        await adapter.ProcessAgentCardAsync(mockHttpRequest.Object, secondResponse.Object, mockAgent.Object, "/a2a", CancellationToken.None);

        Assert.Equal(firstResponse.Object.Headers.LastModified, secondResponse.Object.Headers.LastModified);
        Assert.True(DateTimeOffset.TryParseExact(
            firstResponse.Object.Headers.LastModified,
            "R",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out _));
    }

    [Fact]
    public async Task ProcessAgentCardAsync_WithAgentAttribute_ShouldUseAttributeValues()
    {
        // Arrange
        var adapter = new A2AAdapter(_mockTaskStore.Object, _mockLogger);
        var mockHttpRequest = CreateMockHttpRequest("https", "localhost:3978");
        var mockHttpResponse = CreateMockHttpResponse();
        var agent = new TestAgentWithAttributes();

        // Act
        await adapter.ProcessAgentCardAsync(mockHttpRequest.Object, mockHttpResponse.Object, agent, "/a2a", CancellationToken.None);

        // Assert
        mockHttpResponse.VerifySet(r => r.ContentType = "application/json", Times.Once);
    }

    [Fact]
    public async Task ProcessAgentCardAsync_WithAgentCardHandler_ShouldCallHandler()
    {
        // Arrange
        var adapter = new A2AAdapter(_mockTaskStore.Object, _mockLogger);
        var mockHttpRequest = CreateMockHttpRequest("https", "localhost:3978");
        var mockHttpResponse = CreateMockHttpResponse();
        var agent = new TestAgentWithCardHandler();

        // Act
        await adapter.ProcessAgentCardAsync(mockHttpRequest.Object, mockHttpResponse.Object, agent, "/a2a", CancellationToken.None);

        // Assert
        Assert.True(agent.CardHandlerCalled);
        mockHttpResponse.VerifySet(r => r.ContentType = "application/json", Times.Once);
    }

    [Fact]
    public async Task ProcessAgentCardAsync_WithSkills_ShouldIncludeSkills()
    {
        // Arrange
        var adapter = new A2AAdapter(_mockTaskStore.Object, _mockLogger);
        var mockHttpRequest = CreateMockHttpRequest("https", "localhost:3978");
        var mockHttpResponse = CreateMockHttpResponse();
        var agent = new TestAgentWithSkills();

        // Act
        await adapter.ProcessAgentCardAsync(mockHttpRequest.Object, mockHttpResponse.Object, agent, "/a2a", CancellationToken.None);

        // Assert
        mockHttpResponse.VerifySet(r => r.ContentType = "application/json", Times.Once);
    }

    #endregion

    #region

    [Fact]
    public async Task ProcessJsonRpcMessageSendAsync()
    {
        var record = UseRecord(record =>
        {
            var options = new TestApplicationOptions(record.Storage);
            var agent = new TestApplication(options);
            agent.OnActivity(ActivityTypes.Message, async (context, state, ct) =>
            {
                await context.SendActivityAsync($"Echo: {context.Activity.Text}", cancellationToken: ct);
            });
            return agent;
        });

        var jsonRpcRequest = CreateSendMessageRequest("context-1234");
        var context = CreateHttpContext(JsonSerializer.Serialize(jsonRpcRequest));

        var result = await record.Adapter.ProcessJsonRpcAsync(context.Request, context.Response, record.Agent, CancellationToken.None);
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        var task = ReadTaskResponse(context);
        Assert.Equal("context-1234", task.ContextId);
        Assert.NotEmpty(task.Id);
        Assert.NotNull(task.Status.Message);
        Assert.Equal("Echo: Hello", task.Status.Message.Parts[0].Text);
    }

    [Fact]
    public async Task ProcessJsonRpcMessageSendAsync_WithContentFreeEndOfConversation_CompletesTask()
    {
        var record = UseRecord(record =>
        {
            var options = new TestApplicationOptions(record.Storage);
            var agent = new TestApplication(options);
            agent.OnActivity(ActivityTypes.Message, async (context, state, ct) =>
            {
                await context.SendActivityAsync(
                    new Activity { Type = ActivityTypes.EndOfConversation },
                    cancellationToken: ct);
            });
            return agent;
        });

        var context = CreateHttpContext(JsonSerializer.Serialize(CreateSendMessageRequest("context-eoc")));

        var result = await record.Adapter.ProcessJsonRpcAsync(context.Request, context.Response, record.Agent, CancellationToken.None);
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        var task = ReadTaskResponse(context);
        Assert.Equal(TaskState.Completed, task.Status.State);
        Assert.Null(task.Status.Message);
    }

    [Fact]
    public async Task ProcessJsonRpcMessageSendAsync_WithAutoSignIn_ExposesRequestToken()
    {
        var connections = Mock.Of<IConnections>();
        var record = UseRecord(record =>
        {
            var options = new TestApplicationOptions(record.Storage)
            {
                UserAuthorization = new UserAuthorizationOptions(
                    NullLoggerFactory.Instance,
                    record.Storage,
                    connections,
                    new A2AUserAuthorization("request", connections, new OBOSettings()))
                {
                    DefaultHandlerName = "request",
                    AutoSignIn = UserAuthorizationOptions.AutoSignInOnForAny
                }
            };
            var agent = new TestApplication(options);
            agent.OnActivity(ActivityTypes.Message, async (context, state, ct) =>
            {
                var token = await agent.UserAuthorization.GetTurnTokenAsync(context, "request", ct);
                await context.SendActivityAsync($"Token: {token}", cancellationToken: ct);
            });
            return agent;
        });

        var context = CreateHttpContext(JsonSerializer.Serialize(CreateSendMessageRequest("context-oauth")));
        AuthenticateContext(context, "opaque-token");

        var result = await record.Adapter.ProcessJsonRpcAsync(context.Request, context.Response, record.Agent, CancellationToken.None);
        await result.ExecuteAsync(context);

        Assert.Equal("Token: opaque-token", ReadTaskResponse(context).Status.Message.Parts[0].Text);
    }

    [Fact]
    public async Task ProcessJsonRpcMessageStreamAsync()
    {
        // Arrange
        var record = UseRecord((record) =>
        {
            var options = new TestApplicationOptions(record.Storage);
            var agent = new TestApplication(options);

            agent.OnActivity(ActivityTypes.Message, async (context, state, ct) =>
            {
                await context.SendActivityAsync($"Echo: {context.Activity.Text}", cancellationToken: ct);
            });

            return agent;
        });

        var jsonRpcRequest = new JsonRpcRequest
        {
            Id = Guid.NewGuid().ToString(),
            Method = A2AMethods.SendStreamingMessage,
            Params = JsonSerializer.SerializeToElement(new SendMessageRequest
            {
                Message = new Message
                {
                    ContextId = "context-1234",
                    Parts = [new Part() { Text = "Hello" }]
                },
                Configuration = new SendMessageConfiguration
                {
                    HistoryLength = 10,
                }
            })
        };

        var context = CreateHttpContext(JsonSerializer.Serialize(jsonRpcRequest));

        // Act

        var result = await record.Adapter.ProcessJsonRpcAsync(context.Request, context.Response, record.Agent, CancellationToken.None);
        await result.ExecuteAsync(context);

        // Assert

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var reader = new StreamReader(context.Response.Body);
        var streamText = reader.ReadToEnd();
        Assert.NotEmpty(streamText);
        Assert.Contains("Echo: Hello", streamText);
    }

    [Fact]
    public async Task ProcessJsonRpcGetExtendedAgentCardAsync_WhenUnsupported_ReturnsUnsupportedOperation()
    {
        // Arrange
        var record = UseRecord(record =>
        {
            var options = new TestApplicationOptions(record.Storage);
            return new TestApplication(options);
        });

        var jsonRpcRequest = new JsonRpcRequest
        {
            Id = Guid.NewGuid().ToString(),
            Method = A2AMethods.GetExtendedAgentCard,
            Params = JsonSerializer.SerializeToElement(new GetExtendedAgentCardRequest())
        };

        var context = CreateHttpContext(JsonSerializer.Serialize(jsonRpcRequest));

        // Act
        var result = await record.Adapter.ProcessJsonRpcAsync(context.Request, context.Response, record.Agent, CancellationToken.None);
        await result.ExecuteAsync(context);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var response = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal((int)A2AErrorCode.UnsupportedOperation, response.RootElement.GetProperty("error").GetProperty("code").GetInt32());
    }

    #endregion

    #region Helper Methods

    private A2AAdapter CreateAdapter(IConfiguration configuration)
    {
        return new A2AAdapter(_mockTaskStore.Object, _mockLogger, configuration: configuration);
    }

    private static IConfiguration CreateConfiguration(IDictionary<string, string> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private AgentApplication CreateSkillAgent(params string[] handlerNames)
    {
        var agent = new AgentApplication(new AgentApplicationOptions(_mockStorage.Object));
        var extension = new A2AAgentExtension(agent);
        agent.RegisteredExtensions.Add(extension);
        extension.Skill("weather", skill => skill
            .WithName("Weather")
            .WithDescription("Gets weather.")
            .OnMessage((_, _, _) => Task.CompletedTask, autoSigninHandlers: handlerNames));
        return agent;
    }

    private static void ApplySafeOptions(A2AProtocolAgentCard agentCard, A2AAgentCardOptions options)
    {
        var method = typeof(A2AAgentCardComposer).GetMethod(
            "ApplySafeOptions",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        try
        {
            method.Invoke(null, [agentCard, options]);
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
        }
    }

    private async Task<A2AProtocolAgentCard> ProcessAgentCardAsync(A2AAdapter adapter, IAgent agent)
    {
        var request = CreateMockHttpRequest("https", "localhost:3978");
        var response = CreateMockHttpResponse();

        await adapter.ProcessAgentCardAsync(request.Object, response.Object, agent, "/a2a", CancellationToken.None);

        response.Object.Body.Position = 0;
        return (await JsonSerializer.DeserializeAsync<A2AProtocolAgentCard>(response.Object.Body, A2AJsonUtilities.DefaultOptions))!;
    }

    private Mock<HttpRequest> CreateMockHttpRequest(string scheme = "https", string host = "localhost:3978")
    {
        var mockRequest = new Mock<HttpRequest>();
        mockRequest.Setup(r => r.Scheme).Returns(scheme);
        mockRequest.Setup(r => r.Host).Returns(new HostString(host));
        mockRequest.Setup(r => r.Headers).Returns(new HeaderDictionary());
        mockRequest.Setup(r => r.Body).Returns(new MemoryStream());
        return mockRequest;
    }

    private Mock<HttpResponse> CreateMockHttpResponse()
    {
        var mockResponse = new Mock<HttpResponse>();
        var memoryStream = new MemoryStream();
        mockResponse.SetupProperty(r => r.ContentType);
        mockResponse.Setup(r => r.Headers).Returns(new HeaderDictionary());
        mockResponse.Setup(r => r.Body).Returns(memoryStream);
        return mockResponse;
    }

    #endregion

    #region Test Helper Classes

    [Agent(name: "TestAgent", description: "A test agent", version: "1.0.0")]
    private class TestAgentWithAttributes : IAgent
    {
        public Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    [Agent(name: "SkillAgent")]
    private class TestAgentWithSkills : IAgent
    {
        public Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private class TestAgentWithCardHandler : IAgent, IAgentCardHandler
    {
        public bool CardHandlerCalled { get; private set; }

        public Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<A2AProtocolAgentCard> GetAgentCard(A2AProtocolAgentCard defaultCard)
        {
            CardHandlerCalled = true;
            defaultCard.Name = "Custom Agent";
            return Task.FromResult(defaultCard);
        }
    }

    private sealed class TestAgentApplicationWithCardHandler : AgentApplication, IAgentCardHandler
    {
        public TestAgentApplicationWithCardHandler(AgentApplicationOptions options)
            : base(options)
        {
        }

        public bool CardHandlerCalled { get; private set; }

        public Task<A2AProtocolAgentCard> GetAgentCard(A2AProtocolAgentCard defaultCard)
        {
            CardHandlerCalled = true;
            defaultCard.Name = "Customized agent";
            defaultCard.SecuritySchemes["handlerBearer"] = new SecurityScheme
            {
                HttpAuthSecurityScheme = new HttpAuthSecurityScheme { Scheme = "bearer" },
            };
            return Task.FromResult(defaultCard);
        }
    }

    private sealed class TestAgentApplicationWithInvalidSecurityRequirement : AgentApplication, IAgentCardHandler
    {
        public TestAgentApplicationWithInvalidSecurityRequirement(AgentApplicationOptions options)
            : base(options)
        {
        }

        public Task<A2AProtocolAgentCard> GetAgentCard(A2AProtocolAgentCard defaultCard)
        {
            defaultCard.SecuritySchemes["deviceCode"] = new SecurityScheme
            {
                OAuth2SecurityScheme = new OAuth2SecurityScheme
                {
                    Flows = new OAuthFlows
                    {
                        DeviceCode = new DeviceCodeOAuthFlow
                        {
                            DeviceAuthorizationUrl = "https://login.example.com/devicecode",
                            TokenUrl = "https://login.example.com/token",
                            Scopes = new Dictionary<string, string>
                            {
                                ["agent.read"] = "Read the agent",
                            },
                        },
                    },
                },
            };
            defaultCard.SecurityRequirements =
            [
                new SecurityRequirement
                {
                    Schemes = new Dictionary<string, StringList>
                    {
                        ["deviceCode"] = new StringList { List = ["agent.write"] },
                    },
                },
            ];
            return Task.FromResult(defaultCard);
        }
    }

    #endregion

    private static JsonRpcRequest CreateSendMessageRequest(string contextId)
    {
        return new JsonRpcRequest
        {
            Id = Guid.NewGuid().ToString(),
            Method = A2AMethods.SendMessage,
            Params = JsonSerializer.SerializeToElement(new SendMessageRequest
            {
                Message = new Message
                {
                    ContextId = contextId,
                    Parts = [new Part() { Text = "Hello" }]
                }
            })
        };
    }

    private static AgentTask ReadTaskResponse(DefaultHttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var response = ProtocolJsonSerializer.ToObject<JsonRpcResponse>(new StreamReader(context.Response.Body).ReadToEnd());
        return ProtocolJsonSerializer.ToObject<AgentTask>(response.Result.AsObject().GetAt(0).Value);
    }

    private static DefaultHttpContext CreateHttpContext(string requestContent = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(requestContent));
        context.Request.Method = HttpMethods.Post;
        context.Response.StatusCode = 0;
        context.Response.Body = new MemoryStream();
        return context;
    }

    /// <summary>
    /// Applies the authentication result an ASP.NET Core handler with <c>SaveToken = true</c> produces:
    /// an authenticated principal plus a ticket carrying the validated access token.
    /// </summary>
    private static void AuthenticateContext(DefaultHttpContext context, string validatedToken)
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "caller")], authenticationType: "Test"));
        var properties = new AuthenticationProperties();
        properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = validatedToken }]);

        context.User = principal;
        context.Features.Set<IAuthenticateResultFeature>(new StubAuthenticateResultFeature
        {
            AuthenticateResult = AuthenticateResult.Success(
                new AuthenticationTicket(principal, properties, "Test")),
        });
    }

    private sealed class StubAuthenticateResultFeature : IAuthenticateResultFeature
    {
        public AuthenticateResult AuthenticateResult { get; set; }
    }

    private static Record UseRecord(Func<Record, IAgent> createAgent)
    {
        var storage = new MemoryStorage();
        var adapter = new A2AAdapter(storage, NullLoggerFactory.Instance);
        var record = new Record(storage, adapter, null);

        if (createAgent != null)
        {
            record.Agent = createAgent(record);
        }

        return record;
    }

    private record Record(
        IStorage Storage,
        A2AAdapter Adapter,
        IAgent Agent)
    {

        public IAgent Agent { get; set; } = Agent;
        public IStorage Storage { get; set; } = Storage;
    }

}