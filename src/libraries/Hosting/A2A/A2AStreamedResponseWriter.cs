// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.A2A.Protocol;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Http;
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

        public A2AStreamedResponseWriter(ITaskStore taskStore, string requestId, string contextId, string taskId)
        {
            _taskStore = taskStore;
            _requestId = requestId;
            _contextId = contextId;
            _taskId = taskId;
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

                    var statusUpdate = A2AConverter.StatusUpdateFromActivity(_contextId, _taskId, TaskState.Working, artifactId: entity.StreamId, activity: isInformative ? activity : null);
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
                    var artifactUpdate = A2AConverter.ArtifactUpdateFromActivity(_contextId, _taskId, activity, artifactId: entity.StreamId, append: false, lastChunk: isLastChunk);
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
            else if (!activity.IsType(ActivityTypes.Typing))
            {
                var message = A2AConverter.MessageFromActivity(_contextId, _taskId, activity);
                await WriteEvent(httpResponse, message.Kind, message, cancellationToken).ConfigureAwait(false);

                await _taskStore.UpdateTaskAsync(message, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task ResponseEnd(HttpResponse httpResponse, object data, CancellationToken cancellationToken = default)
        {
            //TBD:  Not convinced "end of turn" is same as TaskState.Completed.
            var final = A2AConverter.StatusUpdateFromActivity(_contextId, _taskId, TaskState.Completed, isFinal: true);
            var task = await _taskStore.UpdateTaskAsync(final, cancellationToken).ConfigureAwait(false);

            await WriteEvent(httpResponse, task.Kind, task, cancellationToken).ConfigureAwait(false);
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

            System.Diagnostics.Trace.WriteLine(sse);
            await httpResponse.Body.WriteAsync(Encoding.UTF8.GetBytes(sse), cancellationToken).ConfigureAwait(false);
            await httpResponse.Body.FlushAsync(cancellationToken);
        }
    }
}
