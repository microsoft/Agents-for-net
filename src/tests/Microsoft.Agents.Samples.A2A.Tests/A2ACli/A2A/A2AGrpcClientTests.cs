// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using A2A.Grpc.Extensions.Protos;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Agents.Samples.A2ACli;
using Microsoft.Agents.Samples.A2ACli.A2A;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2ACli.Tests.A2A;

public class A2AGrpcClientTests
{
    [Fact]
    public async Task GrpcOnlyPlaintextListener_ActualCliDiscoversCardAndStartsConsole()
    {
        await using var host = await GrpcTestHost.StartAsync();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add(typeof(Program).Assembly.Location);
        start.ArgumentList.Add("--agent");
        start.ArgumentList.Add(host.Address.AbsoluteUri);
        start.ArgumentList.Add("--transport");
        start.ArgumentList.Add("grpc");
        start.ArgumentList.Add("--auth-mode");
        start.ArgumentList.Add("none");
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("The CLI process did not start.");
        try
        {
            Task<string> output = process.StandardOutput.ReadToEndAsync(cancellation.Token);
            Task<string> error = process.StandardError.ReadToEndAsync(cancellation.Token);
            await process.StandardInput.WriteAsync("n\nhello\n\n:q\n");
            process.StandardInput.Close();
            await process.WaitForExitAsync(cancellation.Token);

            Assert.Equal(0, process.ExitCode);
            Assert.Contains("reply: hello", await output, StringComparison.Ordinal);
            Assert.Empty(await error);
            Assert.Contains(host.Requests, request => request.Path == "/.well-known/agent-card.json");
            Assert.All(host.Requests, request => Assert.Equal("HTTP/2", request.Protocol));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GrpcOnlyCard_DiscoversAndSendsUnaryAndStreamingOverHttp2(bool useTls)
    {
        await using var host = await GrpcTestHost.StartAsync(useTls);
        using var httpClient = host.CreateHttpClient();
        AgentCard card = await new A2ACardResolver(host.Address, httpClient).GetAgentCardAsync();
        using A2AClientConnection connection = Program.CreateClient(card, httpClient, host.Address);
        IA2AClient client = connection.Client;

        SendMessageResponse response = await client.SendMessageAsync(CreateRequest("hello"));
        var updates = new List<StreamResponse>();
        await foreach (StreamResponse update in client.SendStreamingMessageAsync(CreateRequest("stream")))
        {
            updates.Add(update);
        }
        AgentTask history = await client.GetTaskAsync(new GetTaskRequest { Id = "task-1", HistoryLength = 100 });

        Assert.Equal(TaskState.Completed, response.Task!.Status.State);
        Assert.Equal("reply: hello", response.Task.Status.Message!.Parts[0].Text);
        Assert.Equal(TaskState.Completed, Assert.Single(updates).Task!.Status.State);
        Assert.Equal("reply: stream", updates[0].Task!.Status.Message!.Parts[0].Text);
        Assert.Equal("task-1", history.Id);
        Assert.Contains(host.Requests, request => request.Path == "/.well-known/agent-card.json" && request.Protocol == "HTTP/2");
        Assert.All(host.Requests.Where(request => request.Path.StartsWith("/lf.", StringComparison.Ordinal)),
            request => Assert.Equal("1.0", request.Version));
    }

    [Fact]
    public async Task GrpcRequests_PreserveAdvertisedExtensionActivation()
    {
        await using var host = await GrpcTestHost.StartAsync();
        using var httpClient = host.CreateHttpClient();
        AgentCard card = host.CreateCard();
        card.Capabilities.Extensions = [new AgentExtension { Uri = A2AInTaskAuthorizationClient.ExtensionUri }];
        using A2AClientConnection connection = Program.CreateClient(card, httpClient, host.Address);

        await connection.Client.SendMessageAsync(CreateRequest("extension enabled"));

        Assert.Equal(A2AInTaskAuthorizationClient.ExtensionUri, Assert.Single(host.Requests).Extensions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GrpcConsole_PreservesAttachmentsContinuationHistoryAndHttpCallbacks(bool streaming)
    {
        await using var host = await GrpcTestHost.StartAsync();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var notification = new TaskCompletionSource<StreamResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var receiver = await PushNotificationReceiver.StartAsync(
            new Uri("http://127.0.0.1:0"), value => notification.TrySetResult(value), cancellation.Token);
        var requests = new List<SendMessageRequest>();
        async Task<SendMessageResponse> ReceiveAsync(SendMessageRequest request, CancellationToken token)
        {
            requests.Add(request);
            using var callbackClient = new HttpClient();
            using HttpResponseMessage callback = await callbackClient.PostAsJsonAsync(
                request.Configuration!.TaskPushNotificationConfig!.Url,
                new StreamResponse { Task = CreateTask("push callback") },
                A2AJsonUtilities.DefaultOptions, token);
            callback.EnsureSuccessStatusCode();
            AgentTask task = CreateTask("console reply");
            task.Status.State = requests.Count == 1 ? TaskState.InputRequired : TaskState.Completed;
            return new SendMessageResponse { Task = task };
        }
        host.Handler.Setup(value => value.SendMessageAsync(
                It.IsAny<SendMessageRequest>(), It.IsAny<A2ARequestContext>(), It.IsAny<CancellationToken>()))
            .Returns<SendMessageRequest, A2ARequestContext?, CancellationToken>((request, _, token) => ReceiveAsync(request, token));
        host.Handler.Setup(value => value.SendStreamingMessageAsync(
                It.IsAny<SendMessageRequest>(), It.IsAny<A2ARequestContext>(), It.IsAny<CancellationToken>()))
            .Returns<SendMessageRequest, A2ARequestContext?, CancellationToken>((request, _, token) =>
                ToStreamAsync(ReceiveAsync(request, token)));
        AgentTask history = CreateTask("history");
        history.History = [new Message { Role = Role.User, Parts = [Part.FromText("preserved history")] }];
        host.Handler.Setup(value => value.GetTaskAsync(
                It.IsAny<GetTaskRequest>(), It.IsAny<A2ARequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(history);
        using var httpClient = host.CreateHttpClient();
        AgentCard card = host.CreateCard();
        using A2AClientConnection connection = Program.CreateClient(card, httpClient, host.Address);
        string attachment = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(attachment, [1, 2, 3], cancellation.Token);
            var output = new StringWriter();
            var console = new A2AConsole(connection.Client, card, new A2AAuthenticationSession(),
                new StringReader($"{(streaming ? "y" : "n")}\nfirst\n{attachment}\nsecond\n\n:q\n"),
                output, true, receiver.NotificationUri);

            int exitCode = await console.RunAsync(cancellation.Token);
            StreamResponse push = await notification.Task.WaitAsync(cancellation.Token);

            Assert.Equal(0, exitCode);
            Assert.Equal(2, requests.Count);
            Assert.Equal(new byte[] { 1, 2, 3 }, requests[0].Message.Parts[1].Raw);
            Assert.Equal(Path.GetFileName(attachment), requests[0].Message.Parts[1].Filename);
            Assert.Null(requests[0].Message.TaskId);
            Assert.Equal("task-1", requests[1].Message.TaskId);
            Assert.Equal("context-1", requests[1].Message.ContextId);
            Assert.All(requests, request => Assert.Equal(receiver.NotificationUri.AbsoluteUri, request.Configuration!.TaskPushNotificationConfig!.Url));
            Assert.Equal("reply: push callback", push.Task!.Status.Message!.Parts[0].Text);
            Assert.Contains("preserved history", output.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("Request failed", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(attachment);
        }
    }

    [Fact]
    public async Task GrpcStreaming_CallerCancellation_ReleasesServerCall()
    {
        await using var host = await GrpcTestHost.StartAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Handler.Setup(value => value.SendStreamingMessageAsync(
                It.IsAny<SendMessageRequest>(), It.IsAny<A2ARequestContext>(), It.IsAny<CancellationToken>()))
            .Returns<SendMessageRequest, A2ARequestContext?, CancellationToken>((_, _, token) => WaitForCancellationAsync(entered, exited, token));
        using var httpClient = host.CreateHttpClient();
        using A2AClientConnection connection = Program.CreateClient(host.CreateCard(), httpClient, host.Address);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using IAsyncEnumerator<StreamResponse> stream = connection.Client
            .SendStreamingMessageAsync(CreateRequest("cancel"), cancellation.Token).GetAsyncEnumerator(cancellation.Token);

        Task<bool> next = stream.MoveNextAsync().AsTask();
        await entered.Task.WaitAsync(cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => next);
        await exited.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GrpcRequests_ResolveAuthenticationForEveryCall()
    {
        await using var host = await GrpcTestHost.StartAsync();
        var session = new A2AAuthenticationSession();
        var tokens = new Mock<IA2AAccessTokenProvider>();
        tokens.Setup(value => value.GetAccessTokenAsync(
                It.IsAny<A2AAgentCardAuthentication>(), It.IsAny<CancellationToken>()))
            .Returns<A2AAgentCardAuthentication?, CancellationToken>((authentication, _) =>
                Task.FromResult(authentication is null ? null : $"{authentication.Mode}-agent-token"));
        using var httpClient = host.CreateHttpClient(session, tokens.Object);
        using A2AClientConnection connection = Program.CreateClient(host.CreateCard(), httpClient, host.Address);
        IA2AClient client = connection.Client;

        session.SetAutomaticAuthentication(CreateAuthentication(A2AAuthMode.Delegated));
        await client.SendMessageAsync(CreateRequest("delegated"));
        session.SetMode(A2AAuthMode.None);
        await client.SendMessageAsync(CreateRequest("anonymous"));
        session.SetAutomaticAuthentication(CreateAuthentication(A2AAuthMode.App));
        await client.SendMessageAsync(CreateRequest("application"));

        Assert.Equal(
            ["Bearer Delegated-agent-token", "", "Bearer App-agent-token"],
            host.Requests.Select(request => request.Authorization));
    }

    [Fact]
    public async Task GrpcAuthResponse_UsesEnvelopeAndSeparateCredentialsForConsecutiveChallenges()
    {
        await using var host = await GrpcTestHost.StartAsync();
        host.Authorization.NextTask = call => call == 1 ? CreateAuthRequiredTask("auth-2") : CreateTask("authorized");
        using var httpClient = host.CreateHttpClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "agent-api-token");
        var tokens = new Mock<IA2AAccessTokenProvider>();
        tokens.Setup(value => value.GetAccessTokenAsync(
                It.IsAny<A2AAgentCardAuthentication>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("task-delegated-token");
        using A2AClientConnection connection = Program.CreateClient(host.CreateCard(), httpClient, host.Address);
        var client = A2AInTaskAuthorizationClient.Create(connection, httpClient, tokens.Object);

        AgentTask? result = await client.ResumeIfRequiredAsync(CreateAuthRequiredTask("auth-1"), CancellationToken.None);

        Assert.Equal(TaskState.Completed, result!.Status.State);
        Assert.Equal(2, host.Authorization.Calls.Count);
        foreach (AuthorizationCall call in host.Authorization.Calls)
        {
            Assert.Equal(A2AInTaskAuthorizationClient.ExtensionUri + "#auth-response", call.OperationId);
            Assert.Equal("Bearer agent-api-token", call.AgentAuthorization);
            Assert.Equal("task-delegated-token", call.TaskAuthorization);
            Assert.Equal(A2AInTaskAuthorizationClient.ExtensionUri, call.Extensions);
            Assert.Equal("1.0", call.Version);
            Assert.DoesNotContain("token", call.Payload, StringComparison.OrdinalIgnoreCase);
        }
        using JsonDocument first = JsonDocument.Parse(host.Authorization.Calls[0].Payload);
        using JsonDocument second = JsonDocument.Parse(host.Authorization.Calls[1].Payload);
        Assert.Equal("task-1", first.RootElement.GetProperty("taskId").GetString());
        Assert.Equal("context-1", first.RootElement.GetProperty("contextId").GetString());
        Assert.Equal("auth-1", first.RootElement.GetProperty("authorizationRequestId").GetString());
        Assert.Equal("auth-2", second.RootElement.GetProperty("authorizationRequestId").GetString());
    }

    [Fact]
    public async Task GrpcAuthResponse_RepeatedChallenge_IsNotSubmittedTwice()
    {
        await using var host = await GrpcTestHost.StartAsync();
        host.Authorization.NextTask = _ => CreateAuthRequiredTask("auth-1");
        using var httpClient = host.CreateHttpClient();
        var tokens = new Mock<IA2AAccessTokenProvider>();
        tokens.Setup(value => value.GetAccessTokenAsync(
                It.IsAny<A2AAgentCardAuthentication>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("task-delegated-token");
        using A2AClientConnection connection = Program.CreateClient(host.CreateCard(), httpClient, host.Address);
        var client = A2AInTaskAuthorizationClient.Create(connection, httpClient, tokens.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ResumeIfRequiredAsync(CreateAuthRequiredTask("auth-1"), CancellationToken.None));

        Assert.Single(host.Authorization.Calls);
    }

    [Fact]
    public async Task GrpcAuthResponse_RpcFailure_IsRecoverableWithoutSensitiveDiagnostics()
    {
        await using var host = await GrpcTestHost.StartAsync();
        host.Handler.Setup(value => value.SendMessageAsync(
                It.IsAny<SendMessageRequest>(), It.IsAny<A2ARequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendMessageResponse { Task = CreateAuthRequiredTask("auth-1") });
        host.Authorization.Failure = new RpcException(
            new Status(StatusCode.PermissionDenied, "sensitive server detail"),
            new Metadata { { "sensitive-metadata", "task-delegated-token" } });
        using var httpClient = host.CreateHttpClient();
        var tokens = new Mock<IA2AAccessTokenProvider>();
        tokens.Setup(value => value.GetAccessTokenAsync(
                It.IsAny<A2AAgentCardAuthentication>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("task-delegated-token");
        AgentCard card = host.CreateCard(streaming: false);
        using A2AClientConnection connection = Program.CreateClient(card, httpClient, host.Address);
        var authorization = A2AInTaskAuthorizationClient.Create(connection, httpClient, tokens.Object);
        var output = new StringWriter();
        var console = new A2AConsole(connection.Client, card, new A2AAuthenticationSession(),
            new StringReader("hello\n\n:auth none\n:q\n"), output, false, null, authorization);

        int exitCode = await console.RunAsync(CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("PermissionDenied", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Authentication mode: none", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive server detail", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("task-delegated-token", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GrpcAuthResponse_CallerCancellation_RemainsCancellation()
    {
        await using var host = await GrpcTestHost.StartAsync();
        host.Authorization.WaitForCancellation = true;
        using var httpClient = host.CreateHttpClient();
        var tokens = new Mock<IA2AAccessTokenProvider>();
        tokens.Setup(value => value.GetAccessTokenAsync(
                It.IsAny<A2AAgentCardAuthentication>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("task-delegated-token");
        using A2AClientConnection connection = Program.CreateClient(host.CreateCard(), httpClient, host.Address);
        var client = A2AInTaskAuthorizationClient.Create(connection, httpClient, tokens.Object);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        Task<AgentTask?> pending = client.ResumeIfRequiredAsync(CreateAuthRequiredTask("auth-1"), cancellation.Token);
        await host.Authorization.Entered.Task.WaitAsync(cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    private static SendMessageRequest CreateRequest(string text) => new()
    {
        Message = new Message
        {
            MessageId = Guid.NewGuid().ToString("N"),
            Role = Role.User,
            Parts = [Part.FromText(text)],
        },
    };

    private static AgentTask CreateTask(string text) => new()
    {
        Id = "task-1",
        ContextId = "context-1",
        Status = new global::A2A.TaskStatus
        {
            State = TaskState.Completed,
            Message = new Message { Role = Role.Agent, Parts = [Part.FromText($"reply: {text}")] },
        },
    };

    private static AgentTask CreateAuthRequiredTask(string requestId)
    {
        AgentTask task = CreateTask("authorization required");
        task.Status.State = TaskState.AuthRequired;
        task.Status.Message!.Metadata = new Dictionary<string, JsonElement>
        {
            [A2AInTaskAuthorizationClient.ExtensionUri] = JsonSerializer.SerializeToElement(new
            {
                authorizationRequest = new
                {
                    id = requestId,
                    oauth2 = new
                    {
                        flows = new
                        {
                            deviceCode = new
                            {
                                deviceAuthorizationUrl = "https://identity.example/device",
                                tokenUrl = "https://identity.example/token",
                                scopes = new Dictionary<string, string> { ["resource.read"] = "Read the resource" },
                            },
                        },
                    },
                    requiredScopes = new[] { "resource.read" },
                },
            }),
        };
        return task;
    }

    private static A2AAgentCardAuthentication CreateAuthentication(A2AAuthMode mode)
    {
        var card = new AgentCard
        {
            SecuritySchemes = new Dictionary<string, SecurityScheme>
            {
                ["agent"] = new()
                {
                    OAuth2SecurityScheme = new OAuth2SecurityScheme
                    {
                        Flows = new OAuthFlows
                        {
                            DeviceCode = new() { DeviceAuthorizationUrl = "https://identity.example/device", TokenUrl = "https://identity.example/token" },
                            ClientCredentials = new() { TokenUrl = "https://identity.example/token" },
                        },
                    },
                },
            },
            SecurityRequirements = [new SecurityRequirement { Schemes = new Dictionary<string, StringList> { ["agent"] = new() { List = ["agent.read"] } } }],
        };
        return A2AAgentCardAuthentication.Select(card, mode);
    }

    private static async IAsyncEnumerable<StreamResponse> ToStreamAsync(Task<SendMessageResponse> response)
    {
        yield return new StreamResponse { Task = (await response).Task };
    }

    private static async IAsyncEnumerable<StreamResponse> WaitForCancellationAsync(
        TaskCompletionSource entered,
        TaskCompletionSource exited,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        entered.TrySetResult();
        try
        {
            await new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task.WaitAsync(cancellationToken);
        }
        finally
        {
            exited.TrySetResult();
        }
        yield break;
    }

    private sealed class GrpcTestHost : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly X509Certificate2? _certificate;

        private GrpcTestHost(WebApplication app, X509Certificate2? certificate, Mock<IA2ARequestHandler> handler, AuthorizationService authorization)
        {
            _app = app;
            _certificate = certificate;
            Handler = handler;
            Authorization = authorization;
        }

        public Uri Address => new(_app.Urls.Single());
        public Mock<IA2ARequestHandler> Handler { get; }
        public AuthorizationService Authorization { get; }
        public ConcurrentQueue<ReceivedRequest> Requests { get; } = new();

        public static async Task<GrpcTestHost> StartAsync(bool useTls = false)
        {
            X509Certificate2? certificate = null;
            if (useTls)
            {
                using RSA key = RSA.Create(2048);
                var request = new CertificateRequest("CN=localhost", key, System.Security.Cryptography.HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                var alternatives = new SubjectAlternativeNameBuilder();
                alternatives.AddIpAddress(IPAddress.Loopback);
                request.CertificateExtensions.Add(alternatives.Build());
                using X509Certificate2 generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
                certificate = X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx), null);
            }

            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0, listen =>
            {
                listen.Protocols = HttpProtocols.Http2;
                if (certificate is not null)
                {
                    listen.UseHttps(certificate);
                }
            }));
            var handler = new Mock<IA2ARequestHandler>(MockBehavior.Strict);
            handler.Setup(value => value.SendMessageAsync(
                    It.IsAny<SendMessageRequest>(), It.IsAny<A2ARequestContext>(), It.IsAny<CancellationToken>()))
                .Returns<SendMessageRequest, A2ARequestContext?, CancellationToken>((request, _, _) =>
                    Task.FromResult(new SendMessageResponse { Task = CreateTask(request.Message.Parts[0].Text!) }));
            handler.Setup(value => value.SendStreamingMessageAsync(
                    It.IsAny<SendMessageRequest>(), It.IsAny<A2ARequestContext>(), It.IsAny<CancellationToken>()))
                .Returns<SendMessageRequest, A2ARequestContext?, CancellationToken>((request, _, token) => StreamAsync(request, token));
            handler.Setup(value => value.GetTaskAsync(
                    It.IsAny<GetTaskRequest>(), It.IsAny<A2ARequestContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateTask("history"));
            var authorization = new AuthorizationService();
            builder.Services.AddSingleton(handler.Object);
            builder.Services.AddSingleton(authorization);
            builder.Services.AddA2AGrpc();
            WebApplication app = builder.Build();
            var host = new GrpcTestHost(app, certificate, handler, authorization);
            app.Use(async (context, next) =>
            {
                host.Requests.Enqueue(new ReceivedRequest(
                    context.Request.Path,
                    context.Request.Protocol,
                    context.Request.Headers.Authorization.ToString(),
                    context.Request.Headers["a2a-version"].ToString(),
                    context.Request.Headers["a2a-extensions"].ToString()));
                await next(context);
            });
            app.MapGet("/.well-known/agent-card.json", () => Results.Json(host.CreateCard(), A2AJsonUtilities.DefaultOptions));
            app.MapGrpcA2A();
            app.MapGrpcService<AuthorizationService>();
            try
            {
                await app.StartAsync();
                return host;
            }
            catch
            {
                await host.DisposeAsync();
                throw;
            }
        }

        public AgentCard CreateCard(bool streaming = true) => new()
        {
            Name = "gRPC test agent",
            Capabilities = new AgentCapabilities { Streaming = streaming },
            SupportedInterfaces = [new AgentInterface { ProtocolBinding = ProtocolBindingNames.Grpc, Url = Address.AbsoluteUri, ProtocolVersion = "1.0" }],
        };

        public HttpClient CreateHttpClient(A2AAuthenticationSession? session = null, IA2AAccessTokenProvider? tokens = null)
        {
            var inner = new HttpClientHandler { AllowAutoRedirect = false };
            if (_certificate is not null)
            {
                inner.ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
                    certificate?.Thumbprint == _certificate.Thumbprint;
            }
            HttpMessageHandler handler = session is null
                ? inner
                : new AuthenticatedA2AHttpHandler(session, tokens!, Address, inner);
            return Program.CreateAgentHttpClient(handler, Address, A2ATransport.Grpc);
        }

        public async ValueTask DisposeAsync()
        {
            await _app.DisposeAsync();
            _certificate?.Dispose();
        }

        private static async IAsyncEnumerable<StreamResponse> StreamAsync(
            SendMessageRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new StreamResponse { Task = CreateTask(request.Message.Parts[0].Text!) };
            await Task.CompletedTask;
        }
    }

    public sealed class AuthorizationService : A2AExtensionService.A2AExtensionServiceBase
    {
        internal List<AuthorizationCall> Calls { get; } = [];
        internal Func<int, AgentTask> NextTask { get; set; } = _ => CreateTask("authorized");
        internal RpcException? Failure { get; set; }
        internal bool WaitForCancellation { get; set; }
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task<ExtensionOperationResponse> InvokeExtensionOperation(
            ExtensionOperationRequest request, ServerCallContext context)
        {
            Calls.Add(new AuthorizationCall(
                request.OperationId,
                request.Payload.ToStringUtf8(),
                context.RequestHeaders.GetValue("authorization") ?? "",
                context.RequestHeaders.GetValue(A2AInTaskAuthorizationClient.TokenHeader) ?? "",
                context.RequestHeaders.GetValue("a2a-extensions") ?? "",
                context.RequestHeaders.GetValue("a2a-version") ?? ""));
            Entered.TrySetResult();
            if (WaitForCancellation)
            {
                await new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task.WaitAsync(context.CancellationToken);
            }
            if (Failure is not null)
            {
                throw Failure;
            }
            return new ExtensionOperationResponse
            {
                Payload = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(NextTask(Calls.Count), A2AJsonUtilities.DefaultOptions)),
            };
        }
    }

    internal sealed record AuthorizationCall(string OperationId, string Payload, string AgentAuthorization, string TaskAuthorization, string Extensions, string Version);
    private sealed record ReceivedRequest(string Path, string Protocol, string Authorization, string Version, string Extensions);
}
