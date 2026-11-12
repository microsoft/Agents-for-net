// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using A2A.Grpc;
using A2A.Grpc.Extensions.Protos;
using Google.Protobuf;
using Empty = Google.Protobuf.WellKnownTypes.Empty;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.A2A.AgentCard;
using Microsoft.Agents.Extensions.A2A.Authorization;
using Microsoft.Agents.Extensions.A2A.Pipeline;
using Microsoft.Agents.Extensions.A2A.ProtocolExtensions.InTaskAuthorization;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ProtocolAgentCard = A2A.AgentCard;
using Proto = A2A.Grpc.Protos;

namespace Microsoft.Agents.Extensions.A2A.Tests;

public class A2AGrpcHostingTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
    private static readonly AgentSpec AllBindings = new("GrpcAgent",
        (A2AAgentTransportProtocol.JsonRpc, "/a2a"),
        (A2AAgentTransportProtocol.HttpJson, "/a2a"),
        (A2AAgentTransportProtocol.Grpc, "/"));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AttributeMappedGrpc_UsesNativeHttp2AndSharesTasksWithHttp(bool tls)
    {
        await using var host = new GrpcHost([AllBindings], tls: tls);
        await host.StartAsync();
        using var client = host.CreateClient();
        using var timeout = new CancellationTokenSource(Timeout);

        var sent = await client.SendMessageAsync(Request("unary"), timeout.Token);
        Assert.Equal(TaskState.Completed, sent.Task.Status.State);
        Assert.Equal("GrpcAgent:anonymous:none", sent.Task.Status.Message.Parts[0].Text);

        var httpTask = await host.GetHttpTaskAsync(sent.Task.Id);
        Assert.Equal(sent.Task.Id, httpTask.Id);
        Assert.Equal(TaskState.Completed, httpTask.Status.State);

        using var httpResponse = await host.Http.PostAsync("/a2a/message:send",
            JsonContent(Request("http")), timeout.Token);
        httpResponse.EnsureSuccessStatusCode();
        var httpSent = JsonSerializer.Deserialize<SendMessageResponse>(
            await httpResponse.Content.ReadAsStringAsync(timeout.Token), A2AJsonUtilities.DefaultOptions);
        var retrieved = await client.GetTaskAsync(new GetTaskRequest { Id = httpSent.Task.Id }, timeout.Token);
        Assert.Equal("GrpcAgent:anonymous:none", retrieved.Status.Message.Parts[0].Text);

        var events = new List<StreamResponse>();
        await foreach (var item in client.SendStreamingMessageAsync(Request("stream"), timeout.Token))
        {
            events.Add(item);
        }
        Assert.Contains(events, item => item.StatusUpdate?.Status.State == TaskState.Completed);
        await AssertRequestReleasedAsync(host, host.Scenario.For("stream"));

        var card = await host.GetCardAsync();
        Assert.Equal(["JSONRPC", "HTTP+JSON", "GRPC"],
            card.SupportedInterfaces.Select(item => item.ProtocolBinding));
        var grpc = Assert.Single(card.SupportedInterfaces, item => item.ProtocolBinding == "GRPC");
        Assert.Equal(host.Http.BaseAddress.AbsoluteUri, grpc.Url);
        Assert.Equal("1.0", grpc.ProtocolVersion);
        Assert.False(card.Capabilities.ExtendedAgentCard);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://grpc.example.com:7443/")]
    public async Task GrpcOnly_PublicHttpDiscoveryComposesTargetBeforeCustomizationAndSigning(string grpcUrl)
    {
        await using var host = new GrpcHost(
            [new AgentSpec("GrpcOnly", (A2AAgentTransportProtocol.Grpc, "/"))],
            requireAuth: true,
            grpcUrl: grpcUrl);
        host.Scenario.SignCard = true;
        await host.StartAsync();

        var card = await host.GetCardAsync();

        var grpc = Assert.Single(card.SupportedInterfaces);
        Assert.Equal("GRPC", grpc.ProtocolBinding);
        Assert.Equal(grpcUrl ?? host.Http.BaseAddress.AbsoluteUri, grpc.Url);
        Assert.Equal("customized", card.Name);
        var signature = Assert.Single(card.Signatures);
        card.Signatures = null;
        Assert.True(host.Scenario.SigningKey.VerifyData(
            SigningInput(card, signature.Protected),
            Convert.FromBase64String(signature.Signature),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1));

        using var client = host.CreateClient("valid-alice");
        var exception = await Assert.ThrowsAsync<A2AException>(() =>
            client.GetExtendedAgentCardAsync(new GetExtendedAgentCardRequest()));
        Assert.Equal(A2AErrorCode.UnsupportedOperation, exception.ErrorCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentAuthenticatedCalls_UseDeclaredAgentAndValidatedTicket(bool streaming)
    {
        await using var host = new GrpcHost(
        [
            new AgentSpec("HttpAgent", (A2AAgentTransportProtocol.JsonRpc, "/a2a")),
            new AgentSpec("GrpcAgent", (A2AAgentTransportProtocol.Grpc, "/")),
        ], requireAuth: true);
        var first = host.Scenario.For("first");
        first.Block = true;
        await host.StartAsync();
        using var alice = host.CreateClient("valid-alice");
        using var bob = host.CreateClient("valid-bob");
        using var timeout = new CancellationTokenSource(Timeout);
        async Task<AgentTask> SendFirstAsync()
        {
            if (!streaming)
            {
                return (await alice.SendMessageAsync(Request("first"), timeout.Token)).Task;
            }

            await foreach (var _ in alice.SendStreamingMessageAsync(Request("first"), timeout.Token))
            {
            }
            return await alice.GetTaskAsync(
                new GetTaskRequest { Id = first.TaskId }, timeout.Token);
        }

        var pending = SendFirstAsync();
        try
        {
            await first.Started.Task.WaitAsync(Timeout);
            Assert.False(first.Lifetime.Disposed.Task.IsCompleted);

            var second = await bob.SendMessageAsync(Request("second"), timeout.Token);
            Assert.Equal("GrpcAgent:bob:valid-bob", second.Task.Status.Message.Parts[0].Text);
            Assert.Equal("GrpcAgent", first.AgentType);
            Assert.Equal("alice", first.Identity);
            Assert.Equal("valid-alice", first.AccessToken);
        }
        finally
        {
            first.Release.TrySetResult();
        }

        Assert.Equal("GrpcAgent:alice:valid-alice",
            (await pending).Status.Message.Parts[0].Text);
        await AssertRequestReleasedAsync(host, first);
        Assert.Equal("GRPC", Assert.Single((await host.GetCardAsync()).SupportedInterfaces).ProtocolBinding);

        using var ticketClient = host.CreateClient("attacker-supplied-token", ticketOnly: true);
        var ticketResult = await ticketClient.SendMessageAsync(Request("ticket-only"), timeout.Token);
        Assert.Equal("GrpcAgent:ticket-user:none", ticketResult.Task.Status.Message.Parts[0].Text);

        using var anonymous = host.CreateClient();
        var rejected = await Assert.ThrowsAsync<A2AException>(() =>
            anonymous.SendMessageAsync(Request("unauthorized"), timeout.Token));
        Assert.Equal(StatusCode.Unauthenticated, Assert.IsType<RpcException>(rejected.InnerException).StatusCode);
        Assert.False(host.Scenario.For("unauthorized").Started.Task.IsCompleted);
    }

    [Fact]
    public async Task StreamingAndSubscription_KeepRequestScopesUntilCompletion()
    {
        await using var host = new GrpcHost([AllBindings]);
        var execution = host.Scenario.For("blocked");
        execution.Block = true;
        await host.StartAsync();
        using var client = host.CreateClient();
        using var timeout = new CancellationTokenSource(Timeout);
        await using var stream = client.SendStreamingMessageAsync(Request("blocked"), timeout.Token)
            .GetAsyncEnumerator(timeout.Token);
        Assert.True(await stream.MoveNextAsync());
        await execution.Started.Task.WaitAsync(Timeout);
        Assert.False(execution.Lifetime.Disposed.Task.IsCompleted);

        await using var subscription = client.SubscribeToTaskAsync(
            new SubscribeToTaskRequest { Id = execution.TaskId }, timeout.Token)
            .GetAsyncEnumerator(timeout.Token);
        Assert.True(await subscription.MoveNextAsync());
        var subscriptionLifetime = host.Scenario.AgentRequests.Last();
        Assert.NotSame(execution.Lifetime, subscriptionLifetime);
        Assert.False(subscriptionLifetime.Disposed.Task.IsCompleted);

        execution.Release.TrySetResult();
        await DrainAsync(stream);
        await DrainAsync(subscription);
        Assert.Equal(TaskState.Completed, (await host.GetHttpTaskAsync(execution.TaskId)).Status.State);
        await AssertRequestReleasedAsync(host, execution);
        await AssertRequestReleasedAsync(host, subscriptionLifetime);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrossBindingCancellation_StopsTheSharedServersActiveExecution(bool cancelWithGrpc)
    {
        await using var host = new GrpcHost([AllBindings]);
        var execution = host.Scenario.For("cross-binding-cancel");
        execution.Block = true;
        await host.StartAsync();
        using var client = host.CreateClient();
        using var timeout = new CancellationTokenSource(Timeout);
        var request = new SendMessageRequest
        {
            Message = Request("cross-binding-cancel").Message,
            Configuration = new SendMessageConfiguration { ReturnImmediately = true },
        };
        AgentTask task;
        if (cancelWithGrpc)
        {
            using var response = await host.Http.PostAsync("/a2a/message:send",
                JsonContent(request), timeout.Token);
            response.EnsureSuccessStatusCode();
            task = JsonSerializer.Deserialize<SendMessageResponse>(
                await response.Content.ReadAsStringAsync(timeout.Token), A2AJsonUtilities.DefaultOptions).Task;
        }
        else
        {
            task = (await client.SendMessageAsync(request, timeout.Token)).Task;
        }
        await execution.Started.Task.WaitAsync(Timeout);

        if (cancelWithGrpc)
        {
            Assert.Equal(TaskState.Canceled,
                (await client.CancelTaskAsync(new CancelTaskRequest { Id = task.Id }, timeout.Token)).Status.State);
        }
        else
        {
            using var response = await host.Http.PostAsync(
                $"/a2a/tasks/{task.Id}:cancel", JsonContent(new { }), timeout.Token);
            response.EnsureSuccessStatusCode();
        }

        await execution.CancellationObserved.Task.WaitAsync(Timeout);
        Assert.Equal(TaskState.Canceled, (await host.GetHttpTaskAsync(task.Id)).Status.State);
        await AssertRequestReleasedAsync(host, execution);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledGrpcCall_ReleasesRequestScope(bool streaming)
    {
        await using var host = new GrpcHost([AllBindings]);
        var execution = host.Scenario.For("cancelled");
        execution.Block = true;
        await host.StartAsync();
        using var client = host.CreateClient();
        using var timeout = new CancellationTokenSource(Timeout);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);

        async Task InvokeAsync()
        {
            if (streaming)
            {
                await foreach (var _ in client.SendStreamingMessageAsync(Request("cancelled"), cancellation.Token))
                {
                }
            }
            else
            {
                await client.SendMessageAsync(Request("cancelled"), cancellation.Token);
            }
        }

        var call = InvokeAsync();
        try
        {
            await execution.Started.Task.WaitAsync(Timeout);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
            await AssertRequestReleasedAsync(host, execution);
        }
        finally
        {
            execution.Release.TrySetResult();
        }
    }

    [Fact]
    public async Task DisposingGrpcStream_ReleasesScopeAndDoesNotContaminateTheNextCall()
    {
        await using var host = new GrpcHost([AllBindings]);
        var execution = host.Scenario.For("disposed-stream");
        execution.Block = true;
        await host.StartAsync();
        using var client = host.CreateClient();
        using var timeout = new CancellationTokenSource(Timeout);
        var stream = client.SendStreamingMessageAsync(Request("disposed-stream"), timeout.Token)
            .GetAsyncEnumerator(timeout.Token);
        try
        {
            Assert.True(await stream.MoveNextAsync());
            await execution.Started.Task.WaitAsync(Timeout);
            Assert.False(execution.Lifetime.Disposed.Task.IsCompleted);

            await stream.DisposeAsync();

            await AssertRequestReleasedAsync(host, execution);
            Assert.Equal(TaskState.Completed,
                (await client.SendMessageAsync(Request("after-disposal"), timeout.Token)).Task.Status.State);
        }
        finally
        {
            execution.Release.TrySetResult();
            await stream.DisposeAsync();
        }
    }

    [Fact]
    public async Task HostDisposal_CancelsTheSharedServersDetachedExecution()
    {
        await using var host = new GrpcHost([AllBindings]);
        var execution = host.Scenario.For("detached");
        execution.Block = true;
        await host.StartAsync();
        using var client = host.CreateClient();
        await client.SendMessageAsync(new SendMessageRequest
        {
            Message = Request("detached").Message,
            Configuration = new SendMessageConfiguration { ReturnImmediately = true },
        });
        await execution.Started.Task.WaitAsync(Timeout);

        await host.DisposeAsync();

        Assert.True(execution.CancellationObserved.Task.IsCompletedSuccessfully);
        Assert.True(execution.Completed.Task.IsCompletedSuccessfully);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GrpcAuthResponse_UsesSharedRegistryAndResumesTheSameTask(bool streaming)
    {
        await using var host = new GrpcHost([AllBindings], inTaskAuthorization: true);
        await host.StartAsync();
        using var client = host.CreateClient(extensions: true);
        using var timeout = new CancellationTokenSource(Timeout);
        AgentTask initial;
        if (streaming)
        {
            string taskId = null;
            await foreach (var item in client.SendStreamingMessageAsync(Request("protected"), timeout.Token))
            {
                taskId ??= item.Task?.Id;
            }
            initial = await client.GetTaskAsync(new GetTaskRequest { Id = taskId }, timeout.Token);
        }
        else
        {
            initial = (await client.SendMessageAsync(Request("protected"), timeout.Token)).Task;
        }
        Assert.Equal(TaskState.AuthRequired, initial.Status.State);
        var requestId = initial.Status.Message.Metadata[InTaskAuthorizationExtension.Uri]
            .GetProperty("authorizationRequest").GetProperty("id").GetString();
        using var channel = host.CreateChannel();
        var extensions = new A2AExtensionService.A2AExtensionServiceClient(channel);
        var headers = ExtensionHeaders();
        headers.Add(InTaskAuthorizationExtension.TokenHeader, "delegated-token");
        using var call = extensions.InvokeExtensionOperationAsync(new ExtensionOperationRequest
        {
            OperationId = InTaskAuthorizationExtension.Uri + "#auth-response",
            Payload = ByteString.CopyFromUtf8(JsonSerializer.Serialize(new
            {
                taskId = initial.Id,
                contextId = initial.ContextId,
                authorizationRequestId = requestId,
            })),
        }, headers, cancellationToken: timeout.Token);

        var response = await call.ResponseAsync;
        var resumed = JsonSerializer.Deserialize<AgentTask>(
            response.Payload.ToStringUtf8(), A2AJsonUtilities.DefaultOptions);
        Assert.Equal(initial.Id, resumed.Id);
        Assert.Equal(TaskState.Completed, resumed.Status.State);
        Assert.Equal("protected:delegated-token", resumed.Status.Message.Parts[0].Text);
        Assert.Equal(InTaskAuthorizationExtension.Uri,
            (await call.ResponseHeadersAsync).GetValue("a2a-extensions"));
        Assert.Equal(InTaskAuthorizationExtension.Uri, call.GetTrailers().GetValue("a2a-extensions"));
        Assert.Equal(TaskState.Completed,
            (await host.GetHttpTaskAsync(initial.Id)).Status.State);
    }

    [Theory]
    [InlineData("version", StatusCode.FailedPrecondition, "VERSION_NOT_SUPPORTED", false)]
    [InlineData("missing-task", StatusCode.NotFound, "TASK_NOT_FOUND", true)]
    [InlineData("operation", StatusCode.NotFound, "TASK_NOT_FOUND", true)]
    [InlineData("inactive-operation", StatusCode.FailedPrecondition, "EXTENSION_SUPPORT_REQUIRED", false)]
    public async Task GrpcFailures_PreserveUpstreamRichTrailersAndActivation(
        string failure, StatusCode status, string reason, bool activated)
    {
        await using var host = new GrpcHost([AllBindings]);
        await host.StartAsync();
        using var channel = host.CreateChannel();
        var headers = ExtensionHeaders();
        RpcException exception;
        if (failure.EndsWith("operation", StringComparison.Ordinal))
        {
            if (failure == "inactive-operation")
            {
                headers = new Metadata { { "a2a-version", "1.0" } };
            }
            headers.Add(InTaskAuthorizationExtension.TokenHeader, "delegated-token");
            var extensions = new A2AExtensionService.A2AExtensionServiceClient(channel);
            using var call = extensions.InvokeExtensionOperationAsync(new ExtensionOperationRequest
            {
                OperationId = InTaskAuthorizationExtension.Uri + "#auth-response",
                Payload = ByteString.CopyFromUtf8(
                    """{"taskId":"missing","contextId":"context","authorizationRequestId":"request"}"""),
            }, headers);
            exception = await Assert.ThrowsAsync<RpcException>(() => call.ResponseAsync);
        }
        else
        {
            if (failure == "version")
            {
                headers.Add("a2a-version", "2.0");
            }
            var standard = new Proto.A2AService.A2AServiceClient(channel);
            using var call = standard.GetTaskAsync(new Proto.GetTaskRequest { Id = "missing" }, headers);
            exception = await Assert.ThrowsAsync<RpcException>(() => call.ResponseAsync);
        }

        Assert.Equal(status, exception.StatusCode);
        var rich = Google.Rpc.Status.Parser.ParseFrom(
            exception.Trailers.Get("grpc-status-details-bin").ValueBytes);
        var detail = Assert.Single(rich.Details).Unpack<Google.Rpc.ErrorInfo>();
        Assert.Equal("a2a-protocol.org", detail.Domain);
        Assert.Equal(reason, detail.Reason);
        Assert.Equal(activated ? InTaskAuthorizationExtension.Uri : null,
            exception.Trailers.GetValue("a2a-extensions"));
    }

    [Theory]
    [InlineData("/prefix")]
    [InlineData("/{tenant}")]
    public async Task GrpcAttributePrefix_IsRejectedAtStartup(string path)
    {
        await using var host = new GrpcHost([new AgentSpec("GrpcAgent", (A2AAgentTransportProtocol.Grpc, path))]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());

        Assert.Contains("root", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/prefix", false)]
    [InlineData("/prefix", true)]
    public async Task GrpcInsidePrefixedRouteGroup_IsRejectedAtStartup(string prefix, bool nested)
    {
        await using var host = new GrpcHost([AllBindings], prefix: prefix, nestedGroup: nested);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());

        Assert.Contains("root", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("attributes")]
    [InlineData("agents")]
    [InlineData("mapping")]
    [InlineData("upstream-before")]
    [InlineData("upstream-after")]
    [InlineData("trailing-slash")]
    [InlineData("no-leading-slash")]
    public async Task CompetingRootGrpcMappings_AreRejectedAtStartup(string collision)
    {
        var agents = collision switch
        {
            "attributes" => new[] { new AgentSpec("GrpcAgent", (A2AAgentTransportProtocol.Grpc, "/"), (A2AAgentTransportProtocol.Grpc, "/")) },
            "agents" => new[] { AllBindings, new AgentSpec("OtherGrpcAgent", (A2AAgentTransportProtocol.Grpc, "/")) },
            _ => [AllBindings],
        };
        await using var host = new GrpcHost(agents, collision: collision);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());

        Assert.Contains("gRPC", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("root", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("grpc.example.com")]
    [InlineData("https://grpc.example.com/a2a")]
    [InlineData("ftp://grpc.example.com/")]
    [InlineData("https://grpc.example.com/?query=1")]
    [InlineData("https://user:password@grpc.example.com/")]
    public async Task InvalidAdvertisedGrpcAddress_IsRejectedAtStartup(string address)
    {
        await using var host = new GrpcHost([AllBindings], grpcUrl: address);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => host.StartAsync());

        Assert.Contains("root", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OtherGrpcServices_DoNotRequireAnAgentOrAdapterRequestContext()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.WebHost.ConfigureKestrel(options =>
            options.ConfigureEndpointDefaults(listen => listen.Protocols = HttpProtocols.Http2));
        builder.Services.AddA2AAdapter();
        await using var app = builder.Build();
        app.MapGrpcService<OtherGrpcService>();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>().Addresses.Single();
        using var channel = GrpcChannel.ForAddress(address);
        using var call = channel.CreateCallInvoker().AsyncUnaryCall(
            OtherGrpcService.PingMethod, null, new CallOptions(), new Empty());

        Assert.NotNull(await call.ResponseAsync);
    }

    [Fact]
    public async Task NoInterfaceAttributes_PreserveJsonRpcDefault()
    {
        await using var host = new GrpcHost([new AgentSpec("DefaultAgent")]);
        await host.StartAsync();
        using var response = await host.Http.PostAsync("/a2a", JsonContent(new
        {
            jsonrpc = "2.0",
            id = "default",
            method = A2AMethods.SendMessage,
            @params = Request("default"),
        }));
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(body.RootElement.TryGetProperty("error", out _));
        Assert.Equal("JSONRPC", Assert.Single((await host.GetCardAsync()).SupportedInterfaces).ProtocolBinding);
    }

    private static SendMessageRequest Request(string text) => new()
    {
        Message = new Message
        {
            MessageId = Guid.NewGuid().ToString("N"),
            Role = Role.User,
            ContextId = Guid.NewGuid().ToString("N"),
            Parts = [Part.FromText(text)],
        },
    };

    private static StringContent JsonContent(object value) =>
        new(JsonSerializer.Serialize(value, A2AJsonUtilities.DefaultOptions), Encoding.UTF8, "application/json");

    private static Metadata ExtensionHeaders() => new()
    {
        { "a2a-version", "1.0" },
        { "a2a-extensions", InTaskAuthorizationExtension.Uri },
    };

    private static async Task DrainAsync(IAsyncEnumerator<StreamResponse> stream)
    {
        while (await stream.MoveNextAsync())
        {
        }
    }

    private static Task AssertRequestReleasedAsync(GrpcHost host, CallState state) =>
        AssertRequestReleasedAsync(host, state.Lifetime);

    private static async Task AssertRequestReleasedAsync(GrpcHost host, CallLifetime lifetime)
    {
        await lifetime.Disposed.Task.WaitAsync(Timeout);
        using var turn = new TurnContext(host.App.Services.GetRequiredService<A2AAdapter>(),
            new Activity { RequestId = lifetime.RequestId });
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            turn.Adapter.SendActivitiesAsync(turn, [], CancellationToken.None));
    }

    private static byte[] SigningInput(ProtocolAgentCard card, string protectedHeader) =>
        Encoding.UTF8.GetBytes(protectedHeader + "." +
            Convert.ToBase64String(Encoding.UTF8.GetBytes(ProtocolJsonSerializer.ToJson(card)))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_'));

    public sealed class Scenario : IDisposable
    {
        private readonly ConcurrentDictionary<string, CallState> _calls = new();
        public RSA SigningKey { get; } = RSA.Create(2048);
        public bool SignCard { get; set; }
        public bool InTaskAuthorization { get; set; }
        public ConcurrentQueue<CallLifetime> AgentRequests { get; } = new();
        public CallState For(string text) => _calls.GetOrAdd(text, _ => new CallState());
        public void Dispose() => SigningKey.Dispose();
    }

    public sealed class CallState
    {
        public bool Block { get; set; }
        public string TaskId { get; set; }
        public string RequestId { get; set; }
        public string AgentType { get; set; }
        public string Identity { get; set; }
        public string AccessToken { get; set; }
        public CallLifetime Lifetime { get; set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public sealed class CallLifetime : IDisposable
    {
        public CallLifetime(IHttpContextAccessor accessor)
        {
            RequestId = accessor.HttpContext.TraceIdentifier;
        }

        public string RequestId { get; }
        public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Dispose() => Disposed.TrySetResult();
    }

    [BindServiceMethod(typeof(OtherGrpcService), nameof(OtherGrpcService.BindService))]
    public class OtherGrpcService
    {
        private static readonly Marshaller<Empty> EmptyMarshaller =
            Marshallers.Create<Empty>(value => value.ToByteArray(), value => Empty.Parser.ParseFrom(value));
        public static readonly Method<Empty, Empty> PingMethod =
            new(MethodType.Unary, "tests.OtherService", "Ping", EmptyMarshaller, EmptyMarshaller);

        public static void BindService(ServiceBinderBase binder, OtherGrpcService service) =>
            binder.AddMethod(PingMethod, service == null ? null : service.Ping);

        public virtual Task<Empty> Ping(Empty request, ServerCallContext context) => Task.FromResult(new Empty());
    }

    public class BoundaryAgent : AgentApplication, IAgentCardHandler
    {
        private readonly Scenario _scenario;
        private readonly CallLifetime _lifetime;

        public BoundaryAgent(AgentApplicationOptions options, Scenario scenario, CallLifetime lifetime) : base(options)
        {
            _scenario = scenario;
            _lifetime = lifetime;
            scenario.AgentRequests.Enqueue(lifetime);
            OnActivity(ActivityTypes.Message, OnMessageAsync,
                autoSignInHandlers: scenario.InTaskAuthorization ? ["request"] : null);
        }

        private async Task OnMessageAsync(ITurnContext context, ITurnState state, CancellationToken cancellationToken)
        {
            var call = _scenario.For(context.Activity.Text);
            var authentication = context.Services.Get<A2ARequestAuthentication>();
            call.AgentType = GetType().Name;
            call.Identity = authentication.Identity.Name ?? "anonymous";
            call.AccessToken = authentication.AccessToken;
            call.TaskId = context.Services.Get<RequestContext>().TaskId;
            call.RequestId = context.Activity.RequestId;
            call.Lifetime = _lifetime;
            call.Started.TrySetResult();
            try
            {
                if (call.Block)
                {
                    await call.Release.Task.WaitAsync(cancellationToken);
                }
                var text = _scenario.InTaskAuthorization
                    ? $"protected:{await UserAuthorization.GetTurnTokenAsync(context, "request", cancellationToken)}"
                    : $"{call.AgentType}:{call.Identity}:{call.AccessToken ?? "none"}";
                await context.SendActivityAsync(
                    new Activity { Type = ActivityTypes.EndOfConversation, Text = text },
                    cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                call.CancellationObserved.TrySetResult();
                throw;
            }
            finally
            {
                call.Completed.TrySetResult();
            }
        }

        public Task<ProtocolAgentCard> GetAgentCard(ProtocolAgentCard defaultCard)
        {
            if (_scenario.SignCard)
            {
                Assert.Contains(defaultCard.SupportedInterfaces, item => item.ProtocolBinding == "GRPC");
                defaultCard.Name = "customized";
                var header = Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"alg":"RS256"}"""))
                    .TrimEnd('=').Replace('+', '-').Replace('/', '_');
                defaultCard.Signatures =
                [
                    new AgentCardSignature
                    {
                        Protected = header,
                        Signature = Convert.ToBase64String(_scenario.SigningKey.SignData(
                            SigningInput(defaultCard, header), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)),
                    },
                ];
            }
            return Task.FromResult(defaultCard);
        }
    }

    private sealed record AgentSpec(string Name, params (string Protocol, string Path)[] Interfaces);

    private sealed class GrpcHost : IAsyncDisposable
    {
        private readonly AgentSpec[] _agents;
        private readonly bool _requireAuth;
        private readonly string _prefix;
        private readonly bool _nestedGroup;
        private readonly string _collision;
        private readonly string _grpcUrl;
        private readonly X509Certificate2 _certificate;
        private readonly List<HttpClient> _grpcHttpClients = [];
        private readonly List<GrpcChannel> _channels = [];
        private bool _disposed;
        public Scenario Scenario { get; } = new();
        public WebApplication App { get; private set; }
        public HttpClient Http { get; private set; }
        public Uri GrpcAddress { get; private set; }

        public GrpcHost(AgentSpec[] agents, bool tls = false, bool requireAuth = false,
            string grpcUrl = null, string prefix = null, bool nestedGroup = false,
            string collision = null, bool inTaskAuthorization = false)
        {
            _agents = agents;
            _requireAuth = requireAuth;
            _grpcUrl = grpcUrl;
            _prefix = prefix;
            _nestedGroup = nestedGroup;
            _collision = collision;
            Scenario.InTaskAuthorization = inTaskAuthorization;
            if (tls)
            {
                using var key = RSA.Create(2048);
                var request = new CertificateRequest("CN=localhost", key,
                    HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                var names = new SubjectAlternativeNameBuilder();
                names.AddIpAddress(IPAddress.Loopback);
                request.CertificateExtensions.Add(names.Build());
                using var generated = request.CreateSelfSigned(
                    DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
                _certificate = new X509Certificate2(generated.Export(X509ContentType.Pfx));
            }
        }

        public async Task StartAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            ListenOptions grpcListener = null;
            ListenOptions httpListener = null;
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Listen(IPAddress.Loopback, 0, listen =>
                {
                    grpcListener = listen;
                    listen.Protocols = HttpProtocols.Http2;
                    if (_certificate != null)
                    {
                        listen.UseHttps(_certificate);
                    }
                });
                options.Listen(IPAddress.Loopback, 0, listen =>
                {
                    httpListener = listen;
                    listen.Protocols = HttpProtocols.Http1;
                });
            });
            if (_grpcUrl != null)
            {
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["A2AAdapterOptions:GrpcUrl"] = _grpcUrl,
                });
            }
            var storage = new MemoryStorage();
            var options = new AgentApplicationOptions(storage);
            if (Scenario.InTaskAuthorization)
            {
                var connections = Mock.Of<IConnections>();
                options.UserAuthorization = new UserAuthorizationOptions(
                    NullLoggerFactory.Instance, storage, connections,
                    new A2AUserAuthorization("request", storage, connections, new A2AUserAuthorizationSettings
                    {
                        Mode = A2AUserAuthorizationMode.InTask,
                        OAuthFlows = new OAuthFlows
                        {
                            DeviceCode = new DeviceCodeOAuthFlow
                            {
                                DeviceAuthorizationUrl = "https://login.example.com/devicecode",
                                TokenUrl = "https://login.example.com/token",
                                Scopes = new Dictionary<string, string> { ["agent.read"] = "Read agent" },
                            },
                        },
                        RequiredScopes = ["agent.read"],
                    }))
                {
                    DefaultHandlerName = "request",
                    AutoSignIn = UserAuthorizationOptions.AutoSignInOff,
                };
            }
            builder.Services.AddSingleton<IStorage>(storage);
            builder.Services.AddSingleton(options);
            builder.Services.AddSingleton(Scenario);
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<CallLifetime>();
            builder.Services.AddAgentCore<CloudAdapter>();
            builder.Services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
            builder.Services.AddAuthorization();

            var (types, map) = CreateAgentAssembly(_agents);
            foreach (var type in types)
            {
                builder.Services.AddTransient(type);
            }
            builder.Services.AddTransient(typeof(IAgent), sp => sp.GetRequiredService(types[0]));
            App = builder.Build();
            App.UseAuthentication();
            App.UseAuthorization();
            if (_collision == "upstream-before")
            {
                App.MapGrpcA2A();
            }
            Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints = App;
            if (_prefix != null)
            {
                endpoints = App.MapGroup(_prefix);
                if (_nestedGroup)
                {
                    endpoints = endpoints.MapGroup("");
                }
            }
            map(endpoints, _requireAuth, "/a2a");
            if (_collision == "mapping")
            {
                map(endpoints, _requireAuth, "/a2a");
            }
            if (_collision == "upstream-after")
            {
                App.MapGrpcA2A();
            }
            if (_collision == "trailing-slash")
            {
                App.MapPost("/lf.a2a.v1.A2AService/SendMessage/", () => Results.Ok());
            }
            if (_collision == "no-leading-slash")
            {
                App.MapPost("lf.a2a.v1.A2AService/SendMessage", () => Results.Ok());
            }
            await App.StartAsync();
            GrpcAddress = new Uri($"{(_certificate == null ? "http" : "https")}://127.0.0.1:{grpcListener.IPEndPoint.Port}/");
            Http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{httpListener.IPEndPoint.Port}/") };
        }

        public GrpcChannel CreateChannel(string bearer = null, bool ticketOnly = false, bool extensions = false)
        {
            var handler = new HttpClientHandler();
            if (_certificate != null)
            {
                handler.ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
                    certificate?.Thumbprint == _certificate.Thumbprint;
            }
            var http = new HttpClient(handler);
            if (bearer != null)
            {
                http.DefaultRequestHeaders.Authorization = new("Bearer", bearer);
            }
            if (ticketOnly)
            {
                http.DefaultRequestHeaders.Add("x-test-ticket", "ticket-user");
            }
            if (extensions)
            {
                http.DefaultRequestHeaders.Add("a2a-extensions", InTaskAuthorizationExtension.Uri);
            }
            _grpcHttpClients.Add(http);
            var channel = GrpcChannel.ForAddress(GrpcAddress, new GrpcChannelOptions { HttpClient = http });
            _channels.Add(channel);
            return channel;
        }

        public A2AGrpcClient CreateClient(string bearer = null, bool ticketOnly = false, bool extensions = false) =>
            new(CreateChannel(bearer, ticketOnly, extensions));

        public async Task<ProtocolAgentCard> GetCardAsync()
        {
            using var response = await Http.GetAsync("/.well-known/agent-card.json");
            response.EnsureSuccessStatusCode();
            return ProtocolJsonSerializer.ToObject<ProtocolAgentCard>(await response.Content.ReadAsStringAsync());
        }

        public async Task<AgentTask> GetHttpTaskAsync(string id)
        {
            using var response = await Http.GetAsync($"/a2a/tasks/{id}");
            response.EnsureSuccessStatusCode();
            return JsonSerializer.Deserialize<AgentTask>(
                await response.Content.ReadAsStringAsync(), A2AJsonUtilities.DefaultOptions);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            if (App != null)
            {
                await App.DisposeAsync();
            }
            Http?.Dispose();
            foreach (var channel in _channels)
            {
                channel.Dispose();
            }
            foreach (var http in _grpcHttpClients)
            {
                http.Dispose();
            }
            _certificate?.Dispose();
            Scenario.Dispose();
        }
    }

    private static (Type[] Types, Func<Microsoft.AspNetCore.Routing.IEndpointRouteBuilder, bool?, string,
        Microsoft.AspNetCore.Builder.IEndpointConventionBuilder> Map) CreateAgentAssembly(AgentSpec[] specs)
    {
        // The mapper discovers the calling assembly. Give each host only its declared agents,
        // rather than the many unrelated AgentApplication fixtures in this test assembly.
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("A2AGrpcTestHost" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.RunAndCollect);
        var module = assembly.DefineDynamicModule("Agents");
        var types = new List<Type>();
        var constructorTypes = new[] { typeof(AgentApplicationOptions), typeof(Scenario), typeof(CallLifetime) };
        foreach (var spec in specs)
        {
            var agent = module.DefineType(spec.Name, TypeAttributes.Public, typeof(BoundaryAgent));
            foreach (var (protocol, path) in spec.Interfaces)
            {
                agent.SetCustomAttribute(new CustomAttributeBuilder(
                    typeof(AgentInterfaceAttribute).GetConstructors().Single(), [protocol, path, null]));
            }
            var constructor = agent.DefineConstructor(MethodAttributes.Public,
                CallingConventions.Standard, constructorTypes);
            var body = constructor.GetILGenerator();
            body.Emit(OpCodes.Ldarg_0);
            body.Emit(OpCodes.Ldarg_1);
            body.Emit(OpCodes.Ldarg_2);
            body.Emit(OpCodes.Ldarg_3);
            body.Emit(OpCodes.Call, typeof(BoundaryAgent).GetConstructor(constructorTypes));
            body.Emit(OpCodes.Ret);
            types.Add(agent.CreateType());
        }
        var mapper = module.DefineType("Mapper", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
        var method = mapper.DefineMethod("Map", MethodAttributes.Public | MethodAttributes.Static,
            typeof(IEndpointConventionBuilder),
            [typeof(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder), typeof(bool?), typeof(string)]);
        method.SetImplementationFlags(MethodImplAttributes.NoInlining);
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Call, typeof(A2AServiceExtensions).GetMethod(nameof(A2AServiceExtensions.MapA2AApplicationEndpoints)));
        il.Emit(OpCodes.Ret);
        var map = mapper.CreateType().GetMethod("Map").CreateDelegate<
            Func<Microsoft.AspNetCore.Routing.IEndpointRouteBuilder, bool?, string, IEndpointConventionBuilder>>();
        return (types.ToArray(), map);
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var ticketOnly = Request.Headers["x-test-ticket"] == "ticket-user";
            var bearer = Request.Headers.Authorization.ToString();
            if (!ticketOnly && bearer is not ("Bearer valid-alice" or "Bearer valid-bob"))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }
            var name = ticketOnly ? "ticket-user" : bearer["Bearer valid-".Length..];
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, name)], Scheme.Name);
            var properties = new AuthenticationProperties();
            if (!ticketOnly)
            {
                properties.StoreTokens([new AuthenticationToken
                {
                    Name = "access_token",
                    Value = bearer["Bearer ".Length..],
                }]);
            }
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), properties, Scheme.Name)));
        }
    }
}
