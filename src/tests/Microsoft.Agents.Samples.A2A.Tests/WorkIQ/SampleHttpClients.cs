// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2A.Tests;

internal sealed record CapturedSampleRequest(
    HttpMethod Method, Uri Uri, string? Token, string? Version, string Body);

internal sealed class SampleHttpClients(
    Func<CapturedSampleRequest, CancellationToken, Task<HttpResponseMessage>> respond) : IHttpClientFactory, IDisposable
{
    private readonly SampleHandler _handler = new(respond);
    public ConcurrentQueue<CapturedSampleRequest> Requests => _handler.Requests;

    public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false)
    {
        BaseAddress = new Uri(name == "WorkIQ"
            ? "https://workiq.svc.cloud.microsoft/a2a/"
            : "https://graph.microsoft.com/v1.0/"),
    };

    public void Dispose() => _handler.Dispose();

    internal static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(value, A2AJsonUtilities.DefaultOptions), Encoding.UTF8, "application/json"),
    };

    internal static HttpResponseMessage Rpc(CapturedSampleRequest request, object result)
    {
        using var document = JsonDocument.Parse(request.Body);
        return Json(new { jsonrpc = "2.0", id = document.RootElement.GetProperty("id").Clone(), result });
    }

    internal static HttpResponseMessage Stream(params StreamResponse[] updates) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(string.Concat(updates.Select(update =>
            $"data: {JsonSerializer.Serialize(new { jsonrpc = "2.0", id = "stream", result = update }, A2AJsonUtilities.DefaultOptions)}\n\n")),
            Encoding.UTF8, "text/event-stream"),
    };

    internal static AgentCard Card(bool streaming = true, string endpoint = "https://workiq.svc.cloud.microsoft/a2a/") => new()
    {
        Name = "Microsoft Copilot",
        Description = "Work IQ test endpoint",
        Version = "1.0.0",
        Capabilities = new AgentCapabilities { Streaming = streaming, PushNotifications = false },
        SupportedInterfaces = [new AgentInterface { Url = endpoint, ProtocolBinding = "JSONRPC", ProtocolVersion = "1.0" }],
        DefaultInputModes = ["text"],
        DefaultOutputModes = ["text"],
        Skills = [],
    };

    internal static AgentTask WorkIqTask(TaskState state = TaskState.Working, string? text = null) => new()
    {
        Id = "workiq-task",
        ContextId = "workiq-context",
        Status = new global::A2A.TaskStatus
        {
            State = state,
            Message = text is null ? null : new Message
            {
                Role = Role.Agent,
                MessageId = "reply",
                TaskId = "workiq-task",
                ContextId = "workiq-context",
                Parts = [Part.FromText(text)],
            },
        },
    };

    private sealed class SampleHandler(
        Func<CapturedSampleRequest, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public ConcurrentQueue<CapturedSampleRequest> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var captured = new CapturedSampleRequest(
                request.Method,
                request.RequestUri!,
                request.Headers.Authorization?.Parameter,
                request.Headers.TryGetValues("A2A-Version", out var versions) ? string.Join(",", versions) : null,
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            Requests.Enqueue(captured);
            return await respond(captured, cancellationToken);
        }
    }
}
