// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.A2A.Protocol;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.A2A
{
    internal class A2AStreamedResponseWriter : IChannelResponseWriter
    {
        private const string SseTemplate = "event: {0}\r\ndata: {1}\r\n\r\n";
        private readonly ITaskStore _taskStore;
        private readonly string _requestId;
        private readonly string _contextId;
        private readonly string _taskId;
        private bool _inStreamingResponse = false;
        private ILogger _logger;

        public A2AStreamedResponseWriter(ITaskStore taskStore, string requestId, string contextId, string taskId, ILogger logger)
        {
            _taskStore = taskStore;
            _requestId = requestId;
            _contextId = contextId;
            _taskId = taskId;
            _logger = logger ?? NullLogger.Instance;
        }

        public async Task ResponseBegin(HttpResponse httpResponse, CancellationToken cancellationToken = default)
        {
            var task = await _taskStore.CreateOrUpdateTaskAsync(_contextId, _taskId, TaskState.Submitted, cancellationToken).ConfigureAwait(false);

            httpResponse.ContentType = "text/event-stream";
            await WriteEvent(httpResponse, task.Kind, task, cancellationToken).ConfigureAwait(false);
        }

        public async Task WriteActivity(HttpResponse httpResponse, IActivity activity, CancellationToken cancellationToken = default)
        {
            var entity = activity.GetStreamingEntity();
            if (entity != null)
            {
                var isLastChunk = entity.StreamType == StreamTypes.Final;
                var isInformative = entity.StreamType == StreamTypes.Informative;

                if (!_inStreamingResponse || isInformative)
                {
                    _inStreamingResponse = true;

                    // TODO: Include informative text as a Message in the Status.
                    var statusUpdate = A2AConverter.StatusUpdate(_contextId, _taskId, TaskState.Working, artifactId: entity.StreamId, activity: isInformative ? activity : null);
                    await WriteEvent(httpResponse, statusUpdate.Kind, statusUpdate, cancellationToken).ConfigureAwait(false);

                    await _taskStore.UpdateTaskAsync(statusUpdate, cancellationToken).ConfigureAwait(false);

                    if (isInformative)
                    {
                        return;
                    }
                }

                if (!isLastChunk)
                {
                    //TBD:  We don't know "last chunk" until the final streaming Activity is sent, which probably should be a Message (see `else` block)
                    var artifactUpdate = A2AConverter.ArtifactUpdateFromActivity(_contextId, _taskId, activity, artifactId: entity.StreamId, lastChunk: isLastChunk);
                    await WriteEvent(httpResponse, artifactUpdate.Kind, artifactUpdate, cancellationToken).ConfigureAwait(false);

                    await _taskStore.UpdateTaskAsync(artifactUpdate, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // Send the final streaming Activity as a A2A Message
                    var message = A2AConverter.MessageFromActivity(_contextId, _taskId, activity);

                    await WriteEvent(httpResponse, message.Kind, message, cancellationToken).ConfigureAwait(false);
                    _inStreamingResponse = false;

                    await _taskStore.UpdateTaskAsync(message, cancellationToken).ConfigureAwait(false);
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
                    var statusUpdate = A2AConverter.StatusUpdate(_contextId, _taskId, TaskState.InputRequired);
                    await _taskStore.UpdateTaskAsync(statusUpdate, cancellationToken).ConfigureAwait(false);
                }
            }
            else if (activity.IsType(ActivityTypes.EndOfConversation))
            {
                // Status update will be sent during ResponseEnd
                var statusUpdate = A2AConverter.StatusUpdate(_contextId, _taskId, TaskState.Completed);
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
                var statusUpdate = A2AConverter.StatusUpdate(_contextId, _taskId, TaskState.Working);
                await _taskStore.UpdateTaskAsync(statusUpdate, cancellationToken).ConfigureAwait(false);
                await WriteEvent(httpResponse, statusUpdate.Kind, statusUpdate, cancellationToken).ConfigureAwait(false);
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
                var completeStatusUpdate = A2AConverter.StatusUpdate(_contextId, _taskId, TaskState.Completed);
                task = await _taskStore.UpdateTaskAsync(completeStatusUpdate, cancellationToken).ConfigureAwait(false);
            }

            // Send Task status
            var finalStatusUpdate = A2AConverter.StatusUpdate(_contextId, _taskId, task.Status.State, isFinal: true);
            await WriteEvent(httpResponse, finalStatusUpdate.Kind, finalStatusUpdate, cancellationToken).ConfigureAwait(false);
        }

        private async Task WriteEvent(HttpResponse httpResponse, string eventName, object payload, CancellationToken cancellationToken)
        {
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
                _logger.LogDebug("SSE event {Event}", sse);
            }

            await httpResponse.Body.WriteAsync(Encoding.UTF8.GetBytes(sse), cancellationToken).ConfigureAwait(false);
            await httpResponse.Body.FlushAsync(cancellationToken);
        }
    }
}
