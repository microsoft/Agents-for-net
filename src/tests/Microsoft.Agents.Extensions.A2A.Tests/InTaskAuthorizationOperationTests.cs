// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using A2A.AspNetCore;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.A2A.Pipeline;
using Microsoft.Agents.Extensions.A2A.ProtocolExtensions;
using Microsoft.Agents.Extensions.A2A.ProtocolExtensions.InTaskAuthorization;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.A2A.Tests;

public class InTaskAuthorizationOperationTests
{
    [Fact]
    public void AddOperation_DefinesStableTypedUnaryOperation()
    {
        var builder = new A2AOperationCatalogBuilder();

        var operation = InTaskAuthorizationOperation.AddOperation(builder);
        var catalog = builder.Build();

        Assert.Equal(
            InTaskAuthorizationExtension.Uri + "#resumeAuth",
            operation.Id.Value);
        Assert.Same(
            operation,
            catalog.GetRequired<ResumeAuthRequest, AgentTask>(operation.Id));
    }

    [Fact]
    public async Task JsonRpcAndHttpBindings_InvokeSameHandlerWithTransportSpecificBinding()
    {
        var operationBuilder = new A2AOperationCatalogBuilder();
        var standard = operationBuilder.AddStandardA2AOperations();
        var resumeAuth = InTaskAuthorizationOperation.AddOperation(operationBuilder);
        var operations = operationBuilder.Build();
        var invocations = new List<Invocation>();
        var disposedRequests = new HashSet<string>(StringComparer.Ordinal);

        ValueTask<AgentTask> SharedHandler(
            A2AOperationContext operationContext,
            ResumeAuthRequest request,
            CancellationToken cancellationToken)
        {
            var httpContext = operationContext.Features.GetRequired<HttpContext>();
            invocations.Add(new Invocation(
                request,
                httpContext.Request.Headers.Authorization.ToString(),
                httpContext.Request.Headers[InTaskAuthorizationExtension.TokenHeader].ToString()));
            return ValueTask.FromResult(new AgentTask
            {
                Id = request.TaskId,
                ContextId = request.ContextId,
                Status = new global::A2A.TaskStatus { State = TaskState.Completed },
            });
        }

        var handlers = new A2AOperationHandlerCatalogBuilder()
            .AddStandardA2AHandlers(standard)
            .Map(resumeAuth, SharedHandler)
            .Build(operations);
        var jsonRpcBuilder = new A2AJsonRpcOperationBindingBuilder()
            .AddStandardA2AJsonRpcBindings(standard);
        InTaskAuthorizationOperation.AddJsonRpcBinding(jsonRpcBuilder, resumeAuth);
        var jsonRpcBindings = jsonRpcBuilder.Build(operations);
        var httpBuilder = new A2AHttpOperationBindingBuilder()
            .AddStandardA2AHttpBindings(standard);
        InTaskAuthorizationOperation.AddHttpBinding(httpBuilder, resumeAuth);
        var httpBindings = httpBuilder.Build();
        var requestHandler = Mock.Of<IA2ARequestHandler>();

        ValueTask<A2ARequestScope> CreateScopeAsync(
            HttpContext httpContext,
            CancellationToken cancellationToken)
        {
            var features = new A2AFeatureCollection();
            features.Set(httpContext);
            return ValueTask.FromResult(new A2ARequestScope(
                new A2AOperationContext(requestHandler, features),
                () =>
                {
                    disposedRequests.Add(httpContext.TraceIdentifier);
                    return ValueTask.CompletedTask;
                }));
        }

        var jsonRpcContext = CreateHttpContext(JsonSerializer.Serialize(new JsonRpcRequest
        {
            Id = "json-id",
            Method = InTaskAuthorizationExtension.ResumeAuthOperation,
            Params = JsonSerializer.SerializeToElement(new ResumeAuthRequest
            {
                TaskId = "json-task",
                ContextId = "json-context",
                AuthorizationRequestId = "json-authorization",
            }),
        }));
        SetSeparatedAuthorizationHeaders(jsonRpcContext);

        var jsonRpcResult = await A2AJsonRpcProcessor.ProcessRequestAsync(
            CreateScopeAsync,
            handlers,
            jsonRpcBindings,
            jsonRpcContext.Request,
            CancellationToken.None);

        Assert.DoesNotContain(jsonRpcContext.TraceIdentifier, disposedRequests);
        await jsonRpcResult.ExecuteAsync(jsonRpcContext);
        Assert.Contains(jsonRpcContext.TraceIdentifier, disposedRequests);

        var httpBody = JsonSerializer.Serialize(new ResumeAuthRequest
        {
            TaskId = "body-task-must-not-win",
            ContextId = "http-context",
            AuthorizationRequestId = "http-authorization",
        });
        Assert.DoesNotContain("delegated-token", httpBody, StringComparison.Ordinal);

        var httpContext = CreateHttpContext(httpBody);
        httpContext.Request.ContentType = "application/json";
        httpContext.Request.RouteValues["taskId"] = "route-task";
        SetSeparatedAuthorizationHeaders(httpContext);

        var appBuilder = WebApplication.CreateBuilder();
        appBuilder.Services.AddLogging();
        await using var app = appBuilder.Build();
        app.MapHttpA2A(CreateScopeAsync, handlers, httpBindings, "/a2a");
        httpContext.RequestServices = app.Services;
        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(endpoint => endpoint.RoutePattern.RawText == "/a2a/tasks/{taskId}:resumeAuth");

        await endpoint.RequestDelegate!(httpContext);

        Assert.Contains(httpContext.TraceIdentifier, disposedRequests);
        Assert.Collection(
            invocations,
            invocation =>
            {
                Assert.Equal("json-task", invocation.Request.TaskId);
                AssertSeparatedAuthorizationHeaders(invocation);
            },
            invocation =>
            {
                Assert.Equal("route-task", invocation.Request.TaskId);
                Assert.Equal("http-context", invocation.Request.ContextId);
                Assert.Equal("http-authorization", invocation.Request.AuthorizationRequestId);
                AssertSeparatedAuthorizationHeaders(invocation);
            });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("text/plain")]
    public async Task HttpBinding_NonJsonContentTypeReturnsEmpty415BeforeScopeHandlerOrAuthorizationMutation(
        string contentType)
    {
        var result = await ExecuteHttpBindingAsync(contentType);

        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, result.Context.Response.StatusCode);
        Assert.Equal(0, result.Context.Response.Body.Length);
        Assert.Equal(0, result.ScopeCalls);
        Assert.Equal(0, result.HandlerCalls);
        Assert.Equal(0, result.AuthorizationMutations);
    }

    [Fact]
    public async Task HttpBinding_ApplicationJsonInvokesScopeAndHandler()
    {
        var result = await ExecuteHttpBindingAsync("application/json");

        Assert.Equal(StatusCodes.Status200OK, result.Context.Response.StatusCode);
        Assert.True(result.Context.Response.Body.Length > 0);
        Assert.Equal(1, result.ScopeCalls);
        Assert.Equal(1, result.HandlerCalls);
        Assert.Equal(1, result.AuthorizationMutations);
    }

    [Fact]
    public async Task DisposingOldScope_DoesNotRemoveReplacementWithSameRequestId()
    {
        var adapter = new A2AAdapter(new InMemoryTaskStore(), NullLoggerFactory.Instance);
        var httpContext = CreateHttpContext();
        var agent = Mock.Of<IAgent>();
        await using var firstScope = await adapter.CreateRequestScopeAsync(
            httpContext, agent, CancellationToken.None);
        var duplicateHttpContext = CreateHttpContext();
        duplicateHttpContext.TraceIdentifier = httpContext.TraceIdentifier;
        await using var duplicateScope = await adapter.CreateRequestScopeAsync(
            duplicateHttpContext, agent, CancellationToken.None);
        Assert.Same(
            firstScope.Context.Features.GetRequired<AgentRequestContext>(),
            duplicateScope.Context.Features.GetRequired<AgentRequestContext>());

        await firstScope.DisposeAsync();
        await using var replacementScope = await adapter.CreateRequestScopeAsync(
            httpContext, agent, CancellationToken.None);
        Assert.NotSame(
            firstScope.Context.Features.GetRequired<AgentRequestContext>(),
            replacementScope.Context.Features.GetRequired<AgentRequestContext>());

        await duplicateScope.DisposeAsync();

        var turnContext = new TurnContext(
            adapter, new Activity { RequestId = httpContext.TraceIdentifier });
        Assert.Empty(await adapter.SendActivitiesAsync(turnContext, [], CancellationToken.None));
        await replacementScope.DisposeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.SendActivitiesAsync(turnContext, [], CancellationToken.None));
    }

    [Fact]
    public async Task CreateRequestScopeAsync_ProvidesRequestSpecificServerAndAgentContextFeature()
    {
        var adapter = new A2AAdapter(new InMemoryTaskStore(), NullLoggerFactory.Instance);
        var agent = Mock.Of<IAgent>();
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers[A2AProtocolExtensionRequest.HeaderName] =
            InTaskAuthorizationExtension.Uri;
        SetSeparatedAuthorizationHeaders(httpContext);

        var firstScope = await adapter.CreateRequestScopeAsync(
            httpContext,
            agent,
            CancellationToken.None);
        var agentContext = firstScope.Context.Features.GetRequired<AgentRequestContext>();
        var secondHttpContext = CreateHttpContext();
        var secondScope = await adapter.CreateRequestScopeAsync(
            secondHttpContext,
            agent,
            CancellationToken.None);

        Assert.IsAssignableFrom<A2AServer>(firstScope.Context.RequestHandler);
        Assert.NotSame(firstScope.Context.RequestHandler, secondScope.Context.RequestHandler);
        Assert.Same(httpContext.Request, agentContext.HttpRequest);
        Assert.Equal(
            InTaskAuthorizationExtension.Uri,
            httpContext.Response.Headers[A2AProtocolExtensionRequest.HeaderName]);
        Assert.Equal("Bearer request-jwt", agentContext.HttpRequest.Headers.Authorization);
        Assert.Equal(
            "delegated-token",
            agentContext.HttpRequest.Headers[InTaskAuthorizationExtension.TokenHeader]);

        var turnContext = new TurnContext(
            adapter,
            new Activity { RequestId = agentContext.RequestId });
        Assert.Empty(await adapter.SendActivitiesAsync(
            turnContext,
            [],
            CancellationToken.None));

        await firstScope.DisposeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.SendActivitiesAsync(turnContext, [], CancellationToken.None));
        await secondScope.DisposeAsync();
    }

    [Fact]
    public async Task StandardJsonRpcOperation_UsesUpstreamProcessorAndRequestScope()
    {
        var operationBuilder = new A2AOperationCatalogBuilder();
        var standard = operationBuilder.AddStandardA2AOperations();
        var operations = operationBuilder.Build();
        var requestHandler = new Mock<IA2ARequestHandler>();
        requestHandler
            .Setup(handler => handler.GetTaskAsync(
                It.Is<GetTaskRequest>(request => request.Id == "standard-task"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentTask
            {
                Id = "standard-task",
                ContextId = "standard-context",
                Status = new global::A2A.TaskStatus { State = TaskState.Completed },
            });
        var handlers = new A2AOperationHandlerCatalogBuilder()
            .AddStandardA2AHandlers(standard)
            .Build(operations);
        var bindings = new A2AJsonRpcOperationBindingBuilder()
            .AddStandardA2AJsonRpcBindings(standard)
            .Build(operations);
        var disposed = false;
        ValueTask<A2ARequestScope> CreateScopeAsync(
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(new A2ARequestScope(
                new A2AOperationContext(requestHandler.Object),
                () =>
                {
                    disposed = true;
                    return ValueTask.CompletedTask;
                }));
        var context = CreateHttpContext(JsonSerializer.Serialize(new JsonRpcRequest
        {
            Id = "standard-id",
            Method = A2AMethods.GetTask,
            Params = JsonSerializer.SerializeToElement(new GetTaskRequest
            {
                Id = "standard-task",
            }),
        }));

        var result = await A2AJsonRpcProcessor.ProcessRequestAsync(
            CreateScopeAsync,
            handlers,
            bindings,
            context.Request,
            CancellationToken.None);

        Assert.False(disposed);
        await result.ExecuteAsync(context);
        Assert.True(disposed);
        requestHandler.VerifyAll();
        Assert.Equal("A2A.AspNetCore", typeof(A2AJsonRpcProcessor).Assembly.GetName().Name);
    }

    private static async Task<HttpBindingResult> ExecuteHttpBindingAsync(string contentType)
    {
        var operationBuilder = new A2AOperationCatalogBuilder();
        var standard = operationBuilder.AddStandardA2AOperations();
        var resumeAuth = InTaskAuthorizationOperation.AddOperation(operationBuilder);
        var operations = operationBuilder.Build();
        var scopeCalls = 0;
        var handlerCalls = 0;
        var authorizationMutations = 0;
        var requestHandler = Mock.Of<IA2ARequestHandler>();
        var handlers = new A2AOperationHandlerCatalogBuilder()
            .AddStandardA2AHandlers(standard)
            .Map(
                resumeAuth,
                (
                    A2AOperationContext operationContext,
                    ResumeAuthRequest request,
                    CancellationToken cancellationToken) =>
                {
                    handlerCalls++;
                    authorizationMutations++;
                    return ValueTask.FromResult(new AgentTask
                    {
                        Id = request.TaskId,
                        ContextId = request.ContextId,
                        Status = new global::A2A.TaskStatus { State = TaskState.Completed },
                    });
                })
            .Build(operations);
        var httpBuilder = new A2AHttpOperationBindingBuilder()
            .AddStandardA2AHttpBindings(standard);
        InTaskAuthorizationOperation.AddHttpBinding(httpBuilder, resumeAuth);
        var httpBindings = httpBuilder.Build();

        ValueTask<A2ARequestScope> CreateScopeAsync(
            HttpContext httpContext,
            CancellationToken cancellationToken)
        {
            scopeCalls++;
            return ValueTask.FromResult(new A2ARequestScope(
                new A2AOperationContext(requestHandler),
                static () => ValueTask.CompletedTask));
        }

        var requestBody = JsonSerializer.Serialize(new ResumeAuthRequest
        {
            TaskId = "body-task-must-not-win",
            ContextId = "http-context",
            AuthorizationRequestId = "http-authorization",
        });
        var context = CreateHttpContext(requestBody);
        context.Request.ContentType = contentType;
        context.Request.RouteValues["taskId"] = "route-task";

        var appBuilder = WebApplication.CreateBuilder();
        appBuilder.Services.AddLogging();
        await using var app = appBuilder.Build();
        app.MapHttpA2A(CreateScopeAsync, handlers, httpBindings, "/a2a");
        context.RequestServices = app.Services;
        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(endpoint => endpoint.RoutePattern.RawText == "/a2a/tasks/{taskId}:resumeAuth");

        await endpoint.RequestDelegate!(context);

        return new HttpBindingResult(
            context,
            scopeCalls,
            handlerCalls,
            authorizationMutations);
    }

    private static DefaultHttpContext CreateHttpContext(string requestContent = "")
    {
        var context = new DefaultHttpContext();
        context.TraceIdentifier = Guid.NewGuid().ToString("N");
        context.RequestServices = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();
        context.Request.Method = HttpMethods.Post;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(requestContent));
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static void SetSeparatedAuthorizationHeaders(DefaultHttpContext context)
    {
        context.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "caller")],
                authenticationType: "Test"));
        context.Request.Headers.Authorization = "Bearer request-jwt";
        context.Request.Headers[InTaskAuthorizationExtension.TokenHeader] =
            "delegated-token";
    }

    private static void AssertSeparatedAuthorizationHeaders(Invocation invocation)
    {
        Assert.Equal("Bearer request-jwt", invocation.Authorization);
        Assert.Equal("delegated-token", invocation.DelegatedToken);
        Assert.NotEqual(invocation.Authorization, invocation.DelegatedToken);
    }

    private sealed record Invocation(
        ResumeAuthRequest Request,
        string Authorization,
        string DelegatedToken);

    private sealed record HttpBindingResult(
        DefaultHttpContext Context,
        int ScopeCalls,
        int HandlerCalls,
        int AuthorizationMutations);
}
