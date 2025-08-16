// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
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
    private readonly ILogger _logger;
    private readonly bool _sse;
    private readonly MessageSendParams _sendParams;
    private readonly AgentTask _incomingTask;

    public A2AResponseHandler(ITaskStore taskStore, RequestId requestId, AgentTask incomingTask, MessageSendParams sendParams, bool sse, ILogger logger)
    {
        AssertionHelpers.ThrowIfNull(requestId, nameof(requestId));
        AssertionHelpers.ThrowIfNull(taskStore, nameof(taskStore));
        AssertionHelpers.ThrowIfNull(incomingTask, nameof(incomingTask));
        AssertionHelpers.ThrowIfNull(sendParams, nameof(sendParams));

        _taskStore = taskStore;
        _requestId = requestId;
        _incomingTask = incomingTask;
        _sendParams = sendParams;
        _logger = logger ?? NullLogger.Instance;
        _sse = sse;
    }

    public async Task ResponseBegin(HttpResponse httpResponse, CancellationToken cancellationToken = default)
    {
        if (_sse)
        {
            httpResponse.ContentType = "text/event-stream";

            //var task = await _taskStore.GetTaskAsync(_incomingTask.Id, cancellationToken).ConfigureAwait(false);
            await WriteEvent(httpResponse, _incomingTask.Kind, _incomingTask, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// ITurnContext.SendActivity ultimately ends up here by way of A2AAdapter -> ChannelResponseQueue -> IChannelResponseHandler.OnResponse.
    /// </summary>
    /// <param name="httpResponse"></param>
    /// <param name="activity"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task OnResponse(HttpResponse httpResponse, IActivity activity, CancellationToken cancellationToken = default)
    {
        var entity = activity.GetStreamingEntity();
        if (entity != null)
        {
            await OnStreamingResponse(httpResponse, activity, entity, cancellationToken).ConfigureAwait(false);
        }
        else if (activity.IsType(ActivityTypes.Message))
        {
            await OnMessageResponse(httpResponse, activity, cancellationToken).ConfigureAwait(false);
        }
        else if (activity.IsType(ActivityTypes.EndOfConversation))
        {
            await OnEndOfConversationResponse(httpResponse, activity, cancellationToken).ConfigureAwait(false);
        }
        else if (activity.IsType(ActivityTypes.Typing))
        {
            await OnTypingResponse(httpResponse, activity, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ResponseEnd(HttpResponse httpResponse, object data, CancellationToken cancellationToken = default)
    {
        AgentTask task = null;

        if (data != null)
        {
            // data is probably InvokeResponse.
            var artifactUpdate = new TaskArtifactUpdateEvent()
            {
                TaskId = _incomingTask.Id,
                ContextId = _incomingTask.ContextId,
                Artifact = A2AConverter.ArtifactFromObject(data, name: data.GetType().Name),
                Append = true,
                LastChunk = true
            };
            task = await _taskStore.UpdateTaskAsync(artifactUpdate, cancellationToken).ConfigureAwait(false);
            await WriteEvent(httpResponse, artifactUpdate.Kind, artifactUpdate, cancellationToken).ConfigureAwait(false);
        }

        task ??= await _taskStore.GetTaskAsync(_incomingTask.Id, cancellationToken).ConfigureAwait(false);

        // Send Task status
        if (_sse)
        {
            // TODO: We could send the AgentTask if completed?
            var finalStatusUpdate = A2AConverter.CreateStatusUpdate(_incomingTask.ContextId, _incomingTask.Id, task.Status.State, isFinal: true);
            await WriteEvent(httpResponse, finalStatusUpdate.Kind, finalStatusUpdate, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            task = task.WithHistoryTrimmedTo(_sendParams?.Configuration?.HistoryLength);
            var response = A2AConverter.CreateResponse(_requestId, task);
            await WriteResponseAsync(httpResponse, response, logger: _logger, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task OnMessageResponse(HttpResponse httpResponse, IActivity activity, CancellationToken cancellationToken = default)
    {
        // Send a Message (from Activity)
        var message = A2AConverter.MessageFromActivity(_incomingTask.ContextId, _incomingTask.Id, activity);
        var task = await _taskStore.UpdateTaskAsync(message, cancellationToken).ConfigureAwait(false);

        await WriteEvent(httpResponse, message.Kind, message, cancellationToken).ConfigureAwait(false);

        // Update Task state if expecting input
        if (task.Status.State != TaskState.InputRequired && (activity.InputHint == InputHints.ExpectingInput || activity.InputHint == InputHints.AcceptingInput))
        {
            // Status update will be sent during ResponseEnd
            var statusUpdate = A2AConverter.CreateStatusUpdate(_incomingTask.ContextId, _incomingTask.Id, TaskState.InputRequired);
            await _taskStore.UpdateTaskAsync(statusUpdate, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task OnStreamingResponse(HttpResponse httpResponse, IActivity activity, StreamInfo entity, CancellationToken cancellationToken = default)
    {
        var isLastChunk = entity.StreamType == StreamTypes.Final;
        var isInformative = entity.StreamType == StreamTypes.Informative;

        if (isInformative)
        {
            // Informative is a Status update with a Message
            var statusUpdate = A2AConverter.CreateStatusUpdate(_incomingTask.ContextId, _incomingTask.Id, TaskState.Working, artifactId: entity.StreamId, activity: isInformative ? activity : activity);
            await _taskStore.UpdateTaskAsync(statusUpdate, cancellationToken).ConfigureAwait(false);

            await WriteEvent(httpResponse, statusUpdate.Kind, statusUpdate, cancellationToken).ConfigureAwait(false);

            return;
        }

        // TODO:  The artifact update is likely pointless if not SSE?
        // This is using entity.StreamId for the artifactId.  This will result in a single Artifact in the Task
        var artifactUpdate = A2AConverter.ArtifactUpdateFromActivity(_incomingTask.ContextId, _incomingTask.Id, activity, artifactId: entity.StreamId, lastChunk: isLastChunk);
        await _taskStore.UpdateTaskAsync(artifactUpdate, cancellationToken).ConfigureAwait(false);

        await WriteEvent(httpResponse, artifactUpdate.Kind, artifactUpdate, cancellationToken).ConfigureAwait(false);

        if (isLastChunk)
        {
            // Send the final streaming Activity as a A2A Message
            var message = A2AConverter.MessageFromActivity(_incomingTask.ContextId, _incomingTask.Id, activity);
            await _taskStore.UpdateTaskAsync(message, cancellationToken).ConfigureAwait(false);

            await WriteEvent(httpResponse, message.Kind, message, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task OnTypingResponse(HttpResponse httpResponse, IActivity activity, CancellationToken cancellationToken = default)
    {
        // non-StreamingResponse Typing
        // Status update will be sent during ResponseEnd
        var statusUpdate = A2AConverter.CreateStatusUpdate(_incomingTask.ContextId, _incomingTask.Id, TaskState.Working);
        await _taskStore.UpdateTaskAsync(statusUpdate, cancellationToken).ConfigureAwait(false);
    }

    private async Task OnEndOfConversationResponse(HttpResponse httpResponse, IActivity activity, CancellationToken cancellationToken = default)
    {
        // Upate status to terminal.  Status event sent in ResponseEnd
        TaskState taskState = activity.Code switch
        {
            EndOfConversationCodes.Error => TaskState.Failed,
            EndOfConversationCodes.UserCancelled => TaskState.Canceled,
            _ => TaskState.Completed,
        };

        var statusMessage = activity.HasMessageContent() ? activity : null;
        var statusUpdate = A2AConverter.CreateStatusUpdate(_incomingTask.ContextId, _incomingTask.Id, taskState, activity: statusMessage);
        await _taskStore.UpdateTaskAsync(statusUpdate, cancellationToken).ConfigureAwait(false);

        // Set optional EOC Value as an Artifact
        if (activity.Value != null)
        {
            var artifactUpdate = new TaskArtifactUpdateEvent()
            {
                TaskId = _incomingTask.Id,
                ContextId = _incomingTask.ContextId,
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
