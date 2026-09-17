extern alias A2AAgentSample;

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.A2A;
using Microsoft.Agents.Extensions.A2A.Authorization;
using Microsoft.Agents.Extensions.A2A.Pipeline;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.IO;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using A2AAgentSample::A2AAgent;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class A2AAgentOAuthRouteTests
{
    private const string GraphHandlerName = "graph";
    private const string GitHubHandlerName = "github";

    [Fact]
    public async Task AgentCard_FromSampleConfiguration_AdvertisesOnlyMeAndIssuesSkills()
    {
        AgentCard card = await LoadCardFromSampleConfigurationAsync();

        Assert.Equal(
            ["GitHub assigned issues", "Microsoft Graph profile"],
            card.Skills!.Select(skill => skill.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task AgentCard_FromSampleConfiguration_AdvertisesTwoSchemes_AndEachSkillUsesOnlyItsOwnScope()
    {
        AgentCard card = await LoadCardFromSampleConfigurationAsync();

        Assert.NotNull(card.SecuritySchemes);
        Assert.Equal("https://login.microsoftonline.com/organizations/oauth2/v2.0/token", card.SecuritySchemes["delegated"].OAuth2SecurityScheme!.Flows!.DeviceCode!.TokenUrl);
        Assert.Equal("https://github.com/login/oauth/access_token", card.SecuritySchemes["github"].OAuth2SecurityScheme!.Flows!.DeviceCode!.TokenUrl);
        AssertSkillRequirement(card, "Microsoft Graph profile", "delegated", "api://22222222-2222-2222-2222-222222222222/access_as_user");
        AssertSkillRequirement(card, "GitHub assigned issues", "github", "repo");
    }

    [Fact]
    public async Task SampleConfiguration_UsesOnlyRedactedTwoProviderPlaceholders()
    {
        string settings = await LoadSampleConfigurationTextAsync();

        Assert.Contains("\"TenantId\": \"<tenant-id>\"", settings, StringComparison.Ordinal);
        Assert.Contains("\"api://<agent-client-id>/access_as_user\"", settings, StringComparison.Ordinal);
        Assert.Contains("A2A Agent API app registration client ID", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("{{TenantId}}", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("{{ClientId}}", settings, StringComparison.Ordinal);
    }

    private static void AssertSkillRequirement(AgentCard card, string skillId, string schemeName, string scope)
    {
        AgentSkill skill = Assert.Single(card.Skills, candidate => candidate.Id == skillId);
        Assert.NotNull(skill.SecurityRequirements);
        SecurityRequirement requirement = Assert.Single(skill.SecurityRequirements);
        Assert.NotNull(requirement.Schemes);
        Assert.Equal([scope], requirement.Schemes[schemeName].List);
    }

    [Fact]
    public void GraphSkill_DeclaresAutoSignInHandlerAndNoDuplicateRoute()
    {
        MethodInfo method = typeof(MyAgent).GetMethod("OnGraphAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        A2ASkillAttribute skill = Assert.Single(method.GetCustomAttributes<A2ASkillAttribute>());

        Assert.Equal([GraphHandlerName], skill.AutoSignInHandlers);
        Assert.Empty(method.GetCustomAttributes<A2AMessageRouteAttribute>());
    }

    [Fact]
    public void GitHubSkill_DeclaresAutoSignInHandlerAndNoDuplicateRoute()
    {
        MethodInfo method = typeof(MyAgent).GetMethod("OnGitHubIssuesAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        A2ASkillAttribute skill = Assert.Single(method.GetCustomAttributes<A2ASkillAttribute>());

        Assert.Equal([GitHubHandlerName], skill.AutoSignInHandlers);
        Assert.Empty(method.GetCustomAttributes<A2AMessageRouteAttribute>());
    }

    [Fact]
    public async Task GraphRoute_UsesMailThenFallsBackToUserPrincipalName()
    {
        var graph = CreateAuthorizationHandler(GraphHandlerName, "graph-token");
        var github = CreateAuthorizationHandler(GitHubHandlerName, "github-token");
        var graphClient = new Mock<IGraphProfileClient>(MockBehavior.Strict);
        graphClient
            .SetupSequence(client => client.GetMeAsync("graph-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphProfile("Ada Lovelace", "ada@example.com", "ada@contoso.com"))
            .ReturnsAsync(new GraphProfile("Grace Hopper", null, "grace@contoso.com"));

        var record = CreateRecord(graph.Object, github.Object, graphClient.Object, Mock.Of<IGitHubIssuesClient>());

        AgentTask first = ReadTaskResponse(await ExecuteMessageAsync(record, "-me", CreateDelegatedIdentity()));
        AgentTask second = ReadTaskResponse(await ExecuteMessageAsync(record, "-me", CreateDelegatedIdentity()));

        Assert.Contains("Email: ada@example.com", first.Status.Message!.Parts[0].Text, StringComparison.Ordinal);
        Assert.Contains("Email: grace@contoso.com", second.Status.Message!.Parts[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GitHubRoute_UsesGitHubHandlerToken_AndNotGraphToken()
    {
        var graph = CreateAuthorizationHandler(GraphHandlerName, "graph-token");
        var github = CreateAuthorizationHandler(GitHubHandlerName, "github-token");
        var issuesClient = new Mock<IGitHubIssuesClient>(MockBehavior.Strict);
        issuesClient
            .Setup(client => client.GetAssignedIssuesSummaryAsync("github-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync("- octo/repo#17 Harden scopes");

        var record = CreateRecord(graph.Object, github.Object, Mock.Of<IGraphProfileClient>(), issuesClient.Object);
        AgentTask task = ReadTaskResponse(await ExecuteMessageAsync(record, "-issues", CreateDelegatedIdentity()));

        github.Verify(handler => handler.SignInUserAsync(It.IsAny<ITurnContext>(), true, It.IsAny<string>(), It.IsAny<System.Collections.Generic.IList<string>>(), It.IsAny<CancellationToken>()), Times.Once);
        graph.Verify(handler => handler.SignInUserAsync(It.IsAny<ITurnContext>(), true, It.IsAny<string>(), It.IsAny<System.Collections.Generic.IList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains("octo/repo#17 Harden scopes", task.Status.Message!.Parts[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GraphRoute_DoesNotDependOnSampleTokenIdentityHelper()
    {
        Assert.Null(typeof(MyAgent).Assembly.GetType("A2AAgent.A2ATokenIdentity"));
    }

    [Fact]
    public async Task GraphRoute_UsesHandlerConfiguredScopeWithoutRouteSpecificClaimCode()
    {
        string delegatedToken = CreateDelegatedToken("custom_scope");
        var authorization = new A2AUserAuthorization(
            GraphHandlerName,
            Mock.Of<IConnections>(),
            new A2AUserAuthorizationSettings
            {
                EnforceRequiredScopes = true,
                RequiredScopes = ["api://agent/custom_scope"],
            });
        var graphClient = new Mock<IGraphProfileClient>(MockBehavior.Strict);
        graphClient
            .Setup(client => client.GetMeAsync(
                delegatedToken,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphProfile("Ada Lovelace", "ada@example.com"));
        var github = CreateAuthorizationHandler(GitHubHandlerName, "github-token");

        DefaultHttpContext context = await ExecuteAuthenticatedMessageAsync(
            CreateRecord(authorization, github.Object, graphClient.Object, Mock.Of<IGitHubIssuesClient>()),
            "-me",
            delegatedToken);

        AgentTask task = ReadTaskResponse(context);
        Assert.Contains("Ada Lovelace", task.Status.Message!.Parts[0].Text);
    }

    [Fact]
    public async Task GraphRoute_HandlerRejectsMissingConfiguredScopeBeforeCallingGraph()
    {
        string delegatedToken = CreateDelegatedToken("other_scope");
        var authorization = new A2AUserAuthorization(
            GraphHandlerName,
            Mock.Of<IConnections>(),
            new A2AUserAuthorizationSettings
            {
                EnforceRequiredScopes = true,
                RequiredScopes = ["api://agent/custom_scope"],
            });
        var graphClient = new Mock<IGraphProfileClient>(MockBehavior.Strict);
        var github = CreateAuthorizationHandler(GitHubHandlerName, "github-token");

        DefaultHttpContext context = await ExecuteAuthenticatedMessageAsync(
            CreateRecord(authorization, github.Object, graphClient.Object, Mock.Of<IGitHubIssuesClient>()),
            "-me",
            delegatedToken);

        Assert.Contains(
            "custom_scope",
            ReadResponseText(context),
            StringComparison.Ordinal);
        graphClient.VerifyNoOtherCalls();
    }

    private static Record CreateRecord(
        IUserAuthorization graph,
        IUserAuthorization github,
        IGraphProfileClient graphClient,
        IGitHubIssuesClient gitHubIssuesClient)
    {
        var storage = new MemoryStorage();
        var options = new AgentApplicationOptions(storage)
        {
            UserAuthorization = new UserAuthorizationOptions(
                NullLoggerFactory.Instance,
                storage,
                Mock.Of<IConnections>(),
                graph,
                github)
            {
                DefaultHandlerName = GraphHandlerName,
                AutoSignIn = UserAuthorizationOptions.AutoSignInOff
            }
        };

        return new Record(
            new A2AAdapter(storage, NullLoggerFactory.Instance),
            new MyAgent(options, graphClient, gitHubIssuesClient));
    }

    private static async Task<AgentCard> LoadCardFromSampleConfigurationAsync()
    {
        const string tenantId = "11111111-1111-1111-1111-111111111111";
        const string clientId = "22222222-2222-2222-2222-222222222222";
        string settings = await LoadSampleConfigurationTextAsync();
        settings = settings.Replace("{{TenantId}}", tenantId, StringComparison.Ordinal)
            .Replace("{{ClientId}}", clientId, StringComparison.Ordinal)
            .Replace("<tenant-id>", tenantId, StringComparison.Ordinal)
            .Replace("<agent-client-id>", clientId, StringComparison.Ordinal);

        using var settingsStream = new MemoryStream(Encoding.UTF8.GetBytes(settings));
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonStream(settingsStream)
            .Build();
        var storage = new MemoryStorage();
        var adapter = new A2AAdapter(storage, NullLoggerFactory.Instance, configuration: configuration);
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("agent.example");
        context.Response.Body = new MemoryStream();

        await adapter.ProcessAgentCardAsync(context.Request, context.Response, new MyAgent(new AgentApplicationOptions(storage), Mock.Of<IGraphProfileClient>(), Mock.Of<IGitHubIssuesClient>()), "/a2a", CancellationToken.None);
        context.Response.Body.Position = 0;
        return (await JsonSerializer.DeserializeAsync<AgentCard>(context.Response.Body, A2AJsonUtilities.DefaultOptions))!;
    }

    private static Task<string> LoadSampleConfigurationTextAsync()
        => File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "A2AAgent.appsettings.json"));
    private static Mock<IUserAuthorization> CreateAuthorizationHandler(string name, string token)
    {
        var response = new TokenResponse
        {
            Token = token,
            Expiration = DateTimeOffset.UtcNow.AddHours(1),
            IsExchangeable = false
        };

        var handler = new Mock<IUserAuthorization>(MockBehavior.Strict);
        handler.SetupGet(value => value.Name).Returns(name);
        handler.Setup(value => value.SignInUserAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<System.Collections.Generic.IList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        handler.Setup(value => value.GetRefreshedUserTokenAsync(
                It.IsAny<ITurnContext>(),
                It.IsAny<string>(),
                It.IsAny<System.Collections.Generic.IList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        handler.Setup(value => value.SignOutUserAsync(It.IsAny<ITurnContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        handler.Setup(value => value.ResetStateAsync(It.IsAny<ITurnContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return handler;
    }

    private static async Task<DefaultHttpContext> ExecuteMessageAsync(Record record, string text, ClaimsIdentity identity)
    {
        var context = CreateHttpContext(text, identity);
        var result = await record.Adapter.ProcessJsonRpcAsync(context.Request, context.Response, record.Agent, CancellationToken.None);
        await result.ExecuteAsync(context);
        return context;
    }

    private static async Task<DefaultHttpContext> ExecuteAuthenticatedMessageAsync(
        Record record,
        string text,
        string token)
    {
        ClaimsIdentity identity = new(
            new JwtSecurityTokenHandler().ReadJwtToken(token).Claims,
            authenticationType: "Bearer");
        var context = CreateHttpContext(text, identity, token);
        var result = await record.Adapter.ProcessJsonRpcAsync(
            context.Request,
            context.Response,
            record.Agent,
            CancellationToken.None);
        await result.ExecuteAsync(context);
        return context;
    }

    private static DefaultHttpContext CreateHttpContext(
        string text,
        ClaimsIdentity identity,
        string? validatedToken = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(CreateSendMessageRequest(text))));
        context.Request.Method = HttpMethods.Post;
        context.User = new ClaimsPrincipal(identity);
        context.Response.Body = new MemoryStream();

        if (validatedToken != null)
        {
            var properties = new AuthenticationProperties();
            properties.StoreTokens(
            [
                new AuthenticationToken
                {
                    Name = "access_token",
                    Value = validatedToken,
                },
            ]);
            context.Features.Set<IAuthenticateResultFeature>(
                new StubAuthenticateResultFeature
                {
                    AuthenticateResult = AuthenticateResult.Success(
                        new AuthenticationTicket(
                            context.User,
                            properties,
                            "Test")),
                });
        }

        return context;
    }

    private static JsonRpcRequest CreateSendMessageRequest(string text)
    {
        return new JsonRpcRequest
        {
            Id = Guid.NewGuid().ToString(),
            Method = A2AMethods.SendMessage,
            Params = JsonSerializer.SerializeToElement(new SendMessageRequest
            {
                Message = new Message
                {
                    ContextId = Guid.NewGuid().ToString(),
                    Parts = [new Part { Text = text }]
                }
            })
        };
    }

    private static AgentTask ReadTaskResponse(DefaultHttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var response = ProtocolJsonSerializer.ToObject<JsonRpcResponse>(new StreamReader(context.Response.Body).ReadToEnd());
        return ProtocolJsonSerializer.ToObject<AgentTask>(response.Result!.AsObject().GetAt(0).Value!);
    }

    private static string ReadResponseText(DefaultHttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return new StreamReader(context.Response.Body).ReadToEnd();
    }

    private static ClaimsIdentity CreateDelegatedIdentity(string scope = "access_as_user")
    {
        return new ClaimsIdentity(
        [
            new Claim("tid", "tenant-123"),
            new Claim("oid", "user-456"),
            new Claim("sub", "subject-789"),
            new Claim("scp", scope)
        ],
        authenticationType: "Bearer");
    }

    private static string CreateDelegatedToken(string scope)
    {
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            claims: [new Claim("scp", scope)],
            expires: DateTime.UtcNow.AddMinutes(30)));
    }

    private sealed class StubAuthenticateResultFeature : IAuthenticateResultFeature
    {
        public AuthenticateResult? AuthenticateResult { get; set; }
    }

    private sealed record Record(A2AAdapter Adapter, IAgent Agent);
}
