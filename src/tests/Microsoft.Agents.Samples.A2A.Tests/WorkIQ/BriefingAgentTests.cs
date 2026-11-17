extern alias WorkIQBriefingSample;

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.A2A.Pipeline;
using Microsoft.Agents.Extensions.A2A.Authorization;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WorkIQBriefingSample::WorkIQBriefingAgent;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class BriefingAgentTests
{
    [Fact]
    public async Task FullConversation_ResearchesRevisesReviewsAndActuallyCallsGraphOnlyAfterCurrentApproval()
    {
        using var http = CreateHttpClients();
        await using var record = CreateAgent(http);
        var task = await SendAsync(record, "Prepare a briefing for my Contoso meeting");
        Assert.Equal(TaskState.InputRequired, task.Status.State);
        Assert.Contains("Original briefing", Text(task));
        var outerId = task.Id;

        task = await SendAsync(record, "to reviewer@example.com", task);
        var previousCode = ApprovalCode(task);
        Assert.DoesNotContain(http.Requests, request => request.Uri.Host == "graph.microsoft.com");

        task = await SendAsync(record, "revise Focus on commitments.", task);
        Assert.Equal(outerId, task.Id);
        Assert.Contains("Revised briefing", Text(task));
        var revisedCode = ApprovalCode(task);

        task = await SendAsync(record, $"send {previousCode}", task);
        Assert.Equal(TaskState.InputRequired, task.Status.State);
        Assert.DoesNotContain(http.Requests, request => request.Uri.Host == "graph.microsoft.com");

        task = await SendAsync(record, $"send {revisedCode}", task);
        Assert.Equal(TaskState.Completed, task.Status.State);
        Assert.Contains("accepted", Text(task), StringComparison.OrdinalIgnoreCase);
        var artifact = Assert.Single(task.Artifacts!, value => value.ArtifactId == "briefing");
        Assert.Equal("briefing.md", artifact.Name);
        Assert.Equal("Revised briefing", string.Concat(artifact.Parts.Select(part => part.Text)));
        var sent = Assert.Single(http.Requests, request => request.Uri.Host == "graph.microsoft.com");
        Assert.Equal("graph-token", sent.Token);
        using var email = JsonDocument.Parse(sent.Body);
        Assert.Equal("Revised briefing", email.RootElement.GetProperty("message").GetProperty("body").GetProperty("content").GetString());
        Assert.Equal("reviewer@example.com", email.RootElement.GetProperty("message").GetProperty("toRecipients")[0]
            .GetProperty("emailAddress").GetProperty("address").GetString());

        var workIqCalls = http.Requests.Where(request => request.Method == HttpMethod.Post && request.Uri.Host == "workiq.svc.cloud.microsoft").ToArray();
        Assert.Equal(2, workIqCalls.Length);
        Assert.All(workIqCalls, request => Assert.Equal("workiq-token", request.Token));
        using var followup = JsonDocument.Parse(workIqCalls[1].Body);
        var message = followup.RootElement.GetProperty("params").GetProperty("message");
        Assert.Equal("workiq-context", message.GetProperty("contextId").GetString());
        Assert.False(message.TryGetProperty("taskId", out var oldTask) && oldTask.ValueKind == JsonValueKind.String);
    }

    [Fact]
    public async Task SendCommandWithoutAReviewedDraft_DoesNotCallEitherService()
    {
        using var http = CreateHttpClients();
        await using var record = CreateAgent(http);

        var task = await SendAsync(record, "send anything");

        Assert.Equal(TaskState.InputRequired, task.Status.State);
        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task CancelAfterReview_DoesNotSend()
    {
        using var http = CreateHttpClients();
        await using var record = CreateAgent(http);
        var task = await SendAsync(record, "Prepare a briefing");
        task = await SendAsync(record, "to reviewer@example.com", task);

        task = await SendAsync(record, "cancel", task);

        Assert.Equal(TaskState.Canceled, task.Status.State);
        Assert.DoesNotContain(http.Requests, request => request.Uri.Host == "graph.microsoft.com");
    }

    [Theory]
    [InlineData("other-user")]
    [InlineData("other-tenant")]
    public async Task DifferentIdentity_CannotReadTheTaskOrSendItsApprovedEmail(string otherIdentity)
    {
        using var http = CreateHttpClients();
        await using var record = CreateAgent(http);
        var task = await SendAsync(record, "Prepare a briefing");
        task = await SendAsync(record, "to reviewer@example.com", task);
        var code = ApprovalCode(task);

        var read = await ExecuteAsync(record, A2AMethods.GetTask, new GetTaskRequest { Id = task.Id }, otherIdentity);
        Assert.NotNull(read.Error);
        var cancel = await ExecuteAsync(record, A2AMethods.CancelTask, new CancelTaskRequest { Id = task.Id }, otherIdentity);
        Assert.NotNull(cancel.Error);
        var approve = await ExecuteAsync(record, A2AMethods.SendMessage, Request($"send {code}", task), otherIdentity);
        Assert.NotNull(approve.Error);
        Assert.DoesNotContain(http.Requests, request => request.Uri.Host == "graph.microsoft.com");
    }

    [Fact]
    public async Task ConfiguredAuthorization_ExchangesTheAgentTokenSeparatelyForWorkIqAndGraph()
    {
        const string appId = "55555555-5555-5555-5555-555555555555";
        var inboundToken = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            audience: appId,
            claims:
            [
                new Claim("azp", appId),
                new Claim("ver", "2.0"),
                new Claim("scp", "access_as_user"),
                new Claim("tid", "11111111-1111-1111-1111-111111111111"),
                new Claim("oid", "22222222-2222-2222-2222-222222222222"),
            ],
            expires: DateTime.UtcNow.AddHours(1)));
        var provider = new Mock<IAccessTokenProvider>(MockBehavior.Strict);
        provider.As<IOBOExchange>().Setup(value => value.AcquireTokenOnBehalfOf(
                It.IsAny<IEnumerable<string>>(), inboundToken))
            .Returns((IEnumerable<string> scopes, string assertion) => Task.FromResult(new TokenResponse
            {
                Token = scopes.Single() switch
                {
                    "api://workiq.svc.cloud.microsoft/WorkIQAgent.Ask" => "workiq-token",
                    "https://graph.microsoft.com/Mail.Send" => "graph-token",
                    _ => throw new InvalidOperationException("Unexpected downstream resource."),
                },
                Expiration = DateTimeOffset.UtcNow.AddHours(1),
            }));
        IAccessTokenProvider tokenProvider = provider.Object;
        var connections = new Mock<IConnections>(MockBehavior.Strict);
        connections.Setup(value => value.TryGetConnection("ServiceConnection", out tokenProvider)).Returns(true);
        var settings = (await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "WorkIQBriefingAgent.appsettings.json")))
            .Replace("{{TenantId}}", "11111111-1111-1111-1111-111111111111", StringComparison.Ordinal)
            .Replace("{{ClientId}}", appId, StringComparison.Ordinal);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(settings));
        var configuration = new ConfigurationBuilder().AddJsonStream(stream).Build();
        IUserAuthorization Handler(string name) => new A2AUserAuthorization(name, connections.Object,
            A2AUserAuthorizationSettings.FromConfiguration(
                configuration.GetSection($"AgentApplication:UserAuthorization:Handlers:{name}:Settings")));
        using var http = CreateHttpClients();
        await using var record = CreateAgent(http, Handler("workiq"), Handler("mail"));

        var task = await SendAsync(record, "Prepare a briefing", validatedToken: inboundToken);
        Assert.True(task.Status.State == TaskState.InputRequired, Text(task));
        task = await SendAsync(record, "to reviewer@example.com", task, inboundToken);
        task = await SendAsync(record, $"send {ApprovalCode(task)}", task, inboundToken);

        Assert.Equal(TaskState.Completed, task.Status.State);
        Assert.All(http.Requests.Where(request => request.Uri.Host == "workiq.svc.cloud.microsoft"),
            request => Assert.Equal("workiq-token", request.Token));
        Assert.Equal("graph-token", Assert.Single(http.Requests, request => request.Uri.Host == "graph.microsoft.com").Token);
    }

    private static SampleHttpClients CreateHttpClients()
    {
        var researchCount = 0;
        return new SampleHttpClients((request, ct) =>
        {
            if (request.Uri.Host == "graph.microsoft.com")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
            }
            if (request.Method == HttpMethod.Get)
            {
                return Task.FromResult(SampleHttpClients.Json(SampleHttpClients.Card(streaming: false)));
            }

            var task = SampleHttpClients.WorkIqTask(TaskState.Completed,
                ++researchCount == 1 ? "Original briefing" : "Revised briefing");
            task.Id = $"workiq-{researchCount}";
            return Task.FromResult(SampleHttpClients.Rpc(request, new SendMessageResponse { Task = task }));
        });
    }

    private static AgentRecord CreateAgent(SampleHttpClients http, params IUserAuthorization[] handlers)
    {
        var storage = new MemoryStorage();
        var options = new AgentApplicationOptions(storage)
        {
            UserAuthorization = new UserAuthorizationOptions(
                NullLoggerFactory.Instance, storage, Mock.Of<IConnections>(),
                handlers.Length > 0 ? handlers : [Authorization("workiq", "workiq-token"), Authorization("mail", "graph-token")])
            {
                DefaultHandlerName = "workiq",
                AutoSignIn = UserAuthorizationOptions.AutoSignInOff,
            },
        };
        var workIq = new WorkIqClient(http, TimeZoneInfo.Utc, NullLogger<WorkIqClient>.Instance);
        var workflow = new BriefingWorkflow(new GraphMailSender(http), TimeProvider.System);
        var accessor = new HttpContextAccessor();
        return new AgentRecord(
            new A2AAdapter(new UserTaskStore(accessor), NullLoggerFactory.Instance),
            new BriefingAgent(options, workflow, workIq, NullLogger<BriefingAgent>.Instance),
            accessor);
    }

    private static IUserAuthorization Authorization(string name, string token)
    {
        var authorization = new Mock<IUserAuthorization>(MockBehavior.Strict);
        var response = new TokenResponse { Token = token, Expiration = DateTimeOffset.UtcNow.AddHours(1) };
        authorization.SetupGet(handler => handler.Name).Returns(name);
        authorization.Setup(handler => handler.SignInUserAsync(It.IsAny<ITurnContext>(), It.IsAny<bool>(),
            It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);
        authorization.Setup(handler => handler.GetRefreshedUserTokenAsync(It.IsAny<ITurnContext>(),
            It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);
        return authorization.Object;
    }

    private static async Task<AgentTask> SendAsync(AgentRecord record, string text, AgentTask? previous = null, string? validatedToken = null)
    {
        var envelope = await ExecuteAsync(record, A2AMethods.SendMessage, Request(text, previous), validatedToken: validatedToken);
        Assert.Null(envelope.Error);
        return envelope.Result.Deserialize<SendMessageResponse>(A2AJsonUtilities.DefaultOptions)!.Task!;
    }

    private static SendMessageRequest Request(string text, AgentTask? previous) => new()
    {
        Message = new Message
        {
            Role = Role.User,
            MessageId = Guid.NewGuid().ToString("N"),
            ContextId = previous?.ContextId,
            TaskId = previous?.Id,
            Parts = [Part.FromText(text)],
        },
    };

    private static async Task<JsonRpcResponse> ExecuteAsync(AgentRecord record, string method, object parameters,
        string user = "task-owner", string? validatedToken = null)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tid", user == "other-tenant" ? "44444444-4444-4444-4444-444444444444" : "11111111-1111-1111-1111-111111111111"),
            new Claim("oid", user == "other-user" ? "33333333-3333-3333-3333-333333333333" : "22222222-2222-2222-2222-222222222222"),
            new Claim("scp", "access_as_user"),
        ], "Bearer"));
        if (validatedToken is not null)
        {
            // This feature represents the token ASP.NET Core already validated. Only Entra's external
            // token exchange is replaced; the sample's actual A2A authorization handlers still run.
            var properties = new AuthenticationProperties();
            properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = validatedToken }]);
            context.Features.Set<IAuthenticateResultFeature>(new AuthenticateResultFeature
            {
                AuthenticateResult = AuthenticateResult.Success(new AuthenticationTicket(context.User, properties, "Bearer")),
            });
        }
        context.Request.Method = HttpMethods.Post;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new JsonRpcRequest
        {
            Id = Guid.NewGuid().ToString("N"),
            Method = method,
            Params = JsonSerializer.SerializeToElement(parameters, A2AJsonUtilities.DefaultOptions),
        }, A2AJsonUtilities.DefaultOptions)));
        context.Response.Body = new MemoryStream();
        record.Accessor.HttpContext = context;
        try
        {
            var result = await record.Adapter.ProcessJsonRpcAsync(context.Request, context.Response, record.Agent, timeout.Token);
            await result.ExecuteAsync(context);
            context.Response.Body.Position = 0;
            return (await JsonSerializer.DeserializeAsync<JsonRpcResponse>(
                context.Response.Body, A2AJsonUtilities.DefaultOptions, timeout.Token))!;
        }
        finally
        {
            record.Accessor.HttpContext = null;
        }
    }

    private static string Text(AgentTask task)
        => string.Join("\n", (task.Status.Message?.Parts ?? []).Select(part => part.Text)
            .Concat(task.Artifacts?.SelectMany(artifact => artifact.Parts).Select(part => part.Text) ?? []));

    private static string ApprovalCode(AgentTask task)
    {
        var match = Regex.Match(Text(task), @"send ([A-F0-9]{12})");
        Assert.True(match.Success, Text(task));
        return match.Groups[1].Value;
    }

    private sealed record AgentRecord(A2AAdapter Adapter, BriefingAgent Agent, HttpContextAccessor Accessor) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Adapter.DisposeAsync();
    }

    private sealed class AuthenticateResultFeature : IAuthenticateResultFeature
    {
        public AuthenticateResult? AuthenticateResult { get; set; }
    }
}
