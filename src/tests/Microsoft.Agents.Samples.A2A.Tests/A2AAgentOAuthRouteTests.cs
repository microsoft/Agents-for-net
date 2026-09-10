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
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using A2AAgentSample::A2AAgent;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class A2AAgentOAuthRouteTests
{
    private const string DelegatedHandlerName = "delegated";
    private const string GraphHandlerName = "graph";
    private const string AppHandlerName = "app";

    [Fact]
    public async Task DelegatedRoute_UsesDelegatedHandlerOnly()
    {
        var delegated = CreateAuthorizationHandler(DelegatedHandlerName, "delegated-request-token");
        var graph = CreateAuthorizationHandler(GraphHandlerName, "graph-request-token");
        var app = CreateAuthorizationHandler(AppHandlerName, "app-request-token");
        var graphClient = new Mock<IGraphProfileClient>(MockBehavior.Strict);
        var record = CreateRecord(delegated, graph, app, graphClient);

        var context = await ExecuteMessageAsync(record, "-delegated", CreateDelegatedIdentity());
        var task = ReadTaskResponse(context);

        delegated.Verify(handler => handler.SignInUserAsync(
            It.IsAny<ITurnContext>(),
            true,
            It.IsAny<string>(),
            It.IsAny<System.Collections.Generic.IList<string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        delegated.Verify(handler => handler.GetRefreshedUserTokenAsync(
            It.IsAny<ITurnContext>(),
            It.IsAny<string>(),
            It.IsAny<System.Collections.Generic.IList<string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        VerifyHandlerNotInvoked(graph);
        VerifyHandlerNotInvoked(app);
        Assert.Contains("tenant-123", task.Status.Message!.Parts[0].Text);
        Assert.Contains("user-456", task.Status.Message.Parts[0].Text);
        Assert.Contains("subject-789", task.Status.Message.Parts[0].Text);
        Assert.Contains("Bearer", task.Status.Message.Parts[0].Text);
    }

    [Fact]
    public async Task GraphRoute_UsesGraphHandlerOnly_AndReturnsProfile()
    {
        var delegated = CreateAuthorizationHandler(DelegatedHandlerName, "delegated-request-token");
        var graph = CreateAuthorizationHandler(GraphHandlerName, "graph-token");
        var app = CreateAuthorizationHandler(AppHandlerName, "app-request-token");
        var graphClient = new Mock<IGraphProfileClient>(MockBehavior.Strict);
        graphClient
            .Setup(client => client.GetMeAsync("graph-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphProfile("Ada Lovelace", "ada@example.com"));
        var record = CreateRecord(delegated, graph, app, graphClient);

        var context = await ExecuteMessageAsync(record, "-me", CreateDelegatedIdentity());
        var task = ReadTaskResponse(context);

        VerifyHandlerNotInvoked(delegated);
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
        VerifyHandlerNotInvoked(app);
        graphClient.Verify(client => client.GetMeAsync("graph-token", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("Ada Lovelace", task.Status.Message!.Parts[0].Text);
        Assert.Contains("ada@example.com", task.Status.Message.Parts[0].Text);
    }

    [Fact]
    public async Task ApplicationRoute_UsesAppHandlerOnly()
    {
        var delegated = CreateAuthorizationHandler(DelegatedHandlerName, "delegated-request-token");
        var graph = CreateAuthorizationHandler(GraphHandlerName, "graph-request-token");
        var app = CreateAuthorizationHandler(AppHandlerName, "app-request-token");
        var graphClient = new Mock<IGraphProfileClient>(MockBehavior.Strict);
        var record = CreateRecord(delegated, graph, app, graphClient);

        var context = await ExecuteMessageAsync(record, "-app", CreateApplicationIdentity());
        var task = ReadTaskResponse(context);

        VerifyHandlerNotInvoked(delegated);
        VerifyHandlerNotInvoked(graph);
        app.Verify(handler => handler.SignInUserAsync(
            It.IsAny<ITurnContext>(),
            true,
            It.IsAny<string>(),
            It.IsAny<System.Collections.Generic.IList<string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        app.Verify(handler => handler.GetRefreshedUserTokenAsync(
            It.IsAny<ITurnContext>(),
            It.IsAny<string>(),
            It.IsAny<System.Collections.Generic.IList<string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains("tenant-123", task.Status.Message!.Parts[0].Text);
        Assert.Contains("subject-789", task.Status.Message.Parts[0].Text);
        Assert.Contains("client-app-id", task.Status.Message.Parts[0].Text);
        Assert.Contains("Bearer", task.Status.Message.Parts[0].Text);
    }

    [Fact]
    public async Task EchoRoute_DoesNotInvokeAuthorizationHandlers()
    {
        var delegated = CreateAuthorizationHandler(DelegatedHandlerName, "delegated-request-token");
        var graph = CreateAuthorizationHandler(GraphHandlerName, "graph-request-token");
        var app = CreateAuthorizationHandler(AppHandlerName, "app-request-token");
        var graphClient = new Mock<IGraphProfileClient>(MockBehavior.Strict);
        var record = CreateRecord(delegated, graph, app, graphClient);

        var context = await ExecuteMessageAsync(record, "hello", CreateDelegatedIdentity());
        var task = ReadTaskResponse(context);

        VerifyHandlerNotInvoked(delegated);
        VerifyHandlerNotInvoked(graph);
        VerifyHandlerNotInvoked(app);
        Assert.Equal("You said: hello", task.Status.Message!.Parts[0].Text);
    }

    [Fact]
    public async Task ApplicationRoute_RejectsDelegatedIdentity()
    {
        var context = await ExecuteMessageAsync(
            CreateRecord(
                CreateAuthorizationHandler(DelegatedHandlerName, "delegated-request-token"),
                CreateAuthorizationHandler(GraphHandlerName, "graph-request-token"),
                CreateAuthorizationHandler(AppHandlerName, "app-request-token"),
                new Mock<IGraphProfileClient>(MockBehavior.Strict)),
            "-app",
            CreateDelegatedIdentity());

        using var response = await ReadJsonResponseAsync(context);
        Assert.Contains("This route requires an application token.", response.RootElement.GetProperty("error").GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("-delegated")]
    [InlineData("-me")]
    public async Task DelegatedRoutes_RejectApplicationIdentity(string message)
    {
        var context = await ExecuteMessageAsync(
            CreateRecord(
                CreateAuthorizationHandler(DelegatedHandlerName, "delegated-request-token"),
                CreateAuthorizationHandler(GraphHandlerName, "graph-request-token"),
                CreateAuthorizationHandler(AppHandlerName, "app-request-token"),
                new Mock<IGraphProfileClient>(MockBehavior.Strict)),
            message,
            CreateApplicationIdentity());

        using var response = await ReadJsonResponseAsync(context);
        Assert.Contains("This route requires a delegated user token.", response.RootElement.GetProperty("error").GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    private static Record CreateRecord(
        Mock<IUserAuthorization> delegated,
        Mock<IUserAuthorization> graph,
        Mock<IUserAuthorization> app,
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
                delegated.Object,
                graph.Object,
                app.Object)
            {
                DefaultHandlerName = DelegatedHandlerName,
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

    private static ClaimsIdentity CreateDelegatedIdentity()
    {
        return new ClaimsIdentity(
        [
            new Claim("tid", "tenant-123"),
            new Claim("oid", "user-456"),
            new Claim("sub", "subject-789"),
            new Claim("scp", "User.Read")
        ],
        authenticationType: "Bearer");
    }

    private static ClaimsIdentity CreateApplicationIdentity()
    {
        return new ClaimsIdentity(
        [
            new Claim("tid", "tenant-123"),
            new Claim("sub", "subject-789"),
            new Claim("idtyp", "app"),
            new Claim("roles", "Task.Run"),
            new Claim("azp", "client-app-id")
        ],
        authenticationType: "Bearer");
    }

    private sealed record Record(A2AAdapter Adapter, IAgent Agent);
}
