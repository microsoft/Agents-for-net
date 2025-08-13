// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.A2A.JsonRpc;
using Microsoft.Agents.Hosting.A2A.Protocol;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.A2A;

internal class A2AResponseHandler : IChannelResponseHandler
{
    private const string SseTemplate = "event: {0}\r\ndata: {1}\r\n\r\n";
    private readonly ITaskStore _taskStore;
    private readonly RequestId _requestId;
    private readonly string _contextId;
    private readonly string _taskId;
    private readonly ILogger _logger;
    private readonly bool _sse;

    public A2AResponseHandler(ITaskStore taskStore, RequestId requestId, string contextId, string taskId, bool sse, ILogger logger)
    {
        _taskStore = taskStore;
        _requestId = requestId;
        _contextId = contextId;
        _taskId = taskId;
        _logger = logger ?? NullLogger.Instance;
        _sse = sse;
    }

    public async Task ResponseBegin(HttpResponse httpResponse, CancellationToken cancellationToken = default)
    {
        if (_sse)
        {
            httpResponse.ContentType = "text/event-stream";

            var task = await _taskStore.GetTaskAsync(_taskId, cancellationToken).ConfigureAwait(false);
            await WriteEvent(httpResponse, task.Kind, task, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task OnResponse(HttpResponse httpResponse, IActivity activity, CancellationToken cancellationToken = default)
    {
        var entity = activity.GetStreamingEntity();
        if (entity != null)
        {
            var isLastChunk = entity.StreamType == StreamTypes.Final;
            var isInformative = entity.StreamType == StreamTypes.Informative;

            if (isInformative)
            {
                // Informative is a Status update with a Message
                var statusUpdate = A2AConverter.CreateStatusUpdate(_contextId, _taskId, TaskState.Working, artifactId: entity.StreamId, activity: isInformative ? activity : activity);
                await _taskStore.UpdateTaskAsync(statusUpdate, cancellationToken).ConfigureAwait(false);

                await WriteEvent(httpResponse, statusUpdate.Kind, statusUpdate, cancellationToken).ConfigureAwait(false);

                return;
            }

            // This is using entity.StreamId for the artifactId.  This will result in a single Artifact in the Task
            var artifactUpdate = A2AConverter.ArtifactUpdateFromActivity(_contextId, _taskId, activity, artifactId: entity.StreamId, lastChunk: isLastChunk);
            await _taskStore.UpdateTaskAsync(artifactUpdate, cancellationToken).ConfigureAwait(false);

            await WriteEvent(httpResponse, artifactUpdate.Kind, artifactUpdate, cancellationToken).ConfigureAwait(false);

            if (isLastChunk)
            {
                // Send the final streaming Activity as a A2A Message
                var message = A2AConverter.MessageFromActivity(_contextId, _taskId, activity);
                await _taskStore.UpdateTaskAsync(message, cancellationToken).ConfigureAwait(false);

                await WriteEvent(httpResponse, message.Kind, message, cancellationToken).ConfigureAwait(false);
            }
        }
        else if (activity.IsType(ActivityTypes.Message))
        {
            // Send a Message (from Activity)
            var message = A2AConverter.MessageFromActivity(_contextId, _taskId, activity);
            var task = await _taskStore.UpdateTaskAsync(message, cancellationToken).ConfigureAwait(false);

            await WriteEvent(httpResponse, message.Kind, message, cancellationToken).ConfigureAwait(false);

            // Update Task state if expecting input
            if (task.Status.State != TaskState.InputRequired && activity.InputHint == InputHints.ExpectingInput)
            {
                // Status update will be sent during ResponseEnd
                var statusUpdate = A2AConverter.CreateStatusUpdate(_contextId, _taskId, TaskState.InputRequired);
                await _taskStore.UpdateTaskAsync(statusUpdate, cancellationToken).ConfigureAwait(false);
            }
        }
        else if (activity.IsType(ActivityTypes.EndOfConversation))
        {
            // Status update will be sent during ResponseEnd
            var statusUpdate = A2AConverter.CreateStatusUpdate(_contextId, _taskId, TaskState.Completed);
            await _taskStore.UpdateTaskAsync(statusUpdate, cancellationToken).ConfigureAwait(false);

            // Add optional EOC Value as an Artifact
            if (activity.Value != null)
            {
                var artifactUpdate = new TaskArtifactUpdateEvent()
                {
                    TaskId = _taskId,
                    ContextId = _contextId,
                    Artifact = A2AConverter.ArtifactFromObject(
                        activity.Value,
                        name: "Result",
                        description: "Task completion result",
                        mediaType: "application/json"),
                    Append = false,
                    LastChunk = true
                };
                await _taskStore.UpdateTaskAsync(artifactUpdate, cancellationToken).ConfigureAwait(false);

                await WriteEvent(httpResponse, artifactUpdate.Kind, artifactUpdate, cancellationToken).ConfigureAwait(false);
            }
        }
        else if (activity.IsType(ActivityTypes.Typing))
        {
            // non-streamingresponse Typing
            // Status update will be sent during ResponseEnd
            var statusUpdate = A2AConverter.CreateStatusUpdate(_contextId, _taskId, TaskState.Working);
            await _taskStore.UpdateTaskAsync(statusUpdate, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ResponseEnd(HttpResponse httpResponse, object data, CancellationToken cancellationToken = default)
    {
        if (data != null)
        {
            // TODO: data is probably InvokeResponse.  Could do a more complete job of writing the value and
            // full schema (including the InvokeResponse.Body).
            var artifactUpdate = new TaskArtifactUpdateEvent()
            {
                TaskId = _taskId,
                ContextId = _contextId,
                Artifact = A2AConverter.ArtifactFromObject(data),
                Append = true,
                LastChunk = true
            };
            await _taskStore.UpdateTaskAsync(artifactUpdate, cancellationToken).ConfigureAwait(false);
            await WriteEvent(httpResponse, artifactUpdate.Kind, artifactUpdate, cancellationToken).ConfigureAwait(false);
        }

        var task = await _taskStore.GetTaskAsync(_taskId, cancellationToken).ConfigureAwait(false);
        if (task.Status.State != TaskState.InputRequired && task.Status.State != TaskState.Completed)
        {
            // auto complete if a message requiring input wasn't sent.
            // TODO: AP supports a long running (not complete) task, which would end with a proactive notification.  This
            // needs to be enhanced to support a push notification.
            // Impl notes: Implement IChannelAdapter.ProcessProactiveAsync to update TaskStore and send push notification (if enabled)
            var completeStatusUpdate = A2AConverter.CreateStatusUpdate(_contextId, _taskId, TaskState.Completed);
            task = await _taskStore.UpdateTaskAsync(completeStatusUpdate, cancellationToken).ConfigureAwait(false);
        }

        // Send Task status
        if (_sse)
        {
            var finalStatusUpdate = A2AConverter.CreateStatusUpdate(_contextId, _taskId, task.Status.State, isFinal: true);
            await WriteEvent(httpResponse, finalStatusUpdate.Kind, finalStatusUpdate, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var response = A2AConverter.CreateResponse(_requestId, task);
            await WriteResponseAsync(httpResponse, response, logger: _logger, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WriteEvent(HttpResponse httpResponse, string eventName, object payload, CancellationToken cancellationToken)
    {
        if (!_sse)
        {
            return;
        }

        var sse = string.Format(
            SseTemplate,
            eventName, 
            A2AConverter.ToJson(
                A2AConverter.StreamingMessageResponse(
                    _requestId,
                    payload)
                )
            );

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("SSE event:\r\n{Event}", sse);
        }

        await httpResponse.Body.WriteAsync(Encoding.UTF8.GetBytes(sse), cancellationToken).ConfigureAwait(false);
        await httpResponse.Body.FlushAsync(cancellationToken);
    }

    public static async Task WriteResponseAsync(HttpResponse response, object payload, bool streamed = false, HttpStatusCode code = HttpStatusCode.OK, ILogger logger = null,  CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(payload);

        response.StatusCode = (int)code;

        var json = JsonSerializer.Serialize(payload, A2AConverter.SerializerOptions);
        if (!streamed)
        {
            response.ContentType = "application/json";
        }
        else
        {
            response.ContentType = "text/event-stream";
            json = $"data: {json}\r\n\r\n";
        }

        if (logger != null && logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("WriteResponseAsync: {Payload}", json);
        }

        await response.Body.WriteAsync(Encoding.UTF8.GetBytes(json), cancellationToken).ConfigureAwait(false);
        await response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<T?> ReadRequestAsync<T>(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            return await JsonSerializer.DeserializeAsync<T>(request.Body, A2AConverter.SerializerOptions);
        }
        catch (Exception ex)
        {
            throw new A2AException(ex.Message, ex, A2AErrors.InvalidRequest);
        }
    }
}
