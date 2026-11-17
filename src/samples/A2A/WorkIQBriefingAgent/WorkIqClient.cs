// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WorkIQBriefingAgent;

internal sealed record WorkIqProgress(string? TaskId, string? ContextId, string Status, string TextDelta);
internal sealed record WorkIqReply(string Text, string? TaskId, string? ContextId, bool RequiresInput);

internal sealed class WorkIqClient(
    IHttpClientFactory httpClientFactory,
    TimeZoneInfo timeZone,
    ILogger<WorkIqClient> logger)
{
    public const string HttpClientName = "WorkIQ";

    public async Task<WorkIqReply> RunAsync(
        string request,
        string? contextId,
        string? taskId,
        string accessToken,
        Func<WorkIqProgress, CancellationToken, Task> progress,
        CancellationToken cancellationToken)
    {
        using var http = CreateHttpClient(accessToken);
        var card = await http.GetFromJsonAsync<AgentCard>(
            ".well-known/agent-card.json", A2AJsonUtilities.DefaultOptions, cancellationToken)
            ?? throw new InvalidOperationException("Work IQ returned no Agent Card.");

        // Discovery describes capabilities; it is not permission to forward a bearer token elsewhere.
        bool trustedInterface = card.SupportedInterfaces.Any(agentInterface =>
            agentInterface.ProtocolBinding == "JSONRPC"
            && agentInterface.ProtocolVersion == "1.0"
            && Uri.TryCreate(agentInterface.Url, UriKind.Absolute, out var address)
            && string.Equals(address.AbsoluteUri.TrimEnd('/'), http.BaseAddress!.AbsoluteUri.TrimEnd('/'), StringComparison.Ordinal));
        if (!trustedInterface)
        {
            throw new InvalidOperationException("Work IQ did not advertise the configured A2A 1.0 JSON-RPC endpoint.");
        }

        using var client = new global::A2A.A2AClient(http.BaseAddress!, http);
        var accumulator = new ReplyAccumulator(progress);
        var send = new SendMessageRequest
        {
            Message = new Message
            {
                Role = Role.User,
                MessageId = Guid.NewGuid().ToString("N"),
                ContextId = contextId,
                // Continue an input-required task only. A completed task must not be sent again.
                TaskId = taskId,
                Parts = [Part.FromText(request)],
                Metadata = new Dictionary<string, JsonElement>
                {
                    ["Location"] = JsonSerializer.SerializeToElement(new
                    {
                        timeZone = timeZone.Id,
                        timeZoneOffset = (int)timeZone.GetUtcOffset(DateTimeOffset.UtcNow).TotalMinutes,
                    }),
                },
            },
            Configuration = new SendMessageConfiguration { AcceptedOutputModes = ["text"] },
        };

        try
        {
            if (card.Capabilities.Streaming == true)
            {
                await accumulator.ReadAsync(client.SendStreamingMessageAsync(send, cancellationToken), cancellationToken);
            }
            else
            {
                var response = await client.SendMessageAsync(send, cancellationToken);
                await accumulator.ApplyAsync(new StreamResponse { Task = response.Task, Message = response.Message }, cancellationToken);
            }

            if (accumulator.IsWorking)
            {
                // Observe the existing task. Resending the request could execute the work twice.
                await accumulator.ApplyAsync(new StreamResponse
                {
                    Task = await client.GetTaskAsync(new GetTaskRequest { Id = accumulator.Task!.Id }, cancellationToken),
                }, cancellationToken);
                if (accumulator.IsWorking)
                {
                    await accumulator.ReadAsync(client.SubscribeToTaskAsync(
                        new SubscribeToTaskRequest { Id = accumulator.Task!.Id }, cancellationToken), cancellationToken);
                }
            }

            return accumulator.GetReply();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var downstreamTaskId = accumulator.Task?.Id ?? taskId;
            if (downstreamTaskId is not null)
            {
                // The caller's token is already cancelled. Give the remote cancellation its own short deadline.
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await client.CancelTaskAsync(new CancelTaskRequest { Id = downstreamTaskId }, timeout.Token);
                }
                catch (Exception exception) when (exception is A2AException or HttpRequestException or IOException or OperationCanceledException)
                {
                    logger.LogWarning("Cancellation of Work IQ task {TaskId} was not confirmed ({ErrorType}).",
                        downstreamTaskId, exception.GetType().Name);
                }
            }
            throw;
        }
    }

    public async Task<WorkIqReply> ObserveAsync(
        string taskId,
        string accessToken,
        Func<WorkIqProgress, CancellationToken, Task> progress,
        CancellationToken cancellationToken)
    {
        using var http = CreateHttpClient(accessToken);
        using var client = new global::A2A.A2AClient(http.BaseAddress!, http);
        var accumulator = new ReplyAccumulator(progress);
        await accumulator.ApplyAsync(new StreamResponse
        {
            Task = await client.GetTaskAsync(new GetTaskRequest { Id = taskId }, cancellationToken),
        }, cancellationToken);
        if (accumulator.IsWorking)
        {
            await accumulator.ReadAsync(client.SubscribeToTaskAsync(new SubscribeToTaskRequest { Id = taskId }, cancellationToken), cancellationToken);
        }
        return accumulator.GetReply();
    }

    public async Task<AgentTask> CancelAsync(string taskId, string accessToken, CancellationToken cancellationToken)
    {
        using var http = CreateHttpClient(accessToken);
        using var client = new global::A2A.A2AClient(http.BaseAddress!, http);
        return await client.CancelTaskAsync(new CancelTaskRequest { Id = taskId }, cancellationToken);
    }

    private HttpClient CreateHttpClient(string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        var http = httpClientFactory.CreateClient(HttpClientName);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        // Discovery needs the header too; omitting it selects Work IQ's older 0.3 contract.
        http.DefaultRequestHeaders.Add("A2A-Version", "1.0");
        return http;
    }

    private sealed class ReplyAccumulator(Func<WorkIqProgress, CancellationToken, Task> progress)
    {
        private Message? _message;
        private string _publishedText = string.Empty;
        public AgentTask? Task { get; private set; }
        public bool IsWorking => Task?.Status.State is TaskState.Working or TaskState.Submitted;

        public async Task ReadAsync(IAsyncEnumerable<StreamResponse> updates, CancellationToken cancellationToken)
        {
            await foreach (var update in updates.WithCancellation(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ApplyAsync(update, cancellationToken);
            }
        }

        public async Task ApplyAsync(StreamResponse update, CancellationToken cancellationToken)
        {
            Task = TaskProjection.Apply(Task, update);
            _message = update.Message ?? _message;
            var text = ReadText();
            // Artifacts can replace earlier text. Stream append-only additions; the final preview is authoritative.
            var delta = text.StartsWith(_publishedText, StringComparison.Ordinal) ? text[_publishedText.Length..] : string.Empty;
            _publishedText = text;
            await progress(new WorkIqProgress(Task?.Id ?? _message?.TaskId,
                Task?.ContextId ?? _message?.ContextId, Task?.Status.State.ToString() ?? "Message", delta), cancellationToken);
        }

        public WorkIqReply GetReply()
        {
            if (Task is not null && Task.Status.State is not (TaskState.Completed or TaskState.InputRequired))
            {
                throw new InvalidOperationException($"Work IQ task ended in state {Task.Status.State}; no email was prepared.");
            }
            var text = ReadText();
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("Work IQ returned no briefing text.");
            }
            return new WorkIqReply(text, Task?.Id ?? _message?.TaskId,
                Task?.ContextId ?? _message?.ContextId, Task?.Status.State == TaskState.InputRequired);
        }

        private string ReadText()
        {
            if (Task?.Status.State == TaskState.InputRequired)
            {
                // A partial draft may precede a question. The caller needs the question to continue.
                return string.Concat((Task.Status.Message ?? _message)?.Parts.Select(part => part.Text) ?? []);
            }
            var artifacts = string.Join("\n\n", Task?.Artifacts?
                .Select(artifact => string.Concat(artifact.Parts.Select(part => part.Text)))
                .Where(text => !string.IsNullOrWhiteSpace(text)) ?? []);
            return !string.IsNullOrWhiteSpace(artifacts) ? artifacts
                : string.Concat((Task?.Status.Message ?? _message)?.Parts.Select(part => part.Text) ?? []);
        }
    }
}
