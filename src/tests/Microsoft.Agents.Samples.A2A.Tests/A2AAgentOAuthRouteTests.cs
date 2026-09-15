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
using Microsoft.Agents.Extensions.A2A.Pipeline;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.IO;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AgentCard_FromSampleConfiguration_AdvertisesDelegatedOAuthForProtectedSkill(bool protectedSkills)
    {
        const string tenantId = "11111111-1111-1111-1111-111111111111";
        const string clientId = "22222222-2222-2222-2222-222222222222";
        string settings = (await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "A2AAgent.appsettings.json")))
            .Replace("{{TenantId}}", tenantId, StringComparison.Ordinal)
            .Replace("{{ClientId}}", clientId, StringComparison.Ordinal);
        using var settingsStream = new MemoryStream(Encoding.UTF8.GetBytes(settings));
        var configuration = new ConfigurationBuilder().AddJsonStream(settingsStream).Build();
        Assert.False(configuration.GetSection("AgentApplication:A2A:AgentCard").Exists());
        var handler = Assert.Single(configuration.GetSection("AgentApplication:UserAuthorization:Handlers").GetChildren());
        Assert.Equal(GraphHandlerName, handler.Key);
        Assert.Equal(GraphHandlerName, configuration["AgentApplication:UserAuthorization:DefaultHandlerName"]);
        Assert.Equal("ServiceConnection", handler["Settings:OBOConnectionName"]);
        Assert.Equal("User.Read", handler["Settings:OBOScopes:0"]);
        Assert.Null(handler["Settings:AuthorizationPolicy"]);

        var storage = new MemoryStorage();
        var adapter = new A2AAdapter(storage, NullLoggerFactory.Instance, configuration: configuration);
        IAgent agent = protectedSkills
            ? new MyAgent(new AgentApplicationOptions(storage), Mock.Of<IGraphProfileClient>())
            : new AgentApplication(new AgentApplicationOptions(storage));
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("agent.example");
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        await adapter.ProcessAgentCardAsync(context.Request, context.Response, agent, "/a2a", CancellationToken.None);
        responseBody.Position = 0;
        var card = (await JsonSerializer.DeserializeAsync<AgentCard>(responseBody, A2AJsonUtilities.DefaultOptions))!;
        Assert.DoesNotContain("User.Read", Encoding.UTF8.GetString(responseBody.ToArray()), StringComparison.Ordinal);
        Assert.NotNull(card.SecuritySchemes);
        Assert.Null(card.SecurityRequirements);
        var delegated = card.SecuritySchemes["delegated"].OAuth2SecurityScheme!.Flows!.DeviceCode!;

        Assert.Equal("https://login.microsoftonline.com/organizations/oauth2/v2.0/token", delegated.TokenUrl);
        Assert.Equal("https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode", delegated.DeviceAuthorizationUrl);
        if (protectedSkills)
        {
            AssertSkillRequirement(
                card,
                "Microsoft Graph profile",
                "delegated",
                "api://22222222-2222-2222-2222-222222222222/access_as_user");
        }
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
    public void MyAgent_ResolvesFromStartupServiceRegistration()
    {
        var builder = WebApplication.CreateBuilder();
        builder.AddAgentDefaults();
        builder.Services.AddSingleton<IStorage, MemoryStorage>();
        builder.Services.AddSingleton(sp => new AgentApplicationOptions(sp.GetRequiredService<IStorage>()));
        builder.Services.AddHttpClient<IGraphProfileClient, GraphProfileClient>(client =>
        {
            client.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/");
        });
        builder.AddAgent<MyAgent>();

        using var provider = builder.Services.BuildServiceProvider();

        _ = provider.GetRequiredService<MyAgent>();
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
    public async Task GraphRoute_UsesDelegatedTokenToReturnProfile()
    {
        var graph = CreateAuthorizationHandler(GraphHandlerName, "graph-token");
        var graphClient = new Mock<IGraphProfileClient>(MockBehavior.Strict);
        graphClient
            .Setup(client => client.GetMeAsync("graph-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphProfile("Ada Lovelace", "ada@example.com"));
        var record = CreateRecord(graph, graphClient);

        var context = await ExecuteMessageAsync(record, "-me", CreateDelegatedIdentity());
        var task = ReadTaskResponse(context);

        graph.Verify(handler => handler.SignInUserAsync(
            It.IsAny<ITurnContext>(),
            true,
            It.IsAny<string>(),
            It.IsAny<System.Collections.Generic.IList<string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        graph.Verify(handler => handler.GetRefreshedUserTokenAsync(
            It.IsAny<ITurnContext>(),
            It.IsAny<string>(),
            It.IsAny<System.Collections.Generic.IList<string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        graphClient.Verify(client => client.GetMeAsync("graph-token", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("Ada Lovelace", task.Status.Message!.Parts[0].Text);
        Assert.Contains("ada@example.com", task.Status.Message.Parts[0].Text);
    }

    [Fact]
    public async Task EchoRoute_DoesNotInvokeAuthorizationHandlers()
    {
        var graph = CreateAuthorizationHandler(GraphHandlerName, "graph-token");
        var record = CreateRecord(graph, new Mock<IGraphProfileClient>(MockBehavior.Strict));

        var context = await ExecuteMessageAsync(record, "hello", CreateDelegatedIdentity());
        var task = ReadTaskResponse(context);

        VerifyHandlerNotInvoked(graph);
        Assert.Equal("You said: hello", task.Status.Message!.Parts[0].Text);
    }

    [Fact]
    public async Task GraphRoute_RejectsApplicationIdentity()
    {
        var context = await ExecuteMessageAsync(
            CreateRecord(
                CreateAuthorizationHandler(GraphHandlerName, "graph-token"),
                new Mock<IGraphProfileClient>(MockBehavior.Strict)),
            "-me",
            CreateApplicationIdentity());

        using var response = await ReadJsonResponseAsync(context);
        Assert.Contains("This route requires a delegated user token.", response.RootElement.GetProperty("error").GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GraphRoute_RejectsUnrelatedDelegatedScope()
    {
        var context = await ExecuteMessageAsync(
            CreateRecord(
                CreateAuthorizationHandler(GraphHandlerName, "graph-token"),
                new Mock<IGraphProfileClient>(MockBehavior.Strict)),
            "-me",
            CreateDelegatedIdentity("Other.Scope"));

        using var response = await ReadJsonResponseAsync(context);
        Assert.Contains("This route requires the access_as_user delegated scope.", response.RootElement.GetProperty("error").GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    private static Record CreateRecord(
        Mock<IUserAuthorization> graph,
        Mock<IGraphProfileClient> graphClient)
    {
        var storage = new MemoryStorage();
        var connections = Mock.Of<IConnections>();
        var options = new AgentApplicationOptions(storage)
        {
            UserAuthorization = new UserAuthorizationOptions(
                NullLoggerFactory.Instance,
                storage,
                connections,
                graph.Object)
            {
                DefaultHandlerName = GraphHandlerName,
                AutoSignIn = UserAuthorizationOptions.AutoSignInOff
            }
        };

        return new Record(
            new A2AAdapter(storage, NullLoggerFactory.Instance),
            new MyAgent(options, graphClient.Object));
    }

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

    private static void VerifyHandlerNotInvoked(Mock<IUserAuthorization> handler)
    {
        handler.Verify(value => value.SignInUserAsync(
            It.IsAny<ITurnContext>(),
            It.IsAny<bool>(),
            It.IsAny<string>(),
            It.IsAny<System.Collections.Generic.IList<string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        handler.Verify(value => value.GetRefreshedUserTokenAsync(
            It.IsAny<ITurnContext>(),
            It.IsAny<string>(),
            It.IsAny<System.Collections.Generic.IList<string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static async Task<DefaultHttpContext> ExecuteMessageAsync(Record record, string text, ClaimsIdentity identity)
    {
        var context = CreateHttpContext(text, identity);
        var result = await record.Adapter.ProcessJsonRpcAsync(context.Request, context.Response, record.Agent, CancellationToken.None);
        await result.ExecuteAsync(context);
        return context;
    }

    private static DefaultHttpContext CreateHttpContext(string text, ClaimsIdentity identity)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(CreateSendMessageRequest(text))));
        context.Request.Method = HttpMethods.Post;
        context.Request.Headers.Authorization = "Bearer request-access-token";
        context.User = new ClaimsPrincipal(identity);
        context.Response.Body = new MemoryStream();
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

    private static async Task<JsonDocument> ReadJsonResponseAsync(DefaultHttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonDocument.ParseAsync(context.Response.Body);
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

    private static ClaimsIdentity CreateApplicationIdentity()
    {
        return new ClaimsIdentity(
        [
            new Claim("tid", "tenant-123"),
            new Claim("sub", "subject-789"),
            new Claim("idtyp", "app")
        ],
        authenticationType: "Bearer");
    }

    private sealed record Record(A2AAdapter Adapter, IAgent Agent);
}
